using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2Protocol.Tests;

[TestClass]
public sealed class ReservationClientTests
{
    private const ulong Cookie = 0x0186000047CF0FD8;
    private const uint Challenge = 0x12345678;

    [TestMethod]
    public async Task ReserveAsyncSendsTypedSettingsAndReportsAcceptance()
    {
        var settings = RealSessionSettings.EncodeReservationSettings();
        using var fixture = new ReservationFixture(ReservationResponseMode.Accepted);
        var fixtureTask = fixture.ServeAsync(settings);
        var result = await InvokeReserveAsync(fixture.Endpoint, settings).ConfigureAwait(false);
        await fixtureTask.ConfigureAwait(false);

        AssertOutcome(result, "Accepted");
    }

    [TestMethod]
    public async Task ReserveAsyncReportsRejectedResponse()
    {
        var settings = RealSessionSettings.EncodeReservationSettings();
        using var fixture = new ReservationFixture(ReservationResponseMode.Rejected);
        var fixtureTask = fixture.ServeAsync(settings);
        var result = await InvokeReserveAsync(fixture.Endpoint, settings).ConfigureAwait(false);
        await fixtureTask.ConfigureAwait(false);

        AssertOutcome(result, "Rejected");
    }

    [TestMethod]
    public async Task ReserveAsyncReportsHostVersionMismatch()
    {
        var settings = RealSessionSettings.EncodeReservationSettings();
        using var fixture = new ReservationFixture(ReservationResponseMode.HostVersionMismatch);
        var fixtureTask = fixture.ServeAsync(settings);
        var result = await InvokeReserveAsync(fixture.Endpoint, settings).ConfigureAwait(false);
        await fixtureTask.ConfigureAwait(false);

        AssertOutcome(result, "HostVersionMismatch");
    }

    [TestMethod]
    public async Task ReserveAsyncPreservesPacketWhenReservationResponseTimesOut()
    {
        var settings = RealSessionSettings.EncodeReservationSettings();
        using var fixture = new ReservationFixture(ReservationResponseMode.ReservationTimeout);
        var fixtureTask = fixture.ServeAsync(settings);
        var result = await InvokeReserveAsync(
            fixture.Endpoint,
            settings,
            TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        await fixtureTask.ConfigureAwait(false);

        AssertOutcome(result, "Timeout");
        Assert.IsNotNull(result.GetType().GetProperty("Packet")?.GetValue(result));
    }

    [TestMethod]
    public async Task ReserveAsyncReturnsNoPacketWhenChallengeTimesOut()
    {
        var settings = RealSessionSettings.EncodeReservationSettings();
        using var fixture = new ReservationFixture(ReservationResponseMode.ChallengeTimeout);
        var fixtureTask = fixture.ServeAsync(settings);
        var result = await InvokeReserveAsync(
            fixture.Endpoint,
            settings,
            TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        await fixtureTask.ConfigureAwait(false);

        AssertOutcome(result, "Timeout");
        Assert.IsNull(result.GetType().GetProperty("Packet")?.GetValue(result));
    }

    [TestMethod]
    public void ReservationClientDoesNotExposeUdpReceiveResultInMethodSignatures()
    {
        var leaked = typeof(ReservationClient)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(static method =>
                new[] { method.ReturnType }.Concat(method.GetParameters().Select(parameter => parameter.ParameterType)))
            .Any(static type => ContainsType(type, typeof(UdpReceiveResult)));

        Assert.IsFalse(leaked, "ASF's trimmed runtime cannot load UdpReceiveResult from a plugin signature.");
    }

    [TestMethod]
    public void ReservationClientUsesRuntimeSupportedSocketSendOverload()
    {
        var reserveMethod = typeof(ReservationClient).GetMethod(
            "ReserveAsync",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("ReservationClient.ReserveAsync must be available.");
        var stateMachine = reserveMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new AssertFailedException("ReservationClient.ReserveAsync must compile to an async state machine.");
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Reservation client's async state machine must expose MoveNext.");

        var socketSendCalls = EnumerateMethodCalls(moveNext)
            .Where(static called => called.DeclaringType == typeof(Socket) &&
                (called.Name == nameof(Socket.Send) || called.Name == nameof(Socket.SendAsync)))
            .ToArray();
        var unsupportedCall = socketSendCalls.FirstOrDefault(static called =>
        {
            var parameters = called.GetParameters();
            return called.Name != nameof(Socket.Send) ||
                parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(ReadOnlySpan<byte>) ||
                parameters[1].ParameterType != typeof(SocketFlags);
        });

        Assert.IsNull(
            unsupportedCall,
            "ASF's trimmed runtime only retains Socket.Send(ReadOnlySpan<byte>, SocketFlags) for this path.");
        Assert.AreEqual(2, socketSendCalls.Length, "Reservation requires one challenge datagram and one reservation datagram.");
    }

    private static async Task<object> InvokeReserveAsync(
        IPEndPoint endpoint,
        byte[] settings,
        TimeSpan? timeout = null)
    {
        var clientType = typeof(ReservationProtocol).Assembly.GetType("ReservationClient")
            ?? throw new AssertFailedException("ReservationClient must own the asynchronous UDP lifecycle.");
        var reserveMethod = clientType.GetMethod(
            "ReserveAsync",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("ReservationClient.ReserveAsync must be available.");
        var task = reserveMethod.Invoke(
            null,
            [endpoint, Cookie, settings, timeout ?? TimeSpan.FromSeconds(1), ReservationProtocol.DefaultHostVersion]);
        Assert.IsInstanceOfType<Task>(task);
        await ((Task)task).ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task)
            ?? throw new AssertFailedException("ReservationClient must return a result.");
    }

    private static void AssertOutcome(object result, string expectedOutcome) =>
        Assert.AreEqual(
            expectedOutcome,
            result.GetType().GetProperty("Outcome")?.GetValue(result)?.ToString());

    private static bool ContainsType(Type candidate, Type expected) =>
        candidate == expected || candidate.GetGenericArguments().Any(argument => ContainsType(argument, expected));

    private static IEnumerable<MethodBase> EnumerateMethodCalls(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray()
            ?? throw new AssertFailedException("Expected async state-machine IL.");
        var oneByteOpcodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(static field => (OpCode)field.GetValue(null)!)
            .Where(static opcode => opcode.Size == 1)
            .ToDictionary(static opcode => (byte)opcode.Value);
        var twoByteOpcodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(static field => (OpCode)field.GetValue(null)!)
            .Where(static opcode => opcode.Size == 2)
            .ToDictionary(static opcode => (byte)opcode.Value);

        for (var offset = 0; offset < il.Length;)
        {
            var opcode = il[offset++] == 0xFE
                ? twoByteOpcodes[il[offset++]]
                : oneByteOpcodes[il[offset - 1]];
            var operandSize = GetOperandSize(opcode.OperandType, il, offset);
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                var token = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset, sizeof(int)));
                yield return method.Module.ResolveMethod(token)
                    ?? throw new AssertFailedException($"Unable to resolve method token 0x{token:X8}.");
            }

            offset += operandSize;
        }
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int offset) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod or
            OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or
            OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => sizeof(int) +
            (BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset, sizeof(int))) * sizeof(int)),
        _ => throw new AssertFailedException($"Unsupported IL operand type {operandType}.")
    };

    private sealed class ReservationFixture : IDisposable
    {
        private readonly ReservationResponseMode _mode;
        private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));

        internal ReservationFixture(ReservationResponseMode mode) => _mode = mode;

        internal IPEndPoint Endpoint => (IPEndPoint)_server.Client.LocalEndPoint!;

        internal async Task ServeAsync(byte[] expectedSettings)
        {
            var challengeRequest = await _server.ReceiveAsync().ConfigureAwait(false);
            CollectionAssert.AreEqual(ReservationProtocol.CreateChallengeRequest(), challengeRequest.Buffer);
            if (_mode == ReservationResponseMode.ChallengeTimeout)
                return;

            var challengeResponse = new byte[32];
            BinaryPrimitives.WriteUInt32LittleEndian(challengeResponse, 0xFFFFFFFF);
            challengeResponse[4] = (byte)'A';
            BinaryPrimitives.WriteUInt32LittleEndian(challengeResponse.AsSpan(5), Challenge);
            BinaryPrimitives.WriteInt32LittleEndian(challengeResponse.AsSpan(9), 3);
            BinaryPrimitives.WriteUInt64LittleEndian(challengeResponse.AsSpan(15), 1);
            challengeResponse[23] = 1;
            Encoding.ASCII.GetBytes("reserve\0").CopyTo(challengeResponse, 24);
            await _server.SendAsync(
                challengeResponse,
                challengeResponse.Length,
                challengeRequest.RemoteEndPoint).ConfigureAwait(false);

            var reservationRequest = await _server.ReceiveAsync().ConfigureAwait(false);
            var expected = ReservationProtocol.CreateReservationRequest(
                ReservationProtocol.DefaultHostVersion,
                Challenge,
                Cookie,
                expectedSettings);
            CollectionAssert.AreEqual(expected.Request, reservationRequest.Buffer);
            if (_mode == ReservationResponseMode.ReservationTimeout)
                return;

            var reservationResponse = new byte[10];
            BinaryPrimitives.WriteUInt32LittleEndian(reservationResponse, 0xFFFFFFFF);
            reservationResponse[4] = (byte)'p';
            BinaryPrimitives.WriteInt32LittleEndian(
                reservationResponse.AsSpan(5),
                _mode == ReservationResponseMode.HostVersionMismatch
                    ? ReservationProtocol.DefaultHostVersion + 1
                    : ReservationProtocol.DefaultHostVersion);
            reservationResponse[9] = _mode == ReservationResponseMode.Rejected ? (byte)0 : (byte)1;
            await _server.SendAsync(
                reservationResponse,
                reservationResponse.Length,
                reservationRequest.RemoteEndPoint).ConfigureAwait(false);
        }

        public void Dispose() => _server.Dispose();
    }

    private enum ReservationResponseMode
    {
        Accepted,
        Rejected,
        HostVersionMismatch,
        ReservationTimeout,
        ChallengeTimeout
    }
}

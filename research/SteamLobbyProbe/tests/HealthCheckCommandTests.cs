using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class HealthCheckCommandTests
{
    [TestMethod]
    public async Task HealthCheckWithMissingLibraryEmitsMachineReadableFailure()
    {
        var probeAssembly = Path.Combine(AppContext.BaseDirectory, "SteamLobbyProbe.dll");
        var missingLibrary = Path.Combine(Path.GetTempPath(), $"missing-steam-api-{Guid.NewGuid():N}.so");
        var result = await RunProbeAsync(probeAssembly, missingLibrary, "health-check", "--json");

        Assert.AreNotEqual(0, result.ExitCode);

        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.IsFalse(document.RootElement.GetProperty("ready").GetBoolean());
        Assert.AreEqual("steam_api_load_failed", document.RootElement.GetProperty("failure").GetString());
    }

    [TestMethod]
    [DoNotParallelize]
    public void SteamApiInitializationFailureMarksInitCheckAsFailed()
    {
        var probeAssemblyPath = Path.Combine(AppContext.BaseDirectory, "SteamLobbyProbe.dll");
        var programType = Assembly.LoadFrom(probeAssemblyPath).GetType("Program");
        var writeResult = programType?.GetMethod("WriteHealthCheckResult", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(writeResult);

        var originalStandardOutput = Console.Out;
        using var standardOutput = new StringWriter();
        try
        {
            Console.SetOut(standardOutput);
            var emitted = (bool?)writeResult.Invoke(null, [false, "steam_api_init_failed", 0, null, null]);
            Assert.IsTrue(emitted);
        }
        finally
        {
            Console.SetOut(originalStandardOutput);
        }

        using var document = JsonDocument.Parse(standardOutput.ToString());
        Assert.AreEqual("failed", document.RootElement.GetProperty("checks").GetProperty("steamApiInit").GetString());
    }

    [TestMethod]
    public void LobbyCreatedDecoderReadsTheNativeTwelveBytePayload()
    {
        var probeAssemblyPath = Path.Combine(AppContext.BaseDirectory, "SteamLobbyProbe.dll");
        var assembly = Assembly.LoadFrom(probeAssemblyPath);
        var programType = assembly.GetType("Program");
        var apiCallResultType = assembly.GetType("Program+ApiCallResult");
        var decoder = programType?.GetMethod("DecodeLobbyCreated", BindingFlags.NonPublic | BindingFlags.Static);
        var constructor = apiCallResultType?.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SingleOrDefault();

        Assert.IsNotNull(decoder);
        Assert.IsNotNull(constructor);

        const ulong lobbyId = 109775242233456789;
        var raw = new byte[12];
        BitConverter.GetBytes(1).CopyTo(raw, 0);
        BitConverter.GetBytes(lobbyId).CopyTo(raw, sizeof(int));
        var apiCallResult = constructor.Invoke([true, false, raw]);

        var decoded = decoder.Invoke(null, [apiCallResult]);
        Assert.IsNotNull(decoded);
        var decodedType = decoded.GetType();
        Assert.AreEqual(1, decodedType.GetProperty("Result")?.GetValue(decoded));
        Assert.AreEqual(lobbyId, decodedType.GetProperty("LobbyId")?.GetValue(decoded));
        Assert.AreEqual(true, decodedType.GetProperty("CallbackOk")?.GetValue(decoded));
    }

    [TestMethod]
    public void LobbyCreatedDecoderReadsTheManualDispatchPaddedPayload()
    {
        var probeAssemblyPath = Path.Combine(AppContext.BaseDirectory, "SteamLobbyProbe.dll");
        var assembly = Assembly.LoadFrom(probeAssemblyPath);
        var programType = assembly.GetType("Program");
        var apiCallResultType = assembly.GetType("Program+ApiCallResult");
        var decoder = programType?.GetMethod("DecodeLobbyCreated", BindingFlags.NonPublic | BindingFlags.Static);
        var constructor = apiCallResultType?.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SingleOrDefault();

        Assert.IsNotNull(decoder);
        Assert.IsNotNull(constructor);

        const ulong lobbyId = 109775242233456789;
        var raw = new byte[16];
        BitConverter.GetBytes(1).CopyTo(raw, 0);
        BitConverter.GetBytes(lobbyId).CopyTo(raw, sizeof(int) * 2);
        var apiCallResult = constructor.Invoke([true, false, raw]);

        var decoded = decoder.Invoke(null, [apiCallResult]);
        Assert.IsNotNull(decoded);
        Assert.AreEqual(lobbyId, decoded.GetType().GetProperty("LobbyId")?.GetValue(decoded));
    }

    private static async Task<ProcessResult> RunProbeAsync(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

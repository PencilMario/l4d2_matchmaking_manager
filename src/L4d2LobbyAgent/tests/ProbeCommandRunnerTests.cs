using L4d2LobbyAgent.Probe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2LobbyAgent.Tests;

[TestClass]
public sealed class ProbeCommandRunnerTests
{
    [TestMethod]
    public async Task RunAsyncInvokesTheReadOnlyHealthCommandAndParsesSuccess()
    {
        var launcher = new FakeProcessLauncher(0, """
            {"ready":true,"failure":null,"checks":{"appId":500,"lobbyCount":3}}
            """);
        var runner = new ProbeCommandRunner(
            new ProbeCommandOptions("/opt/probe/SteamLobbyProbe", "/opt/steam/libsteam_api.so", TimeSpan.FromSeconds(5)),
            launcher);

        var result = await runner.RunAsync(CancellationToken.None);

        Assert.IsTrue(result.Ready);
        Assert.AreEqual((uint)500, result.AppId);
        Assert.AreEqual((uint)3, result.LobbyCount);
        CollectionAssert.AreEqual(
            new[] { "/opt/steam/libsteam_api.so", "health-check", "--json" },
            launcher.Arguments);
    }

    [TestMethod]
    public async Task RunAsyncParsesTheFinalJsonLineAfterProbeDiagnostics()
    {
        var runner = new ProbeCommandRunner(
            new ProbeCommandOptions("probe", "steam-api", TimeSpan.FromSeconds(5)),
            new FakeProcessLauncher(0, """
                ManualCallback id=510 size=4 raw=32000000
                {"ready":true,"failure":null,"checks":{"appId":500,"lobbyCount":50}}
                """));

        var result = await runner.RunAsync(CancellationToken.None);

        Assert.IsTrue(result.Ready);
        Assert.AreEqual((uint)500, result.AppId);
        Assert.AreEqual((uint)50, result.LobbyCount);
    }

    [TestMethod]
    public async Task RunAsyncReturnsStableFailureWhenTheProbeOutputIsNotJson()
    {
        var runner = new ProbeCommandRunner(
            new ProbeCommandOptions("probe", "steam-api", TimeSpan.FromSeconds(5)),
            new FakeProcessLauncher(2, "not-json"));

        var result = await runner.RunAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("probe_output_invalid", result.Failure);
    }

    [TestMethod]
    public async Task RunAsyncAcceptsNullOptionalCheckValuesFromFailedProbe()
    {
        var runner = new ProbeCommandRunner(
            new ProbeCommandOptions("probe", "steam-api", TimeSpan.FromSeconds(5)),
            new FakeProcessLauncher(2, """
                {"ready":false,"failure":"steam_api_init_failed","checks":{"appId":null,"lobbyCount":null}}
                """));

        var result = await runner.RunAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("steam_api_init_failed", result.Failure);
        Assert.IsNull(result.AppId);
        Assert.IsNull(result.LobbyCount);
    }

    [TestMethod]
    public async Task RunAsyncRejectsAnUnconfiguredSteamApiLibraryWithoutLaunchingTheProbe()
    {
        var launcher = new FakeProcessLauncher(0, "{}");
        var runner = new ProbeCommandRunner(
            new ProbeCommandOptions("probe", string.Empty, TimeSpan.FromSeconds(5)),
            launcher);

        var result = await runner.RunAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("steam_api_library_not_configured", result.Failure);
        Assert.AreEqual(0, launcher.CallCount);
    }

    private sealed class FakeProcessLauncher(int exitCode, string standardOutput) : IProbeProcessLauncher
    {
        public string[] Arguments { get; private set; } = [];
        public int CallCount { get; private set; }

        public Task<ProbeProcessOutput> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Arguments = arguments.ToArray();
            return Task.FromResult(new ProbeProcessOutput(exitCode, standardOutput));
        }
    }
}

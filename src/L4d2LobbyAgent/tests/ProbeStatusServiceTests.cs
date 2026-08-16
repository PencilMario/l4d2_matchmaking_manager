using L4d2LobbyAgent.Probe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2LobbyAgent.Tests;

[TestClass]
public sealed class ProbeStatusServiceTests
{
    [TestMethod]
    public async Task GetAsyncRunsTheProbeForEveryRequestAndSerializesCalls()
    {
        var runner = new BlockingFakeRunner();
        var service = new ProbeStatusService(runner, new FakeDesktopDetector(true));

        var results = await Task.WhenAll(
            service.GetAsync(CancellationToken.None),
            service.GetAsync(CancellationToken.None));

        Assert.AreEqual(2, runner.CallCount);
        Assert.AreEqual(1, runner.MaximumConcurrency);
        Assert.IsTrue(results.All(result => result.Ready));
        Assert.AreNotEqual(results[0].ObservedAt, results[1].ObservedAt);
    }

    [TestMethod]
    public async Task GetAsyncReportsDesktopFailureWithoutPersistingThePreviousSuccess()
    {
        var runner = new ReadyFakeRunner();
        var service = new ProbeStatusService(runner, new FakeDesktopDetector(false));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("steam_desktop_unavailable", result.Failure);
        Assert.AreEqual(1, runner.CallCount);
    }

    [TestMethod]
    public async Task GetAsyncMarksSteamApiInitializationFailureAsFailed()
    {
        var service = new ProbeStatusService(
            new FixedFakeRunner(new ProbeCommandResult(false, "steam_api_init_failed", null, null)),
            new FakeDesktopDetector(true));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("steam_api_init_failed", result.Failure);
        Assert.AreEqual("failed", result.Checks.SteamApiInit);
    }

    [TestMethod]
    public async Task GetAsyncMarksInitializationAsUnknownWhenTheProbeWasNotConfigured()
    {
        var service = new ProbeStatusService(
            new FixedFakeRunner(new ProbeCommandResult(false, "steam_api_library_not_configured", null, null)),
            new FakeDesktopDetector(true));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.IsFalse(result.Ready);
        Assert.AreEqual("unknown", result.Checks.SteamApiInit);
    }

    private sealed class BlockingFakeRunner : IProbeCommandRunner
    {
        private int activeCalls;
        private int maximumConcurrency;
        private int callCount;

        public int CallCount => callCount;
        public int MaximumConcurrency => maximumConcurrency;

        public async Task<ProbeCommandResult> RunAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            var active = Interlocked.Increment(ref activeCalls);
            maximumConcurrency = Math.Max(maximumConcurrency, active);
            await Task.Delay(50, cancellationToken);
            Interlocked.Decrement(ref activeCalls);
            return ProbeCommandResult.Success(550, 0);
        }
    }

    private sealed class ReadyFakeRunner : IProbeCommandRunner
    {
        public int CallCount { get; private set; }

        public Task<ProbeCommandResult> RunAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(ProbeCommandResult.Success(550, 0));
        }
    }

    private sealed class FixedFakeRunner(ProbeCommandResult result) : IProbeCommandRunner
    {
        public Task<ProbeCommandResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeDesktopDetector(bool isRunning) : ISteamDesktopDetector
    {
        public bool IsRunning() => isRunning;
    }
}

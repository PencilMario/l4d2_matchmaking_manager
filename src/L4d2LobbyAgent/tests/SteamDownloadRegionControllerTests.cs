using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2LobbyAgent.Tests;

[TestClass]
public sealed class SteamDownloadRegionControllerTests
{
    [TestMethod]
    public async Task WritesNumericTargetBeforeRestartingSteam()
    {
        var path = CreateTemporaryPath();
        var desktop = new RecordingDesktopController(() => File.ReadAllText(path));
        var controller = new SteamDownloadRegionController(path, desktop);

        await controller.ApplyAsync("197", CancellationToken.None);

        Assert.AreEqual("197", File.ReadAllText(path).Trim());
        Assert.AreEqual("197", desktop.ObservedTarget?.Trim());
        Assert.AreEqual(1, desktop.RestartCalls);
        File.Delete(path);
    }

    [TestMethod]
    public async Task EmptyTargetClearsThePreviousRegion()
    {
        var path = CreateTemporaryPath();
        File.WriteAllText(path, "197\n");
        var desktop = new RecordingDesktopController(() => File.ReadAllText(path));
        var controller = new SteamDownloadRegionController(path, desktop);

        await controller.ApplyAsync(null, CancellationToken.None);

        Assert.AreEqual(string.Empty, File.ReadAllText(path));
        Assert.AreEqual(string.Empty, desktop.ObservedTarget);
        File.Delete(path);
    }

    [TestMethod]
    public async Task KeepsWrittenTargetWhenSteamRestartFails()
    {
        var path = CreateTemporaryPath();
        var desktop = new RecordingDesktopController(() => File.ReadAllText(path))
        {
            Failure = new InvalidOperationException("steam_restart_failed"),
        };
        var controller = new SteamDownloadRegionController(path, desktop);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => controller.ApplyAsync("32", CancellationToken.None));

        Assert.AreEqual("32", File.ReadAllText(path).Trim());
        File.Delete(path);
    }

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), "steam-region-controller-" + Guid.NewGuid().ToString("N"));

    private sealed class RecordingDesktopController(Func<string> readTarget) : ISteamDesktopController
    {
        public int RestartCalls { get; private set; }
        public string? ObservedTarget { get; private set; }
        public Exception? Failure { get; init; }

        public Task RestartAsync(CancellationToken cancellationToken)
        {
            ObservedTarget = readTarget();
            RestartCalls++;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}

using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class DockerAgentContainerRuntimeTests
{
    [TestMethod]
    public async Task CreateUsesOnlyTheManagedContainerDefinition()
    {
        var runtime = new FakeRuntime();
        var options = new AgentContainerOptions(
            "l4d2-steam-lobby-agent:local",
            "/srv/steam-library",
            "matchmaking-network",
            18083,
            18183);
        var service = new WarmupAgentContainerService(runtime, options);
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "account-1" };

        await service.CreateAsync(agent, CancellationToken.None);

        Assert.IsNotNull(runtime.Definition);
        Assert.AreEqual("l4d2-steam-lobby-agent:local", runtime.Definition.Image);
        Assert.AreEqual("127.0.0.1", runtime.Definition.NoVncBinding.HostIp);
        CollectionAssert.DoesNotContain(runtime.Definition.PublishedContainerPorts.ToList(), 8080);
        CollectionAssert.Contains(runtime.Definition.VolumeNames.ToList(), "steam-data-" + agent.Id.ToString("N"));
        CollectionAssert.Contains(runtime.Definition.VolumeNames.ToList(), "agent-config-" + agent.Id.ToString("N"));
        Assert.IsTrue(runtime.Definition.BindMounts.Any(mount =>
            mount.Source == options.SharedLibraryHostPath && mount.Target == "/mnt/steam-library"));
        Assert.AreEqual("true", runtime.Definition.Labels["com.l4d2.matchmaking.managed"]);
    }

    [TestMethod]
    public async Task DeleteDoesNotDeleteAccountVolumes()
    {
        var runtime = new FakeRuntime();
        var service = new WarmupAgentContainerService(runtime, new AgentContainerOptions("image", "/library", "network", 18083, 18183));

        await service.DeleteAsync(new WarmupAgent { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.IsTrue(runtime.DeleteCalled);
        Assert.IsFalse(runtime.DeleteVolumesRequested);
    }

    private sealed class FakeRuntime : IAgentContainerRuntime
    {
        public ManagedAgentContainerDefinition? Definition { get; private set; }
        public bool DeleteCalled { get; private set; }
        public bool DeleteVolumesRequested { get; private set; }

        public Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken)
        {
            Definition = definition;
            return Task.FromResult("container-id");
        }

        public Task StartAsync(string containerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(string containerId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            DeleteVolumesRequested = deleteVolumes;
            return Task.CompletedTask;
        }
    }
}

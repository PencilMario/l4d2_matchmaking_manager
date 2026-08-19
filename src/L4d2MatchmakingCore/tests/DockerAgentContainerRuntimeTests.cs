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
            18183,
            "/mnt/steam-library/libsteam_api.so");
        var service = new WarmupAgentContainerService(runtime, options);
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "account-1", NoVncPort = 18083 };

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
        var service = new WarmupAgentContainerService(runtime, new AgentContainerOptions("image", "/library", "network", 18083, 18183, "/mnt/steam-library/libsteam_api.so"));

        await service.DeleteAsync(new WarmupAgent { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.IsTrue(runtime.DeleteCalled);
        Assert.IsFalse(runtime.DeleteVolumesRequested);
    }

    [TestMethod]
    public async Task CreateProvidesRequiredSteamEnvironment()
    {
        var runtime = new FakeRuntime();
        var options = new AgentContainerOptions(
            "image",
            "/library",
            "network",
            18083,
            18183,
            "/mnt/steam-library/steamapps/common/Left 4 Dead 2/bin/linux64/libsteam_api.so");
        var service = new WarmupAgentContainerService(runtime, options);
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "account-1", NoVncPort = 18083, KeepVncAlive = true };

        await service.CreateAsync(agent, CancellationToken.None);

        Assert.IsNotNull(runtime.Definition);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "PUID=1000",
                "PGID=1000",
                "UMASK=077",
                "WEB_UI_MODE=vnc",
                "PORT_NOVNC_WEB=8083",
                "ENABLE_VNC_AUDIO=false",
                "ENABLE_STEAM=true",
                "STEAM_LOGIN_UI_MODE=always",
                "ENABLE_SUNSHINE=false",
                "ENABLE_EVDEV_INPUTS=false",
                "FORCE_X11_DUMMY_CONFIG=true",
                "NVIDIA_VISIBLE_DEVICES=",
                "LIBGL_ALWAYS_SOFTWARE=1",
                "STEAM_SHARED_LIBRARY_PATH=/mnt/steam-library",
                "STEAM_API_LIBRARY_PATH=/mnt/steam-library/steamapps/common/Left 4 Dead 2/bin/linux64/libsteam_api.so",
                "STEAM_DOWNLOAD_REGION=",
            },
            runtime.Definition.Environment.ToArray());
        Assert.AreEqual(2L * 1024 * 1024 * 1024, runtime.Definition.SharedMemoryBytes);
        Assert.AreEqual("unless-stopped", runtime.Definition.RestartPolicy);
        CollectionAssert.AreEquivalent(
            new[] { "apparmor=unconfined", "seccomp=unconfined" },
            runtime.Definition.SecurityOptions.ToArray());
        CollectionAssert.Contains(
            runtime.Definition.Devices.ToList(),
            new AgentDeviceMapping("/dev/fuse", "/dev/fuse", "rwm"));
    }

    [TestMethod]
    public async Task CreateDefaultsToReclaimedVncMode()
    {
        var runtime = new FakeRuntime();
        var service = new WarmupAgentContainerService(runtime, new AgentContainerOptions("image", "/library", "network", 18083, 18183, "/mnt/steam-library/libsteam_api.so"));
        var agent = new WarmupAgent { Id = Guid.NewGuid(), Name = "account-1", NoVncPort = 18083 };

        await service.CreateAsync(agent, CancellationToken.None);

        Assert.IsNotNull(runtime.Definition);
        CollectionAssert.Contains(runtime.Definition.Environment.ToList(), "STEAM_LOGIN_UI_MODE=auto");
    }

    [TestMethod]
    public async Task VncLifecycleUsesTheManagedRuntimeContract()
    {
        var runtime = new FakeRuntime();
        var service = new WarmupAgentContainerService(runtime, new AgentContainerOptions("image", "/library", "network", 18083, 18183, "/mnt/steam-library/libsteam_api.so"));
        var agent = new WarmupAgent { Id = Guid.NewGuid(), ContainerId = "container-id" };

        Assert.IsTrue(await service.EnsureVncStartedAsync(agent, CancellationToken.None));
        await service.StopVncAsync(agent, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "status", "start", "stop" },
            runtime.VncCommands.ToArray());
    }

    [TestMethod]
    public void VncStatusParserTreatsSupervisorStoppedAsStartable()
    {
        Assert.AreEqual(AgentVncState.Running, AgentVncStateParser.Parse(0, "x11vnc RUNNING pid 17\nfrontend RUNNING pid 18"));
        Assert.AreEqual(AgentVncState.Stopped, AgentVncStateParser.Parse(3, "x11vnc STOPPED Not started\nfrontend STOPPED Not started"));
        Assert.AreEqual(AgentVncState.Partial, AgentVncStateParser.Parse(3, "x11vnc RUNNING pid 17\nfrontend STOPPED Not started"));
        Assert.ThrowsException<InvalidOperationException>(() => AgentVncStateParser.Parse(4, "vnc UNKNOWN"));
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

        public Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

        public Task StartAsync(string containerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(string containerId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            DeleteVolumesRequested = deleteVolumes;
            return Task.CompletedTask;
        }

        public List<string> VncCommands { get; } = [];

        public Task<AgentVncState> GetVncStateAsync(string containerId, CancellationToken cancellationToken)
        {
            VncCommands.Add("status");
            return Task.FromResult(AgentVncState.Stopped);
        }

        public Task StartVncAsync(string containerId, CancellationToken cancellationToken)
        {
            VncCommands.Add("start");
            return Task.CompletedTask;
        }

        public Task StopVncAsync(string containerId, CancellationToken cancellationToken)
        {
            VncCommands.Add("stop");
            return Task.CompletedTask;
        }
    }
}

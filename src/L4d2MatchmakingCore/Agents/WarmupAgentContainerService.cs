using L4d2MatchmakingCore.Data;

namespace L4d2MatchmakingCore.Agents;

public sealed class WarmupAgentContainerService(
    IAgentContainerRuntime runtime,
    AgentContainerOptions options)
{
    public async Task<string> CreateAsync(WarmupAgent agent, CancellationToken cancellationToken)
    {
        if (agent.NoVncPort < options.NoVncPortStart || agent.NoVncPort > options.NoVncPortEnd)
            throw new InvalidOperationException("invalid_novnc_port");
        if (string.IsNullOrWhiteSpace(options.SteamApiLibraryPath))
            throw new InvalidOperationException("core_agent_steam_api_library_path_not_configured");
        agent.SteamDataVolumeName = string.IsNullOrWhiteSpace(agent.SteamDataVolumeName)
            ? $"steam-data-{agent.Id:N}"
            : agent.SteamDataVolumeName;
        agent.AccountConfigVolumeName = string.IsNullOrWhiteSpace(agent.AccountConfigVolumeName)
            ? $"agent-config-{agent.Id:N}"
            : agent.AccountConfigVolumeName;
        var containerId = await runtime.CreateAsync(new ManagedAgentContainerDefinition(
            $"l4d2-agent-{agent.Id:N}",
            options.Image,
            new Dictionary<string, string>
            {
                ["com.l4d2.matchmaking.managed"] = "true",
                ["com.l4d2.matchmaking.agent-id"] = agent.Id.ToString(),
            },
            [agent.SteamDataVolumeName, agent.AccountConfigVolumeName],
            [
                new AgentVolumeMount(agent.SteamDataVolumeName, "/home/default"),
                new AgentVolumeMount(agent.AccountConfigVolumeName, "/var/lib/l4d2-agent"),
            ],
            [new AgentBindMount(options.SharedLibraryHostPath, "/mnt/steam-library")],
            [8083],
            new AgentPortBinding("127.0.0.1", agent.NoVncPort, 8083),
            options.Network,
            [
                "PUID=1000",
                "PGID=1000",
                "UMASK=077",
                "WEB_UI_MODE=vnc",
                "PORT_NOVNC_WEB=8083",
                "ENABLE_VNC_AUDIO=false",
                "ENABLE_STEAM=true",
                "STEAM_LOGIN_UI_MODE=" + options.SteamLoginUiMode,
                "ENABLE_SUNSHINE=false",
                "ENABLE_EVDEV_INPUTS=false",
                "FORCE_X11_DUMMY_CONFIG=true",
                "NVIDIA_VISIBLE_DEVICES=",
                "LIBGL_ALWAYS_SOFTWARE=1",
                "STEAM_SHARED_LIBRARY_PATH=/mnt/steam-library",
                "STEAM_API_LIBRARY_PATH=" + options.SteamApiLibraryPath,
                "STEAM_DOWNLOAD_REGION=" + (agent.DownloadRegion ?? string.Empty),
            ],
            2L * 1024 * 1024 * 1024,
            [new AgentDeviceMapping("/dev/fuse", "/dev/fuse", "rwm")],
            ["apparmor=unconfined", "seccomp=unconfined"],
            "unless-stopped"), cancellationToken);
        agent.ContainerId = containerId;
        return containerId;
    }

    public Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken) =>
        runtime.GetUsedHostPortsAsync(cancellationToken);

    public Task StartAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
        runtime.StartAsync(agent.ContainerId ?? throw new InvalidOperationException("agent_container_not_created"), cancellationToken);

    public Task StopAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
        runtime.StopAsync(agent.ContainerId ?? throw new InvalidOperationException("agent_container_not_created"), cancellationToken);

    public Task DeleteAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
        runtime.DeleteAsync(agent.ContainerId ?? $"l4d2-agent-{agent.Id:N}", false, cancellationToken);

}

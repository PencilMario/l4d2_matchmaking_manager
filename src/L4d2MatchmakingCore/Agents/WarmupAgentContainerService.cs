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
            agent.DownloadRegion), cancellationToken);
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

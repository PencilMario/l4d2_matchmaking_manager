using L4d2MatchmakingCore.Data;

namespace L4d2MatchmakingCore.Agents;

public sealed class WarmupAgentContainerService(
    IAgentContainerRuntime runtime,
    AgentContainerOptions options)
{
    public async Task<string> CreateAsync(WarmupAgent agent, CancellationToken cancellationToken)
    {
        agent.SteamDataVolumeName = $"steam-data-{agent.Id:N}";
        agent.AccountConfigVolumeName = $"agent-config-{agent.Id:N}";
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
            new AgentPortBinding("127.0.0.1", SelectNoVncPort(agent.Id), 8083),
            options.Network,
            agent.DownloadRegion), cancellationToken);
        agent.ContainerId = containerId;
        return containerId;
    }

    public Task DeleteAsync(WarmupAgent agent, CancellationToken cancellationToken) =>
        runtime.DeleteAsync(agent.ContainerId ?? $"l4d2-agent-{agent.Id:N}", false, cancellationToken);

    private int SelectNoVncPort(Guid agentId)
    {
        if (options.NoVncPortEnd < options.NoVncPortStart)
            throw new InvalidOperationException("invalid_novnc_port_range");
        var span = options.NoVncPortEnd - options.NoVncPortStart + 1;
        return options.NoVncPortStart + (int)(BitConverter.ToUInt32(agentId.ToByteArray(), 0) % span);
    }
}

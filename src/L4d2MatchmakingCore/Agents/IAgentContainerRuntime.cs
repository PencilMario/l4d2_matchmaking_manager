namespace L4d2MatchmakingCore.Agents;

public interface IAgentContainerRuntime
{
    Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken);
    Task StartAsync(string containerId, CancellationToken cancellationToken);
    Task StopAsync(string containerId, CancellationToken cancellationToken);
    Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken);
}

public sealed record AgentBindMount(string Source, string Target);

public sealed record AgentVolumeMount(string Name, string Target);

public sealed record AgentPortBinding(string HostIp, int HostPort, int ContainerPort);

public sealed record ManagedAgentContainerDefinition(
    string Name,
    string Image,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<string> VolumeNames,
    IReadOnlyList<AgentVolumeMount> VolumeMounts,
    IReadOnlyList<AgentBindMount> BindMounts,
    IReadOnlyList<int> PublishedContainerPorts,
    AgentPortBinding NoVncBinding,
    string Network,
    string? DownloadRegion);

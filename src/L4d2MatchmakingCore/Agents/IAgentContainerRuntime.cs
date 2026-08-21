namespace L4d2MatchmakingCore.Agents;

public interface IAgentContainerRuntime
{
    Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken);
    Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken);
    Task StartAsync(string containerId, CancellationToken cancellationToken);
    Task StopAsync(string containerId, CancellationToken cancellationToken);
    Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken);
    Task<AgentVncState> GetVncStateAsync(string containerId, CancellationToken cancellationToken);
    Task StartVncAsync(string containerId, CancellationToken cancellationToken);
    Task StopVncAsync(string containerId, CancellationToken cancellationToken);
}

public enum AgentVncState
{
    Stopped,
    Running,
    Partial,
}

public static class AgentVncStateParser
{
    private static readonly string[] VncPrograms = ["x11vnc", "frontend"];

    public static AgentVncState Parse(int exitCode, string standardOutput)
    {
        var statuses = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length >= 2 && VncPrograms.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
        if (statuses.Count != VncPrograms.Length)
            throw new InvalidOperationException(exitCode == 0 ? "agent_vnc_status_invalid" : "agent_vnc_status_failed");
        if (VncPrograms.All(program => string.Equals(statuses[program], "RUNNING", StringComparison.OrdinalIgnoreCase)))
            return AgentVncState.Running;
        if (VncPrograms.All(program => IsStopped(statuses[program])))
        {
            return AgentVncState.Stopped;
        }
        return AgentVncState.Partial;
    }

    private static bool IsStopped(string status) =>
        status.Equals("STOPPED", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("EXITED", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("FATAL", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("BACKOFF", StringComparison.OrdinalIgnoreCase);
}

public sealed record AgentBindMount(string Source, string Target);

public sealed record AgentVolumeMount(string Name, string Target);

public sealed record AgentPortBinding(string HostIp, int HostPort, int ContainerPort);

public sealed record AgentDeviceMapping(string HostPath, string ContainerPath, string Permissions);

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
    IReadOnlyList<string> Environment,
    long SharedMemoryBytes,
    long MemoryLimitBytes,
    IReadOnlyList<AgentDeviceMapping> Devices,
    IReadOnlyList<string> SecurityOptions,
    string RestartPolicy);

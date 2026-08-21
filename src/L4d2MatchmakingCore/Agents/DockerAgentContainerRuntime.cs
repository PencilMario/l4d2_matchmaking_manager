using Docker.DotNet;
using Docker.DotNet.Models;

namespace L4d2MatchmakingCore.Agents;

public sealed class DockerAgentContainerRuntime : IAgentContainerRuntime, IDisposable
{
    private static readonly string[] VncPrograms = ["x11vnc", "frontend"];
    private readonly DockerClient _client = new DockerClientConfiguration().CreateClient();

    public async Task<IReadOnlySet<int>> GetUsedHostPortsAsync(CancellationToken cancellationToken)
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true }, cancellationToken);
        return containers
            .SelectMany(container => container.Ports)
            .Where(port => port.PublicPort > 0)
            .Select(port => (int)port.PublicPort)
            .ToHashSet();
    }

    public async Task<string> CreateAsync(ManagedAgentContainerDefinition definition, CancellationToken cancellationToken)
    {
        foreach (var volume in definition.VolumeNames)
            await _client.Volumes.CreateAsync(new VolumesCreateParameters { Name = volume }, cancellationToken);

        var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = definition.Name,
            Image = definition.Image,
            Labels = definition.Labels.ToDictionary(pair => pair.Key, pair => pair.Value),
            Env = definition.Environment.ToList(),
            ExposedPorts = definition.PublishedContainerPorts.ToDictionary(
                port => $"{port}/tcp",
                _ => new EmptyStruct()),
            HostConfig = new HostConfig
            {
                Binds = definition.VolumeMounts
                    .Select(mount => $"{mount.Name}:{mount.Target}")
                    .Concat(definition.BindMounts.Select(mount => $"{mount.Source}:{mount.Target}"))
                    .ToList(),
                NetworkMode = definition.Network,
                ShmSize = definition.SharedMemoryBytes,
                Memory = definition.MemoryLimitBytes,
                SecurityOpt = definition.SecurityOptions.ToList(),
                RestartPolicy = new RestartPolicy
                {
                    Name = definition.RestartPolicy == "unless-stopped"
                        ? RestartPolicyKind.UnlessStopped
                        : throw new InvalidOperationException("invalid_managed_agent_restart_policy"),
                },
                Devices = definition.Devices
                    .Select(device => new DeviceMapping
                    {
                        PathOnHost = device.HostPath,
                        PathInContainer = device.ContainerPath,
                        CgroupPermissions = device.Permissions,
                    })
                    .ToList(),
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [$"{definition.NoVncBinding.ContainerPort}/tcp"] =
                    [new PortBinding
                    {
                        HostIP = definition.NoVncBinding.HostIp,
                        HostPort = definition.NoVncBinding.HostPort.ToString(),
                    }],
                },
            },
        }, cancellationToken);
        return response.ID;
    }

    public async Task<long> GetMemoryLimitAsync(string containerId, CancellationToken cancellationToken)
    {
        var container = await InspectManagedAsync(containerId, cancellationToken);
        return container.HostConfig?.Memory ?? 0;
    }

    public async Task UpdateMemoryLimitAsync(string containerId, long memoryLimitBytes, CancellationToken cancellationToken)
    {
        if (memoryLimitBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(memoryLimitBytes));

        await EnsureManagedAsync(containerId, cancellationToken);
        await _client.Containers.UpdateContainerAsync(containerId, new ContainerUpdateParameters
        {
            Memory = memoryLimitBytes,
        }, cancellationToken);
    }

    public async Task StartAsync(string containerId, CancellationToken cancellationToken)
    {
        await EnsureManagedAsync(containerId, cancellationToken);
        await _client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);
    }

    public async Task StopAsync(string containerId, CancellationToken cancellationToken)
    {
        await EnsureManagedAsync(containerId, cancellationToken);
        await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters(), cancellationToken);
    }

    public async Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureManagedAsync(containerId, cancellationToken);
            await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
            {
                Force = true,
                RemoveVolumes = deleteVolumes,
            }, cancellationToken);
        }
        catch (DockerContainerNotFoundException)
        {
            // A prior interrupted recreate can leave a stale database container ID.
        }
    }

    public async Task<AgentVncState> GetVncStateAsync(string containerId, CancellationToken cancellationToken)
    {
        var result = await ExecuteSupervisorCommandAsync(containerId, "status", cancellationToken);
        return AgentVncStateParser.Parse(result.ExitCode, result.StandardOutput);
    }

    public async Task StartVncAsync(string containerId, CancellationToken cancellationToken)
    {
        await ExecuteSupervisorCommandAsync(containerId, "start", cancellationToken);
        if (await GetVncStateAsync(containerId, cancellationToken) != AgentVncState.Running)
            throw new InvalidOperationException("agent_vnc_start_failed");
    }

    public async Task StopVncAsync(string containerId, CancellationToken cancellationToken)
    {
        await ExecuteSupervisorCommandAsync(containerId, "stop", cancellationToken);
        if (await GetVncStateAsync(containerId, cancellationToken) != AgentVncState.Stopped)
            throw new InvalidOperationException("agent_vnc_stop_failed");
    }

    private async Task<AgentExecResult> ExecuteSupervisorCommandAsync(
        string containerId,
        string operation,
        CancellationToken cancellationToken)
    {
        await EnsureManagedAsync(containerId, cancellationToken);
        var exec = await _client.Exec.ExecCreateContainerAsync(containerId, new ContainerExecCreateParameters
        {
            Cmd = ["supervisorctl", operation, .. VncPrograms],
            AttachStdout = true,
            AttachStderr = true,
        }, cancellationToken);
        using var stream = await _client.Exec.StartAndAttachContainerExecAsync(exec.ID, false, cancellationToken);
        var output = await stream.ReadOutputToEndAsync(cancellationToken);
        var inspection = await _client.Exec.InspectContainerExecAsync(exec.ID, cancellationToken);
        return new AgentExecResult(checked((int)inspection.ExitCode), output.stdout, output.stderr);
    }

    private async Task EnsureManagedAsync(string containerId, CancellationToken cancellationToken)
    {
        await InspectManagedAsync(containerId, cancellationToken);
    }

    private async Task<ContainerInspectResponse> InspectManagedAsync(string containerId, CancellationToken cancellationToken)
    {
        var container = await _client.Containers.InspectContainerAsync(containerId, cancellationToken);
        if (container.Config.Labels is null ||
            !container.Config.Labels.TryGetValue("com.l4d2.matchmaking.managed", out var managed) ||
            !string.Equals(managed, "true", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unmanaged_agent_container");
        }

        return container;
    }

    public void Dispose() => _client.Dispose();
}

public sealed record AgentExecResult(int ExitCode, string StandardOutput, string StandardError);

using Docker.DotNet;
using Docker.DotNet.Models;

namespace L4d2MatchmakingCore.Agents;

public sealed class DockerAgentContainerRuntime : IAgentContainerRuntime, IDisposable
{
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
            Env = definition.DownloadRegion is null ? [] : ["STEAM_DOWNLOAD_REGION=" + definition.DownloadRegion],
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
        await EnsureManagedAsync(containerId, cancellationToken);
        await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
        {
            Force = true,
            RemoveVolumes = deleteVolumes,
        }, cancellationToken);
    }

    private async Task EnsureManagedAsync(string containerId, CancellationToken cancellationToken)
    {
        var container = await _client.Containers.InspectContainerAsync(containerId, cancellationToken);
        if (container.Config.Labels is null ||
            !container.Config.Labels.TryGetValue("com.l4d2.matchmaking.managed", out var managed) ||
            !string.Equals(managed, "true", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unmanaged_agent_container");
        }
    }

    public void Dispose() => _client.Dispose();
}

using Docker.DotNet;
using Docker.DotNet.Models;

namespace L4d2MatchmakingCore.Agents;

public sealed class DockerAgentContainerRuntime : IAgentContainerRuntime, IDisposable
{
    private readonly DockerClient _client = new DockerClientConfiguration().CreateClient();

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

    public Task StartAsync(string containerId, CancellationToken cancellationToken) =>
        _client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);

    public Task StopAsync(string containerId, CancellationToken cancellationToken) =>
        _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters(), cancellationToken);

    public Task DeleteAsync(string containerId, bool deleteVolumes, CancellationToken cancellationToken) =>
        _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
        {
            Force = true,
            RemoveVolumes = deleteVolumes,
        }, cancellationToken);

    public void Dispose() => _client.Dispose();
}

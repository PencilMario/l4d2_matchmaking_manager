namespace L4d2LobbyAgent.Probe;

public interface IAgentReadinessMarker
{
    Task MarkReadyAsync(CancellationToken cancellationToken);
}

public sealed class FileAgentReadinessMarker(string path) : IAgentReadinessMarker
{
    public static FileAgentReadinessMarker FromEnvironment()
    {
        var homeDirectory = Environment.GetEnvironmentVariable("HOME") ?? "/home/default";
        return new FileAgentReadinessMarker(Path.Combine(
            homeDirectory,
            ".steam",
            "steam",
            "config",
            "l4d2-agent-ready"));
    }

    public async Task MarkReadyAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(path))
            return;

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("agent_readiness_marker_path_invalid");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "ready\n", cancellationToken);
    }
}

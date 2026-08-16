using System.Diagnostics;
using System.Text.Json;

namespace L4d2LobbyAgent.Probe;

public sealed class ProbeCommandRunner(ProbeCommandOptions options, IProbeProcessLauncher launcher) : IProbeCommandRunner
{
    public async Task<ProbeCommandResult> RunAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.SteamApiLibraryPath))
            return new ProbeCommandResult(false, "steam_api_library_not_configured", null, null);

        ProbeProcessOutput output;
        try
        {
            output = await launcher.RunAsync(
                options.ExecutablePath,
                [options.SteamApiLibraryPath, "health-check", "--json"],
                options.Timeout,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProbeCommandResult(false, "probe_execution_timeout", null, null);
        }
        catch (Exception)
        {
            return new ProbeCommandResult(false, "probe_execution_failed", null, null);
        }

        if (output.TimedOut)
            return new ProbeCommandResult(false, "probe_execution_timeout", null, null);

        try
        {
            var jsonOutput = output.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault(line => line.StartsWith('{')) ?? output.StandardOutput;
            using var document = JsonDocument.Parse(jsonOutput);
            var root = document.RootElement;
            if (!root.TryGetProperty("ready", out var readyElement) || readyElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                return new ProbeCommandResult(false, "probe_output_invalid", null, null);

            var ready = readyElement.GetBoolean();
            var failure = ReadString(root, "failure");
            var checks = root.TryGetProperty("checks", out var checksElement) && checksElement.ValueKind == JsonValueKind.Object
                ? checksElement
                : default;
            var appId = ReadUInt32(checks, "appId");
            var lobbyCount = ReadUInt32(checks, "lobbyCount");

            if (ready && output.ExitCode == 0)
                return new ProbeCommandResult(true, null, appId, lobbyCount);

            return new ProbeCommandResult(false, failure ?? "probe_check_failed", appId, lobbyCount);
        }
        catch (JsonException)
        {
            return new ProbeCommandResult(false, "probe_output_invalid", null, null);
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static uint? ReadUInt32(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number && property.TryGetUInt32(out var value)
            ? value
            : null;
}

public sealed class ProcessProbeProcessLauncher : IProbeProcessLauncher
{
    public async Task<ProbeProcessOutput> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(timeout, cancellationToken);
        if (await Task.WhenAny(exitTask, timeoutTask) == timeoutTask)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            return new ProbeProcessOutput(-1, string.Empty, TimedOut: true);
        }

        await exitTask;
        return new ProbeProcessOutput(process.ExitCode, await outputTask);
    }
}

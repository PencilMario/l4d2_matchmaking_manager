using L4d2Matchmaking.Contracts;

namespace L4d2LobbyAgent.Reporting;

public sealed record PlayerEntryReportingOptions(
    Uri? Origin,
    string? Token,
    int QueueCapacity = 4096,
    int BatchSize = PlayerEntryStatisticsContract.MaxBatchSize,
    TimeSpan RequestTimeout = default)
{
    public bool Enabled => Origin is not null && !string.IsNullOrWhiteSpace(Token);

    public static PlayerEntryReportingOptions FromEnvironment() => FromConfiguration(
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PLAYER_ENTRY_REPORTING_ORIGIN"] = Environment.GetEnvironmentVariable("PLAYER_ENTRY_REPORTING_ORIGIN"),
            ["PLAYER_ENTRY_REPORTING_TOKEN"] = Environment.GetEnvironmentVariable("PLAYER_ENTRY_REPORTING_TOKEN"),
        });

    public static PlayerEntryReportingOptions FromConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        var originText = values.GetValueOrDefault("PLAYER_ENTRY_REPORTING_ORIGIN")?.Trim();
        var token = values.GetValueOrDefault("PLAYER_ENTRY_REPORTING_TOKEN")?.Trim();
        var origin = Uri.TryCreate(originText, UriKind.Absolute, out var parsedOrigin) &&
            parsedOrigin.Scheme is "http" or "https"
            ? parsedOrigin
            : null;
        return new PlayerEntryReportingOptions(
            origin,
            string.IsNullOrWhiteSpace(token) ? null : token,
            4096,
            PlayerEntryStatisticsContract.MaxBatchSize,
            TimeSpan.FromSeconds(2));
    }
}

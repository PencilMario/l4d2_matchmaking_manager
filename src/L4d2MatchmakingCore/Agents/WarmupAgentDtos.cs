namespace L4d2MatchmakingCore.Agents;

public sealed record CreateWarmupAgentRequest(string Name, string? DownloadRegion);

public sealed record UpdateWarmupAgentRequest(string Name, string? DownloadRegion);

public sealed record WarmupAgentResponse(
    Guid Id,
    string Name,
    string Status,
    string? DownloadRegion,
    int NoVncPort,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

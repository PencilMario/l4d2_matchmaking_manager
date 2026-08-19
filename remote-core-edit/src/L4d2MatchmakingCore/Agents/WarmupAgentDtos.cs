namespace L4d2MatchmakingCore.Agents;

public sealed record CreateWarmupAgentRequest(string Name, string? DownloadRegion, bool KeepVncAlive = false);

public sealed record UpdateWarmupAgentRequest(string Name, string? DownloadRegion, bool KeepVncAlive = false);

public sealed record WarmupAgentResponse(
    Guid Id,
    string Name,
    string Status,
    string? DownloadRegion,
    bool KeepVncAlive,
    int NoVncPort,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool Ready = false);

public sealed record OpenAgentVncSessionResponse(string Url, DateTimeOffset ExpiresAt);

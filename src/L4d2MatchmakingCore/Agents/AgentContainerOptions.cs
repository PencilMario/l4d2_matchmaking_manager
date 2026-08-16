using Microsoft.Extensions.Configuration;

namespace L4d2MatchmakingCore.Agents;

public sealed record AgentContainerOptions(
    string Image,
    string SharedLibraryHostPath,
    string Network,
    int NoVncPortStart,
    int NoVncPortEnd)
{
    public static AgentContainerOptions FromConfiguration(IConfiguration configuration) => new(
        configuration["CORE_AGENT_IMAGE"] ?? "l4d2-steam-lobby-agent:local",
        configuration["CORE_SHARED_LIBRARY_HOST_PATH"] ?? throw new InvalidOperationException("core_shared_library_path_not_configured"),
        configuration["CORE_AGENT_NETWORK"] ?? "l4d2-matchmaking",
        int.TryParse(configuration["CORE_NOVNC_PORT_START"], out var portStart) ? portStart : 18083,
        int.TryParse(configuration["CORE_NOVNC_PORT_END"], out var portEnd) ? portEnd : 18183);
}

internal static class ProbeLobbyProfile
{
    internal const uint Left4DeadAppId = 500;

    internal static int GetMemberLimit(uint appId)
    {
        EnsureLeft4Dead(appId);
        return 4;
    }

    internal static Dictionary<string, string> CreateMetadata(uint appId)
    {
        EnsureLeft4Dead(appId);
        return CreateLeft4DeadMetadata();
    }

    private static Dictionary<string, string> CreateLeft4DeadMetadata() => new(StringComparer.Ordinal)
    {
        ["Game:campaign"] = "Farm",
        ["Game:chapter"] = "1",
        ["Game:difficulty"] = "Impossible",
        ["Game:dlcrequired"] = "0",
        ["Game:maxrounds"] = "3",
        ["Game:MissionInfo:Author"] = "Valve",
        ["Game:MissionInfo:BuiltIn"] = "1",
        ["Game:MissionInfo:DisplayTitle"] = "#L4D360UI_Campaign_Farm",
        ["Game:MissionInfo:Version"] = "1",
        ["Game:MissionInfo:Website"] = "http://store.steampowered.com/app/500/",
        ["Game:mode"] = "coop",
        ["Game:state"] = "lobby",
        ["Members:numMachines"] = "1",
        ["Members:numPlayers"] = "1",
        ["Members:numSlots"] = "4",
        ["Options:createreason"] = "searchempty",
        ["Options:Server"] = "dedicated",
        ["System:access"] = "public",
        ["System:lock"] = string.Empty,
        ["System:network"] = "LIVE",
    };

    private static void EnsureLeft4Dead(uint appId)
    {
        if (appId != Left4DeadAppId)
            throw new InvalidOperationException("left4dead_app_id_required");
    }
}

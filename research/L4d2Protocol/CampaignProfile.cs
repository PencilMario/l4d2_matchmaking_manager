internal sealed record CampaignProfile(
    string CampaignId,
    string DisplayTitle,
    string MissionFile,
    string Author,
    int SurvivorSet)
{
    internal static IReadOnlyList<CampaignProfile> Official { get; } =
    [
        // This is the L4D1 profile captured from a live AppID 500 public lobby.
        // More campaigns should only be added after their exact metadata is captured.
        new("Farm", "#L4D360UI_Campaign_Farm", string.Empty, "Valve", 1),
    ];

    internal static CampaignProfile SelectRandom() => Official[Random.Shared.Next(Official.Count)];
}

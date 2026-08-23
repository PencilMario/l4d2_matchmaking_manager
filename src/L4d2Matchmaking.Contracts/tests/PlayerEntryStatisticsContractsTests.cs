using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2Matchmaking.Contracts.Tests;

[TestClass]
public sealed class PlayerEntryStatisticsContractsTests
{
    [TestMethod]
    public void AgentOperationRequestKeepsTheExistingConstructorAndReadsLegacyJson()
    {
        var operationId = Guid.NewGuid();
        var request = new AgentOperationRequest(operationId, AgentLobbyMode.Standard, "127.0.0.1", 27015, "secret")
        {
            GameMode = PlayerEntryStatisticsContract.GameModeCoop,
        };

        Assert.IsNull(request.EntryStatisticsContext);

        var restored = JsonSerializer.Deserialize<AgentOperationRequest>(
            $"{{\"OperationId\":\"{operationId}\",\"Mode\":0,\"Ipv4Address\":\"127.0.0.1\",\"Port\":27015,\"RconPassword\":\"secret\",\"GameMode\":\"coop\"}}");

        Assert.IsNotNull(restored);
        Assert.AreEqual(operationId, restored.OperationId);
        Assert.AreEqual(AgentLobbyMode.Standard, restored.Mode);
        Assert.AreEqual("secret", restored.RconPassword);
        Assert.AreEqual(PlayerEntryStatisticsContract.GameModeCoop, restored.GameMode);
        Assert.IsNull(restored.EntryStatisticsContext);
    }

    [TestMethod]
    public void AgentOperationRequestCarriesAnInitOnlyEntryStatisticsContext()
    {
        var context = new EntryStatisticsContext(
            Guid.NewGuid(),
            "Agent snapshot",
            "hongkong",
            Guid.NewGuid(),
            "203.0.113.10:27015",
            "Target snapshot",
            PlayerEntryStatisticsContract.GameModeVersus);

        var request = new AgentOperationRequest(Guid.NewGuid(), AgentLobbyMode.Reserved, "127.0.0.1", 27015)
        {
            EntryStatisticsContext = context,
        };

        var restored = JsonSerializer.Deserialize<AgentOperationRequest>(JsonSerializer.Serialize(request));

        Assert.IsNotNull(restored);
        Assert.AreEqual(context, restored.EntryStatisticsContext);
    }

    [TestMethod]
    public void GameModeAndDownloadRegionNormalizationUseContractDefaults()
    {
        Assert.AreEqual(PlayerEntryStatisticsContract.GameModeVersus, PlayerEntryStatisticsContract.NormalizeGameMode(null));
        Assert.AreEqual(PlayerEntryStatisticsContract.GameModeVersus, PlayerEntryStatisticsContract.NormalizeGameMode("  \t"));
        Assert.AreEqual(PlayerEntryStatisticsContract.GameModeCoop, PlayerEntryStatisticsContract.NormalizeGameMode(" COOP "));
        Assert.AreEqual(PlayerEntryStatisticsContract.GameModeVersus, PlayerEntryStatisticsContract.NormalizeGameMode("VERSUS"));
        Assert.AreEqual(PlayerEntryStatisticsContract.GameModeVersus, PlayerEntryStatisticsContract.NormalizeGameMode("unknown"));

        Assert.IsNull(PlayerEntryStatisticsContract.NormalizeDownloadRegion(null));
        Assert.IsNull(PlayerEntryStatisticsContract.NormalizeDownloadRegion("  \t"));
        Assert.AreEqual("33", PlayerEntryStatisticsContract.NormalizeDownloadRegion(" hongkong "));
    }

    [TestMethod]
    public void PlayerEntryEventSerializesAndRoundTripsWithoutSteamIdentifiers()
    {
        var entryEvent = new PlayerEntryEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "lobby-1",
            PlayerEntryStatisticsContract.LobbyTypeReserved,
            Guid.NewGuid(),
            "Agent snapshot",
            null,
            Guid.NewGuid(),
            "203.0.113.10:27015",
            null,
            PlayerEntryStatisticsContract.GameModeVersus);

        var json = JsonSerializer.Serialize(entryEvent);
        var restored = JsonSerializer.Deserialize<PlayerEntryEvent>(json);

        Assert.IsFalse(json.Contains("SteamId", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(restored);
        Assert.AreEqual(entryEvent, restored);
    }

    [TestMethod]
    public void PlayerEntryBatchContractsExposeTheMaximumAndAcceptedDuplicateCounts()
    {
        var entryEvent = new PlayerEntryEvent(
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid(),
            "lobby-1",
            PlayerEntryStatisticsContract.LobbyTypeStandard,
            Guid.NewGuid(),
            "Agent snapshot",
            "hongkong",
            Guid.NewGuid(),
            "203.0.113.10:27015",
            "Target snapshot",
            PlayerEntryStatisticsContract.GameModeCoop);
        var request = new PlayerEntryEventBatchRequest([entryEvent]);
        var response = new PlayerEntryEventBatchResponse(1, 2);

        Assert.AreEqual(100, PlayerEntryStatisticsContract.MaxBatchSize);
        Assert.AreEqual(entryEvent, request.Events.Single());
        Assert.AreEqual(1, response.Accepted);
        Assert.AreEqual(2, response.Duplicates);
    }
}

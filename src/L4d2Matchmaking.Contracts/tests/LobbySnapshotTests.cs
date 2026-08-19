using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2Matchmaking.Contracts.Tests;

[TestClass]
public sealed class LobbySnapshotTests
{
    [TestMethod]
    public void LobbySnapshotSerializesSteamIdentifiersAsStrings()
    {
        var snapshot = new LobbySnapshot(
            "109775242170052468",
            "76561198000000000",
            [new LobbyMemberSnapshot("76561198000000000", "Agent")],
            new Dictionary<string, string> { ["Game:campaign"] = "L4D2C2" },
            DateTimeOffset.UnixEpoch);

        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<LobbySnapshot>(json);

        StringAssert.Contains(json, "\"109775242170052468\"");
        Assert.IsNotNull(restored);
        Assert.AreEqual(snapshot.LobbyId, restored.LobbyId);
        Assert.AreEqual(snapshot.OwnerSteamId, restored.OwnerSteamId);
        CollectionAssert.AreEqual(snapshot.Members.ToArray(), restored.Members.ToArray());
        CollectionAssert.AreEquivalent(snapshot.Metadata.ToArray(), restored.Metadata.ToArray());
        Assert.AreEqual(snapshot.ObservedAt, restored.ObservedAt);
    }

    [TestMethod]
    public void LobbyMemberAvatarUrlIsOptionalAndSerializable()
    {
        var member = new LobbyMemberSnapshot("76561198000000000", "Agent", "https://cdn.example/avatar.jpg");
        var restored = JsonSerializer.Deserialize<LobbyMemberSnapshot>(JsonSerializer.Serialize(member));

        Assert.IsNotNull(restored);
        Assert.AreEqual(member.AvatarUrl, restored.AvatarUrl);
        Assert.IsNull(new LobbyMemberSnapshot("76561198000000001", "Player").AvatarUrl);
    }
}

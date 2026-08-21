using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class ProbeLobbyProfileTests
{
    [TestMethod]
    public void Left4DeadProfileUsesObservedFourPlayerFarmMetadata()
    {
        var metadata = ProbeLobbyProfile.CreateMetadata(500);

        Assert.AreEqual(4, ProbeLobbyProfile.GetMemberLimit(500));
        Assert.AreEqual("Farm", metadata["Game:campaign"]);
        Assert.AreEqual("#L4D360UI_Campaign_Farm", metadata["Game:MissionInfo:DisplayTitle"]);
        Assert.AreEqual("http://store.steampowered.com/app/500/", metadata["Game:MissionInfo:Website"]);
        Assert.AreEqual("coop", metadata["Game:mode"]);
        Assert.AreEqual("4", metadata["Members:numSlots"]);
        Assert.AreEqual("public", metadata["System:access"]);
        Assert.AreEqual(20, metadata.Count);
    }

    [TestMethod]
    public void NonLeft4DeadAppIdIsRejected()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => ProbeLobbyProfile.CreateMetadata(550));

        Assert.AreEqual("left4dead_app_id_required", exception.Message);
    }
}

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
    public void ExistingLeft4Dead2ProbeProfileRemainsDefault()
    {
        var metadata = ProbeLobbyProfile.CreateMetadata(550);

        Assert.AreEqual(8, ProbeLobbyProfile.GetMemberLimit(550));
        Assert.AreEqual("c1m1_hotel", metadata["game:map"]);
        Assert.AreEqual("8", metadata["members:numSlots"]);
    }
}

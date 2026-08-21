using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2Protocol.Tests;

[TestClass]
public sealed class CampaignProfileTests
{
    [TestMethod]
    public void OfficialProfilesContainOnlyTheLiveCapturedL4d1FarmProfile()
    {
        CollectionAssert.AreEqual(
            new[] { new CampaignProfile("Farm", "#L4D360UI_Campaign_Farm", string.Empty, "Valve", 1) },
            CampaignProfile.Official.ToArray());
    }

    [TestMethod]
    public void DefaultMetadataMatchesTheCapturedAppId500Lobby()
    {
        var metadata = RealSessionSettings.CreateLobbyMetadata();

        Assert.AreEqual(20, metadata.Count);
        Assert.AreEqual("Farm", metadata["Game:campaign"]);
        Assert.AreEqual("Impossible", metadata["Game:difficulty"]);
        Assert.AreEqual("#L4D360UI_Campaign_Farm", metadata["Game:MissionInfo:DisplayTitle"]);
        Assert.AreEqual("http://store.steampowered.com/app/500/", metadata["Game:MissionInfo:Website"]);
        Assert.AreEqual("dedicated", metadata["Options:Server"]);
        Assert.AreEqual("searchempty", metadata["Options:createreason"]);
        Assert.AreEqual("4", metadata["Members:numSlots"]);
        Assert.AreEqual("coop", metadata["Game:mode"]);
    }

    [TestMethod]
    public void VersusMetadataUsesEightSlots()
    {
        var metadata = RealSessionSettings.CreateLobbyMetadata(CampaignProfile.Official[0], "versus");

        Assert.AreEqual("8", metadata["Members:numSlots"]);
        Assert.AreEqual("versus", metadata["Game:mode"]);
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2Protocol.Tests;

[TestClass]
public sealed class CampaignProfileTests
{
    [TestMethod]
    public void OfficialProfilesContainTheCompleteC1ToC14Catalog()
    {
        var expected = new[]
        {
            new CampaignProfile("L4D2C1", "#L4D360UI_CampaignName_C1", "missions/campaign1.txt", "Valve", 2),
            new CampaignProfile("L4D2C2", "#L4D360UI_CampaignName_C2", "missions/campaign2.txt", "Valve", 2),
            new CampaignProfile("L4D2C3", "#L4D360UI_CampaignName_C3", "missions/campaign3.txt", "Valve", 2),
            new CampaignProfile("L4D2C4", "#L4D360UI_CampaignName_C4", "missions/campaign4.txt", "Valve", 2),
            new CampaignProfile("L4D2C5", "#L4D360UI_CampaignName_C5", "missions/campaign5.txt", "Valve", 2),
            new CampaignProfile("L4D2C6", "#L4D360UI_CampaignName_C6", "missions/campaign6.txt", "Valve", 2),
            new CampaignProfile("L4D2C7", "#L4D360UI_CampaignName_C7", "missions/campaign7.txt", "Valve", 1),
            new CampaignProfile("L4D2C8", "#L4D360UI_CampaignName_C8", "missions/campaign8.txt", "Valve", 1),
            new CampaignProfile("L4D2C9", "#L4D360UI_CampaignName_C9", "missions/campaign9.txt", "Valve", 1),
            new CampaignProfile("L4D2C10", "#L4D360UI_CampaignName_C10", "missions/campaign10.txt", "Valve", 1),
            new CampaignProfile("L4D2C11", "#L4D360UI_CampaignName_C11", "missions/campaign11.txt", "Valve", 1),
            new CampaignProfile("L4D2C12", "#L4D360UI_CampaignName_C12", "missions/campaign12.txt", "Valve", 1),
            new CampaignProfile("L4D2C13", "#L4D360UI_CampaignName_C13", "missions/campaign13.txt", "Valve", 2),
            new CampaignProfile("L4D2C14", "#L4D360UI_CampaignName_C14", "missions/campaign14.txt", "Valve, NF, Roku, Jaiz, Wolphin", 1),
        };

        CollectionAssert.AreEqual(expected, CampaignProfile.Official.ToArray());
    }

    [TestMethod]
    public void ProfileControlsAllMapMetadataAsOneUnit()
    {
        var c14 = CampaignProfile.Official.Single(profile => profile.CampaignId == "L4D2C14");

        var metadata = RealSessionSettings.CreateLobbyMetadata(c14);

        Assert.AreEqual(28, metadata.Count);
        Assert.AreEqual("L4D2C14", metadata["Game:campaign"]);
        Assert.AreEqual("#L4D360UI_CampaignName_C14", metadata["Game:MissionInfo:DisplayTitle"]);
        Assert.AreEqual("missions/campaign14.txt", metadata["Game:MissionInfo:MissionFile"]);
        Assert.AreEqual("Valve, NF, Roku, Jaiz, Wolphin", metadata["Game:MissionInfo:Author"]);
        Assert.AreEqual("1", metadata["Game:MissionInfo:SurvivorSet"]);
    }

    [TestMethod]
    public void ParameterlessMetadataKeepsTheHistoricalC2Profile()
    {
        var metadata = RealSessionSettings.CreateLobbyMetadata();

        Assert.AreEqual("L4D2C2", metadata["Game:campaign"]);
        Assert.AreEqual("missions/campaign2.txt", metadata["Game:MissionInfo:MissionFile"]);
    }
}

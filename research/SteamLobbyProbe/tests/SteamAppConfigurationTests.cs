using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SteamLobbyProbe.Tests;

[TestClass]
public sealed class SteamAppConfigurationTests
{
    [TestMethod]
    public void MissingAppIdUsesL4d2Default()
    {
        Assert.AreEqual(550U, SteamAppConfiguration.ParseAppId(null));
        Assert.AreEqual(550U, SteamAppConfiguration.ParseAppId(string.Empty));
    }

    [TestMethod]
    public void L4d1AppIdIsAccepted()
    {
        Assert.AreEqual(500U, SteamAppConfiguration.ParseAppId("500"));
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("left4dead")]
    public void InvalidAppIdIsRejected(string value)
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => SteamAppConfiguration.ParseAppId(value));

        Assert.AreEqual("steam_app_id_invalid", exception.Message);
    }
}

using L4d2AsfPlugin;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2AsfPlugin.Tests;

[TestClass]
public sealed class LobbyMetadataTests
{
    [TestMethod]
    public void SessionMetadataContainsTheRealProfileWithoutServerFields()
    {
        var metadata = LobbyMetadata.CreateSessionMetadata();

        Assert.AreEqual("versus", metadata["Game:Mode"]);
        Assert.AreEqual("game", metadata["Game:state"]);
        Assert.AreEqual(28, metadata.Count);
        Assert.IsFalse(metadata.Keys.Any(static key => key.StartsWith("Server:", StringComparison.Ordinal)));
    }
}

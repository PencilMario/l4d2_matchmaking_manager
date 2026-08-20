using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class MigrationDiscoveryTests
{
    [TestMethod]
    public void SteamWebApiKeyMigrationIsDiscovered()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=matchmaking;Username=matchmaking;Password=unused")
            .Options;
        using var context = new MatchmakingDbContext(options);

        CollectionAssert.Contains(
            context.Database.GetMigrations().ToList(),
            "202608190003_SteamWebApiKey");
    }
}

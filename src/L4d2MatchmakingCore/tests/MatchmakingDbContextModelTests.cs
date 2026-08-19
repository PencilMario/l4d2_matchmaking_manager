using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class MatchmakingDbContextModelTests
{
    [TestMethod]
    public void RotationCursorPriorityIsApplicationProvided()
    {
        using var db = new MatchmakingDbContext(new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var priority = db.Model.FindEntityType(typeof(TargetServerRotationCursor))!
            .FindProperty(nameof(TargetServerRotationCursor.Priority))!;

        Assert.AreEqual(ValueGenerated.Never, priority.ValueGenerated);
    }
}

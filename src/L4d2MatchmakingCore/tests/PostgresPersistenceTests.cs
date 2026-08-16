using L4d2MatchmakingCore.Data;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class PostgresPersistenceTests
{
    [TestMethod]
    public async Task OneReservationLeaseExistsPerTarget()
    {
        PostgreSqlContainer container;

        try
        {
            container = new PostgreSqlBuilder("postgres:17")
                .WithDatabase("matchmaking")
                .WithUsername("matchmaking")
                .WithPassword("matchmaking-test-password")
                .Build();
            await container.StartAsync();
        }
        catch (DockerUnavailableException exception)
        {
            Assert.Inconclusive($"PostgreSQL container is unavailable: {exception.Message}");
            return;
        }

        await using (container)
        {
            var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
                .UseNpgsql(container.GetConnectionString())
                .Options;
            await using var context = new MatchmakingDbContext(options);
            await context.Database.MigrateAsync();
            var targetServerId = Guid.NewGuid();

            context.ReservationLeases.Add(new ReservationLease
            {
                TargetServerId = targetServerId,
                OperationId = Guid.NewGuid(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
            });
            await context.SaveChangesAsync();
            context.ReservationLeases.Add(new ReservationLease
            {
                TargetServerId = targetServerId,
                OperationId = Guid.NewGuid(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
            });

            await Assert.ThrowsExceptionAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}

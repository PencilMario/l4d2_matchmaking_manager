using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using L4d2MatchmakingCore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WarmupStatusEndpointTests
{
    [TestMethod]
    public async Task ListReturnsOnlyCurrentWarmupsWithoutSensitiveConfiguration()
    {
        using var environment = new CoreTestEnvironment();
        await using var factory = new ServerFactory();
        using var anonymous = factory.CreateClient();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CoreTestEnvironment.ApiToken);
        var target = new TargetServer
        {
            Id = Guid.NewGuid(), Host = "203.0.113.7", Port = 27083, RequiresReservation = true,
            AttemptWindowSeconds = 720, RconPasswordCiphertext = "sensitive-ciphertext",
        };
        var agent = new WarmupAgent
        {
            Id = Guid.NewGuid(), Name = "status-agent", Status = "running",
            SteamDataVolumeName = "sensitive-steam-volume", AccountConfigVolumeName = "sensitive-account-volume",
            ContainerId = "sensitive-container-id", NoVncPort = 18083,
        };
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
            db.AddRange(target, agent,
                CreateAttempt(target.Id, agent.Id, "active", startedAt),
                CreateAttempt(target.Id, agent.Id, "uncertain", startedAt.AddSeconds(1)),
                CreateAttempt(target.Id, agent.Id, "completed", startedAt.AddSeconds(2)));
            await db.SaveChangesAsync();
        }

        var unauthorized = await anonymous.GetAsync("/v1/warmups");
        var response = await client.GetAsync("/v1/warmups");

        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(JsonValueKind.Array, payload.RootElement.ValueKind);
        Assert.AreEqual(2, payload.RootElement.GetArrayLength());
        var states = payload.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("state").GetString())
            .ToHashSet();
        CollectionAssert.AreEquivalent(new[] { "active", "uncertain" }, states.ToArray());
        var first = payload.RootElement[0];
        var second = payload.RootElement[1];
        Assert.AreEqual(target.Id, first.GetProperty("targetServerId").GetGuid());
        Assert.AreEqual("203.0.113.7:27083", first.GetProperty("targetEndpoint").GetString());
        Assert.AreEqual(agent.Id, first.GetProperty("warmupAgentId").GetGuid());
        Assert.AreEqual("status-agent", first.GetProperty("warmupAgentName").GetString());
        Assert.IsTrue(first.GetProperty("deadline").GetDateTimeOffset() > first.GetProperty("startedAt").GetDateTimeOffset());
        Assert.AreEqual(first.GetProperty("startedAt").GetDateTimeOffset(), second.GetProperty("startedAt").GetDateTimeOffset());
        Assert.AreEqual(first.GetProperty("deadline").GetDateTimeOffset(), second.GetProperty("deadline").GetDateTimeOffset());
        Assert.IsTrue(first.GetProperty("remainingSeconds").GetInt32() > 0);
        var responseText = payload.RootElement.GetRawText();
        Assert.IsFalse(responseText.Contains("sensitive-ciphertext", StringComparison.Ordinal));
        Assert.IsFalse(responseText.Contains("sensitive-container-id", StringComparison.Ordinal));
        Assert.IsFalse(responseText.Contains("sensitive-steam-volume", StringComparison.Ordinal));
        Assert.IsFalse(responseText.Contains("sensitive-account-volume", StringComparison.Ordinal));
    }

    private static WarmupAttempt CreateAttempt(Guid targetServerId, Guid warmupAgentId, string state, DateTimeOffset startedAt) => new()
    {
        Id = Guid.NewGuid(),
        TargetServerId = targetServerId,
        WarmupAgentId = warmupAgentId,
        OperationId = Guid.NewGuid(),
        LobbyId = "109775242170052468",
        Mode = "reserved",
        State = state,
        Phase = "awaiting_first_member",
        StartedAt = startedAt,
        LobbyReadyAt = startedAt.AddSeconds(5),
        ObservedAt = startedAt.AddSeconds(10),
    };

    private sealed class ServerFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MatchmakingDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MatchmakingDbContext>>();
                services.AddDbContext<MatchmakingDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }

    private sealed class CoreTestEnvironment : IDisposable
    {
        public const string ApiToken = "test-warmup-status-token";
        private readonly string? _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        private readonly string? _token = Environment.GetEnvironmentVariable("CORE_API_TOKEN");
        private readonly string? _connection = Environment.GetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING");

        public CoreTestEnvironment()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", ApiToken);
            Environment.SetEnvironmentVariable(
                "CORE_DATABASE_CONNECTION_STRING",
                "Host=127.0.0.1;Database=matchmaking_test;Username=matchmaking;Password=unused");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _environment);
            Environment.SetEnvironmentVariable("CORE_API_TOKEN", _token);
            Environment.SetEnvironmentVariable("CORE_DATABASE_CONNECTION_STRING", _connection);
        }
    }
}

using System.Net;
using L4d2MatchmakingCore.A2s;
using L4d2MatchmakingCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class TargetServerObservationCollectorTests
{
    [TestMethod]
    public async Task RefreshOnceAsyncStoresOnlineA2sObservation()
    {
        var target = CreateTarget("127.0.0.1", 27015);
        using var services = await CreateServicesAsync(target);
        var store = new TargetServerObservationStore();
        var a2s = new FakeA2s();
        var collector = CreateCollector(services, store, a2s);

        await collector.RefreshOnceAsync(CancellationToken.None);

        Assert.AreEqual(1, a2s.Calls);
        var observation = store.Get(target.Id);
        Assert.IsNotNull(observation);
        Assert.AreEqual("online", observation.Status);
        Assert.AreEqual("L4D1 test server", observation.ServerName);
        Assert.AreEqual(2, observation.PlayerCount);
        Assert.AreEqual(12, observation.MaxPlayers);
        Assert.IsTrue(observation.ObservedAt > DateTimeOffset.UtcNow.AddSeconds(-2));
    }

    [TestMethod]
    public async Task RefreshOnceAsyncStoresUnavailableSnapshotsForFailedTargets()
    {
        var timeout = CreateTarget("127.0.0.1", 27016);
        var dnsFailure = CreateTarget("invalid host", 27017);
        var invalidPacket = CreateTarget("127.0.0.1", 27018);
        using var services = await CreateServicesAsync(timeout, dnsFailure, invalidPacket);
        var store = new TargetServerObservationStore();
        var collector = CreateCollector(services, store, new FakeA2s());

        await collector.RefreshOnceAsync(CancellationToken.None);

        foreach (var target in new[] { timeout, dnsFailure, invalidPacket })
        {
            var observation = store.Get(target.Id);
            Assert.IsNotNull(observation);
            Assert.AreEqual("unavailable", observation.Status);
            Assert.IsNull(observation.ServerName);
            Assert.IsNull(observation.PlayerCount);
            Assert.IsNull(observation.MaxPlayers);
            Assert.IsNotNull(observation.ObservedAt);
        }
    }

    [TestMethod]
    public void StoreHasNoSnapshotBeforeFirstObservation()
    {
        var store = new TargetServerObservationStore();
        var targetServerId = Guid.NewGuid();

        Assert.IsNull(store.Get(targetServerId));
        store.Replace(targetServerId, new TargetServerObservation(targetServerId, "online", "test", 1, 4, DateTimeOffset.UtcNow));
        Assert.IsNotNull(store.Get(targetServerId));
    }

    [TestMethod]
    public async Task RefreshOnceAsyncReplacesOnlineValuesWhenLaterObservationFails()
    {
        var target = CreateTarget("127.0.0.1", 27015);
        using var services = await CreateServicesAsync(target);
        var store = new TargetServerObservationStore();
        var a2s = new FakeA2s();
        var collector = CreateCollector(services, store, a2s);
        await collector.RefreshOnceAsync(CancellationToken.None);

        a2s.FailDefaultEndpoint = true;
        await collector.RefreshOnceAsync(CancellationToken.None);

        var observation = store.Get(target.Id);
        Assert.IsNotNull(observation);
        Assert.AreEqual("unavailable", observation.Status);
        Assert.IsNull(observation.ServerName);
        Assert.IsNull(observation.PlayerCount);
        Assert.IsNull(observation.MaxPlayers);
        Assert.IsNotNull(observation.ObservedAt);
    }

    [TestMethod]
    public async Task RefreshOnceAsyncDoesNotRejectServersByAppId()
    {
        var target = CreateTarget("127.0.0.1", 27015);
        using var services = await CreateServicesAsync(target);
        var store = new TargetServerObservationStore();
        var collector = CreateCollector(services, store, new FakeA2s { AppId = 550 });

        await collector.RefreshOnceAsync(CancellationToken.None);

        Assert.AreEqual("online", store.Get(target.Id)?.Status);
    }

    private static TargetServerObservationCollector CreateCollector(
        ServiceProvider services,
        TargetServerObservationStore store,
        ISourceA2sClient a2s) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), a2s, store);

    private static async Task<ServiceProvider> CreateServicesAsync(params TargetServer[] targets)
    {
        var collection = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        collection.AddDbContext<MatchmakingDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        var services = collection.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
        dbContext.TargetServers.AddRange(targets);
        await dbContext.SaveChangesAsync();
        return services;
    }

    private static TargetServer CreateTarget(string host, int port) => new()
    {
        Id = Guid.NewGuid(),
        Host = host,
        Port = port,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private sealed class FakeA2s : ISourceA2sClient
    {
        public bool FailDefaultEndpoint { get; set; }
        public uint AppId { get; set; } = 500;
        public int Calls { get; private set; }

        public Task<A2sServerInfo> GetInfoAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            Calls++;
            return endpoint.Port switch
            {
                27016 => Task.FromException<A2sServerInfo>(new TimeoutException("a2s_query_timeout")),
                27018 => Task.FromException<A2sServerInfo>(new InvalidDataException("a2s_invalid_info_response")),
                _ when FailDefaultEndpoint => Task.FromException<A2sServerInfo>(new TimeoutException("a2s_query_timeout")),
                _ => Task.FromResult(new A2sServerInfo("L4D1 test server", 2, 12, DateTimeOffset.UtcNow, AppId)),
            };
        }
    }
}

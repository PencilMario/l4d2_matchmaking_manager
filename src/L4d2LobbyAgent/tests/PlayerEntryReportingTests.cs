using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using L4d2LobbyAgent.Reporting;
using L4d2Matchmaking.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2LobbyAgent.Tests;

[TestClass]
public sealed class PlayerEntryReportingTests
{
    [TestMethod]
    public async Task QueueIsBoundedAndTryEnqueueDoesNotWaitWhenFull()
    {
        await using var queue = new PlayerEntryEventQueue(2);
        var first = Event();
        var second = Event();

        Assert.IsTrue(queue.TryEnqueue(first));
        Assert.IsTrue(queue.TryEnqueue(second));
        Assert.IsFalse(queue.TryEnqueue(Event()));

        var batch = await queue.ReadBatchAsync(10, CancellationToken.None);
        CollectionAssert.AreEqual(new[] { first, second }, batch.ToArray());
    }

    [TestMethod]
    public async Task UploaderSendsBatchWithBearerTokenAndRetriesOneServerFailure()
    {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new PlayerEntryEventBatchResponse(1, 0)),
            });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://core:8080") };
        await using var queue = new PlayerEntryEventQueue(8);
        var options = new PlayerEntryReportingOptions(
            new Uri("http://core:8080"),
            "agent-secret",
            8,
            PlayerEntryStatisticsContract.MaxBatchSize,
            TimeSpan.FromSeconds(2));
        var uploader = new PlayerEntryEventUploader(queue, client, options, NullLogger<PlayerEntryEventUploader>.Instance);

        var entry = Event();
        await uploader.UploadBatchAsync([entry], CancellationToken.None);

        Assert.AreEqual(2, handler.Requests.Count);
        Assert.AreEqual("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.AreEqual("agent-secret", handler.Requests[0].Headers.Authorization?.Parameter);
        var body = JsonSerializer.Deserialize<PlayerEntryEventBatchRequest>(
            handler.Bodies[1],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(body);
        Assert.AreEqual(entry, body.Events.Single());
    }

    [TestMethod]
    public void MissingReportingEnvironmentDisablesTheReporter()
    {
        var options = PlayerEntryReportingOptions.FromConfiguration(
            new Dictionary<string, string?>
            {
                ["PLAYER_ENTRY_REPORTING_ORIGIN"] = "",
                ["PLAYER_ENTRY_REPORTING_TOKEN"] = "",
            });

        Assert.IsFalse(options.Enabled);
    }

    private static PlayerEntryEvent Event() => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        Guid.NewGuid(),
        "lobby-1",
        PlayerEntryStatisticsContract.LobbyTypeStandard,
        Guid.NewGuid(),
        "agent",
        null,
        Guid.NewGuid(),
        "127.0.0.1:27015",
        null,
        PlayerEntryStatisticsContract.GameModeVersus);

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        private int _index;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            var response = responses[Math.Min(_index++, responses.Length - 1)];
            return response;
        }
    }
}

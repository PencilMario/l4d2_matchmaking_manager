using System.Net;
using System.Text;
using System.Text.Json;
using L4d2MatchmakingCore.Profiles;
using L4d2MatchmakingCore.Settings;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace L4d2MatchmakingCore.Tests;

[TestClass]
public sealed class SteamProfileServiceTests
{
    [TestMethod]
    public async Task ResolvesProfilesAndCachesSuccessfulResponses()
    {
        var handler = new ProfileHandler(_ => JsonResponse([
            new { steamid = "76561198000000000", personaname = "Player One", avatarmedium = "https://cdn.example/one.jpg" },
        ]));
        var service = CreateService(handler);

        var first = await service.ResolveAsync(["76561198000000000"], CancellationToken.None);
        var second = await service.ResolveAsync(["76561198000000000"], CancellationToken.None);

        Assert.AreEqual("Player One", first["76561198000000000"].PersonaName);
        Assert.AreEqual("https://cdn.example/one.jpg", first["76561198000000000"].AvatarUrl);
        Assert.AreEqual(1, handler.Requests.Count);
        Assert.AreEqual(first["76561198000000000"], second["76561198000000000"]);
    }

    [TestMethod]
    public async Task SplitsRequestsIntoBatchesOfOneHundredIds()
    {
        var handler = new ProfileHandler(request => JsonResponse(
            QueryHelpers.ParseQuery(request.RequestUri!.Query)["steamids"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => new { steamid = id, personaname = id, avatarmedium = (string?)null })));
        var service = CreateService(handler);
        var ids = Enumerable.Range(0, 101).Select(index => $"7656119800000{index:00000}").ToArray();

        var result = await service.ResolveAsync(ids, CancellationToken.None);

        Assert.AreEqual(101, result.Count);
        Assert.AreEqual(2, handler.Requests.Count);
        Assert.IsTrue(handler.Requests.All(request => QueryHelpers.ParseQuery(request.RequestUri!.Query)["steamids"].ToString().Split(',').Length <= 100));
    }

    [TestMethod]
    public async Task ReturnsEmptyProfilesWhenSteamApiFails()
    {
        var handler = new ProfileHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        var result = await service.ResolveAsync(["76561198000000000"], CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.IsNull(result["76561198000000000"].PersonaName);
        Assert.IsNull(result["76561198000000000"].AvatarUrl);
    }

    [TestMethod]
    public async Task DoesNotCallSteamApiWithoutAKey()
    {
        var handler = new ProfileHandler(_ => JsonResponse(Array.Empty<object>()));
        var service = CreateService(handler, null);

        var result = await service.ResolveAsync(["76561198000000000"], CancellationToken.None);

        Assert.AreEqual(0, handler.Requests.Count);
        Assert.AreEqual(1, result.Count);
    }

    private static SteamProfileService CreateService(ProfileHandler handler, string? key = "test-key") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.steampowered.com") }, new FakeKeyProvider(key));

    private static HttpResponseMessage JsonResponse<T>(IEnumerable<T> players)
    {
        var body = JsonSerializer.Serialize(new { response = new { players } });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class FakeKeyProvider(string? key) : ISteamWebApiKeyProvider
    {
        public Task<string?> GetSteamWebApiKeyAsync(CancellationToken cancellationToken) => Task.FromResult(key);
    }

    private sealed class ProfileHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}

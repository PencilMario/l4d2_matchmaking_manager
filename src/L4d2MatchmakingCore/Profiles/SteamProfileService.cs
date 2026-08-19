using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using L4d2MatchmakingCore.Settings;
using Microsoft.AspNetCore.WebUtilities;

namespace L4d2MatchmakingCore.Profiles;

public interface ISteamProfileService
{
    Task<IReadOnlyDictionary<string, SteamProfileData>> ResolveAsync(
        IReadOnlyCollection<string> steamIds,
        CancellationToken cancellationToken);
}

public sealed class SteamProfileService(
    HttpClient httpClient,
    ISteamWebApiKeyProvider keyProvider) : ISteamProfileService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.Ordinal);

    public async Task<IReadOnlyDictionary<string, SteamProfileData>> ResolveAsync(
        IReadOnlyCollection<string> steamIds,
        CancellationToken cancellationToken)
    {
        var ids = steamIds.Distinct(StringComparer.Ordinal).ToArray();
        var results = new Dictionary<string, SteamProfileData>(StringComparer.Ordinal);
        var missing = new List<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var id in ids)
        {
            if (cache.TryGetValue(id, out var entry) && entry.ExpiresAt > now)
                results[id] = entry.Profile;
            else
                missing.Add(id);
        }

        if (missing.Count == 0)
            return results;

        var key = await keyProvider.GetSteamWebApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            foreach (var id in missing)
                results[id] = EmptyProfile;
            return results;
        }

        foreach (var batch in missing.Chunk(BatchSize))
        {
            var batchIds = batch.ToArray();
            var profiles = await QueryBatchAsync(key, batchIds, cancellationToken);
            foreach (var id in batchIds)
            {
                var profile = profiles.GetValueOrDefault(id) ?? EmptyProfile;
                results[id] = profile;
                cache[id] = new CacheEntry(
                    profile,
                    DateTimeOffset.UtcNow + (profiles.ContainsKey(id) ? SuccessCacheDuration : FailureCacheDuration));
            }
        }

        return results;
    }

    private async Task<IReadOnlyDictionary<string, SteamProfileData>> QueryBatchAsync(
        string key,
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = QueryHelpers.AddQueryString(
                "/ISteamUser/GetPlayerSummaries/v0002/",
                new Dictionary<string, string?>
                {
                    ["key"] = key,
                    ["steamids"] = string.Join(',', ids),
                    ["format"] = "json",
                });
            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return EmptyResults(ids);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<SteamSummaryResponse>(stream, cancellationToken: cancellationToken);
            return payload?.Response?.Players?.Where(player => !string.IsNullOrWhiteSpace(player.SteamId))
                .ToDictionary(
                    player => player.SteamId!,
                    player => new SteamProfileData(player.PersonaName, player.AvatarMedium),
                    StringComparer.Ordinal)
                ?? EmptyResults(ids);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EmptyResults(ids);
        }
        catch (HttpRequestException)
        {
            return EmptyResults(ids);
        }
        catch (JsonException)
        {
            return EmptyResults(ids);
        }
    }

    private static IReadOnlyDictionary<string, SteamProfileData> EmptyResults(IEnumerable<string> ids) =>
        ids.ToDictionary(id => id, _ => EmptyProfile, StringComparer.Ordinal);

    private static readonly SteamProfileData EmptyProfile = new(null, null);

    private sealed record CacheEntry(SteamProfileData Profile, DateTimeOffset ExpiresAt);

    private sealed record SteamSummaryResponse(
        [property: JsonPropertyName("response")] SteamSummary? Response);

    private sealed record SteamSummary(
        [property: JsonPropertyName("players")] IReadOnlyList<SteamPlayer>? Players);

    private sealed record SteamPlayer(
        [property: JsonPropertyName("steamid")] string? SteamId,
        [property: JsonPropertyName("personaname")] string? PersonaName,
        [property: JsonPropertyName("avatarmedium")] string? AvatarMedium);
}

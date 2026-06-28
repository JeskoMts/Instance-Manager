using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public sealed class RobloxGamesService : IRobloxGamesService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const string GamesDetailsUrl = "https://games.roblox.com/v1/games?universeIds=";
    private const string ThumbnailsUrl =
        "https://thumbnails.roblox.com/v1/games/multiget/thumbnails?countPerUniverse=1&size=768x432&format=Png&defaults=true&universeIds=";
    private const string PlaceToUniverseUrl = "https://apis.roblox.com/universes/v1/places/{0}/universe";

    private const string ExploreSortsUrl = "https://apis.roblox.com/explore-api/v1/get-sorts?sessionId=";
    private const string GamesListUrl = "https://games.roblox.com/v1/games/list?maxRows=24&keyword=";
    private const string OmniSearchUrl = "https://apis.roblox.com/search-api/omni-search?pageType=all&searchQuery=";

    private static readonly long[] CuratedPlaceIds =
    {
        2753915549,
        4924922222,
        920587237,
        606849621,
        6516141723,
        142823291,
        1962086868,
        286090429,
        370731277,
        8737899170,
        189707,
        192800
    };

    private const int MaxGames = 30;

    private readonly HttpClient _http;

    public RobloxGamesService(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<GameInfo>> GetPopularAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<long> live = await TryGetPopularUniverseIdsAsync(cancellationToken);
            if (live.Count > 0)
            {
                IReadOnlyList<GameInfo> games = await EnrichAsync(live, cancellationToken);
                if (games.Count > 0)
                    return games;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
        }

        try
        {
            IReadOnlyList<long> curated = await ResolvePlacesToUniversesAsync(CuratedPlaceIds, cancellationToken);
            return await EnrichAsync(curated, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return Array.Empty<GameInfo>();
        }
    }

    public async Task<IReadOnlyList<GameInfo>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<GameInfo>();

        if (GameLinkParser.TryParsePlaceId(query, out long placeId))
        {
            long? universeId = await ResolvePlaceToUniverseAsync(placeId, cancellationToken);
            return universeId is long u
                ? await EnrichAsync(new[] { u }, cancellationToken)
                : Array.Empty<GameInfo>();
        }

        try
        {
            IReadOnlyList<long> ids = await TryGetSearchUniverseIdsAsync(query.Trim(), cancellationToken);
            return ids.Count == 0 ? Array.Empty<GameInfo>() : await EnrichAsync(ids, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return Array.Empty<GameInfo>();
        }
    }

    public async Task<byte[]?> GetThumbnailAsync(string? imageUrl, CancellationToken cancellationToken = default)
    {
        if (!TryGetOfficialImageUri(imageUrl, out Uri? uri))
            return null;

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            byte[]? bytes = await BoundedHttpContentReader.ReadAsync(
                response.Content,
                MaxImageBytes,
                cancellationToken);
            return bytes is { Length: > 0 } ? bytes : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<GameInfo>> EnrichAsync(IReadOnlyList<long> universeIds, CancellationToken cancellationToken)
    {
        long[] ids = universeIds.Distinct().Take(MaxGames).ToArray();
        if (ids.Length == 0)
            return Array.Empty<GameInfo>();

        string joined = string.Join(",", ids);

        Dictionary<long, GameDetail> details = await LoadDetailsAsync(joined, cancellationToken);
        if (details.Count == 0)
            return Array.Empty<GameInfo>();

        Dictionary<long, string> thumbnails = await LoadThumbnailUrlsAsync(joined, cancellationToken);

        var result = new List<GameInfo>(ids.Length);
        foreach (long id in ids)
        {
            if (!details.TryGetValue(id, out GameDetail? d) || d.RootPlaceId <= 0)
                continue;

            thumbnails.TryGetValue(id, out string? thumb);
            result.Add(new GameInfo(
                UniverseId: id,
                PlaceId: d.RootPlaceId,
                Name: string.IsNullOrWhiteSpace(d.Name) ? $"Game {d.RootPlaceId}" : d.Name!,
                CreatorName: d.Creator?.Name ?? "Unknown",
                PlayerCount: d.Playing,
                ThumbnailUrl: thumb));
        }
        return result;
    }

    private async Task<Dictionary<long, GameDetail>> LoadDetailsAsync(string joinedIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, GameDetail>();
        using HttpResponseMessage response = await _http.GetAsync(GamesDetailsUrl + joinedIds, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return map;

        DetailsResponse? payload = await response.Content.ReadFromJsonAsync<DetailsResponse>(cancellationToken);
        if (payload?.Data == null)
            return map;

        foreach (GameDetail d in payload.Data)
        {
            if (d.Id > 0)
                map[d.Id] = d;
        }
        return map;
    }

    private async Task<Dictionary<long, string>> LoadThumbnailUrlsAsync(string joinedIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, string>();
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(ThumbnailsUrl + joinedIds, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return map;

            ThumbnailsResponse? payload = await response.Content.ReadFromJsonAsync<ThumbnailsResponse>(cancellationToken);
            if (payload?.Data == null)
                return map;

            foreach (UniverseThumbnails entry in payload.Data)
            {
                ThumbnailItem? completed = entry.Thumbnails?.FirstOrDefault(
                    t => string.Equals(t.State, "Completed", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(t.ImageUrl));
                if (entry.UniverseId > 0 && completed?.ImageUrl is string url)
                    map[entry.UniverseId] = url;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
        }
        return map;
    }

    private async Task<long?> ResolvePlaceToUniverseAsync(long placeId, CancellationToken cancellationToken)
    {
        try
        {
            string url = string.Format(CultureInfo.InvariantCulture, PlaceToUniverseUrl, placeId);
            using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            UniverseResponse? payload = await response.Content.ReadFromJsonAsync<UniverseResponse>(cancellationToken);
            return payload?.UniverseId is long u && u > 0 ? u : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<long>> ResolvePlacesToUniversesAsync(IReadOnlyList<long> placeIds, CancellationToken cancellationToken)
    {
        long?[] resolved = await Task.WhenAll(placeIds.Select(p => ResolvePlaceToUniverseAsync(p, cancellationToken)));
        return resolved.Where(u => u.HasValue).Select(u => u!.Value).ToList();
    }

    private async Task<IReadOnlyList<long>> TryGetPopularUniverseIdsAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(ExploreSortsUrl + Guid.NewGuid().ToString("N"), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Array.Empty<long>();

        ExploreSortsResponse? payload = await response.Content.ReadFromJsonAsync<ExploreSortsResponse>(cancellationToken);
        if (payload?.Sorts == null)
            return Array.Empty<long>();

        var ids = new List<long>();
        foreach (ExploreSort sort in payload.Sorts)
        {
            if (sort.Games == null)
                continue;
            foreach (ExploreGame g in sort.Games)
            {
                if (g.UniverseId > 0 && !ids.Contains(g.UniverseId))
                    ids.Add(g.UniverseId);
                if (ids.Count >= MaxGames)
                    return ids;
            }
        }
        return ids;
    }

    private async Task<IReadOnlyList<long>> TryGetSearchUniverseIdsAsync(string keyword, CancellationToken cancellationToken)
    {
        string encoded = Uri.EscapeDataString(keyword);

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(GamesListUrl + encoded, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                GamesListResponse? payload = await response.Content.ReadFromJsonAsync<GamesListResponse>(cancellationToken);
                List<long> ids = payload?.Games?
                    .Where(g => g.UniverseId > 0)
                    .Select(g => g.UniverseId)
                    .Distinct()
                    .Take(MaxGames)
                    .ToList() ?? new List<long>();
                if (ids.Count > 0)
                    return ids;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
        }

        using HttpResponseMessage omni = await _http.GetAsync(
            OmniSearchUrl + encoded + "&sessionId=" + Guid.NewGuid().ToString("N"), cancellationToken);
        if (!omni.IsSuccessStatusCode)
            return Array.Empty<long>();

        OmniSearchResponse? omniPayload = await omni.Content.ReadFromJsonAsync<OmniSearchResponse>(cancellationToken);
        if (omniPayload?.SearchResults == null)
            return Array.Empty<long>();

        var result = new List<long>();
        foreach (OmniGroup group in omniPayload.SearchResults)
        {
            if (group.Contents == null)
                continue;
            foreach (OmniContent c in group.Contents)
            {
                if (c.UniverseId > 0 && !result.Contains(c.UniverseId))
                    result.Add(c.UniverseId);
                if (result.Count >= MaxGames)
                    return result;
            }
        }
        return result;
    }

    private static bool TryGetOfficialImageUri(string? value, out Uri? uri)
    {
        bool valid = Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                     uri.Scheme == Uri.UriSchemeHttps &&
                     (string.Equals(uri.Host, "rbxcdn.com", StringComparison.OrdinalIgnoreCase) ||
                      uri.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase));
        if (!valid)
            uri = null;
        return valid;
    }

    private sealed class DetailsResponse
    {
        public GameDetail[]? Data { get; set; }
    }

    private sealed class GameDetail
    {
        public long Id { get; set; }
        public long RootPlaceId { get; set; }
        public string? Name { get; set; }
        public long Playing { get; set; }
        public GameCreator? Creator { get; set; }
    }

    private sealed class GameCreator
    {
        public string? Name { get; set; }
    }

    private sealed class ThumbnailsResponse
    {
        public UniverseThumbnails[]? Data { get; set; }
    }

    private sealed class UniverseThumbnails
    {
        public long UniverseId { get; set; }
        public ThumbnailItem[]? Thumbnails { get; set; }
    }

    private sealed class ThumbnailItem
    {
        public string? State { get; set; }
        public string? ImageUrl { get; set; }
    }

    private sealed class UniverseResponse
    {
        public long UniverseId { get; set; }
    }

    private sealed class ExploreSortsResponse
    {
        public ExploreSort[]? Sorts { get; set; }
    }

    private sealed class ExploreSort
    {
        public ExploreGame[]? Games { get; set; }
    }

    private sealed class ExploreGame
    {
        public long UniverseId { get; set; }
    }

    private sealed class GamesListResponse
    {
        public GamesListGame[]? Games { get; set; }
    }

    private sealed class GamesListGame
    {
        public long UniverseId { get; set; }
    }

    private sealed class OmniSearchResponse
    {
        public OmniGroup[]? SearchResults { get; set; }
    }

    private sealed class OmniGroup
    {
        public OmniContent[]? Contents { get; set; }
    }

    private sealed class OmniContent
    {
        public long UniverseId { get; set; }
    }
}

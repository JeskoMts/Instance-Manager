using System;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

public sealed class RobloxAvatarService : IRobloxAvatarService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const string HeadshotBaseUrl =
        "https://thumbnails.roblox.com/v1/users/avatar-headshot";

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<long, Lazy<Task<byte[]?>>> _cache = new();

    public RobloxAvatarService(HttpClient http) => _http = http;

    public async Task<byte[]?> GetAvatarAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return null;

        Lazy<Task<byte[]?>> pending = _cache.GetOrAdd(
            userId,
            id => new Lazy<Task<byte[]?>>(
                () => LoadAvatarAsync(id),
                LazyThreadSafetyMode.ExecutionAndPublication));

        byte[]? result = await pending.Value.WaitAsync(cancellationToken);
        _cache.TryRemove(userId, out _);
        return result;
    }

    private async Task<byte[]?> LoadAvatarAsync(long userId)
    {
        try
        {
            string requestUrl =
                $"{HeadshotBaseUrl}?userIds={userId}&size=150x150&format=Png&isCircular=false";
            using HttpResponseMessage thumbnailResponse = await _http.GetAsync(requestUrl);
            if (!thumbnailResponse.IsSuccessStatusCode)
                return null;

            ThumbnailResponse? payload = await thumbnailResponse.Content
                .ReadFromJsonAsync<ThumbnailResponse>();
            ThumbnailResult? thumbnail = payload?.Data?.FirstOrDefault();
            if (thumbnail is null ||
                !string.Equals(thumbnail.State, "Completed", StringComparison.OrdinalIgnoreCase) ||
                !TryGetOfficialImageUri(thumbnail.ImageUrl, out Uri? imageUri))
                return null;

            using HttpResponseMessage imageResponse = await _http.GetAsync(imageUri);
            if (!imageResponse.IsSuccessStatusCode)
                return null;

            byte[]? bytes = await BoundedHttpContentReader.ReadAsync(
                imageResponse.Content,
                MaxImageBytes);
            return bytes is { Length: > 0 } ? bytes : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
            JsonException or NotSupportedException or IOException)
        {
            return null;
        }
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

    private sealed class ThumbnailResponse
    {
        public ThumbnailResult[]? Data { get; set; }
    }

    private sealed class ThumbnailResult
    {
        public string? State { get; set; }
        public string? ImageUrl { get; set; }
    }
}

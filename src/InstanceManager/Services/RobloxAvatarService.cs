using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

public sealed class RobloxAvatarService : IRobloxAvatarService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxImageUrlBytes = 2048;
    private const int MaxUsersPerLookup = 100;
    private const string HeadshotBaseUrl =
        "https://thumbnails.roblox.com/v1/users/avatar-headshot";

    private static readonly TimeSpan LookupWindow = TimeSpan.FromMilliseconds(20);

    private readonly HttpClient _http;
    private readonly string? _cacheDirectory;
    private readonly ConcurrentDictionary<long, Lazy<Task<byte[]?>>> _cache = new();
    private readonly object _lookupGate = new();
    private Dictionary<long, TaskCompletionSource<string?>>? _pendingLookups;

    public RobloxAvatarService(HttpClient http, string? cacheDirectory = null)
    {
        _http = http;
        _cacheDirectory = cacheDirectory;
    }

    public async Task<byte[]?> GetCachedAvatarAsync(long userId, CancellationToken cancellationToken = default)
    {
        string? path = CachePath(userId);
        try
        {
            var file = path == null ? null : new FileInfo(path);
            if (file is not { Exists: true } || file.Length is 0 or > MaxImageBytes)
                return null;
            return await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string? CachePath(long userId) =>
        _cacheDirectory == null || userId <= 0 ? null : Path.Combine(_cacheDirectory, $"{userId}.png");

    private string? SourcePath(long userId) =>
        _cacheDirectory == null || userId <= 0 ? null : Path.Combine(_cacheDirectory, $"{userId}.url");

    private byte[]? ReadCacheFrom(long userId, string imageUrl)
    {
        string? path = CachePath(userId);
        string? source = SourcePath(userId);
        if (path == null || source == null)
            return null;

        try
        {
            var sourceFile = new FileInfo(source);
            var file = new FileInfo(path);
            if (!sourceFile.Exists || sourceFile.Length > MaxImageUrlBytes ||
                !file.Exists || file.Length is 0 or > MaxImageBytes ||
                !string.Equals(File.ReadAllText(source), imageUrl, StringComparison.Ordinal))
            {
                return null;
            }
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void SaveToCache(long userId, byte[] bytes, string imageUrl)
    {
        string? path = CachePath(userId);
        string? source = SourcePath(userId);
        if (path == null || source == null)
            return;

        string tmp = path + ".tmp";
        try
        {
            var existing = new FileInfo(path);
            if (!existing.Exists || existing.Length != bytes.Length || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                Directory.CreateDirectory(_cacheDirectory!);
                if (existing.Exists)
                    File.Delete(source);
                File.WriteAllBytes(tmp, bytes);
                File.Move(tmp, path, overwrite: true);
            }

            File.WriteAllBytes(source, Encoding.UTF8.GetBytes(imageUrl));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(tmp); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
        }
    }

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

        byte[]? result = await pending.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        _cache.TryRemove(userId, out _);
        return result;
    }

    private async Task<byte[]?> LoadAvatarAsync(long userId)
    {
        try
        {
            string? imageUrl = await LookUpImageUrlAsync(userId).ConfigureAwait(false);
            if (!TryGetOfficialImageUri(imageUrl, out Uri? imageUri))
                return null;

            byte[]? cached = ReadCacheFrom(userId, imageUrl!);
            if (cached != null)
                return cached;

            using HttpResponseMessage imageResponse = await _http.GetAsync(
                imageUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!imageResponse.IsSuccessStatusCode)
                return null;

            byte[]? bytes = await BoundedHttpContentReader.ReadAsync(
                imageResponse.Content,
                MaxImageBytes).ConfigureAwait(false);
            if (bytes is not { Length: > 0 })
                return null;

            SaveToCache(userId, bytes, imageUrl!);
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    private Task<string?> LookUpImageUrlAsync(long userId)
    {
        Dictionary<long, TaskCompletionSource<string?>>? full = null;
        Task<string?> result;
        lock (_lookupGate)
        {
            if (_pendingLookups == null)
            {
                _pendingLookups = new Dictionary<long, TaskCompletionSource<string?>>();
                _ = SendAfterWindowAsync(_pendingLookups);
            }

            if (!_pendingLookups.TryGetValue(userId, out TaskCompletionSource<string?>? lookup))
            {
                lookup = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingLookups[userId] = lookup;
            }
            result = lookup.Task;

            if (_pendingLookups.Count >= MaxUsersPerLookup)
            {
                full = _pendingLookups;
                _pendingLookups = null;
            }
        }

        if (full != null)
            _ = SendLookupsAsync(full);
        return result;
    }

    private async Task SendAfterWindowAsync(Dictionary<long, TaskCompletionSource<string?>> batch)
    {
        await Task.Delay(LookupWindow).ConfigureAwait(false);
        lock (_lookupGate)
        {
            if (!ReferenceEquals(_pendingLookups, batch))
                return;
            _pendingLookups = null;
        }
        await SendLookupsAsync(batch).ConfigureAwait(false);
    }

    private async Task SendLookupsAsync(Dictionary<long, TaskCompletionSource<string?>> batch)
    {
        try
        {
            long[] userIds = batch.Keys.ToArray();
            Dictionary<long, string> urls = await FetchImageUrlsAsync(userIds).ConfigureAwait(false);
            foreach (long userId in userIds)
                batch[userId].TrySetResult(urls.GetValueOrDefault(userId));
        }
        finally
        {
            foreach (TaskCompletionSource<string?> lookup in batch.Values)
                lookup.TrySetResult(null);
        }
    }

    private async Task<Dictionary<long, string>> FetchImageUrlsAsync(long[] userIds)
    {
        var urls = new Dictionary<long, string>();
        try
        {
            string requestUrl =
                $"{HeadshotBaseUrl}?userIds={string.Join(",", userIds.Select(id => id.ToString(CultureInfo.InvariantCulture)))}" +
                "&size=75x75&format=Png&isCircular=false";
            using HttpResponseMessage response = await _http.GetAsync(requestUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return urls;

            ThumbnailResponse? payload = await response.Content
                .ReadFromJsonAsync<ThumbnailResponse>().ConfigureAwait(false);
            foreach (ThumbnailResult? thumbnail in payload?.Data ?? Array.Empty<ThumbnailResult>())
            {
                if (thumbnail?.ImageUrl is { } url &&
                    Array.IndexOf(userIds, thumbnail.TargetId) >= 0 &&
                    string.Equals(thumbnail.State, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    urls[thumbnail.TargetId] = url;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
            JsonException or NotSupportedException)
        {
        }
        return urls;
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
        public ThumbnailResult?[]? Data { get; set; }
    }

    private sealed class ThumbnailResult
    {
        public long TargetId { get; set; }
        public string? State { get; set; }
        public string? ImageUrl { get; set; }
    }
}

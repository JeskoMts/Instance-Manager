using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

public sealed class UpdateService
{
    public const string UpdatedArgument = "--updated";

    private const string LatestReleaseUrl = "https://api.github.com/repos/JeskoMts/Instance-Manager/releases/latest";
    private const string DownloadPrefix = "https://github.com/JeskoMts/Instance-Manager/releases/download/";
    private const string ExecutableName = "InstanceManager.exe";
    private const string AssemblyName = "InstanceManager.dll";
    private const string BackupSuffix = ".im-old";
    private const string StagingFolderName = ".im-update";
    private const string DigestPrefix = "sha256:";
    private const int MaxReleaseJsonBytes = 1024 * 1024;
    private const int MaxPackageBytes = 64 * 1024 * 1024;
    private const long MaxUnpackedBytes = 256L * 1024 * 1024;
    private const int MaxPackageEntries = 256;
    private const int MaxRedirects = 5;

    private static readonly string[] DownloadHosts =
    {
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com"
    };

    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

    private readonly HttpClient _http;
    private readonly string _appDirectory;

    public UpdateService(HttpClient http)
        : this(http, AppContext.BaseDirectory, typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0))
    {
    }

    internal UpdateService(HttpClient http, string appDirectory, Version currentVersion)
    {
        _http = http;
        _appDirectory = Path.GetFullPath(appDirectory);
        CurrentVersion = Normalize(currentVersion);
    }

    public Version CurrentVersion { get; }

    public string ExecutablePath => Path.Combine(_appDirectory, ExecutableName);

    private string StagingDirectory => Path.Combine(_appDirectory, StagingFolderName);

    public void CleanupPreviousUpdate()
    {
        try
        {
            foreach (string backup in Directory.GetFiles(_appDirectory, "*" + BackupSuffix))
            {
                try { File.Delete(backup); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        TryDeleteDirectory(StagingDirectory);
    }

    public async Task<Version?> DownloadAsync(CancellationToken cancellationToken = default)
    {
        byte[]? json;
        using (HttpResponseMessage response = await SendAsync(
                   new Uri(LatestReleaseUrl), "application/vnd.github+json", cancellationToken).ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
                return null;
            json = await BoundedHttpContentReader.ReadAsync(
                response.Content, MaxReleaseJsonBytes, cancellationToken).ConfigureAwait(false);
        }

        if (json is null || !TryParseRelease(json, out ReleaseInfo? release) || release!.Version <= CurrentVersion)
            return null;

        byte[]? package = await DownloadPackageAsync(release.DownloadUri, cancellationToken).ConfigureAwait(false);
        if (package is null || !MatchesDigest(package, release.Sha256))
            return null;

        return Stage(package) ? release.Version : null;
    }

    public bool Apply()
    {
        string staging = StagingDirectory;
        if (!Directory.Exists(staging))
            return false;

        var replaced = new List<(string Target, string? Backup)>();
        try
        {
            foreach (string source in Directory.GetFiles(staging))
            {
                string target = Path.Combine(_appDirectory, Path.GetFileName(source));
                string? backup = null;
                if (File.Exists(target))
                {
                    backup = target + BackupSuffix;
                    File.Delete(backup);
                    File.Move(target, backup);
                }

                try
                {
                    File.Move(source, target);
                }
                catch
                {
                    if (backup != null)
                        File.Move(backup, target);
                    throw;
                }

                replaced.Add((target, backup));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            for (int i = replaced.Count - 1; i >= 0; i--)
            {
                (string target, string? backup) = replaced[i];
                try
                {
                    File.Delete(target);
                    if (backup != null)
                        File.Move(backup, target);
                }
                catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException)
                {
                }
            }

            TryDeleteDirectory(staging);
            return false;
        }

        TryDeleteDirectory(staging);
        return true;
    }

    internal bool Stage(byte[] package)
    {
        string staging = StagingDirectory;
        try
        {
            TryDeleteDirectory(staging);
            using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
            if (!IsValidPackage(archive))
                return false;

            Directory.CreateDirectory(staging);
            foreach (ZipArchiveEntry entry in archive.Entries)
                entry.ExtractToFile(Path.Combine(staging, entry.FullName));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            TryDeleteDirectory(staging);
            return false;
        }
    }

    internal static bool TryParseRelease(byte[] json, out ReleaseInfo? release)
    {
        release = null;
        ReleaseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ReleaseDto>(json);
        }
        catch (JsonException)
        {
            return false;
        }

        string? tag = dto?.TagName?.Trim().TrimStart('v', 'V');
        if (tag is null || dto!.Assets is null || !Version.TryParse(tag, out Version? parsed))
            return false;

        string assetName = $"InstanceManager-{tag}.zip";
        foreach (AssetDto asset in dto.Assets)
        {
            if (!string.Equals(asset.Name, assetName, StringComparison.Ordinal))
                continue;

            if (asset.Url is null ||
                !asset.Url.StartsWith(DownloadPrefix, StringComparison.Ordinal) ||
                !Uri.TryCreate(asset.Url, UriKind.Absolute, out Uri? uri) ||
                asset.Digest is null ||
                !asset.Digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            release = new ReleaseInfo(Normalize(parsed), uri, asset.Digest[DigestPrefix.Length..]);
            return true;
        }

        return false;
    }

    internal static bool IsAllowedDownloadUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        Array.Exists(DownloadHosts, host => string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase));

    internal static bool MatchesDigest(byte[] package, string sha256Hex) =>
        string.Equals(Convert.ToHexString(SHA256.HashData(package)), sha256Hex, StringComparison.OrdinalIgnoreCase);

    private async Task<byte[]?> DownloadPackageAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (int redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            if (!IsAllowedDownloadUri(uri))
                return null;

            using HttpResponseMessage response = await SendAsync(
                uri, "application/octet-stream", cancellationToken).ConfigureAwait(false);
            if (ServerLinkResolver.IsRedirect(response.StatusCode))
            {
                if (response.Headers.Location is not { } location)
                    return null;
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }

            return response.IsSuccessStatusCode
                ? await BoundedHttpContentReader.ReadAsync(response.Content, MaxPackageBytes, cancellationToken).ConfigureAwait(false)
                : null;
        }

        return null;
    }

    private Task<HttpResponseMessage> SendAsync(Uri uri, string accept, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("InstanceManager", CurrentVersion.ToString(3)));
        request.Headers.Accept.ParseAdd(accept);
        return _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static bool IsValidPackage(ZipArchive archive)
    {
        if (archive.Entries.Count is 0 or > MaxPackageEntries)
            return false;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long unpacked = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = entry.FullName;
            if (name.Length == 0 ||
                name.IndexOfAny(InvalidNameChars) >= 0 ||
                name.EndsWith('.') ||
                name.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase) ||
                !names.Add(name))
            {
                return false;
            }

            unpacked += entry.Length;
            if (unpacked > MaxUnpackedBytes)
                return false;
        }

        return names.Contains(ExecutableName) && names.Contains(AssemblyName);
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal sealed record ReleaseInfo(Version Version, Uri DownloadUri, string Sha256);

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("assets")] public List<AssetDto>? Assets { get; set; }
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? Url { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}

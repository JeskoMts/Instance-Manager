using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    internal const string ExecutableName = "Instance Manager.exe";
    internal const string LegacyExecutableName = "InstanceManager.exe";
    internal const string LegacyMarkerName = "InstanceManager.deps.json";

    private const string LatestReleaseUrl = "https://api.github.com/repos/JeskoMts/Instance-Manager/releases/latest";
    private const string ReleasesUrl = "https://api.github.com/repos/JeskoMts/Instance-Manager/releases?per_page=15";
    private const string PreviousPrefix = "Instance Manager ";
    private const string DownloadPrefix = "https://github.com/JeskoMts/Instance-Manager/releases/download/";
    private const string BackupSuffix = ".im-old";
    private const string StagingFolderName = ".im-update";
    private const string StagedFileName = "update.exe";
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

    private static readonly string[] LegacyFiles =
    {
        LegacyExecutableName,
        "CommunityToolkit.Mvvm.dll",
        "InstanceManager.dll",
        "InstanceManager.pdb",
        "InstanceManager.runtimeconfig.json",
        "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
        "Microsoft.Extensions.DependencyInjection.dll",
        "Microsoft.Web.WebView2.Core.dll",
        "Microsoft.Web.WebView2.Core.xml",
        "Microsoft.Web.WebView2.WinForms.dll",
        "Microsoft.Web.WebView2.WinForms.xml",
        "Microsoft.Web.WebView2.Wpf.dll",
        "Microsoft.Web.WebView2.Wpf.xml",
        "WebView2Loader.dll",
        "THIRD-PARTY-NOTICES.md",
        LegacyMarkerName
    };

    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

    private readonly HttpClient _http;
    private readonly string _appDirectory;
    private readonly bool _isSingleFile;

    public UpdateService(HttpClient http)
        : this(
            http,
            Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ExecutableName),
            typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0),
            IsRunningAsSingleFile())
    {
    }

    internal UpdateService(HttpClient http, string executablePath, Version currentVersion, bool isSingleFile)
    {
        _http = http;
        ExecutablePath = Path.GetFullPath(executablePath);
        _appDirectory = Path.GetDirectoryName(ExecutablePath)!;
        CurrentVersion = Normalize(currentVersion);
        _isSingleFile = isSingleFile;
    }

    public Version CurrentVersion { get; }

    public string ExecutablePath { get; }

    private string StagingDirectory => Path.Combine(_appDirectory, StagingFolderName);

    private string StagedExecutablePath => Path.Combine(StagingDirectory, StagedFileName);

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
        RemoveLegacyInstall();
    }

    public bool CanUpdate => _isSingleFile;

    public async Task<Version?> DownloadAsync(CancellationToken cancellationToken = default)
    {
        ReleaseInfo? release = await CheckAsync(includePrereleases: false, cancellationToken).ConfigureAwait(false);
        return release != null && await DownloadAndStageAsync(release, cancellationToken).ConfigureAwait(false)
            ? release.Version
            : null;
    }

    public async Task<ReleaseInfo?> CheckAsync(bool includePrereleases, CancellationToken cancellationToken = default)
    {
        if (!_isSingleFile)
            return null;

        byte[]? json;
        using (HttpResponseMessage response = await SendAsync(
                   new Uri(includePrereleases ? ReleasesUrl : LatestReleaseUrl), "application/vnd.github+json", cancellationToken).ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"GitHub answered {(int)response.StatusCode}.");
            json = await BoundedHttpContentReader.ReadAsync(
                response.Content, MaxReleaseJsonBytes, cancellationToken).ConfigureAwait(false);
        }

        if (json is null)
            return null;

        ReleaseInfo? release = includePrereleases ? PickNewest(json) : TryParseRelease(json, out ReleaseInfo? latest) ? latest : null;
        return release != null && release.Version > CurrentVersion ? release : null;
    }

    public async Task<bool> DownloadAndStageAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        byte[]? package = await DownloadPackageAsync(release.DownloadUri, cancellationToken).ConfigureAwait(false);
        return package is not null && MatchesDigest(package, release.Sha256) && Stage(package);
    }

    public bool KeepCurrentVersion(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, PreviousPrefix + CurrentVersion.ToString(3) + ".exe");
            foreach (string old in Directory.GetFiles(directory, "*.exe"))
            {
                if (!string.Equals(old, target, StringComparison.OrdinalIgnoreCase))
                    File.Delete(old);
            }
            File.Copy(ExecutablePath, target, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static (Version Version, string Path)? FindKeptVersion(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
                return null;
            foreach (string file in Directory.GetFiles(directory, PreviousPrefix + "*.exe"))
            {
                string name = Path.GetFileNameWithoutExtension(file)[PreviousPrefix.Length..];
                if (Version.TryParse(name, out Version? version))
                    return (Normalize(version), file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    public bool StageLocal(string executable)
    {
        string staging = StagingDirectory;
        try
        {
            TryDeleteDirectory(staging);
            Directory.CreateDirectory(staging);
            File.Copy(executable, StagedExecutablePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteDirectory(staging);
            return false;
        }
    }

    public bool HasStagedUpdate => File.Exists(StagedExecutablePath);

    public void DiscardStaged() => TryDeleteDirectory(StagingDirectory);

    public bool Apply()
    {
        string staged = StagedExecutablePath;
        if (!File.Exists(staged))
            return false;

        string backup = ExecutablePath + BackupSuffix;
        bool movedAside = false;
        try
        {
            if (File.Exists(ExecutablePath))
            {
                File.Delete(backup);
                File.Move(ExecutablePath, backup);
                movedAside = true;
            }

            File.Move(staged, ExecutablePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (movedAside)
            {
                try { File.Move(backup, ExecutablePath); }
                catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException) { }
            }

            TryDeleteDirectory(StagingDirectory);
            return false;
        }

        TryDeleteDirectory(StagingDirectory);
        return true;
    }

    internal bool Stage(byte[] package)
    {
        string staging = StagingDirectory;
        try
        {
            TryDeleteDirectory(staging);
            using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
            ZipArchiveEntry? executable = FindExecutable(archive);
            if (executable is null)
                return false;

            Directory.CreateDirectory(staging);
            executable.ExtractToFile(StagedExecutablePath);
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

        return dto != null && TryParseRelease(dto, out release);
    }

    internal static ReleaseInfo? PickNewest(byte[] json)
    {
        List<ReleaseDto>? releases;
        try
        {
            releases = JsonSerializer.Deserialize<List<ReleaseDto>>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        ReleaseInfo? newest = null;
        foreach (ReleaseDto dto in releases ?? new List<ReleaseDto>())
        {
            if (dto.Draft || !TryParseRelease(dto, out ReleaseInfo? candidate))
                continue;
            if (newest == null || candidate!.Version > newest.Version)
                newest = candidate;
        }
        return newest;
    }

    private static bool TryParseRelease(ReleaseDto dto, out ReleaseInfo? release)
    {
        release = null;
        string? rawTag = dto.TagName?.Trim();
        string? tag = rawTag?.TrimStart('v', 'V');
        if (tag is null || dto.Assets is null || !Version.TryParse(tag, out Version? parsed))
            return false;

        string assetName = PackageName(tag);
        AssetDto? asset = dto.Assets.Find(a => string.Equals(a.Name, assetName, StringComparison.Ordinal));
        if (asset?.Url is null ||
            !Uri.TryCreate(asset.Url, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.AbsoluteUri, DownloadPrefix + rawTag + "/" + assetName, StringComparison.Ordinal) ||
            asset.Digest is null ||
            !asset.Digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        release = new ReleaseInfo(Normalize(parsed), uri, asset.Digest[DigestPrefix.Length..],
            IsReleasePage(dto.HtmlUrl) ? dto.HtmlUrl : null, dto.Prerelease);
        return true;
    }

    internal static string PackageName(string tag) => $"InstanceManager-{tag}.zip";

    private static bool IsReleasePage(string? url) =>
        url != null && url.StartsWith("https://github.com/JeskoMts/Instance-Manager/releases/", StringComparison.Ordinal);

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

    private static ZipArchiveEntry? FindExecutable(ZipArchive archive)
    {
        if (archive.Entries.Count is 0 or > MaxPackageEntries)
            return null;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long unpacked = 0;
        ZipArchiveEntry? executable = null;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = entry.FullName;
            if (name.Length == 0 ||
                name.IndexOfAny(InvalidNameChars) >= 0 ||
                name.EndsWith('.') ||
                name.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase) ||
                !names.Add(name))
            {
                return null;
            }

            unpacked += entry.Length;
            if (unpacked > MaxUnpackedBytes)
                return null;

            if (string.Equals(name, ExecutableName, StringComparison.OrdinalIgnoreCase))
                executable = entry;
        }

        return executable is { Length: > 0 } ? executable : null;
    }

    private void RemoveLegacyInstall()
    {
        if (!_isSingleFile || !File.Exists(Path.Combine(_appDirectory, LegacyMarkerName)))
            return;

        foreach (string name in LegacyFiles)
        {
            string path = Path.Combine(_appDirectory, name);
            if (!File.Exists(path) || string.Equals(path, ExecutablePath, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { File.Move(path, path + BackupSuffix, overwrite: true); }
                catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    [UnconditionalSuppressMessage("SingleFile", "IL3000")]
    private static bool IsRunningAsSingleFile() => string.IsNullOrEmpty(typeof(UpdateService).Assembly.Location);

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

    public sealed record ReleaseInfo(Version Version, Uri DownloadUri, string Sha256,
        string? PageUrl = null, bool IsPrerelease = false);

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")] public List<AssetDto>? Assets { get; set; }
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? Url { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}

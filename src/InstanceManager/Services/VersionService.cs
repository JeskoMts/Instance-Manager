using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public sealed class VersionService
{
    private readonly HttpClient _http;
    private readonly IRobloxExecutableValidator _executableValidator;

    public VersionService(HttpClient http, IRobloxExecutableValidator? executableValidator = null)
    {
        _http = http;
        _executableValidator = executableValidator ?? new RobloxExecutableValidator();
    }

    public IReadOnlyList<RobloxVersion> Enumerate(string? versionsPathOverride = null)
    {
        string requestedRoot = string.IsNullOrWhiteSpace(versionsPathOverride)
            ? AppPaths.DefaultRobloxVersionsPath
            : versionsPathOverride!;

        var result = new List<RobloxVersion>();
        if (!LocalPathPolicy.TryNormalizeFixedLocalDirectory(requestedRoot, out string root))
            return result;

        if (!Directory.Exists(root))
            return result;

        try
        {
            foreach (string dir in Directory.EnumerateDirectories(root))
            {
                string exe = Path.Combine(dir, "RobloxPlayerBeta.exe");
                if (!File.Exists(exe) || !_executableValidator.TryValidate(root, exe, out _))
                    continue;

                string fileVersion = ReadFileVersion(exe);
                result.Add(new RobloxVersion
                {
                    FolderPath = dir,
                    VersionGuid = Path.GetFileName(dir),
                    PlayerExePath = exe,
                    FileVersion = fileVersion,
                    BuildNumber = ExtractBuildNumber(fileVersion)
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return result
            .OrderByDescending(v => v.BuildNumber)
            .ThenByDescending(v => v.FileVersion, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ReadFileVersion(string exePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            return NormalizeVersion(info.ProductVersion ?? info.FileVersion ?? string.Empty);
        }
        catch (FileNotFoundException) { return string.Empty; }
        catch (IOException) { return string.Empty; }
    }

    public static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return string.Empty;
        return version.Replace(" ", string.Empty).Replace(',', '.');
    }

    public static long ExtractBuildNumber(string fileVersion)
    {
        if (string.IsNullOrWhiteSpace(fileVersion))
            return 0;
        string[] parts = fileVersion.Split('.', ',');
        return long.TryParse(parts[^1].Trim(), out long n) ? n : 0;
    }

    public async Task<string?> GetLatestVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<ClientVersionResponse>(
                "https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer", ct);
            return resp?.Version;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed class ClientVersionResponse
    {
        public string? Version { get; set; }
        public string? ClientVersionUpload { get; set; }
    }
}

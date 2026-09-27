using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class UpdateServiceTests : IDisposable
{
    private const string DownloadRoot = "https://github.com/JeskoMts/Instance-Manager/releases/download/1.2.0/";
    private const string DownloadUrl = DownloadRoot + "Instance.Manager.1.2.0.zip";
    private const string LegacyDownloadUrl = DownloadRoot + "InstanceManager-1.2.0.zip";
    private readonly string _appDir = Path.Combine(Path.GetTempPath(), "im-update-" + Guid.NewGuid().ToString("N"));
    private readonly string _exe;

    public UpdateServiceTests()
    {
        Directory.CreateDirectory(_appDir);
        _exe = Path.Combine(_appDir, "Instance Manager.exe");
        File.WriteAllText(_exe, "old exe");
    }

    public void Dispose()
    {
        try { Directory.Delete(_appDir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task DownloadAsync_NewerRelease_FollowsGitHubRedirectAndReplacesOnlyTheRunningExe()
    {
        byte[] package = Zip(("Instance Manager.exe", "new exe"));
        byte[] legacy = Zip(("InstanceManager.exe", "legacy exe"), ("InstanceManager.dll", "legacy dll"));
        var requests = new List<Uri>();
        var http = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.Contains("InstanceManager", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
            return request.RequestUri!.Host switch
            {
                "api.github.com" => Json(ReleaseJson("1.2.0",
                    (Legacy: true, Url: LegacyDownloadUrl, Digest: "sha256:" + Sha256(legacy)),
                    (Legacy: false, Url: DownloadUrl, Digest: "sha256:" + Sha256(package)))),
                "github.com" => new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://release-assets.githubusercontent.com/asset?sig=1") }
                },
                "release-assets.githubusercontent.com" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) },
                _ => throw new InvalidOperationException(request.RequestUri.ToString())
            };
        }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 1, 0), isSingleFile: true);

        Version? next = await updater.DownloadAsync();

        Assert.Equal(new Version(1, 2, 0), next);
        Assert.Equal(DownloadUrl, requests[1].ToString());
        Assert.Equal(3, requests.Count);
        Assert.True(updater.Apply());
        Assert.Equal("new exe", File.ReadAllText(_exe));
        Assert.Equal("old exe", File.ReadAllText(_exe + ".im-old"));
        Assert.Equal(new[] { _exe, _exe + ".im-old" }, Sorted(Directory.GetFiles(_appDir)));

        updater.CleanupPreviousUpdate();
        Assert.False(File.Exists(_exe + ".im-old"));
        Assert.False(Directory.Exists(Path.Combine(_appDir, ".im-update")));
    }

    [Fact]
    public async Task DownloadAsync_OnlyLegacyPackage_TakesItsExecutableAndKeepsTheRunningFileName()
    {
        byte[] legacy = Zip(("InstanceManager.exe", "legacy exe"), ("InstanceManager.dll", "legacy dll"));
        var http = new HttpClient(new StubHandler(request => request.RequestUri!.Host == "api.github.com"
            ? Json(ReleaseJson("1.2.0", (Legacy: true, Url: LegacyDownloadUrl, Digest: "sha256:" + Sha256(legacy))))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(legacy) }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2), isSingleFile: true);

        Assert.Equal(new Version(1, 2, 0), await updater.DownloadAsync());
        Assert.True(updater.Apply());

        Assert.Equal("legacy exe", File.ReadAllText(_exe));
        Assert.False(File.Exists(Path.Combine(_appDir, "InstanceManager.dll")));
    }

    [Fact]
    public async Task DownloadAsync_NotSingleFileBuild_NeverContactsGitHub()
    {
        int calls = 0;
        var http = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2), isSingleFile: false);

        Assert.Null(await updater.DownloadAsync());
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task DownloadAsync_DigestMismatch_StagesNothing()
    {
        byte[] package = Zip(("Instance Manager.exe", "new exe"));
        var http = new HttpClient(new StubHandler(request => request.RequestUri!.Host == "api.github.com"
            ? Json(ReleaseJson("1.2.0", (Legacy: false, Url: DownloadUrl, Digest: "sha256:" + new string('0', 64))))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 1), isSingleFile: true);

        Assert.Null(await updater.DownloadAsync());
        Assert.False(updater.Apply());
        Assert.Equal("old exe", File.ReadAllText(_exe));
    }

    [Fact]
    public async Task DownloadAsync_SameVersion_DoesNotDownloadPackage()
    {
        int calls = 0;
        var http = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return Json(ReleaseJson("1.1.2", (Legacy: false, Url: DownloadUrl.Replace("1.2.0", "1.1.2"), Digest: "sha256:00")));
        }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2, 0), isSingleFile: true);

        Assert.Null(await updater.DownloadAsync());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("https://evil.example/Instance.Manager.1.2.0.zip", "sha256:00")]
    [InlineData(DownloadUrl, null)]
    public void TryParseRelease_RejectsForeignUrlOrMissingDigest(string url, string? digest)
    {
        byte[] json = Encoding.UTF8.GetBytes(ReleaseJson("1.2.0", (Legacy: false, Url: url, Digest: digest)));

        Assert.False(UpdateService.TryParseRelease(json, out _));
    }

    [Fact]
    public void TryParseRelease_UntrustedNewPackage_DoesNotFallBackToTheLegacyOne()
    {
        byte[] json = Encoding.UTF8.GetBytes(ReleaseJson("1.2.0",
            (Legacy: false, Url: "https://evil.example/Instance.Manager.1.2.0.zip", Digest: "sha256:00"),
            (Legacy: true, Url: LegacyDownloadUrl, Digest: "sha256:00")));

        Assert.False(UpdateService.TryParseRelease(json, out _));
    }

    [Fact]
    public void PackageNames_MatchTheReleaseAssets()
    {
        Assert.Equal(
            new[] { ("Instance.Manager.1.1.2.zip", "Instance Manager.exe"), ("InstanceManager-1.1.2.zip", "InstanceManager.exe") },
            UpdateService.PackageNames("1.1.2"));
    }

    [Theory]
    [InlineData("https://github.com/x", true)]
    [InlineData("https://release-assets.githubusercontent.com/x", true)]
    [InlineData("http://github.com/x", false)]
    [InlineData("https://github.com:8443/x", false)]
    [InlineData("https://github.com.evil.example/x", false)]
    public void IsAllowedDownloadUri_OnlyAcceptsGitHubHttps(string url, bool allowed) =>
        Assert.Equal(allowed, UpdateService.IsAllowedDownloadUri(new Uri(url)));

    [Theory]
    [InlineData("../Instance Manager.exe")]
    [InlineData("sub/Instance Manager.exe")]
    [InlineData("Instance Manager.exe:stream")]
    public void Stage_RejectsUnsafeEntryNames(string name)
    {
        var updater = new UpdateService(new HttpClient(), _exe, new Version(1, 1, 1), isSingleFile: true);

        Assert.False(updater.Stage(Zip((name, "x"), ("Instance Manager.exe", "new exe")), UpdateService.ExecutableName));
        Assert.False(Directory.Exists(Path.Combine(_appDir, ".im-update")));
    }

    [Fact]
    public void Stage_RejectsPackageWithoutExecutable()
    {
        var updater = new UpdateService(new HttpClient(), _exe, new Version(1, 1, 1), isSingleFile: true);

        Assert.False(updater.Stage(Zip(("InstanceManager.dll", "new dll")), UpdateService.ExecutableName));
        Assert.False(updater.Stage(Zip(("Instance Manager.exe", "")), UpdateService.ExecutableName));
    }

    [Fact]
    public void Apply_FailureKeepsTheOriginalExe()
    {
        var updater = new UpdateService(new HttpClient(), _exe, new Version(1, 1, 1), isSingleFile: true);
        Assert.True(updater.Stage(Zip(("Instance Manager.exe", "new exe")), UpdateService.ExecutableName));
        string blockedBackup = _exe + ".im-old";
        File.WriteAllText(blockedBackup, "locked");

        using (new FileStream(blockedBackup, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(updater.Apply());

        Assert.Equal("old exe", File.ReadAllText(_exe));
    }

    [Fact]
    public void CleanupPreviousUpdate_AfterMigratingFrom111_RemovesTheOldLooseFilesOnly()
    {
        string legacyExe = Path.Combine(_appDir, "InstanceManager.exe");
        File.WriteAllText(legacyExe, "single-file 1.1.2");
        string[] leftovers =
        {
            "CommunityToolkit.Mvvm.dll", "InstanceManager.dll", "InstanceManager.deps.json",
            "InstanceManager.runtimeconfig.json", "Microsoft.Web.WebView2.Core.dll", "WebView2Loader.dll",
            "THIRD-PARTY-NOTICES.md", "InstanceManager.dll.im-old"
        };
        foreach (string name in leftovers)
            File.WriteAllText(Path.Combine(_appDir, name), name);
        File.WriteAllText(Path.Combine(_appDir, "notes.txt"), "mine");

        new UpdateService(new HttpClient(), legacyExe, new Version(1, 1, 2), isSingleFile: true).CleanupPreviousUpdate();

        Assert.Equal(
            Sorted(new[] { _exe, legacyExe, Path.Combine(_appDir, "notes.txt") }),
            Sorted(Directory.GetFiles(_appDir)));
    }

    [Theory]
    [InlineData("Instance Manager.exe", true)]
    [InlineData("InstanceManager.exe", false)]
    public void CleanupPreviousUpdate_LeavesOtherInstallsAlone(string runningName, bool isSingleFile)
    {
        string running = Path.Combine(_appDir, runningName);
        File.WriteAllText(running, "running");
        File.WriteAllText(Path.Combine(_appDir, "InstanceManager.deps.json"), "{}");
        File.WriteAllText(Path.Combine(_appDir, "InstanceManager.dll"), "dll");

        new UpdateService(new HttpClient(), running, new Version(1, 1, 2), isSingleFile).CleanupPreviousUpdate();

        Assert.True(File.Exists(Path.Combine(_appDir, "InstanceManager.deps.json")));
        Assert.True(File.Exists(Path.Combine(_appDir, "InstanceManager.dll")));
    }

    private static string[] Sorted(string[] paths)
    {
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static string ReleaseJson(string tag, params (bool Legacy, string Url, string? Digest)[] packages)
    {
        var assets = new List<string>
        {
            $$"""{"name":"InstanceManager-Source-Code-{{tag}}.zip","browser_download_url":"https://github.com/JeskoMts/Instance-Manager/releases/download/{{tag}}/src.zip","digest":"sha256:00"}"""
        };
        foreach ((bool legacy, string url, string? digest) in packages)
        {
            string name = legacy ? $"InstanceManager-{tag}.zip" : $"Instance.Manager.{tag}.zip";
            string digestJson = digest is null ? "" : $",\"digest\":\"{digest}\"";
            assets.Add($$"""{"name":"{{name}}","browser_download_url":"{{url}}"{{digestJson}}}""");
        }
        return $$"""{"tag_name":"{{tag}}","assets":[{{string.Join(",", assets)}}]}""";
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }
        return buffer.ToArray();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}

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
    private const string DownloadUrl = DownloadRoot + "InstanceManager-1.2.0.zip";
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
        var requests = new List<Uri>();
        var http = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.Contains("InstanceManager", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
            return request.RequestUri!.Host switch
            {
                "api.github.com" => Json(ReleaseJson("1.2.0", (Url: DownloadUrl, Digest: "sha256:" + Sha256(package)))),
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
    public async Task DownloadAsync_RenamedExe_KeepsTheRunningFileNameAndIgnoresExtraEntries()
    {
        string renamed = Path.Combine(_appDir, "My Alts.exe");
        File.Move(_exe, renamed);
        byte[] package = Zip(("Instance Manager.exe", "new exe"), ("InstanceManager.dll", "stray dll"));
        var http = new HttpClient(new StubHandler(request => request.RequestUri!.Host == "api.github.com"
            ? Json(ReleaseJson("1.2.0", (Url: DownloadUrl, Digest: "sha256:" + Sha256(package))))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) }));
        var updater = new UpdateService(http, renamed, new Version(1, 1, 2), isSingleFile: true);

        Assert.Equal(new Version(1, 2, 0), await updater.DownloadAsync());
        Assert.True(updater.Apply());

        Assert.Equal("new exe", File.ReadAllText(renamed));
        Assert.False(File.Exists(_exe));
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
            ? Json(ReleaseJson("1.2.0", (Url: DownloadUrl, Digest: "sha256:" + new string('0', 64))))
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
            return Json(ReleaseJson("1.1.2", (Url: DownloadUrl.Replace("1.2.0", "1.1.2"), Digest: "sha256:00")));
        }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2, 0), isSingleFile: true);

        Assert.Null(await updater.DownloadAsync());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("https://evil.example/InstanceManager-1.2.0.zip", "sha256:00")]
    [InlineData(DownloadUrl, null)]
    [InlineData("https://github.com/JeskoMts/Instance-Manager/releases/download/../../../../evil/fork/releases/download/1.2.0/InstanceManager-1.2.0.zip", "sha256:00")]
    [InlineData("https://github.com/JeskoMts/Instance-Manager/releases/download/%2e%2e/%2e%2e/%2e%2e/%2e%2e/evil/fork/releases/download/1.2.0/InstanceManager-1.2.0.zip", "sha256:00")]
    public void TryParseRelease_RejectsForeignUrlOrMissingDigest(string url, string? digest)
    {
        byte[] json = Encoding.UTF8.GetBytes(ReleaseJson("1.2.0", (Url: url, Digest: digest)));

        Assert.False(UpdateService.TryParseRelease(json, out _));
    }

    [Fact]
    public void TryParseRelease_IgnoresAssetsWithOtherNames()
    {
        string json = ReleaseJson("1.2.0", (Url: DownloadRoot + "Instance.Manager.1.2.0.zip", Digest: "sha256:00"))
            .Replace("\"InstanceManager-1.2.0.zip\"", "\"Instance.Manager.1.2.0.zip\"", StringComparison.Ordinal);

        Assert.False(UpdateService.TryParseRelease(Encoding.UTF8.GetBytes(json), out _));
    }

    [Fact]
    public void PackageName_MatchesTheReleaseAsset()
    {
        Assert.Equal("InstanceManager-1.1.2.zip", UpdateService.PackageName("1.1.2"));
        Assert.Equal("Instance Manager.exe", UpdateService.ExecutableName);
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

        Assert.False(updater.Stage(Zip((name, "x"), ("Instance Manager.exe", "new exe"))));
        Assert.False(Directory.Exists(Path.Combine(_appDir, ".im-update")));
    }

    [Fact]
    public void Stage_RejectsPackageWithoutExecutable()
    {
        var updater = new UpdateService(new HttpClient(), _exe, new Version(1, 1, 1), isSingleFile: true);

        Assert.False(updater.Stage(Zip(("InstanceManager.dll", "new dll"))));
        Assert.False(updater.Stage(Zip(("Instance Manager.exe", ""))));
    }

    [Fact]
    public void Apply_FailureKeepsTheOriginalExe()
    {
        var updater = new UpdateService(new HttpClient(), _exe, new Version(1, 1, 1), isSingleFile: true);
        Assert.True(updater.Stage(Zip(("Instance Manager.exe", "new exe"))));
        string blockedBackup = _exe + ".im-old";
        File.WriteAllText(blockedBackup, "locked");

        using (new FileStream(blockedBackup, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(updater.Apply());

        Assert.Equal("old exe", File.ReadAllText(_exe));
    }

    [Theory]
    [InlineData("Instance Manager.exe")]
    [InlineData("InstanceManager.exe")]
    public void CleanupPreviousUpdate_InAnOldFolderInstall_RemovesTheOldFilesButNotItself(string runningName)
    {
        File.Delete(_exe);
        string running = Path.Combine(_appDir, runningName);
        File.WriteAllText(running, "single-file 1.1.2");
        string[] leftovers =
        {
            "InstanceManager.exe", "CommunityToolkit.Mvvm.dll", "InstanceManager.dll", "InstanceManager.deps.json",
            "InstanceManager.runtimeconfig.json", "Microsoft.Web.WebView2.Core.dll", "WebView2Loader.dll",
            "THIRD-PARTY-NOTICES.md", "InstanceManager.dll.im-old"
        };
        foreach (string name in leftovers)
        {
            string path = Path.Combine(_appDir, name);
            if (!string.Equals(path, running, StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(path, name);
        }
        File.WriteAllText(Path.Combine(_appDir, "notes.txt"), "mine");

        new UpdateService(new HttpClient(), running, new Version(1, 1, 2), isSingleFile: true).CleanupPreviousUpdate();

        Assert.Equal(
            Sorted(new[] { running, Path.Combine(_appDir, "notes.txt") }),
            Sorted(Directory.GetFiles(_appDir)));
        Assert.Equal("single-file 1.1.2", File.ReadAllText(running));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CleanupPreviousUpdate_WithoutAnOldInstallOrFromSource_LeavesOtherFilesAlone(bool isSingleFile, bool withMarker)
    {
        string oldExe = Path.Combine(_appDir, "InstanceManager.exe");
        string oldDll = Path.Combine(_appDir, "InstanceManager.dll");
        File.WriteAllText(oldExe, "other exe");
        File.WriteAllText(oldDll, "dll");
        if (withMarker)
            File.WriteAllText(Path.Combine(_appDir, "InstanceManager.deps.json"), "{}");

        new UpdateService(new HttpClient(), _exe, new Version(1, 1, 2), isSingleFile).CleanupPreviousUpdate();

        Assert.True(File.Exists(oldExe));
        Assert.True(File.Exists(oldDll));
    }

    [Fact]
    public async Task CheckAsync_AsksOnlyForTheLatestRelease()
    {
        byte[] package = Zip(("Instance Manager.exe", "new exe"));
        var requests = new List<Uri>();
        var http = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            return Json(ReleaseJson("1.2.0", (DownloadUrl, "sha256:" + Sha256(package))));
        }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2), isSingleFile: true);

        UpdateService.ReleaseInfo? latest = await updater.CheckAsync();

        Assert.Equal(new Version(1, 2, 0), latest?.Version);
        Assert.Null(updater.UnavailableVersion);
        Assert.Equal("https://api.github.com/repos/JeskoMts/Instance-Manager/releases/latest", Assert.Single(requests).AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_NewerReleaseWithoutAVerifiablePackage_IsNamedButNotOffered()
    {
        var http = new HttpClient(new StubHandler(_ => Json(ReleaseJson("v1.2.0", (DownloadUrl, null)))));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2), isSingleFile: true);

        Assert.Null(await updater.CheckAsync());
        Assert.Equal(new Version(1, 2, 0), updater.UnavailableVersion);
    }

    [Fact]
    public async Task CheckAsync_OlderOrMissingRelease_LooksUpToDate()
    {
        var older = new UpdateService(
            new HttpClient(new StubHandler(_ => Json(ReleaseJson("1.1.0", (DownloadUrl, null))))),
            _exe, new Version(1, 1, 2), isSingleFile: true);
        var none = new UpdateService(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))),
            _exe, new Version(1, 1, 2), isSingleFile: true);

        Assert.Null(await older.CheckAsync());
        Assert.Null(older.UnavailableVersion);
        Assert.Null(await none.CheckAsync());
        Assert.Null(none.UnavailableVersion);
    }

    [Fact]
    public async Task CheckAsync_GitHubError_IsReportedInsteadOfLookingUpToDate()
    {
        var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2), isSingleFile: true);

        await Assert.ThrowsAsync<HttpRequestException>(() => updater.CheckAsync());
    }

    [Fact]
    public void KeepCurrentVersion_StoresOneCopyThatCanBeRolledBackTo()
    {
        string kept = Path.Combine(_appDir, "previous");
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(kept, "Instance Manager 1.0.0.exe"), "ancient");
        var updater = new UpdateService(new HttpClient(), _exe, new Version(1, 1, 2, 0), isSingleFile: true);

        Assert.True(updater.KeepCurrentVersion(kept));

        (Version Version, string Path)? found = UpdateService.FindKeptVersion(kept);
        Assert.NotNull(found);
        (Version version, string path) = found.Value;
        Assert.Equal(new Version(1, 1, 2), version);
        Assert.Equal(new[] { Path.Combine(kept, "Instance Manager 1.1.2.exe") }, Directory.GetFiles(kept));

        File.WriteAllText(_exe, "newer exe");
        Assert.True(updater.StageLocal(path));
        Assert.True(updater.HasStagedUpdate);
        Assert.True(updater.Apply());
        Assert.Equal("old exe", File.ReadAllText(_exe));
        Assert.Null(UpdateService.FindKeptVersion(Path.Combine(_appDir, "missing")));
    }

    [Fact]
    public async Task AutomaticCheck_StagesTheLatestRelease_AndInstallsItOnExitWithACopyOfTheOldVersion()
    {
        byte[] package = Zip(("Instance Manager.exe", "new exe"));
        int downloads = 0;
        var http = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.Host == "api.github.com")
                return Json(ReleaseJson("1.2.0", (DownloadUrl, "sha256:" + Sha256(package))));
            downloads++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) };
        }));
        var updater = new UpdateService(http, _exe, new Version(1, 1, 2), isSingleFile: true);
        string previous = Path.Combine(_appDir, "previous");
        var vm = new InstanceManager.ViewModels.UpdatesViewModel(updater, new UpdateSettings(), new SilentShell(), previous);

        await vm.CheckAsync(startup: false, manual: false);
        Assert.True(vm.IsUpdateReady);
        Assert.Equal("1.2.0", vm.ReadyVersion);
        Assert.True(updater.HasStagedUpdate);

        await vm.CheckAsync(startup: false, manual: false);
        Assert.Equal(1, downloads);

        Assert.True(vm.ApplyOnExit());
        Assert.Equal("new exe", File.ReadAllText(_exe));
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(previous, "Instance Manager 1.1.2.exe")));
    }

    [Fact]
    public void UpdateAutomatically_IsTheOnlyUpdateSetting_AndIsSaved()
    {
        var settings = new UpdateSettings();
        var updater = new UpdateService(new HttpClient(), _exe, new Version(1, 1, 2), isSingleFile: true);
        var vm = new InstanceManager.ViewModels.UpdatesViewModel(updater, settings, new SilentShell(), Path.Combine(_appDir, "previous"));

        Assert.True(vm.UpdateAutomatically);
        vm.UpdateAutomatically = false;

        Assert.False(settings.Settings.UpdateCheckAutomatically);
        foreach (string gone in new[]
                 {
                     "UpdateCheckOnStartup", "UpdateCheckIntervalMinutes", "UpdateIncludePrereleases",
                     "SkippedUpdateVersion", "KeepPreviousVersion"
                 })
            Assert.Null(typeof(InstanceManager.Models.AppSettings).GetProperty(gone));
    }

    private sealed class UpdateSettings : InstanceManager.Storage.ISettingsService
    {
        public InstanceManager.Models.AppSettings Settings { get; } = new();
        public void Save() { }
    }

    private sealed class SilentShell : InstanceManager.ViewModels.IShellCoordinator
    {
        public Task LaunchAsync(IReadOnlyList<InstanceManager.Models.Account> accounts) => Task.CompletedTask;
        public void SetStatus(string message) { }
    }

    private static string[] Sorted(string[] paths)
    {
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static string ReleaseJson(string tag, (string Url, string? Digest) package)
    {
        string source = $$"""{"name":"InstanceManager-Source-Code-{{tag}}.zip","browser_download_url":"https://github.com/JeskoMts/Instance-Manager/releases/download/{{tag}}/src.zip","digest":"sha256:00"}""";
        string digestJson = package.Digest is null ? "" : $",\"digest\":\"{package.Digest}\"";
        string app = $$"""{"name":"InstanceManager-{{tag}}.zip","browser_download_url":"{{package.Url}}"{{digestJson}}}""";
        return $$"""{"tag_name":"{{tag}}","assets":[{{source}},{{app}}]}""";
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

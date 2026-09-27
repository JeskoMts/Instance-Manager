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
    private const string DownloadUrl = "https://github.com/JeskoMts/Instance-Manager/releases/download/1.2.0/InstanceManager-1.2.0.zip";
    private readonly string _appDir = Path.Combine(Path.GetTempPath(), "im-update-" + Guid.NewGuid().ToString("N"));

    public UpdateServiceTests()
    {
        Directory.CreateDirectory(_appDir);
        File.WriteAllText(Path.Combine(_appDir, "InstanceManager.exe"), "old exe");
        File.WriteAllText(Path.Combine(_appDir, "InstanceManager.dll"), "old dll");
    }

    public void Dispose()
    {
        try { Directory.Delete(_appDir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task DownloadAsync_NewerRelease_FollowsGitHubRedirectAndStagesVerifiedPackage()
    {
        byte[] package = Zip(("InstanceManager.exe", "new exe"), ("InstanceManager.dll", "new dll"), ("Extra.dll", "extra"));
        var requests = new List<Uri>();
        var http = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.Contains("InstanceManager", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
            return request.RequestUri!.Host switch
            {
                "api.github.com" => Json(ReleaseJson("1.2.0", DownloadUrl, Sha256(package))),
                "github.com" => new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://release-assets.githubusercontent.com/asset?sig=1") }
                },
                "release-assets.githubusercontent.com" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) },
                _ => throw new InvalidOperationException(request.RequestUri.ToString())
            };
        }));
        var updater = new UpdateService(http, _appDir, new Version(1, 1, 1, 0));

        Version? next = await updater.DownloadAsync();

        Assert.Equal(new Version(1, 2, 0), next);
        Assert.Equal(3, requests.Count);
        Assert.True(updater.Apply());
        Assert.Equal("new exe", File.ReadAllText(Path.Combine(_appDir, "InstanceManager.exe")));
        Assert.Equal("extra", File.ReadAllText(Path.Combine(_appDir, "Extra.dll")));
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(_appDir, "InstanceManager.exe.im-old")));

        updater.CleanupPreviousUpdate();
        Assert.False(File.Exists(Path.Combine(_appDir, "InstanceManager.exe.im-old")));
        Assert.False(Directory.Exists(Path.Combine(_appDir, ".im-update")));
    }

    [Fact]
    public async Task DownloadAsync_DigestMismatch_StagesNothing()
    {
        byte[] package = Zip(("InstanceManager.exe", "new exe"), ("InstanceManager.dll", "new dll"));
        var http = new HttpClient(new StubHandler(request => request.RequestUri!.Host == "api.github.com"
            ? Json(ReleaseJson("1.2.0", DownloadUrl, new string('0', 64)))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(package) }));
        var updater = new UpdateService(http, _appDir, new Version(1, 1, 1));

        Assert.Null(await updater.DownloadAsync());
        Assert.False(updater.Apply());
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(_appDir, "InstanceManager.exe")));
    }

    [Fact]
    public async Task DownloadAsync_SameVersion_DoesNotDownloadPackage()
    {
        int calls = 0;
        var http = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return Json(ReleaseJson("1.1.1", DownloadUrl.Replace("1.2.0", "1.1.1"), new string('0', 64)));
        }));
        var updater = new UpdateService(http, _appDir, new Version(1, 1, 1, 0));

        Assert.Null(await updater.DownloadAsync());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("https://evil.example/InstanceManager-1.2.0.zip", "sha256:" + "00")]
    [InlineData(DownloadUrl, null)]
    public void TryParseRelease_RejectsForeignUrlOrMissingDigest(string url, string? digest)
    {
        byte[] json = Encoding.UTF8.GetBytes(ReleaseJsonRaw("1.2.0", url, digest));

        Assert.False(UpdateService.TryParseRelease(json, out _));
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
    [InlineData("../InstanceManager.exe")]
    [InlineData("sub/InstanceManager.exe")]
    [InlineData("InstanceManager.exe:stream")]
    public void Stage_RejectsUnsafeEntryNames(string name)
    {
        var updater = new UpdateService(new HttpClient(), _appDir, new Version(1, 1, 1));

        Assert.False(updater.Stage(Zip((name, "x"), ("InstanceManager.dll", "new dll"))));
        Assert.False(Directory.Exists(Path.Combine(_appDir, ".im-update")));
    }

    [Fact]
    public void Stage_RejectsPackageWithoutExecutable()
    {
        var updater = new UpdateService(new HttpClient(), _appDir, new Version(1, 1, 1));

        Assert.False(updater.Stage(Zip(("InstanceManager.dll", "new dll"))));
    }

    [Fact]
    public void Apply_FailureRestoresEveryOriginalFile()
    {
        var updater = new UpdateService(new HttpClient(), _appDir, new Version(1, 1, 1));
        Assert.True(updater.Stage(Zip(("InstanceManager.exe", "new exe"), ("InstanceManager.dll", "new dll"))));
        string blockedBackup = Path.Combine(_appDir, "InstanceManager.exe.im-old");
        File.WriteAllText(blockedBackup, "locked");

        using (new FileStream(blockedBackup, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(updater.Apply());

        Assert.Equal("old exe", File.ReadAllText(Path.Combine(_appDir, "InstanceManager.exe")));
        Assert.Equal("old dll", File.ReadAllText(Path.Combine(_appDir, "InstanceManager.dll")));
    }

    private static string ReleaseJson(string tag, string url, string sha256) => ReleaseJsonRaw(tag, url, "sha256:" + sha256);

    private static string ReleaseJsonRaw(string tag, string url, string? digest) =>
        $$"""
        {"tag_name":"{{tag}}","assets":[
          {"name":"InstanceManager-Source-Code-{{tag}}.zip","browser_download_url":"https://github.com/JeskoMts/Instance-Manager/releases/download/{{tag}}/src.zip","digest":"sha256:00"},
          {"name":"InstanceManager-{{tag}}.zip","browser_download_url":"{{url}}"{{(digest is null ? "" : $",\"digest\":\"{digest}\"")}}}
        ]}
        """;

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

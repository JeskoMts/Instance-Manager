using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Composition;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class SecurityHardeningTests
{
    private const int FiveMegabytes = 5 * 1024 * 1024;
    private const int ReadSlack = 256 * 1024;

    [Theory]
    [InlineData("CN=Roblox Corporation, O=Roblox Corporation, L=San Mateo, S=California, C=US", true)]
    [InlineData("CN=Build Signing, O=Roblox Corporation, C=US", true)]
    [InlineData("CN=Roblox Corporation, C=US", true)]
    [InlineData("CN=Evil Tools, O=Roblox Corporation Fan Tools Ltd, C=US", false)]
    [InlineData("CN=EvilO=Roblox Corporation, O=Evil Ltd, C=US", false)]
    [InlineData("CN=Roblox Corporation Tools, O=Evil Ltd, C=US", false)]
    [InlineData("CN=Evil, OU=Roblox Corporation, O=Evil Ltd", false)]
    public void RobloxSigner_MustBeRobloxCorporationExactly(string subject, bool expected)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(expected, RobloxExecutableValidator.IsExpectedSigner(certificate));
    }

    [Fact]
    public async Task SharedHttpClient_StopsBufferingOversizedApiAnswers()
    {
        var body = new GeneratedStream(Encoding.UTF8.GetBytes("{\"id\":7,\"name\":\"a\",\"displayName\":\"b\",\"pad\":\""),
            16 * 1024 * 1024, Encoding.UTF8.GetBytes("\"}"));
        using HttpClient http = ServiceCollectionExtensions.CreateHttpClient(new StreamHandler(_ => body));

        SessionStatus status = await new RobloxAuthService(http).CheckSessionAsync("cookie", 7);

        Assert.Equal(SessionStatus.Unknown, status);
        Assert.InRange(body.BytesRead, 1, ServiceCollectionExtensions.MaxResponseContentBytes + ReadSlack);
    }

    [Fact]
    public async Task AvatarDownload_StopsReadingAfterFiveMegabytes()
    {
        var image = new GeneratedStream(Array.Empty<byte>(), 64 * 1024 * 1024, Array.Empty<byte>());
        using var http = new HttpClient(new StreamHandler(request => request.RequestUri!.Host == "thumbnails.roblox.com"
            ? new MemoryStream(Encoding.UTF8.GetBytes(ThumbnailJson(42)))
            : image));

        byte[]? result = await new RobloxAvatarService(http).GetAvatarAsync(42);

        Assert.Null(result);
        Assert.InRange(image.BytesRead, 1, FiveMegabytes + ReadSlack);
    }

    [Fact]
    public async Task GameThumbnailDownload_StopsReadingAfterFiveMegabytes()
    {
        var image = new GeneratedStream(Array.Empty<byte>(), 64 * 1024 * 1024, Array.Empty<byte>());
        using var http = new HttpClient(new StreamHandler(_ => image));

        byte[]? result = await new RobloxGamesService(http).GetThumbnailAsync("https://tr.rbxcdn.com/game.jpg");

        Assert.Null(result);
        Assert.InRange(image.BytesRead, 1, FiveMegabytes + ReadSlack);
    }

    [Fact]
    public async Task SharedHttpClient_StillDownloadsLargeAllowedImages()
    {
        int size = FiveMegabytes - 1024;
        using HttpClient http = ServiceCollectionExtensions.CreateHttpClient(new StreamHandler(request =>
            request.RequestUri!.Host == "thumbnails.roblox.com"
                ? new MemoryStream(Encoding.UTF8.GetBytes(ThumbnailJson(42)))
                : new GeneratedStream(Array.Empty<byte>(), size, Array.Empty<byte>())));

        byte[]? avatar = await new RobloxAvatarService(http).GetAvatarAsync(42);
        byte[]? thumbnail = await new RobloxGamesService(http).GetThumbnailAsync("https://tr.rbxcdn.com/game.jpg");

        Assert.Equal(size, avatar?.Length);
        Assert.Equal(size, thumbnail?.Length);
    }

    [Theory]
    [InlineData("name\u2028FORGED LINE")]
    [InlineData("name\u2029FORGED LINE")]
    [InlineData("name\u0085FORGED LINE")]
    [InlineData("invoice\u202Efdp.exe")]
    [InlineData("a\u2066b\u2069c\u200Fd")]
    public void LogFields_CannotForgeLinesOrReorderText(string value)
    {
        string clean = AutoReconnectLog.SanitizeField(value);

        Assert.DoesNotContain(clean, c => char.IsControl(c) ||
            c is '\u2028' or '\u2029' or '\u200E' or '\u200F' or '\u061C' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069'));
        Assert.Equal(value.Length, clean.Length);
    }

    [Fact]
    public void LoginBrowser_DeniesPermissionRequests()
    {
        string code = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "Views", "AddAccountWindow.xaml.cs"));

        Assert.Contains("PermissionRequested +=", code, StringComparison.Ordinal);
        Assert.Contains("CoreWebView2PermissionState.Deny", code, StringComparison.Ordinal);
    }

    private static string ThumbnailJson(long userId) =>
        $$"""{"data":[{"targetId":{{userId}},"state":"Completed","imageUrl":"https://tr.rbxcdn.com/avatar-{{userId}}.png","version":"TN3"}]}""";

    private static string FindWorkspaceFile(params string[] relativeParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }

    private sealed class StreamHandler(Func<HttpRequestMessage, Stream> body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body(request)) });
    }

    private sealed class GeneratedStream(byte[] prefix, long filler, byte[] suffix) : Stream
    {
        private readonly long _length = prefix.Length + filler + suffix.Length;
        private long _position;

        public long BytesRead => Interlocked.Read(ref _position);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int written = 0;
            while (written < count && _position < _length)
            {
                long at = _position;
                int n;
                if (at < prefix.Length)
                {
                    n = (int)Math.Min(count - written, prefix.Length - at);
                    Array.Copy(prefix, at, buffer, offset + written, n);
                }
                else if (at < prefix.Length + filler)
                {
                    n = (int)Math.Min(count - written, prefix.Length + filler - at);
                    buffer.AsSpan(offset + written, n).Fill((byte)'x');
                }
                else
                {
                    long start = at - prefix.Length - filler;
                    n = (int)Math.Min(count - written, suffix.Length - start);
                    Array.Copy(suffix, start, buffer, offset + written, n);
                }
                written += n;
                Interlocked.Add(ref _position, n);
            }
            return written;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class RobloxAvatarServiceTests
{
    [Fact]
    public async Task GetAvatarAsync_UsesPublicHeadshotEndpointWithoutCookies_AndDoesNotPinBytes()
    {
        var requests = new List<(Uri Uri, bool HasCookie)>();
        var handler = new FakeHandler(request =>
        {
            requests.Add((request.RequestUri!, request.Headers.Contains("Cookie")));
            if (request.RequestUri!.Host == "thumbnails.roblox.com")
            {
                return Json("""
                    {"data":[{"targetId":42,"state":"Completed","imageUrl":"https://tr.rbxcdn.com/avatar.png","version":"TN3"}]}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            };
        });
        using var http = new HttpClient(handler);
        var service = new RobloxAvatarService(http);

        byte[]? first = await service.GetAvatarAsync(42);
        byte[]? second = await service.GetAvatarAsync(42);

        Assert.Equal(new byte[] { 1, 2, 3 }, first);
        Assert.Equal(new byte[] { 1, 2, 3 }, second);
        Assert.NotSame(first, second);
        Assert.Equal(4, requests.Count);
        Assert.Equal(
            "https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds=42&size=150x150&format=Png&isCircular=false",
            requests[0].Uri.AbsoluteUri);
        Assert.All(requests, request => Assert.False(request.HasCookie));
    }

    [Theory]
    [InlineData("Pending", "https://tr.rbxcdn.com/avatar.png")]
    [InlineData("Completed", "http://untrusted.example/avatar.png")]
    [InlineData("Completed", "https://untrusted.example/avatar.png")]
    [InlineData("Completed", "not-a-url")]
    public async Task GetAvatarAsync_ReturnsNullForUnavailableOrUnsafeThumbnail(string state, string imageUrl)
    {
        var handler = new FakeHandler(_ => Json($$"""
            {"data":[{"targetId":42,"state":"{{state}}","imageUrl":"{{imageUrl}}","version":"TN3"}]}
            """));
        using var http = new HttpClient(handler);
        var service = new RobloxAvatarService(http);

        byte[]? result = await service.GetAvatarAsync(42);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAvatarAsync_ReturnsNullForFailedThumbnailRequest()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler);
        var service = new RobloxAvatarService(http);

        byte[]? result = await service.GetAvatarAsync(42);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAvatarAsync_ReturnsNullForEmptyImageBody()
    {
        var handler = new FakeHandler(request => request.RequestUri!.Host == "thumbnails.roblox.com"
            ? Json("""
                {"data":[{"targetId":42,"state":"Completed","imageUrl":"https://tr.rbxcdn.com/avatar.png","version":"TN3"}]}
                """)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) });
        using var http = new HttpClient(handler);
        var service = new RobloxAvatarService(http);

        byte[]? result = await service.GetAvatarAsync(42);

        Assert.Null(result);
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_factory(request));
    }
}

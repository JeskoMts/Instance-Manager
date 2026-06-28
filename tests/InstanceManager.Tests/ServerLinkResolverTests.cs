using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ServerLinkResolverTests
{
    private const string JobId = "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab";

    [Theory]
    [InlineData("https://www.roblox.com/games/start?placeId=920587237&gameInstanceId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab")]
    [InlineData("roblox://experiences/start?placeId=920587237&gameInstanceId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab")]
    public async Task ResolveAsync_DirectServerLink_ReturnsTarget(string input)
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(_ => throw new InvalidOperationException())));

        ServerTargetResolution result = await resolver.ResolveAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(920587237, result.Target!.PlaceId);
        Assert.Equal(JobId, result.Target.JobId);
    }

    [Fact]
    public async Task ResolveAsync_OfficialRedirect_ResolvesTarget()
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(request =>
            new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri($"https://www.roblox.com/games/start?placeId=55&gameInstanceId={JobId}") },
                RequestMessage = request
            })));

        ServerTargetResolution result = await resolver.ResolveAsync("https://www.roblox.com/share?code=test&type=Server");

        Assert.True(result.IsSuccess);
        Assert.Equal(55, result.Target!.PlaceId);
    }

    [Fact]
    public async Task ResolveAsync_ForeignDomain_IsRejectedWithoutRequest()
    {
        int calls = 0;
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        })));

        ServerTargetResolution result = await resolver.ResolveAsync($"https://evil.example/start?placeId=55&gameInstanceId={JobId}");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, calls);
        Assert.Contains("Roblox", result.Error);
    }

    [Fact]
    public async Task ResolveAsync_LinkWithoutBothIds_ReturnsPreciseError()
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request })));

        ServerTargetResolution result = await resolver.ResolveAsync("https://www.roblox.com/games/55/example");

        Assert.False(result.IsSuccess);
        Assert.Contains("Place ID and Job ID", result.Error);
    }

    [Fact]
    public async Task ResolveAsync_MalformedPercentEncoding_ReturnsFailureInsteadOfThrowing()
    {
        int calls = 0;
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        })));

        ServerTargetResolution result = await resolver.ResolveAsync(
            $"https://www.roblox.com/games/start?placeId=%&gameInstanceId={JobId}");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ResolveAsync_ExcessiveInput_IsRejectedWithoutRequest()
    {
        int calls = 0;
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        })));

        string input = "https://www.roblox.com/share?code=" + new string('a', 10_000);
        ServerTargetResolution result = await resolver.ResolveAsync(input);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, calls);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}

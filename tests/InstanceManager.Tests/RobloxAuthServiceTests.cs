using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public class RobloxAuthServiceTests
{
    [Fact]
    public async Task GetAuthTicketAsync_Retries_On429_ThenSucceeds()
    {
        int callCount = 0;
        var handler = new FakeHandler(req =>
        {
            callCount++;
            if (callCount == 1)
            {
                var csrfResp = new HttpResponseMessage(HttpStatusCode.Forbidden);
                csrfResp.Headers.TryAddWithoutValidation("x-csrf-token", "tok123");
                return csrfResp;
            }
            if (callCount == 2)
            {
                var limitResp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limitResp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return limitResp;
            }
            var okResp = new HttpResponseMessage(HttpStatusCode.OK);
            okResp.Headers.TryAddWithoutValidation("rbx-authentication-ticket", "TICKET_OK");
            return okResp;
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://auth.roblox.com") };
        var service = new RobloxAuthService(http);

        string ticket = await service.GetAuthTicketAsync("fakecookie");

        Assert.Equal("TICKET_OK", ticket);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task GetAuthTicketAsync_ThrowsAfterExhausting429Retries()
    {
        var handler = new FakeHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return resp;
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://auth.roblox.com") };
        var service = new RobloxAuthService(http);

        await Assert.ThrowsAsync<RobloxAuthException>(
            () => service.GetAuthTicketAsync("fakecookie"));
    }

    [Fact]
    public async Task GetAuthTicketAsync_RejectsHeaderInjectionBeforeSendingRequest()
    {
        int calls = 0;
        using var http = new HttpClient(new FakeHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var service = new RobloxAuthService(http);

        await Assert.ThrowsAsync<RobloxAuthException>(
            () => service.GetAuthTicketAsync("cookie\r\nX-Injected: true"));

        Assert.Equal(0, calls);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) => _factory = factory;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_factory(request));
    }
}

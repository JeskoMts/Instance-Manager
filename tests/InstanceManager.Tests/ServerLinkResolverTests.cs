using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ServerLinkResolverTests
{
    private const string JobId = "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab";
    private const string LinkCode = "12345678901234567890123456789012";
    private const string ShareCode = "0123456789abcdef0123456789abcdef";
    private const string ShareLink = "https://www.roblox.com/share?code=" + ShareCode + "&type=Server";

    private static readonly DpapiSecureStore Secure = new();

    [Theory]
    [InlineData("https://www.roblox.com/games/start?placeId=920587237&gameInstanceId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab")]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me?gameInstanceId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab")]
    [InlineData("roblox://experiences/start?placeId=920587237&gameInstanceId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab")]
    public async Task ResolveAsync_DirectServerLink_ReturnsTarget(string input)
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(_ => throw new InvalidOperationException())));

        ServerTargetResolution result = await resolver.ResolveAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(JoinMode.PrivateByJobId, result.Target!.Mode);
        Assert.Equal(920587237, result.Target.PlaceId);
        Assert.Equal(JobId, result.Target.JobId);
    }

    [Theory]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me?privateServerLinkCode=12345678901234567890123456789012")]
    [InlineData("https://www.roblox.com/games/920587237?privateServerLinkCode=12345678901234567890123456789012")]
    [InlineData("https://www.roblox.com/de/games/920587237/Adopt-Me?privateServerLinkCode=12345678901234567890123456789012")]
    [InlineData("  https://www.roblox.com/games/920587237/Adopt-Me?privateServerLinkCode=12345678901234567890123456789012  ")]
    public async Task ResolveAsync_PrivateServerLink_ReturnsThePrivateServerWithoutAnyRequest(string input)
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(_ => throw new InvalidOperationException())));

        ServerTargetResolution result = await resolver.ResolveAsync(input);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(JoinMode.PrivateServer, result.Target!.Mode);
        Assert.Equal(920587237, result.Target.PlaceId);
        Assert.Equal(LinkCode, result.Target.LinkCode);
        Assert.Null(result.Target.JobId);
    }

    [Theory]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me?privateServerLinkCode=")]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me?privateServerLinkCode=12%2634")]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me?privateServerLinkCode=a+b")]
    [InlineData("https://www.roblox.com/users/920587237/profile?privateServerLinkCode=12345678901234567890123456789012")]
    public async Task ResolveAsync_PrivateServerLinkWithABrokenCodeOrNoGame_IsNotAServer(string input)
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request })));

        ServerTargetResolution result = await resolver.ResolveAsync(input);

        Assert.False(result.IsSuccess);
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

        ServerTargetResolution result = await resolver.ResolveAsync("https://ro.blox.com/Ebh5?pid=share");

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
    public async Task ResolveAsync_GameLinkWithoutAServer_SaysWhatServerModeTakes()
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request })));

        ServerTargetResolution result = await resolver.ResolveAsync("https://www.roblox.com/games/55/example");

        Assert.False(result.IsSuccess);
        Assert.Contains("doesn't lead to a server", result.Error);
        Assert.Contains("private server link", result.Error);
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

    [Fact]
    public async Task ShareLink_IsLookedUpWithASavedLogin_AndBecomesThePrivateServer()
    {
        var roblox = new FakeRoblox();
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(JoinMode.PrivateServer, result.Target!.Mode);
        Assert.Equal(920587237, result.Target.PlaceId);
        Assert.Equal(LinkCode, result.Target.LinkCode);

        Assert.Equal(
            new[]
            {
                "POST https://apis.roblox.com/sharelinks/v1/resolve-link",
                "POST https://apis.roblox.com/sharelinks/v1/resolve-link",
                "GET https://games.roblox.com/v1/games?universeIds=383310974"
            },
            roblox.Requests.Select(r => $"{r.Method} {r.Uri}"));
        Assert.Equal($$"""{"linkId":"{{ShareCode}}","linkType":"Server"}""", roblox.Requests[1].Body);
        Assert.Equal("token-1", roblox.Requests[1].Csrf);
    }

    [Fact]
    public async Task ShareLink_SendsTheLoginOnlyToTheShareLinkApi()
    {
        var roblox = new FakeRoblox();
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        await resolver.ResolveAsync("https://ro.blox.com/Ebh5?pid=Server");

        Assert.Contains(roblox.Requests, r => r.Uri.Host == "ro.blox.com");
        Assert.Contains(roblox.Requests, r => r.Uri.Host == "games.roblox.com");
        Assert.All(roblox.Requests, r => Assert.Equal(r.Uri.Host == "apis.roblox.com", r.Cookie != null));
        Assert.All(roblox.Requests.Where(r => r.Cookie != null), r => Assert.Equal(".ROBLOSECURITY=cookie-a", r.Cookie));
    }

    [Theory]
    [InlineData("https://ro.blox.com/Ebh5?pid=Server")]
    [InlineData("https://www.roblox.com/share-links?code=0123456789abcdef0123456789abcdef&type=Server&pid=Server")]
    [InlineData("roblox://navigation/share_links?code=0123456789abcdef0123456789abcdef&type=Server")]
    [InlineData("https://www.roblox.com/share?type=server&code=0123456789abcdef0123456789abcdef")]
    public async Task ShareLink_InItsOtherForms_IsLookedUpTheSameWay(string input)
    {
        var roblox = new FakeRoblox();
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(input);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(LinkCode, result.Target!.LinkCode);
        Assert.Contains($"\"linkId\":\"{ShareCode}\",\"linkType\":\"Server\"", roblox.Requests.Last(r => r.Body != null).Body);
    }

    [Fact]
    public async Task ShareLink_WhenALoginHasExpired_TheNextAccountAsks()
    {
        var roblox = new FakeRoblox();
        ServerLinkResolver resolver = Create(roblox, Login("expired", "cookie-old"), Login("fine", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            new[] { ".ROBLOSECURITY=cookie-old", ".ROBLOSECURITY=cookie-a" },
            roblox.Requests.Where(r => r.Csrf != null).Select(r => r.Cookie));
    }

    [Fact]
    public async Task ShareLink_AsksTheAccountsBeingLaunchedFirst()
    {
        var roblox = new FakeRoblox { AcceptedCookies = { "cookie-b" } };
        Account first = Login("first", "cookie-a");
        Account launching = Login("launching", "cookie-b");
        ServerLinkResolver resolver = Create(roblox, first, launching);

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink, new[] { launching });

        Assert.True(result.IsSuccess, result.Error);
        Assert.All(roblox.Requests.Where(r => r.Cookie != null), r => Assert.Equal(".ROBLOSECURITY=cookie-b", r.Cookie));
    }

    [Fact]
    public async Task ShareLink_WithoutAnAccount_SaysALoginIsNeeded_AndAsksNothing()
    {
        var roblox = new FakeRoblox();
        ServerLinkResolver resolver = Create(roblox, new Account { Username = "unreadable", EncryptedCookie = "not-a-cookie" });

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.False(result.IsSuccess);
        Assert.Contains("signed-in account", result.Error);
        Assert.Empty(roblox.Requests);
    }

    [Fact]
    public async Task ShareLink_WhenRobloxAcceptsNoSavedLogin_SaysSo()
    {
        var roblox = new FakeRoblox();
        ServerLinkResolver resolver = Create(roblox, Login("one", "cookie-old"), Login("two", "cookie-older"));

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.False(result.IsSuccess);
        Assert.Contains("Sign in to an account again", result.Error);
        Assert.DoesNotContain(roblox.Requests, r => r.Uri.Host == "games.roblox.com");
    }

    [Fact]
    public async Task ShareLink_StopsAfterTenRejectedLogins()
    {
        var roblox = new FakeRoblox();
        Account[] expired = Enumerable.Range(0, 14).Select(i => Login($"old{i}", $"cookie-old-{i}")).ToArray();
        ServerLinkResolver resolver = Create(roblox, expired);

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.False(result.IsSuccess);
        Assert.Equal(10, roblox.Requests.Count(r => r.Csrf != null));
    }

    [Theory]
    [InlineData("""{"privateServerInviteData":{"status":"Expired","universeId":383310974,"linkCode":"12345678901234567890123456789012"}}""", "expired")]
    [InlineData("""{"privateServerInviteData":{"status":"Invalid","universeId":null,"linkCode":null}}""", "doesn't know this link")]
    [InlineData("""{"privateServerInviteData":null}""", "doesn't know this link")]
    [InlineData("""{"privateServerInviteData":{"status":"Valid","universeId":383310974,"linkCode":"bad code&x=1"}}""", "doesn't know this link")]
    [InlineData("<html>not json</html>", "doesn't know this link")]
    public async Task ShareLink_ThatRobloxDoesNotOpen_ReturnsThePreciseReason(string answer, string expected)
    {
        var roblox = new FakeRoblox { ResolveAnswer = answer };
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.False(result.IsSuccess);
        Assert.Contains(expected, result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "too many requests")]
    [InlineData(HttpStatusCode.BadRequest, "doesn't know this link")]
    [InlineData(HttpStatusCode.InternalServerError, "couldn't look up this link")]
    public async Task ShareLink_WhenTheApiRefuses_ReturnsTheReasonWithoutTryingMoreLogins(HttpStatusCode status, string expected)
    {
        var roblox = new FakeRoblox { ResolveStatus = status, AcceptedCookies = { "cookie-b" } };
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"), Login("second", "cookie-b"));

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.False(result.IsSuccess);
        Assert.Contains(expected, result.Error);
        Assert.DoesNotContain(roblox.Requests, r => r.Cookie == ".ROBLOSECURITY=cookie-b");
    }

    [Fact]
    public async Task ShareLink_WhenTheGameCantBeLookedUp_Fails()
    {
        var roblox = new FakeRoblox { GamesAnswer = """{"data":[]}""" };
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.False(result.IsSuccess);
        Assert.Contains("which game", result.Error);
    }

    [Fact]
    public async Task ExperienceInvite_BecomesTheServerTheInviteLeadsTo()
    {
        var roblox = new FakeRoblox
        {
            ResolveAnswer = $$"""{"experienceInviteData":{"status":"Valid","inviterId":5,"placeId":606849621,"instanceId":"{{JobId}}"},"privateServerInviteData":null}"""
        };
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(
            $"https://www.roblox.com/share?code={ShareCode}&type=ExperienceInvite");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(JoinMode.PrivateByJobId, result.Target!.Mode);
        Assert.Equal(606849621, result.Target.PlaceId);
        Assert.Equal(JobId, result.Target.JobId);
        Assert.Contains("\"linkType\":\"ExperienceInvite\"", roblox.Requests[1].Body);
        Assert.DoesNotContain(roblox.Requests, r => r.Uri.Host == "games.roblox.com");
    }

    [Theory]
    [InlineData("Expired")]
    [InlineData("InviterNotInExperience")]
    public async Task ExperienceInvite_ThatIsOver_Fails(string status)
    {
        var roblox = new FakeRoblox
        {
            ResolveAnswer = $$$"""{"experienceInviteData":{"status":"{{{status}}}","inviterId":5,"placeId":606849621,"instanceId":null}}"""
        };
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(
            $"https://www.roblox.com/share?code={ShareCode}&type=ExperienceInvite");

        Assert.False(result.IsSuccess);
        Assert.Contains("no longer leads to a server", result.Error);
    }

    [Theory]
    [InlineData("https://www.roblox.com/share?code=0123456789abcdef0123456789abcdef&type=Profile")]
    [InlineData("https://www.roblox.com/share?code=0123456789abcdef0123456789abcdef&type=FriendInvite")]
    public async Task ShareLink_ThatIsNoServer_IsRefusedWithoutAnyRequest(string input)
    {
        var roblox = new FakeRoblox();
        ServerLinkResolver resolver = Create(roblox, Login("first", "cookie-a"));

        ServerTargetResolution result = await resolver.ResolveAsync(input);

        Assert.False(result.IsSuccess);
        Assert.Contains("doesn't lead to a server", result.Error);
        Assert.Empty(roblox.Requests);
    }

    [Fact]
    public async Task ShareLink_WithoutTheShareLinkService_FailsInsteadOfThrowing()
    {
        var resolver = new ServerLinkResolver(new HttpClient(new StubHandler(_ => throw new InvalidOperationException())));

        ServerTargetResolution result = await resolver.ResolveAsync(ShareLink);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("https://ro.blox.com/Ebh5?pid=Server")]
    [InlineData(ShareLink)]
    public async Task NoConnectionOrATimeout_ReturnsAFailureInsteadOfThrowing(string input)
    {
        foreach (Exception failure in new Exception[] { new HttpRequestException("offline"), new TaskCanceledException("timeout") })
        {
            var http = new HttpClient(new StubHandler(_ => throw failure));
            var resolver = new ServerLinkResolver(http, new RobloxShareLinkService(
                http, new RobloxGamesService(http), new Accounts(Login("first", "cookie-a")), Secure));

            ServerTargetResolution result = await resolver.ResolveAsync(input);

            Assert.False(result.IsSuccess);
            Assert.Contains("couldn't be reached", result.Error);
        }
    }

    [Fact]
    public async Task CancellingTheLaunch_StillCancelsTheLookup()
    {
        using var cancel = new CancellationTokenSource();
        var http = new HttpClient(new StubHandler(_ =>
        {
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        }));
        var resolver = new ServerLinkResolver(http);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveAsync("https://ro.blox.com/Ebh5?pid=Server", cancel.Token));
    }

    private static ServerLinkResolver Create(HttpMessageHandler handler, params Account[] saved)
    {
        var http = new HttpClient(handler);
        return new ServerLinkResolver(http, new RobloxShareLinkService(
            http, new RobloxGamesService(http), new Accounts(saved), Secure));
    }

    private static Account Login(string name, string cookie) =>
        new() { Username = name, EncryptedCookie = Secure.Protect(cookie) };

    private sealed record Seen(HttpMethod Method, Uri Uri, string? Cookie, string? Csrf, string? Body);

    private sealed class FakeRoblox : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = new();
        public HashSet<string> AcceptedCookies { get; } = new() { "cookie-a" };
        public HttpStatusCode ResolveStatus { get; init; } = HttpStatusCode.OK;
        public string ResolveAnswer { get; init; } =
            """{"experienceInviteData":null,"privateServerInviteData":{"status":"Valid","ownerUserId":1,"universeId":383310974,"privateServerId":77,"linkCode":"12345678901234567890123456789012"}}""";
        public string GamesAnswer { get; init; } =
            """{"data":[{"id":383310974,"rootPlaceId":920587237,"name":"Adopt Me!"}]}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri!;
            string? cookie = request.Headers.TryGetValues("Cookie", out IEnumerable<string>? cookies) ? cookies.Single() : null;
            string? csrf = request.Headers.TryGetValues("X-CSRF-TOKEN", out IEnumerable<string>? tokens) ? tokens.Single() : null;
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Seen(request.Method, uri, cookie, csrf, body));

            if (uri.Host == "apis.roblox.com" && uri.AbsolutePath == "/sharelinks/v1/resolve-link")
            {
                if (csrf == null)
                    return new HttpResponseMessage(HttpStatusCode.Forbidden) { Headers = { { "x-csrf-token", "token-1" } } };
                if (cookie == null || !AcceptedCookies.Contains(cookie[".ROBLOSECURITY=".Length..]))
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                return new HttpResponseMessage(ResolveStatus) { Content = new StringContent(ResolveAnswer, Encoding.UTF8, "application/json") };
            }

            if (uri.Host == "games.roblox.com")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GamesAnswer, Encoding.UTF8, "application/json") };

            if (uri.Host == "ro.blox.com")
            {
                return new HttpResponseMessage(HttpStatusCode.MovedPermanently)
                {
                    Headers = { Location = new Uri($"https://www.roblox.com/share-links?code={ShareCode}&type=Server&pid=Server") }
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html></html>") };
        }
    }

    private sealed class Accounts : IAccountRepository
    {
        private readonly List<Account> _items;
        public Accounts(params Account[] items) => _items = items.ToList();
        public IReadOnlyList<Account> All => _items;
        public Account? FindByUserId(long userId) => _items.Find(a => a.UserId == userId);
        public void Upsert(Account account) { }
        public void Remove(Account account) { }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}

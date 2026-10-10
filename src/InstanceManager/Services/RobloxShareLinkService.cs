using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;

namespace InstanceManager.Services;

public sealed class RobloxShareLinkService
{
    private const string ResolveUrl = "https://apis.roblox.com/sharelinks/v1/resolve-link";
    private const string PrivateServerType = "Server";
    private const string ExperienceInviteType = "ExperienceInvite";
    private const int MaxLoginsTried = 10;

    private const string UnknownLink = "Roblox doesn't know this link. Copy it again from Roblox.";

    private readonly HttpClient _http;
    private readonly IRobloxGamesService _games;
    private readonly IAccountRepository _accounts;
    private readonly DpapiSecureStore _secure;

    public RobloxShareLinkService(
        HttpClient http, IRobloxGamesService games, IAccountRepository accounts, DpapiSecureStore secure)
    {
        _http = http;
        _games = games;
        _accounts = accounts;
        _secure = secure;
    }

    public async Task<ServerTargetResolution> ResolveAsync(
        string code, string type, IReadOnlyList<Account> preferredAccounts, CancellationToken cancellationToken = default)
    {
        string? linkType = new[] { PrivateServerType, ExperienceInviteType }
            .FirstOrDefault(known => string.Equals(known, type, StringComparison.OrdinalIgnoreCase));
        if (linkType == null)
            return ServerTargetResolution.Failure("This share link doesn't lead to a server.");
        if (!GameLinkParser.TryParseLinkCode(code, out string linkId))
            return ServerTargetResolution.Failure(UnknownLink);

        int tried = 0;
        foreach (Account account in Logins(preferredAccounts))
        {
            if (!_secure.TryUnprotect(account.EncryptedCookie, out string cookie) ||
                !RobloxAuthService.IsValidCookieValue(cookie))
                continue;
            if (tried++ == MaxLoginsTried)
                break;

            using HttpResponseMessage response = await PostAsync(cookie, linkId, linkType, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                continue;

            return await ReadAnswerAsync(response, linkType, cancellationToken);
        }

        return ServerTargetResolution.Failure(tried == 0
            ? "Roblox opens this kind of link only for a signed-in account. Add an account first."
            : "Roblox didn't accept the saved logins, so this link couldn't be looked up. Sign in to an account again.");
    }

    private IEnumerable<Account> Logins(IReadOnlyList<Account> preferredAccounts)
    {
        var seen = new HashSet<Guid>();
        foreach (Account account in preferredAccounts.Concat(_accounts.All))
        {
            if (seen.Add(account.Id))
                yield return account;
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string cookie, string linkId, string linkType, CancellationToken cancellationToken)
    {
        string? csrf = null;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ResolveUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { linkId, linkType }), Encoding.UTF8, "application/json")
            };
            RobloxAuthService.AddCookie(request, cookie);
            request.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
            request.Headers.Referrer = new Uri("https://www.roblox.com/");
            if (csrf != null)
                request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);

            HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            if (csrf == null && response.StatusCode == HttpStatusCode.Forbidden &&
                response.Headers.TryGetValues("x-csrf-token", out IEnumerable<string>? tokens) &&
                tokens.FirstOrDefault() is { Length: > 0 } token)
            {
                response.Dispose();
                csrf = token;
                continue;
            }

            return response;
        }
    }

    private async Task<ServerTargetResolution> ReadAnswerAsync(
        HttpResponseMessage response, string linkType, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return ServerTargetResolution.Failure("Roblox is getting too many requests right now. Try again in a minute.");
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            return ServerTargetResolution.Failure(UnknownLink);
        if (!response.IsSuccessStatusCode)
            return ServerTargetResolution.Failure("Roblox couldn't look up this link right now. Try again in a moment.");

        ResolveAnswer? answer;
        try
        {
            answer = await response.Content.ReadFromJsonAsync<ResolveAnswer>(cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            answer = null;
        }

        return linkType == PrivateServerType
            ? await ToPrivateServerAsync(answer?.PrivateServerInviteData, cancellationToken)
            : ToInvitedServer(answer?.ExperienceInviteData);
    }

    private async Task<ServerTargetResolution> ToPrivateServerAsync(PrivateServerInvite? invite, CancellationToken cancellationToken)
    {
        if (string.Equals(invite?.Status, "Expired", StringComparison.OrdinalIgnoreCase))
            return ServerTargetResolution.Failure("This private server link has expired. Ask its owner for a new one.");
        if (invite is not { UniverseId: > 0 } ||
            !string.Equals(invite.Status, "Valid", StringComparison.OrdinalIgnoreCase) ||
            !GameLinkParser.TryParseLinkCode(invite.LinkCode, out string linkCode))
            return ServerTargetResolution.Failure(UnknownLink);

        long? placeId = await _games.GetRootPlaceIdAsync(invite.UniverseId.Value, cancellationToken);
        return placeId is long id
            ? ServerTargetResolution.Success(ServerTarget.PrivateServer(id, linkCode))
            : ServerTargetResolution.Failure("Roblox didn't say which game this private server belongs to. Try again in a moment.");
    }

    private static ServerTargetResolution ToInvitedServer(ExperienceInvite? invite)
    {
        if (invite is not { PlaceId: > 0 })
            return ServerTargetResolution.Failure(UnknownLink);

        return string.Equals(invite.Status, "Valid", StringComparison.OrdinalIgnoreCase) &&
               GameLinkParser.TryParseJobId(invite.InstanceId, out string jobId)
            ? ServerTargetResolution.Success(ServerTarget.ByJob(invite.PlaceId.Value, jobId))
            : ServerTargetResolution.Failure("This invite no longer leads to a server. Ask for a new one.");
    }

    private sealed class ResolveAnswer
    {
        public PrivateServerInvite? PrivateServerInviteData { get; set; }
        public ExperienceInvite? ExperienceInviteData { get; set; }
    }

    private sealed class PrivateServerInvite
    {
        public string? Status { get; set; }
        public long? UniverseId { get; set; }
        public string? LinkCode { get; set; }
    }

    private sealed class ExperienceInvite
    {
        public string? Status { get; set; }
        public long? PlaceId { get; set; }
        public string? InstanceId { get; set; }
    }
}

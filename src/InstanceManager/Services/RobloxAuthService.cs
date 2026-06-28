using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InstanceManager.Services;

public sealed record RobloxUserInfo(long Id, string Name, string DisplayName);

public sealed class RobloxAuthException : Exception
{
    public RobloxAuthException(string message) : base(message) { }
}

public sealed class RobloxAuthService
{
    private const int MaxCookieCharacters = 16_384;
    private const string AuthTicketUrl = "https://auth.roblox.com/v1/authentication-ticket/";
    private const string AuthenticatedUserUrl = "https://users.roblox.com/v1/users/authenticated";
    private const string Referer = "https://www.roblox.com/";

    private readonly HttpClient _http;

    public RobloxAuthService(HttpClient http) => _http = http;

    public async Task<string> GetAuthTicketAsync(string cookie, CancellationToken ct = default)
    {
        if (!IsValidCookieValue(cookie))
            throw new RobloxAuthException("Cookie has an invalid format.");

        string? csrf = null;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, AuthTicketUrl)
            {
                Content = new StringContent(string.Empty)
            };
            AddCookie(req, cookie);
            req.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
            req.Headers.Referrer = new Uri(Referer);
            if (csrf != null)
                req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);

            using HttpResponseMessage resp = await _http.SendAsync(req, ct);

            if (resp.StatusCode == HttpStatusCode.Forbidden &&
                resp.Headers.TryGetValues("x-csrf-token", out var tokens))
            {
                csrf = System.Linq.Enumerable.FirstOrDefault(tokens);
                if (!string.IsNullOrEmpty(csrf))
                    continue;
            }

            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                int waitSeconds = 5;
                if (resp.Headers.RetryAfter?.Delta is TimeSpan delta)
                    waitSeconds = Math.Clamp((int)Math.Ceiling(delta.TotalSeconds), 1, 30);
                else if (resp.Headers.RetryAfter?.Date is DateTimeOffset date)
                    waitSeconds = Math.Clamp((int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds), 1, 30);

                await Task.Delay(waitSeconds * 1000, ct);
                continue;
            }

            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new RobloxAuthException("Cookie is invalid or expired (Roblox rejected the sign-in).");

            resp.EnsureSuccessStatusCode();

            if (resp.Headers.TryGetValues("rbx-authentication-ticket", out var ticketValues))
            {
                string? ticket = System.Linq.Enumerable.FirstOrDefault(ticketValues);
                if (!string.IsNullOrWhiteSpace(ticket))
                    return ticket!;
            }

            throw new RobloxAuthException("Roblox did not return an authentication ticket.");
        }

        throw new RobloxAuthException("Authentication failed after multiple retries (CSRF handshake or rate limit).");
    }

    public async Task<RobloxUserInfo?> GetUserInfoAsync(string cookie, CancellationToken ct = default)
    {
        if (!IsValidCookieValue(cookie))
            return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, AuthenticatedUserUrl);
        AddCookie(req, cookie);

        using HttpResponseMessage resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        var dto = await resp.Content.ReadFromJsonAsync<AuthenticatedUserDto>(cancellationToken: ct);
        if (dto is null || dto.Id == 0)
            return null;

        return new RobloxUserInfo(dto.Id, dto.Name ?? string.Empty, dto.DisplayName ?? dto.Name ?? string.Empty);
    }

    private static void AddCookie(HttpRequestMessage req, string cookie) =>
        req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");

    private static bool IsValidCookieValue(string? cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie) || cookie.Length > MaxCookieCharacters)
            return false;

        foreach (char c in cookie)
        {
            if (char.IsControl(c) || c == ';')
                return false;
        }

        return true;
    }

    private sealed class AuthenticatedUserDto
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
    }
}

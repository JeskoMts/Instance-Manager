using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public sealed class ServerLinkResolver : IServerLinkResolver
{
    private const int MaxRedirects = 5;
    private const int MaxInputLength = 4096;

    private static readonly string[] JobKeys = { "gameInstanceId", "jobId", "gameId" };
    private static readonly string[] LinkCodeKeys = { "privateServerLinkCode", "linkCode" };
    private static readonly string[] SharePages = { "share", "share-links", "share_links" };

    private readonly HttpClient _http;
    private readonly RobloxShareLinkService? _shareLinks;

    public ServerLinkResolver(HttpClient http, RobloxShareLinkService? shareLinks = null)
    {
        _http = http;
        _shareLinks = shareLinks;
    }

    public Task<ServerTargetResolution> ResolveAsync(string input, CancellationToken cancellationToken = default) =>
        ResolveAsync(input, Array.Empty<Account>(), cancellationToken);

    public async Task<ServerTargetResolution> ResolveAsync(
        string input, IReadOnlyList<Account> accounts, CancellationToken cancellationToken = default)
    {
        string value = input?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > MaxInputLength ||
            HasMalformedPercentEncoding(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? link))
        {
            return ServerTargetResolution.Failure("Enter a complete Roblox server link.");
        }

        try
        {
            return await FollowAsync(link, accounts, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException ||
                                   (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return ServerTargetResolution.Failure("Roblox couldn't be reached to check this link. Check your connection and try again.");
        }
    }

    private async Task<ServerTargetResolution> FollowAsync(
        Uri current, IReadOnlyList<Account> accounts, CancellationToken cancellationToken)
    {
        if (string.Equals(current.Scheme, "roblox", StringComparison.OrdinalIgnoreCase))
        {
            if (TryCreateTarget(current, out ServerTarget? target))
                return ServerTargetResolution.Success(target!);

            return TryReadShareLink(current, out string code, out string type)
                ? await ResolveShareLinkAsync(code, type, accounts, cancellationToken)
                : NotAServerLink();
        }

        if (!IsAllowedWebUri(current))
            return ServerTargetResolution.Failure("Only official Roblox server links are supported.");

        for (int redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            if (current.AbsoluteUri.Length > MaxInputLength || HasMalformedPercentEncoding(current.Query))
                return ServerTargetResolution.Failure("The Roblox server link contains invalid encoding.");

            if (TryCreateTarget(current, out ServerTarget? target))
                return ServerTargetResolution.Success(target!);

            if (TryReadShareLink(current, out string code, out string type))
                return await ResolveShareLinkAsync(code, type, accounts, cancellationToken);

            using HttpResponseMessage response = await _http.GetAsync(
                current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!IsRedirect(response.StatusCode) || response.Headers.Location == null)
                return NotAServerLink();

            Uri next = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(current, response.Headers.Location);
            if (!IsAllowedWebUri(next))
                return ServerTargetResolution.Failure("The Roblox link redirected to an unsupported domain.");
            current = next;
        }

        return ServerTargetResolution.Failure("The Roblox server link used too many redirects.");
    }

    private Task<ServerTargetResolution> ResolveShareLinkAsync(
        string code, string type, IReadOnlyList<Account> accounts, CancellationToken cancellationToken) =>
        _shareLinks != null
            ? _shareLinks.ResolveAsync(code, type, accounts, cancellationToken)
            : Task.FromResult(ServerTargetResolution.Failure("Share links can't be checked right now."));

    private static bool HasMalformedPercentEncoding(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '%')
                continue;

            if (i + 2 >= value.Length ||
                !Uri.IsHexDigit(value[i + 1]) ||
                !Uri.IsHexDigit(value[i + 2]))
            {
                return true;
            }

            i += 2;
        }

        return false;
    }

    private static bool TryCreateTarget(Uri uri, out ServerTarget? target)
    {
        target = null;
        Dictionary<string, string> query = ParseQuery(uri.Query);
        if (!TryGetPlaceId(uri, query, out long placeId))
            return false;

        if (GameLinkParser.TryParseJobId(FirstValue(query, JobKeys), out string jobId))
            target = ServerTarget.ByJob(placeId, jobId);
        else if (GameLinkParser.TryParseLinkCode(FirstValue(query, LinkCodeKeys), out string linkCode))
            target = ServerTarget.PrivateServer(placeId, linkCode);

        return target != null;
    }

    private static bool TryGetPlaceId(Uri uri, Dictionary<string, string> query, out long placeId)
    {
        if (query.TryGetValue("placeId", out string? placeText) &&
            long.TryParse(placeText, out placeId) && placeId > 0)
            return true;

        string[] segments = PathSegments(uri);
        int games = Array.FindIndex(segments, segment => string.Equals(segment, "games", StringComparison.OrdinalIgnoreCase));
        placeId = 0;
        return games is 0 or 1 && games + 1 < segments.Length &&
               long.TryParse(segments[games + 1], NumberStyles.None, CultureInfo.InvariantCulture, out placeId) &&
               placeId > 0;
    }

    private static bool TryReadShareLink(Uri uri, out string code, out string type)
    {
        code = string.Empty;
        type = string.Empty;
        string[] segments = PathSegments(uri);
        if (segments.Length == 0 || !SharePages.Contains(segments[^1], StringComparer.OrdinalIgnoreCase))
            return false;

        Dictionary<string, string> query = ParseQuery(uri.Query);
        if (!query.TryGetValue("code", out string? codeText) || !query.TryGetValue("type", out string? typeText))
            return false;

        code = codeText;
        type = typeText;
        return true;
    }

    private static string[] PathSegments(Uri uri) =>
        uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string? FirstValue(Dictionary<string, string> query, string[] keys)
    {
        foreach (string key in keys)
        {
            if (query.TryGetValue(key, out string? value))
                return value;
        }

        return null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=');
            if (separator <= 0) continue;
            string key = Uri.UnescapeDataString(pair[..separator]);
            string value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            values[key] = value;
        }
        return values;
    }

    private static bool IsAllowedWebUri(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        string host = uri.IdnHost;
        return string.Equals(host, "roblox.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "ro.blox.com", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static ServerTargetResolution NotAServerLink() =>
        ServerTargetResolution.Failure(
            "This link doesn't lead to a server. Paste a server link, a private server link or a link with a Job ID.");
}

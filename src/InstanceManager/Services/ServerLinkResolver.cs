using System;
using System.Collections.Generic;
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
    private readonly HttpClient _http;

    public ServerLinkResolver(HttpClient http) => _http = http;

    public async Task<ServerTargetResolution> ResolveAsync(string input, CancellationToken cancellationToken = default)
    {
        string value = input?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > MaxInputLength ||
            HasMalformedPercentEncoding(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? current))
        {
            return ServerTargetResolution.Failure("Enter a complete Roblox server link.");
        }

        if (string.Equals(current.Scheme, "roblox", StringComparison.OrdinalIgnoreCase))
            return TryCreateTarget(current, out ServerTarget? target)
                ? ServerTargetResolution.Success(target!)
                : MissingIds();

        if (!IsAllowedWebUri(current))
            return ServerTargetResolution.Failure("Only official Roblox server links are supported.");

        for (int redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            if (current.AbsoluteUri.Length > MaxInputLength || HasMalformedPercentEncoding(current.Query))
                return ServerTargetResolution.Failure("The Roblox server link contains invalid encoding.");

            if (TryCreateTarget(current, out ServerTarget? target))
                return ServerTargetResolution.Success(target!);

            using HttpResponseMessage response = await _http.GetAsync(
                current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!IsRedirect(response.StatusCode) || response.Headers.Location == null)
                return MissingIds();

            Uri next = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(current, response.Headers.Location);
            if (!IsAllowedWebUri(next))
                return ServerTargetResolution.Failure("The Roblox link redirected to an unsupported domain.");
            current = next;
        }

        return ServerTargetResolution.Failure("The Roblox server link used too many redirects.");
    }

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
        if (!query.TryGetValue("placeId", out string? placeText) ||
            !long.TryParse(placeText, out long placeId) || placeId <= 0)
            return false;

        string? jobText = null;
        foreach (string key in new[] { "gameInstanceId", "jobId", "gameId" })
        {
            if (query.TryGetValue(key, out jobText))
                break;
        }

        if (!GameLinkParser.TryParseJobId(jobText, out string jobId))
            return false;

        target = ServerTarget.ByJob(placeId, jobId);
        return true;
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

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static ServerTargetResolution MissingIds() =>
        ServerTargetResolution.Failure("The server link must contain both a Place ID and Job ID.");
}

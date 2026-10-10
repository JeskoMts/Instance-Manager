using System;
using System.Text.RegularExpressions;

namespace InstanceManager.Services;

public static class GameLinkParser
{
    private static readonly Regex GamesPath = new(
        @"roblox\.com/(?:[a-z\-]{2,5}/)?games/(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PlaceIdQuery = new(
        @"[?&]placeId=(?<id>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PureDigits = new(@"^\s*(?<id>\d{1,19})\s*$", RegexOptions.Compiled);

    private static readonly Regex LinkCode = new(@"\A[A-Za-z0-9_\-]{1,128}\z", RegexOptions.Compiled);

    public static bool TryParsePlaceId(string? input, out long placeId)
    {
        placeId = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string s = input.Trim();

        Match m = PureDigits.Match(s);
        if (!m.Success) m = GamesPath.Match(s);
        if (!m.Success) m = PlaceIdQuery.Match(s);
        if (!m.Success)
            return false;

        return long.TryParse(m.Groups["id"].Value, out placeId) && placeId > 0;
    }

    public static bool LooksLikeServerLink(string? input) =>
        !string.IsNullOrWhiteSpace(input) &&
        (input.Contains("gameInstanceId", StringComparison.OrdinalIgnoreCase) ||
         input.Contains("linkCode", StringComparison.OrdinalIgnoreCase) ||
         input.Contains("roblox.com/share", StringComparison.OrdinalIgnoreCase) ||
         input.Contains("share?code", StringComparison.OrdinalIgnoreCase) ||
         input.Contains("share_links", StringComparison.OrdinalIgnoreCase));

    public static bool TryParseLinkCode(string? input, out string linkCode)
    {
        linkCode = input?.Trim() ?? string.Empty;
        if (LinkCode.IsMatch(linkCode))
            return true;

        linkCode = string.Empty;
        return false;
    }

    public static bool TryParseJobId(string? input, out string jobId)
    {
        jobId = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string s = input.Trim().Trim('{', '}');
        if (Guid.TryParse(s, out Guid g))
        {
            jobId = g.ToString("D");
            return true;
        }
        return false;
    }
}

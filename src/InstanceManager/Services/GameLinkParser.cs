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

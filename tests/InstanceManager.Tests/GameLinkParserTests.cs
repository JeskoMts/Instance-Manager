using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public class GameLinkParserTests
{
    [Theory]
    [InlineData("https://www.roblox.com/de/games/126884695634066/Grow-a-Garden", 126884695634066)]
    [InlineData("https://www.roblox.com/games/126884695634066/Grow-a-Garden", 126884695634066)]
    [InlineData("https://roblox.com/games/920587237/Adopt-Me", 920587237)]
    [InlineData("https://www.roblox.com/en-us/games/606849621/Jailbreak", 606849621)]
    [InlineData("126884695634066", 126884695634066)]
    [InlineData("  920587237  ", 920587237)]
    [InlineData("https://www.roblox.com/games/start?placeId=606849621", 606849621)]
    public void TryParsePlaceId_ValidInputs_ReturnsPlaceId(string input, long expected)
    {
        bool ok = GameLinkParser.TryParsePlaceId(input, out long placeId);
        Assert.True(ok);
        Assert.Equal(expected, placeId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("nicht-eine-url")]
    [InlineData("https://www.roblox.com/users/1/profile")]
    [InlineData("0")]
    public void TryParsePlaceId_InvalidInputs_ReturnsFalse(string? input)
    {
        Assert.False(GameLinkParser.TryParsePlaceId(input, out long placeId));
        Assert.Equal(0, placeId);
    }

    [Theory]
    [InlineData("ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab")]
    [InlineData("{ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab}")]
    public void TryParseJobId_ValidGuid_NormalizesValue(string input)
    {
        bool ok = GameLinkParser.TryParseJobId(input, out string jobId);
        Assert.True(ok);
        Assert.Equal("ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab", jobId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public void TryParseJobId_Invalid_ReturnsFalse(string input)
    {
        Assert.False(GameLinkParser.TryParseJobId(input, out _));
    }

    [Theory]
    [InlineData("12345678901234567890123456789012", "12345678901234567890123456789012")]
    [InlineData("  0123456789abcdef0123456789ABCDEF ", "0123456789abcdef0123456789ABCDEF")]
    [InlineData("a-b_c", "a-b_c")]
    public void TryParseLinkCode_LettersDigitsDashAndUnderscore_AreAccepted(string input, string expected)
    {
        Assert.True(GameLinkParser.TryParseLinkCode(input, out string linkCode));
        Assert.Equal(expected, linkCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12 34")]
    [InlineData("12&linkCode=34")]
    [InlineData("12+34")]
    [InlineData("12%2634")]
    [InlineData("code\nsecond line")]
    public void TryParseLinkCode_AnythingElse_IsRefused(string? input)
    {
        Assert.False(GameLinkParser.TryParseLinkCode(input, out string linkCode));
        Assert.Equal(string.Empty, linkCode);
    }

    [Fact]
    public void TryParseLinkCode_StopsAt128Characters()
    {
        Assert.True(GameLinkParser.TryParseLinkCode(new string('7', 128), out _));
        Assert.False(GameLinkParser.TryParseLinkCode(new string('7', 129), out _));
    }

    [Theory]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me?privateServerLinkCode=123")]
    [InlineData("https://www.roblox.com/share?code=0123456789abcdef0123456789abcdef&type=Server")]
    [InlineData("https://www.roblox.com/share-links?code=0123456789abcdef0123456789abcdef&type=Server")]
    [InlineData("roblox://navigation/share_links?code=0123456789abcdef0123456789abcdef&type=Server")]
    [InlineData("https://www.roblox.com/games/start?placeId=1&gameInstanceId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab")]
    public void LooksLikeServerLink_KnowsEveryServerLinkForm(string input)
    {
        Assert.True(GameLinkParser.LooksLikeServerLink(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("920587237")]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me")]
    public void LooksLikeServerLink_AGameAlone_IsNoServerLink(string? input)
    {
        Assert.False(GameLinkParser.LooksLikeServerLink(input));
    }
}

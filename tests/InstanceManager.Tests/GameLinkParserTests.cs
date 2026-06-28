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
}

using System.Windows.Media;
using InstanceManager.Controls;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ColorMathTests
{
    [Theory]
    [InlineData("#FFFFFF", 255, 255, 255)]
    [InlineData("ff8800", 255, 136, 0)]
    [InlineData("  #0a0B0c ", 10, 11, 12)]
    [InlineData("#f80", 255, 136, 0)]
    public void TryParseHex_AcceptsShortLongAndUnprefixedForms(string text, byte r, byte g, byte b)
    {
        Assert.True(ColorMath.TryParseHex(text, out Color color));
        Assert.Equal(Color.FromRgb(r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("#1234567")]
    public void TryParseHex_RejectsAnythingElse(string? text)
    {
        Assert.False(ColorMath.TryParseHex(text, out _));
    }

    [Fact]
    public void NormalizeHex_ReturnsUppercaseSixDigitsOrNull()
    {
        Assert.Equal("#FF8800", ColorMath.NormalizeHex("f80"));
        Assert.Null(ColorMath.NormalizeHex("nope"));
    }

    [Theory]
    [InlineData(255, 0, 0, 0)]
    [InlineData(0, 255, 0, 120)]
    [InlineData(0, 0, 255, 240)]
    public void ToHsv_FindsTheHueOfPrimaryColors(byte r, byte g, byte b, double hue)
    {
        (double h, double s, double v) = ColorMath.ToHsv(Color.FromRgb(r, g, b));

        Assert.Equal(hue, h, 3);
        Assert.Equal(1, s, 3);
        Assert.Equal(1, v, 3);
    }

    [Theory]
    [InlineData("#6E8FD9")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#7F7F7F")]
    [InlineData("#D94A8C")]
    public void HsvRoundTrip_KeepsTheSameHex(string hex)
    {
        Assert.True(ColorMath.TryParseHex(hex, out Color color));
        (double h, double s, double v) = ColorMath.ToHsv(color);

        Assert.Equal(hex, ColorMath.ToHex(ColorMath.FromHsv(h, s, v)));
    }

    [Fact]
    public void FromHsv_WrapsHueAndClampsTheRest()
    {
        Assert.Equal(ColorMath.FromHsv(0, 1, 1), ColorMath.FromHsv(360, 1, 1));
        Assert.Equal(ColorMath.FromHsv(300, 1, 1), ColorMath.FromHsv(-60, 1, 1));
        Assert.Equal(Colors.White, ColorMath.FromHsv(0, -1, 2));
    }
}

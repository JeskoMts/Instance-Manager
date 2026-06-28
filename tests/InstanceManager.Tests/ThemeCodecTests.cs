using InstanceManager.Models;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ThemeCodecTests
{
    [Fact]
    public void EncodeThenDecode_RoundTripsThemeContent()
    {
        var theme = new ThemeDefinition
        {
            Id = "abc123",
            Name = "Sunset",
            IsBuiltIn = false,
            Palette = new ThemePalette
            {
                Window = "#101015",
                Surface = "#1A1A22",
                Accent = "#FF7A45",
                TextPrimary = "#F5F5F7",
                Danger = "#E5484D"
            }
        };

        string code = ThemeCodec.Encode(theme);

        Assert.StartsWith(ThemeCodec.Prefix, code);
        Assert.DoesNotContain("\n", code);

        Assert.True(ThemeCodec.TryDecode(code, out ThemeDefinition? decoded));
        Assert.Equal(theme.Id, decoded!.Id);
        Assert.Equal(theme.Name, decoded.Name);
        Assert.False(decoded.IsBuiltIn);
        Assert.Equal(theme.Palette.Window, decoded.Palette.Window);
        Assert.Equal(theme.Palette.Accent, decoded.Palette.Accent);
        Assert.Equal(theme.Palette.TextPrimary, decoded.Palette.TextPrimary);
        Assert.Equal(theme.Palette.Danger, decoded.Palette.Danger);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just some text")]
    [InlineData("INSTANCEMANAGER-THEME-v1:not-valid-base64!!")]
    [InlineData("INSTANCEMANAGER-THEME-v1:bm90IGpzb24=")]
    [InlineData("WRONG-PREFIX:eyJpZCI6IngifQ==")]
    public void TryDecode_RejectsInvalidInput(string? code)
    {
        Assert.False(ThemeCodec.TryDecode(code, out ThemeDefinition? decoded));
        Assert.Null(decoded);
    }

    [Fact]
    public void TryDecode_RejectsJsonMissingName()
    {
        string code = ThemeCodec.Encode(new ThemeDefinition { Id = "x", Name = "", Palette = new ThemePalette() });
        Assert.False(ThemeCodec.TryDecode(code, out _));
    }

    [Fact]
    public void TryDecode_RejectsOversizedEncodedPayloadBeforeDecoding()
    {
        string code = ThemeCodec.Prefix + new string('A', ThemeCodec.MaxEncodedPayloadCharacters + 1);

        Assert.False(ThemeCodec.TryDecode(code, out _));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    [InlineData("url(file:///secret)")]
    public void TryDecode_RejectsPaletteValuesOutsideStrictHexFormat(string color)
    {
        var theme = new ThemeDefinition
        {
            Id = "imported",
            Name = "Unsafe",
            Palette = new ThemePalette { Accent = color }
        };

        string code = ThemeCodec.Encode(theme);

        Assert.False(ThemeCodec.TryDecode(code, out _));
    }

    [Fact]
    public void TryDecode_RejectsExcessiveThemeName()
    {
        string code = ThemeCodec.Encode(new ThemeDefinition
        {
            Id = "imported",
            Name = new string('x', ThemeCodec.MaxThemeNameCharacters + 1),
            Palette = new ThemePalette()
        });

        Assert.False(ThemeCodec.TryDecode(code, out _));
    }
}

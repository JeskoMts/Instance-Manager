using System.Collections.Generic;
using InstanceManager.Models;

namespace InstanceManager.Services;

public static class BuiltInThemes
{
    public static ThemeDefinition Dark { get; } = new()
    {
        Id = "dark",
        Name = "Dark",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#0A0A0A", Surface = "#151515", SurfaceHover = "#1E1E1E", SurfaceActive = "#262626",
            Elevated = "#2D2D2D", ElevatedHover = "#383838", Border = "#2C2C2C", BorderStrong = "#3D3D3D",
            Accent = "#EDEDED", OnAccent = "#0A0A0A",
            TextPrimary = "#F2F2F2", TextSecondary = "#B4B4B4", TextMuted = "#8C8C8C",
            Success = "#5B9E7E", Danger = "#C65F55"
        }
    };

    public static ThemeDefinition Light { get; } = new()
    {
        Id = "light",
        Name = "Light",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#F4F4F5", Surface = "#FFFFFF", SurfaceHover = "#F0F0F2", SurfaceActive = "#E5E5EA",
            Elevated = "#FFFFFF", ElevatedHover = "#ECECEF", Border = "#E0E0E4", BorderStrong = "#CFCFD6",
            Accent = "#1A1A1E", OnAccent = "#FFFFFF",
            TextPrimary = "#1A1A1E", TextSecondary = "#54545C", TextMuted = "#8A8A92",
            Success = "#2E8B62", Danger = "#C0392B"
        }
    };

    public static ThemeDefinition Gray { get; } = new()
    {
        Id = "gray",
        Name = "Gray",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#151517", Surface = "#1D1D20", SurfaceHover = "#252529", SurfaceActive = "#2F2F35",
            Elevated = "#252529", ElevatedHover = "#313138", Border = "#33333B", BorderStrong = "#4A4A55",
            Accent = "#ECECF1", OnAccent = "#151517",
            TextPrimary = "#F4F4F8", TextSecondary = "#B8B8C4", TextMuted = "#82828D",
            Success = "#6FB48E", Danger = "#D07069"
        }
    };

    public static ThemeDefinition Ocean { get; } = new()
    {
        Id = "ocean",
        Name = "Ocean",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#0E1620", Surface = "#15212E", SurfaceHover = "#1B2A3A", SurfaceActive = "#223547",
            Elevated = "#1B2A3A", ElevatedHover = "#24394F", Border = "#22323F", BorderStrong = "#33495C",
            Accent = "#56B6C2", OnAccent = "#0E1620",
            TextPrimary = "#E6EEF5", TextSecondary = "#A9BBCA", TextMuted = "#6E8395",
            Success = "#4FB477", Danger = "#E06C6C"
        }
    };

    public static ThemeDefinition Forest { get; } = new()
    {
        Id = "forest",
        Name = "Forest",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#111813", Surface = "#18211A", SurfaceHover = "#1F2A21", SurfaceActive = "#27342A",
            Elevated = "#1F2A21", ElevatedHover = "#2A3A2D", Border = "#26332A", BorderStrong = "#37493B",
            Accent = "#7FB069", OnAccent = "#111813",
            TextPrimary = "#E9F0E8", TextSecondary = "#B2C2AF", TextMuted = "#7B8C79",
            Success = "#6FB48E", Danger = "#D9776C"
        }
    };

    public static ThemeDefinition Catppuccin { get; } = new()
    {
        Id = "catppuccin",
        Name = "Catppuccin",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#181825", Surface = "#1E1E2E", SurfaceHover = "#272739", SurfaceActive = "#313244",
            Elevated = "#28283C", ElevatedHover = "#363650", Border = "#313244", BorderStrong = "#45475A",
            Accent = "#CBA6F7", OnAccent = "#181825",
            TextPrimary = "#CDD6F4", TextSecondary = "#A6ADC8", TextMuted = "#6C7086",
            Success = "#A6E3A1", Danger = "#F38BA8"
        }
    };

    public static ThemeDefinition Dracula { get; } = new()
    {
        Id = "dracula",
        Name = "Dracula",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#21222C", Surface = "#282A36", SurfaceHover = "#2F3140", SurfaceActive = "#383A4A",
            Elevated = "#343746", ElevatedHover = "#404357", Border = "#383A4A", BorderStrong = "#44475A",
            Accent = "#BD93F9", OnAccent = "#21222C",
            TextPrimary = "#F8F8F2", TextSecondary = "#C7C9DB", TextMuted = "#6272A4",
            Success = "#50FA7B", Danger = "#FF5555"
        }
    };

    public static ThemeDefinition Nord { get; } = new()
    {
        Id = "nord",
        Name = "Nord",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#2E3440", Surface = "#343B49", SurfaceHover = "#3B4252", SurfaceActive = "#434C5E",
            Elevated = "#3B4252", ElevatedHover = "#4C566A", Border = "#3B4252", BorderStrong = "#4C566A",
            Accent = "#88C0D0", OnAccent = "#2E3440",
            TextPrimary = "#ECEFF4", TextSecondary = "#D8DEE9", TextMuted = "#7B879C",
            Success = "#A3BE8C", Danger = "#BF616A"
        }
    };

    public static ThemeDefinition Sand { get; } = new()
    {
        Id = "sand",
        Name = "Sand",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#F2ECE1", Surface = "#FBF7EF", SurfaceHover = "#F0E9DC", SurfaceActive = "#E6DCC9",
            Elevated = "#FBF7EF", ElevatedHover = "#EDE5D5", Border = "#E0D6C3", BorderStrong = "#CDBFA6",
            Accent = "#5A4632", OnAccent = "#FBF7EF",
            TextPrimary = "#3A322A", TextSecondary = "#6B5F4E", TextMuted = "#9A8C76",
            Success = "#3E7D54", Danger = "#B5453B"
        }
    };

    public static ThemeDefinition Rose { get; } = new()
    {
        Id = "rose",
        Name = "Rosé",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#191724", Surface = "#1F1D2E", SurfaceHover = "#26233A", SurfaceActive = "#2F2B43",
            Elevated = "#26233A", ElevatedHover = "#332F4A", Border = "#2A2740", BorderStrong = "#403B5A",
            Accent = "#EBBCBA", OnAccent = "#191724",
            TextPrimary = "#E0DEF4", TextSecondary = "#908CAA", TextMuted = "#6E6A86",
            Success = "#7FB58E", Danger = "#EB6F92"
        }
    };

    public static ThemeDefinition Slate { get; } = new()
    {
        Id = "slate",
        Name = "Slate",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#15181D", Surface = "#1C2027", SurfaceHover = "#242933", SurfaceActive = "#2D333F",
            Elevated = "#242933", ElevatedHover = "#313846", Border = "#2A303A", BorderStrong = "#3C4452",
            Accent = "#7AA2C2", OnAccent = "#15181D",
            TextPrimary = "#E4E8EE", TextSecondary = "#A7B0BE", TextMuted = "#717C8C",
            Success = "#5FAE84", Danger = "#D17A72"
        }
    };

    public static ThemeDefinition Gruvbox { get; } = new()
    {
        Id = "gruvbox",
        Name = "Gruvbox",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#1D2021", Surface = "#282828", SurfaceHover = "#32302F", SurfaceActive = "#3C3836",
            Elevated = "#32302F", ElevatedHover = "#423E3A", Border = "#3C3836", BorderStrong = "#504945",
            Accent = "#FABD2F", OnAccent = "#1D2021",
            TextPrimary = "#EBDBB2", TextSecondary = "#BDAE93", TextMuted = "#928374",
            Success = "#B8BB26", Danger = "#FB4934"
        }
    };

    public static ThemeDefinition OneDark { get; } = new()
    {
        Id = "onedark",
        Name = "One Dark",
        IsBuiltIn = true,
        Palette = new ThemePalette
        {
            Window = "#21252B", Surface = "#282C34", SurfaceHover = "#2F343D", SurfaceActive = "#3A3F4B",
            Elevated = "#2F343D", ElevatedHover = "#3A4250", Border = "#353B45", BorderStrong = "#454C59",
            Accent = "#61AFEF", OnAccent = "#21252B",
            TextPrimary = "#D7DAE0", TextSecondary = "#ABB2BF", TextMuted = "#5C6370",
            Success = "#98C379", Danger = "#E06C75"
        }
    };

    public static IReadOnlyList<ThemeDefinition> All { get; } =
        new[] { Dark, Light, Gray, Ocean, Forest, Catppuccin, Nord, Dracula, Sand, Rose, Slate, Gruvbox, OneDark };
}

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using InstanceManager.Models;
using InstanceManager.Storage;

namespace InstanceManager.Services;

public sealed class ThemeService
{
    private readonly IThemeRepository _themes;
    private readonly ISettingsService _settings;

    public ThemeService(IThemeRepository themes, ISettingsService settings)
    {
        _themes = themes;
        _settings = settings;
    }

    public IReadOnlyList<ThemeDefinition> AllThemes() =>
        BuiltInThemes.All.Concat(_themes.All).ToList();

    public ThemeDefinition? Find(string? id) =>
        AllThemes().FirstOrDefault(t => t.Id == id);

    public void ApplyFromSettings()
    {
        ThemeDefinition theme = Find(_settings.Settings.ThemeId) ?? BuiltInThemes.Dark;
        Apply(theme.Palette);
    }

    public void Apply(ThemePalette p)
    {
        SetColor("Color.Window", p.Window);
        SetColor("Color.Surface", p.Surface);
        SetColor("Color.SurfaceHover", p.SurfaceHover);
        SetColor("Color.SurfaceActive", p.SurfaceActive);
        SetColor("Color.Elevated", p.Elevated);
        SetColor("Color.ElevatedHover", p.ElevatedHover);
        SetColor("Color.Border", p.Border);
        SetColor("Color.BorderStrong", p.BorderStrong);
        SetColor("Color.Accent", p.Accent);
        SetColor("Color.OnAccent", p.OnAccent);
        SetColor("Color.TextPrimary", p.TextPrimary);
        SetColor("Color.TextSecondary", p.TextSecondary);
        SetColor("Color.TextMuted", p.TextMuted);
        SetColor("Color.Success", p.Success);
        SetColor("Color.Danger", p.Danger);

        SetBrush("Brush.Window", p.Window);
        SetBrush("Brush.Surface", p.Surface);
        SetBrush("Brush.SurfaceHover", p.SurfaceHover);
        SetBrush("Brush.SurfaceActive", p.SurfaceActive);
        SetBrush("Brush.Elevated", p.Elevated);
        SetBrush("Brush.ElevatedHover", p.ElevatedHover);
        SetBrush("Brush.Border", p.Border);
        SetBrush("Brush.BorderStrong", p.BorderStrong);
        SetBrush("Brush.Accent", p.Accent);
        SetBrush("Brush.OnAccent", p.OnAccent);

        SetBrush("Brush.ButtonPrimary", p.Elevated);
        SetBrush("Brush.ButtonPrimaryHover", p.ElevatedHover);
        SetBrush("Brush.ButtonPrimaryPressed", p.SurfaceActive);

        SetBrush("Brush.TextPrimary", p.TextPrimary);
        SetBrush("Brush.TextSecondary", p.TextSecondary);
        SetBrush("Brush.TextMuted", p.TextMuted);
        SetBrush("Brush.Success", p.Success);
        SetBrush("Brush.Danger", p.Danger);
    }

    private static void SetBrush(string key, string hex)
    {
        if (Application.Current is { } app && TryParse(hex, out Color color))
            app.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetColor(string key, string hex)
    {
        if (Application.Current != null && TryParse(hex, out Color color))
            Application.Current.Resources[key] = color;
    }

    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }
}

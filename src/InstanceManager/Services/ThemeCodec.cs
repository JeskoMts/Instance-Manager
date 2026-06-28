using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstanceManager.Models;

namespace InstanceManager.Services;

public static class ThemeCodec
{
    public const string Prefix = "INSTANCEMANAGER-THEME-v1:";
    internal const int MaxEncodedPayloadCharacters = 65_536;
    internal const int MaxCodeCharacters = MaxEncodedPayloadCharacters + 64;
    internal const int MaxDecodedPayloadBytes = 49_152;
    internal const int MaxThemeNameCharacters = 80;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Encode(ThemeDefinition theme)
    {
        string json = JsonSerializer.Serialize(theme, Options);
        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode(string? code, [NotNullWhen(true)] out ThemeDefinition? theme)
    {
        theme = null;
        if (string.IsNullOrWhiteSpace(code))
            return false;

        string trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        try
        {
            string payload = trimmed[Prefix.Length..].Trim();
            if (payload.Length == 0 || payload.Length > MaxEncodedPayloadCharacters)
                return false;

            byte[] bytes = Convert.FromBase64String(payload);
            if (bytes.Length == 0 || bytes.Length > MaxDecodedPayloadBytes)
                return false;

            string json = StrictUtf8.GetString(bytes);
            ThemeDefinition? parsed = JsonSerializer.Deserialize<ThemeDefinition>(json, Options);
            if (parsed is null || !IsValid(parsed))
                return false;

            theme = parsed;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException or DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsValid(ThemeDefinition theme)
    {
        if (theme.Palette is null)
            return false;

        string name = theme.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxThemeNameCharacters)
            return false;

        ThemePalette p = theme.Palette;
        return IsStrictHexColor(p.Window) &&
               IsStrictHexColor(p.Surface) &&
               IsStrictHexColor(p.SurfaceHover) &&
               IsStrictHexColor(p.SurfaceActive) &&
               IsStrictHexColor(p.Elevated) &&
               IsStrictHexColor(p.ElevatedHover) &&
               IsStrictHexColor(p.Border) &&
               IsStrictHexColor(p.BorderStrong) &&
               IsStrictHexColor(p.Accent) &&
               IsStrictHexColor(p.OnAccent) &&
               IsStrictHexColor(p.TextPrimary) &&
               IsStrictHexColor(p.TextSecondary) &&
               IsStrictHexColor(p.TextMuted) &&
               IsStrictHexColor(p.Success) &&
               IsStrictHexColor(p.Danger);
    }

    private static bool IsStrictHexColor(string? value)
    {
        if (value is not { Length: 7 or 9 } || value[0] != '#')
            return false;

        for (int i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
                return false;
        }

        return true;
    }
}

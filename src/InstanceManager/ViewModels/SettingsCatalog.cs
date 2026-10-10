using System;
using System.Collections.Generic;
using System.Linq;
using InstanceManager.Controls;

namespace InstanceManager.ViewModels;

public sealed record SettingsSection(
    SettingsCategory Category,
    string Group,
    string Title,
    IconKind Icon,
    string Description,
    string Keywords);

public static class SettingsCatalog
{
    public const string AppGroup = "App";
    public const string RobloxGroup = "Roblox";
    public const string SystemGroup = "System";

    public static IReadOnlyList<SettingsSection> Sections { get; } = new SettingsSection[]
    {
        new(SettingsCategory.General, AppGroup, "General", IconKind.Sliders,
            "Startup, the tray icon, avatars, and when Instance Manager asks before it acts.",
            "start windows boot tray minimize close hide confirm confirmation ask question delete remove stop skip games page switch picking avatar avatars picture profile initials account list"),
        new(SettingsCategory.Appearance, AppGroup, "Appearance", IconKind.Palette,
            "Themes and colors for the whole app.",
            "theme themes color colors colour dark light accent custom palette import export share code look"),
        new(SettingsCategory.Notifications, AppGroup, "Notifications", IconKind.Bell,
            "How long pop-ups stay on screen and which ones you see.",
            "notification notifications toast popup pop-up mute display time duration bell"),
        new(SettingsCategory.Launching, RobloxGroup, "Launching", IconKind.Play,
            "How accounts start and which Roblox version they use.",
            "launch launching start pause delay wait queue multi-instance multi instance multiple windows version versions folder path roblox client"),
        new(SettingsCategory.Graphics, RobloxGroup, "Graphics", IconKind.Monitor,
            "Frame rate and FastFlags, written into Roblox right before each launch.",
            "graphics fps frame rate cap limit fastflags fast flags quality api vulkan direct3d directx d3d11 opengl anti-aliasing antialiasing msaa texture textures grass sky lighting fullscreen alt+enter dpi scaling performance"),
        new(SettingsCategory.Reconnect, RobloxGroup, "Reconnect & AFK", IconKind.Refresh,
            "Keep accounts in their game: reconnect after they drop out, and prevent idle kicks.",
            "auto reconnect rejoin kick kicked error disconnect disconnected crash crashed retry retries limit attempts afk anti-afk idle away key space interval"),
        new(SettingsCategory.Updates, SystemGroup, "Updates", IconKind.Download,
            "When Instance Manager looks for new versions and how they install.",
            "update updates version check automatic startup interval pre-release prerelease beta skip roll back rollback previous github download"),
        new(SettingsCategory.Privacy, SystemGroup, "Data & privacy", IconKind.Shield,
            "What stays on this PC.",
            "data privacy folder appdata files storage telemetry about version")
    };

    public static SettingsSection For(SettingsCategory category) =>
        Sections.First(section => section.Category == category);

    public static bool Matches(SettingsSection section, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        string haystack = section.Title + " " + section.Description + " " + section.Keywords;
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(word => haystack.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}

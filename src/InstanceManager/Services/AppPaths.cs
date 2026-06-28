using System;
using System.IO;

namespace InstanceManager.Services;

public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Instance Manager");
    public static string LocalDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Instance Manager");

    public static string AccountsFile => Path.Combine(DataDirectory, "accounts.json");
    public static string GroupsFile => Path.Combine(DataDirectory, "groups.json");
    public static string FavoritesFile => Path.Combine(DataDirectory, "favorites.json");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string ThemesFile => Path.Combine(DataDirectory, "themes.json");

    public static string WebViewDirectory => Path.Combine(LocalDataDirectory, "webview");
    internal static string LegacyWebViewDirectory => Path.Combine(DataDirectory, "webview");

    public static string DefaultRobloxVersionsPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);

    public static void CleanupLegacyWebViewData()
    {
        try
        {
            if (!string.Equals(
                    LegacyWebViewDirectory,
                    WebViewDirectory,
                    StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(LegacyWebViewDirectory))
            {
                Directory.Delete(LegacyWebViewDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

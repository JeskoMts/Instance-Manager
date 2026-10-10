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
    public static string AutomationFile => Path.Combine(DataDirectory, "automations.json");
    public static string PreviousVersionDirectory => Path.Combine(LocalDataDirectory, "previous-version");

    public static string WebViewDirectory => Path.Combine(LocalDataDirectory, "webview");
    public static string AvatarCacheDirectory => Path.Combine(LocalDataDirectory, "avatars");
    internal static string LegacyWebViewDirectory => Path.Combine(DataDirectory, "webview");

    public static string DefaultRobloxVersionsPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);

    private static readonly string[] RemovedFeatureFiles = { "playtime.json", "diagnostics.log", "diagnostics.log.1" };

    public static void CleanupRemovedFeatureFiles() => CleanupRemovedFeatureFiles(DataDirectory);

    internal static void CleanupRemovedFeatureFiles(string directory)
    {
        foreach (string name in RemovedFeatureFiles)
        {
            try
            {
                File.Delete(Path.Combine(directory, name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

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

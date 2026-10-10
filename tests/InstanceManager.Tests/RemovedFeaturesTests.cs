using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class RemovedFeaturesTests
{
    [Fact]
    public void SettingsFromAnEarlierBuild_StillLoad_AndTheirSecretsAreGoneAfterTheNextSave()
    {
        string directory = NewDirectory();
        string path = Path.Combine(directory, "settings.json");
        try
        {
            File.WriteAllText(path, """
                {
                  "LaunchDelayMs": 4000,
                  "AntiAfkEnabled": true,
                  "AntiAfkKey": "Shift",
                  "LocalApiEnabled": true,
                  "LocalApiPort": 7963,
                  "LocalApiProtectedToken": "protected-token",
                  "LocalApiAllowCookies": true,
                  "DiscordProtectedWebhook": "protected-webhook",
                  "DiscordNotifyJoins": true,
                  "LiveOnlineStatus": true,
                  "LiveRefreshSeconds": 30,
                  "LiveRobux": true,
                  "LiveAvatars": false,
                  "LiveInventoryValue": true,
                  "LivePlaytime": true,
                  "Hotkeys": [ { "Action": 0, "Modifiers": 3, "Key": 35, "Enabled": true } ]
                }
                """);

            using var service = new SettingsService(path, TimeSpan.Zero);

            Assert.Equal(4000, service.Settings.LaunchDelayMs);
            Assert.True(service.Settings.AntiAfkEnabled);
            Assert.Equal("LeftShift", service.Settings.AntiAfkKey);
            Assert.False(service.Settings.LiveAvatars);

            service.Save();
            string saved = File.ReadAllText(path);
            Assert.Contains("4000", saved);
            Assert.Contains("\"LiveAvatars\": false", saved);
            Assert.DoesNotContain("protected-token", saved);
            Assert.DoesNotContain("protected-webhook", saved);
            Assert.DoesNotContain("Hotkeys", saved);
            Assert.DoesNotContain("LiveOnlineStatus", saved);
            Assert.DoesNotContain("LiveRefreshSeconds", saved);
            Assert.DoesNotContain("LiveRobux", saved);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AccountInfo_IsGone_ButTheAvatarKeepsItsSwitch()
    {
        Assert.Null(typeof(AccountRowViewModel).GetProperty("LiveSummary"));
        Assert.Null(typeof(AppSettings).GetProperty("LiveOnlineStatus"));
        Assert.Null(typeof(AppSettings).GetProperty("LiveRefreshSeconds"));
        Assert.Null(typeof(AppSettings).GetProperty("LiveRobux"));
        Assert.Null(typeof(AppSettings).Assembly.GetType("InstanceManager.Services.LiveDataService"));
        Assert.DoesNotContain("AccountInfo", Enum.GetNames<SettingsCategory>());
        Assert.False(File.Exists(Path.Combine(SourceRoot(), "Views", "Settings", "AccountInfoSettings.xaml")));

        Assert.True(new AppSettings().LiveAvatars);
        Assert.NotNull(typeof(AccountRowViewModel).GetProperty(nameof(AccountRowViewModel.AvatarImage)));
        Assert.NotNull(typeof(OperationsViewModel).GetProperty(nameof(OperationsViewModel.LiveAvatars)));
    }

    [Fact]
    public void KeyboardShortcuts_AreGone_FromTheWindowAndFromEveryHint()
    {
        string window = File.ReadAllText(Path.Combine(SourceRoot(), "MainWindow.xaml.cs"));
        Assert.DoesNotContain("OnPreviewKeyDown", window, StringComparison.Ordinal);
        Assert.DoesNotContain("ModifierKeys", window, StringComparison.Ordinal);

        var hints = new List<string>();
        foreach (string file in Directory.EnumerateFiles(SourceRoot(), "*.xaml", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(SourceRoot(), file);
            if (relative.StartsWith("obj", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
                continue;

            string xaml = File.ReadAllText(file);
            foreach (string hint in new[] { "Ctrl", "(Esc)", "<KeyBinding", "InputGestureText" })
            {
                if (xaml.Contains(hint, StringComparison.Ordinal))
                    hints.Add($"{relative}: {hint}");
            }
        }

        Assert.Empty(hints);
    }

    [Fact]
    public void Cleanup_DeletesThePlaytimeHistoryAndTheDiagnosticsLog_AndNothingElse()
    {
        string directory = NewDirectory();
        try
        {
            foreach (string name in new[]
                     {
                         "playtime.json", "diagnostics.log", "diagnostics.log.1",
                         "settings.json", "accounts.json", "auto-reconnect.log"
                     })
                File.WriteAllText(Path.Combine(directory, name), "x");

            AppPaths.CleanupRemovedFeatureFiles(directory);

            Assert.Equal(
                new[] { "accounts.json", "auto-reconnect.log", "settings.json" },
                Directory.GetFiles(directory).Select(f => Path.GetFileName(f)!).Order());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Cleanup_InAFolderThatDoesNotExist_DoesNothing() =>
        Assert.Null(Record.Exception(() => AppPaths.CleanupRemovedFeatureFiles(
            Path.Combine(Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N")))));

    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "InstanceManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string SourceRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = Path.Combine(directory.FullName, "src", "InstanceManager");
            if (File.Exists(Path.Combine(path, "App.xaml")))
                return path;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("src/InstanceManager");
    }
}

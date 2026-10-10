using System;
using System.IO;
using System.Linq;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class SettingsLayoutTests
{
    [Fact]
    public void Catalog_ListsEveryCategoryOnceInThreeGroups()
    {
        SettingsCategory[] listed = SettingsCatalog.Sections.Select(s => s.Category).ToArray();

        Assert.Equal(Enum.GetValues<SettingsCategory>(), listed);
        Assert.Equal(new[] { "App", "Roblox", "System" }, SettingsCatalog.Sections.Select(s => s.Group).Distinct());
        Assert.Equal(listed.Length, SettingsCatalog.Sections.Select(s => s.Icon).Distinct().Count());
        Assert.All(SettingsCatalog.Sections, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
    }

    [Theory]
    [InlineData("fps", SettingsCategory.Graphics)]
    [InlineData("TRAY", SettingsCategory.General)]
    [InlineData("toast", SettingsCategory.Notifications)]
    [InlineData("anti afk", SettingsCategory.Reconnect)]
    [InlineData("avatar", SettingsCategory.General)]
    [InlineData("roll back", SettingsCategory.Updates)]
    [InlineData("telemetry", SettingsCategory.Privacy)]
    public void Search_FindsTheCategoryThatHoldsASetting(string query, SettingsCategory expected)
    {
        SettingsCategory[] hits = SettingsCatalog.Sections
            .Where(s => SettingsCatalog.Matches(s, query))
            .Select(s => s.Category)
            .ToArray();

        Assert.Equal(new[] { expected }, hits);
    }

    [Fact]
    public void Search_WithoutWords_ShowsEverything()
    {
        Assert.All(SettingsCatalog.Sections, s => Assert.True(SettingsCatalog.Matches(s, "  ")));
        Assert.DoesNotContain(SettingsCatalog.Sections, s => SettingsCatalog.Matches(s, "fps tray"));
    }

    [Fact]
    public void SettingsPage_OnlyHostsNavigationAndBuildsCategoriesOnDemand()
    {
        string page = Pages("SettingsPage.xaml");

        Assert.Contains("<settings:SettingsCategoryHost x:Name=\"CategoryHost\" Category=\"{Binding SelectedSettingsCategory}\" />", page, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SearchBox\"", page, StringComparison.Ordinal);
        Assert.Contains("ItemContainerStyle=\"{StaticResource SubNavItem}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleSwitch", page, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(PagePath("SettingsPage.xaml"))!, "OperationsSettingsPage.xaml")));
    }

    [Fact]
    public void Categories_KeepRelatedSettingsTogether()
    {
        string notifications = Settings("NotificationSettings.xaml");
        Assert.Contains("Settings.NotificationMutes", notifications, StringComparison.Ordinal);

        string reconnect = Settings("ReconnectSettings.xaml");
        Assert.Contains("Settings.AutoReconnectMaster", reconnect, StringComparison.Ordinal);
        Assert.Contains("Operations.AntiAfkEnabled", reconnect, StringComparison.Ordinal);

        string general = Settings("GeneralSettings.xaml");
        Assert.Contains("Settings.SwitchToAccountsOnGameSelect", general, StringComparison.Ordinal);
        Assert.Contains("Settings.CloseToTray", general, StringComparison.Ordinal);
        Assert.Contains("Operations.LiveAvatars", general, StringComparison.Ordinal);

        string privacy = Settings("PrivacySettings.xaml");
        Assert.Contains("Settings.OpenDataFolderCommand", privacy, StringComparison.Ordinal);

        string updates = Settings("UpdateSettings.xaml");
        Assert.Contains("Updates.UpdateAutomatically", updates, StringComparison.Ordinal);
        Assert.Contains("Updates.CheckNowCommand", updates, StringComparison.Ordinal);
        Assert.Contains("Updates.RollBackCommand", updates, StringComparison.Ordinal);
        Assert.DoesNotContain("Skip", updates, StringComparison.Ordinal);
    }

    [Fact]
    public void CachedCategories_GetTheirDataContextDirectly()
    {
        string host = File.ReadAllText(Path.Combine(SourceRoot(), "Views", "Settings", "SettingsCategoryHost.cs"));

        Assert.Contains("view.DataContext = DataContext;", host, StringComparison.Ordinal);
    }

    private static string Pages(string name) => File.ReadAllText(PagePath(name));

    private static string PagePath(string name) => Path.Combine(SourceRoot(), "Views", "Pages", name);

    private static string Settings(string name) => File.ReadAllText(Path.Combine(SourceRoot(), "Views", "Settings", name));

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

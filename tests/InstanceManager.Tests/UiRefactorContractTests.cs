using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InstanceManager.Tests;

public sealed class UiRefactorContractTests
{
    [Fact]
    public void SearchBox_IsStableAndLeftAlignedAfterIcon()
    {
        string main = Read("src", "InstanceManager", "Views", "Pages", "AccountsPage.xaml");
        string controls = Read("src", "InstanceManager", "Themes", "Controls.xaml");

        Assert.Contains("x:Name=\"SearchBox\"", main, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Left\"", controls, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Left\"", controls, StringComparison.Ordinal);
    }

    [Fact]
    public void LaunchPanel_UsesShortModeLabelsAndSingleServerLinkField()
    {
        string main = Read("src", "InstanceManager", "Views", "Pages", "AccountsPage.xaml");

        Assert.Contains("Content=\"Game\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Server\"", main, StringComparison.Ordinal);
        Assert.Contains("Server link, private server link or Job ID link", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Job ID (GUID)", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_ExposesFavoriteStarGroupsToastsAndLaunchDelay()
    {
        string main = Read("src", "InstanceManager", "MainWindow.xaml");
        string accounts = Read("src", "InstanceManager", "Views", "Pages", "AccountsPage.xaml");
        string settings = Read("src", "InstanceManager", "Views", "Settings", "LaunchSettings.xaml");

        Assert.Contains("TogglePrimaryCommand", accounts, StringComparison.Ordinal);
        Assert.Contains("LaunchGroupCommand", accounts, StringComparison.Ordinal);
        Assert.Contains("Notifications.Items", main, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"30000\"", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Controls_DefinesModernMenuTemplates_AndNoLongerTheOldColorChoices()
    {
        string controls = Read("src", "InstanceManager", "Themes", "Controls.xaml");

        Assert.DoesNotContain("x:Key=\"ColorChoice\"", controls, StringComparison.Ordinal);
        Assert.Contains("<ControlTemplate TargetType=\"MenuItem\"", controls, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }
        throw new FileNotFoundException(Path.Combine(parts));
    }
}

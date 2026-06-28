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
        string main = Read("src", "InstanceManager", "MainWindow.xaml");
        string controls = Read("src", "InstanceManager", "Themes", "Controls.xaml");

        Assert.Contains("x:Name=\"SearchBox\"", main, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Left\"", controls, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Left\"", controls, StringComparison.Ordinal);
    }

    [Fact]
    public void LaunchPanel_UsesNewLabelsAndSingleServerLinkField()
    {
        string main = Read("src", "InstanceManager", "MainWindow.xaml");

        Assert.Contains("Content=\"Game ID (public server)\"", main, StringComparison.Ordinal);
        Assert.Contains("Content=\"Job ID (specific server)\"", main, StringComparison.Ordinal);
        Assert.Contains("Enter a Job ID", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Job ID (GUID)", main, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ExposesFavoriteStarGroupsToastsAndLaunchDelay()
    {
        string main = Read("src", "InstanceManager", "MainWindow.xaml");

        Assert.Contains("TogglePrimaryCommand", main, StringComparison.Ordinal);
        Assert.Contains("LaunchGroupCommand", main, StringComparison.Ordinal);
        Assert.Contains("Notifications.Items", main, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"30000\"", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Controls_DefinesColorChoiceAndModernMenuTemplates()
    {
        string controls = Read("src", "InstanceManager", "Themes", "Controls.xaml");

        Assert.Contains("x:Key=\"ColorChoice\"", controls, StringComparison.Ordinal);
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

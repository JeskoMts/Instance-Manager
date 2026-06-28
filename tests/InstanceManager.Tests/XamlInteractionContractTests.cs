using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace InstanceManager.Tests;

public sealed class XamlInteractionContractTests
{
    [Fact]
    public void LaunchTargetInputs_DebounceSourceUpdates()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Contains(
            "Text=\"{Binding LaunchPanel.TargetInput, UpdateSourceTrigger=PropertyChanged, Delay=300}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Text=\"{Binding LaunchPanel.JobIdInput, UpdateSourceTrigger=PropertyChanged, Delay=300}\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ReleasesSharedSearchFocusWhenClickingOutside()
    {
        string code = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml.cs"));

        Assert.Contains("protected override void OnPreviewMouseDown", code, StringComparison.Ordinal);
        Assert.Contains("Keyboard.FocusedElement is TextBox focusedSearch", code, StringComparison.Ordinal);
        Assert.Contains("TryFindResource(\"SearchTextBox\")", code, StringComparison.Ordinal);
        Assert.Contains("!IsDescendantOrSelf(originalSource, focusedSearch)", code, StringComparison.Ordinal);
        Assert.Contains("Keyboard.ClearFocus()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ModernSlider_UsesFullSizeThumbHitTarget()
    {
        XDocument document = XDocument.Load(FindWorkspaceFile("src", "InstanceManager", "Themes", "Controls.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement sliderStyle = Assert.Single(document.Descendants(presentation + "Style"),
            element => (string?)element.Attribute(xaml + "Key") == "ModernSlider");
        XElement thumb = Assert.Single(sliderStyle.Descendants(presentation + "Thumb"));

        Assert.Equal("28", (string?)thumb.Attribute("Width"));
        Assert.Equal("28", (string?)thumb.Attribute("Height"));
    }

    [Fact]
    public void TabIndicator_IsCenteredUnderTabLabels()
    {
        XDocument document = XDocument.Load(FindWorkspaceFile("src", "InstanceManager", "Themes", "Controls.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement tabStyle = Assert.Single(document.Descendants(presentation + "Style"),
            element => (string?)element.Attribute(xaml + "Key") == "TabButton");
        XElement indicator = Assert.Single(tabStyle.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "Indicator");

        Assert.Equal("56", (string?)indicator.Attribute("Width"));
        Assert.Equal("Center", (string?)indicator.Attribute("HorizontalAlignment"));
        Assert.Equal("24,0,0,0", (string?)indicator.Attribute("Margin"));
    }

    [Fact]
    public void SettingsSliders_DoNotChangeFromMouseWheel_AndDelayUsesHalfSeconds()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.DoesNotContain(
            "PreviewMouseWheel=\"DelaySlider_PreviewMouseWheel\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "SmallChange=\"500\" LargeChange=\"500\" TickFrequency=\"500\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Master switch. When on, all notifications appear only in the bell tray. When off, the individual choices below still apply.",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Master switch. When on, all confirmations are skipped. When off, the individual choices below still apply.",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FavoriteRows_ExposeDragReorderAndKeepArrowCommands()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Contains("FavoriteRow_PreviewMouseLeftButtonDown", xaml, StringComparison.Ordinal);
        Assert.Contains("FavoriteRow_PreviewMouseMove", xaml, StringComparison.Ordinal);
        Assert.Contains("FavoriteRow_DragOver", xaml, StringComparison.Ordinal);
        Assert.Contains("FavoriteRow_Drop", xaml, StringComparison.Ordinal);
        Assert.Contains("LaunchPanel.MoveFavoriteUpCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("LaunchPanel.MoveFavoriteDownCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_ShowCircularAvatarImageOverInitials()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Contains("Source=\"{Binding AvatarImage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<EllipseGeometry Center=\"16,16\" RadiusX=\"16\" RadiusY=\"16\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Initials}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_DoNotExposeAutoReconnectControl()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.DoesNotContain("IsChecked=\"{Binding AutoReconnectEnabled, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto Reconnect - reconnect this account", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Style=\"{StaticResource RejoinToggle}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsAutoReconnect_MergesKickAndErrorIntoOneToggle_AndNamesCrashPerInstance()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Contains("Reconnect after Kick/Error", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding Settings.AutoReconnectOnKickError}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Reconnect after Instance Crash", xaml, StringComparison.Ordinal);

        Assert.DoesNotContain("Rejoin after disconnect/menu drop", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Rejoin after kick/removal", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Rejoin after a game crash", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto Rejoin", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("{Binding Settings.AutoReconnectOnError}", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("{Binding Settings.AutoReconnectOnKick}", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ButtonStyles_HonorVisibleBorderContracts()
    {
        string controls = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "Themes", "Controls.xaml"));
        string main = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));
        string addAccount = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "Views", "AddAccountWindow.xaml"));

        foreach (string styleName in new[] { "IconButton", "InlineMenuItem", "SegmentRadio" })
        {
            string style = Slice(controls, $"x:Key=\"{styleName}\"", "</Style>");
            Assert.Contains("BorderBrush=", style, StringComparison.Ordinal);
            Assert.Contains("BorderThickness=", style, StringComparison.Ordinal);
        }

        string tabButton = Slice(controls, "x:Key=\"TabButton\"", "</Style>");
        Assert.DoesNotContain("BorderBrush=", tabButton, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness=", tabButton, StringComparison.Ordinal);

        string captionButton = Slice(main, "x:Key=\"CaptionButton\"", "</Style>");
        Assert.DoesNotContain("BorderBrush=", captionButton, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness=", captionButton, StringComparison.Ordinal);

        foreach (string styleName in new[] { "UndoButton" })
        {
            string style = Slice(main, $"x:Key=\"{styleName}\"", "</Style>");
            Assert.Contains("BorderBrush=", style, StringComparison.Ordinal);
            Assert.Contains("BorderThickness=", style, StringComparison.Ordinal);
        }

        Assert.Contains("Style=\"{StaticResource CaptionButton}\"", addAccount, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness=\"0\"", addAccount, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineActionMenus_CloseOnOutsideClick_AndKeepRenameDeleteUnseparated()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));
        string code = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml.cs"));

        string groupMenu = Slice(xaml, "Command=\"{Binding RenameGroupCommand}\"", "Command=\"{Binding DeleteGroupCommand}\"");
        Assert.DoesNotContain("Height=\"1\"", groupMenu, StringComparison.Ordinal);

        string accountRemoveLeadIn = Slice(xaml, "Command=\"{Binding RenameCommand}\"", "Command=\"{Binding RemoveCommand}\"");
        Assert.DoesNotContain("Height=\"1\" Background=\"{DynamicResource Brush.Border}\"", accountRemoveLeadIn, StringComparison.Ordinal);

        Assert.Contains("Tag=\"InlineActionMenu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"InlineMenuToggle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CloseOpenMenu()", code, StringComparison.Ordinal);
        Assert.Contains("IsInsideInlineActionMenu", code, StringComparison.Ordinal);
    }

    [Fact]
    public void LaunchDock_UsesSpacedModeSegmentsAndPlainLaunchSelectedText()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        string jobSegment = Slice(xaml, "Content=\"Job ID (specific server)\"", "/>");
        Assert.Contains("Margin=\"4,0,0,0\"", jobSegment, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"Launch selected\" VerticalAlignment=\"Center\" />", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Run Text=\"Launch selected (\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemePicker_UsesOneListBoxAndKeepsMoreThemesToggle()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Equal(1, Count(xaml, "ItemsSource=\"{Binding Theme.Themes}\""));
        Assert.Equal(1, Count(xaml, "SelectedItem=\"{Binding Theme.SelectedTheme, Mode=TwoWay}\""));
        Assert.Contains("AlternationCount=\"2147483647\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"More themes\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding Theme.ShowAllThemes, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Theme.PrimaryThemes", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Theme.MoreThemes", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_MatchesAccountWidthAndScrollBehavior()
    {
        XDocument document = XDocument.Load(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement settingsPage = Assert.Single(document.Descendants(presentation + "Grid"),
            element => (string?)element.Attribute(xaml + "Name") == "SettingsPage");
        XElement scroller = Assert.Single(settingsPage.Elements(presentation + "ScrollViewer"));
        XElement content = Assert.Single(scroller.Elements(presentation + "StackPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "SettingsContent");
        XElement themeGrid = Assert.Single(settingsPage.Descendants(presentation + "UniformGrid"));
        XElement actions = Assert.Single(settingsPage.Descendants(presentation + "WrapPanel"),
            element => (string?)element.Attribute(xaml + "Name") == "ThemeActions");

        Assert.Equal("Disabled", (string?)scroller.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("Stretch", (string?)scroller.Attribute("HorizontalContentAlignment"));
        Assert.Equal("16,12,16,16", (string?)content.Attribute("Margin"));
        Assert.Null(content.Attribute("MaxWidth"));
        Assert.Equal(
            "{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}, Converter={StaticResource ThemeGridColumns}}",
            (string?)themeGrid.Attribute("Columns"));
        Assert.Equal("0,2,0,0", (string?)actions.Attribute("Margin"));
    }

    [Fact]
    public void ThemeDrag_SwapsOnDropNotDuringDragOver()
    {
        string code = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml.cs"));
        string dragOver = Slice(code, "private void ThemeCard_DragOver", "private void ThemeCard_DragLeave");
        string drop = Slice(code, "private void ThemeCard_Drop", "private static void SetThemeDropIndicator");

        Assert.Contains("ThemeSwap.SwapPair", drop, StringComparison.Ordinal);
        Assert.Contains("vm.Theme.ApplyOrder", drop, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.Theme.MoveTheme", drop, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemeSwap.SwapPair", dragOver, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.Theme.ApplyOrder", dragOver, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeCard_PlaysActivationPulseOnEverySelection()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"ActivatePulse\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Storyboard.TargetName=\"ActivatePulse\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Storyboard.TargetProperty=\"Opacity\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeDrag_ListAcceptsThemePayloadAcrossCardsAndGridGaps()
    {
        XDocument document = XDocument.Load(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement themesList = Assert.Single(document.Descendants(presentation + "ListBox"),
            element => (string?)element.Attribute(xaml + "Name") == "ThemesList");

        Assert.Equal("True", (string?)themesList.Attribute("AllowDrop"));
        Assert.Equal("ThemesList_DragOver", (string?)themesList.Attribute("DragOver"));
        Assert.Equal("ThemesList_Drop", (string?)themesList.Attribute("Drop"));
    }

    [Fact]
    public void ThemeDrag_DimsDraggedCardForTheLifetimeOfTheDrag()
    {
        string code = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml.cs"));
        string dragStart = Slice(code, "private void ThemeCard_PreviewMouseMove", "private Point SubtractThemeGrab");

        Assert.Contains("source.Opacity = 0.4", dragStart, StringComparison.Ordinal);
        Assert.Contains("source.Opacity = 1.0", dragStart, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountSelectionActions_LiveInLaunchDockWithoutSelectionBar()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.DoesNotContain("AccountList.HasSelection", xaml, StringComparison.Ordinal);
        Assert.Contains("AccountList.SelectAllVisibleCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("AccountList.ClearSelectionCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("LaunchSelectedCommand", xaml, StringComparison.Ordinal);
    }

    private static int Count(string value, string fragment)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static string Slice(string value, string startMarker, string endMarker)
    {
        int start = value.IndexOf(startMarker, StringComparison.Ordinal);
        int end = value.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not find source slice {startMarker}..{endMarker}");
        return value[start..end];
    }

    private static string FindWorkspaceFile(params string[] relativeParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate workspace file: {Path.Combine(relativeParts)}");
    }
}

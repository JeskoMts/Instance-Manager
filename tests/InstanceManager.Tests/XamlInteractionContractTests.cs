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
    public void MainWindow_ReleasesAnyTextFieldFocusWhenClickingOutside()
    {
        string code = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml.cs"));
        string mouseDown = Slice(code, "protected override void OnPreviewMouseDown", "base.OnPreviewMouseDown(e);");
        string release = Slice(code, "private void ReleaseTextFocus()", "}");

        Assert.Contains("Keyboard.FocusedElement is TextBoxBase editing", mouseDown, StringComparison.Ordinal);
        Assert.Contains("!IsDescendantOrSelf(originalSource, editing)", mouseDown, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchTextBox", mouseDown, StringComparison.Ordinal);
        Assert.Contains("FocusManager.SetFocusedElement(this, null)", release, StringComparison.Ordinal);
        Assert.Contains("Keyboard.ClearFocus()", release, StringComparison.Ordinal);
    }

    [Fact]
    public void ModernSlider_FollowsPressAndHoldAnywhereOnTheTrack()
    {
        string controls = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "Themes", "Controls.xaml"));
        string slider = Slice(controls, "x:Key=\"ModernSlider\"", "<Setter Property=\"Template\">");

        Assert.Contains("<Setter Property=\"behaviors:SliderPressDrag.Enabled\" Value=\"True\" />", slider, StringComparison.Ordinal);
        Assert.DoesNotContain("IsMoveToPointEnabled", slider, StringComparison.Ordinal);
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
    public void TabButton_ActiveStateIsAnAnimatedPillWithoutBorders()
    {
        XDocument document = XDocument.Load(FindWorkspaceFile("src", "InstanceManager", "Themes", "Controls.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        XElement tabStyle = Assert.Single(document.Descendants(presentation + "Style"),
            element => (string?)element.Attribute(xaml + "Key") == "TabButton");
        XElement pill = Assert.Single(tabStyle.Descendants(presentation + "Border"),
            element => (string?)element.Attribute(xaml + "Name") == "Pill");

        Assert.Equal("0", (string?)pill.Attribute("Opacity"));
        Assert.Contains(tabStyle.Descendants(presentation + "DoubleAnimation"),
            animation => (string?)animation.Attribute("Storyboard.TargetName") == "Pill");
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
            "When on, notifications go straight to the bell and never pop up. When off, the choices below decide.",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "When on, the app never asks before removing, deleting or stopping something. When off, the choices below decide.",
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

        string ghost = Slice(controls, "x:Key=\"GhostButton\"", "</Style>");
        Assert.Contains("BorderBrush=", ghost, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=", ghost, StringComparison.Ordinal);

        foreach (string styleName in new[] { "IconButton", "TabButton" })
        {
            string style = Slice(controls, $"x:Key=\"{styleName}\"", "</Style>");
            Assert.DoesNotContain("BorderThickness=", style, StringComparison.Ordinal);
        }

        string captionButton = Slice(main, "x:Key=\"CaptionButton\"", "</Style>");
        Assert.DoesNotContain("BorderBrush=", captionButton, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness=", captionButton, StringComparison.Ordinal);

        string undo = Slice(main, "x:Key=\"UndoButton\"", "</Style>");
        Assert.Contains("BorderBrush=", undo, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=", undo, StringComparison.Ordinal);

        Assert.Contains("Style=\"{StaticResource CaptionButton}\"", addAccount, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness=\"0\"", addAccount, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_ShowOneActionPlusOverflowMenu_WithoutPerRowCombos()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));
        string code = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml.cs"));
        string row = Slice(xaml, "<DataTemplate DataType=\"{x:Type vm:AccountRowViewModel}\">", "</DataTemplate>");

        Assert.DoesNotContain("<ComboBox", row, StringComparison.Ordinal);
        Assert.DoesNotContain("InlineActionMenu", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LaunchCommand}\"", row, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding StopCommand}\"", row, StringComparison.Ordinal);
        Assert.Contains("Click=\"AccountMenu_Click\"", row, StringComparison.Ordinal);
        Assert.Contains("MouseRightButtonUp=\"AccountRow_RightClick\"", row, StringComparison.Ordinal);

        string menu = Slice(code, "private void OpenAccountMenu", "private void OpenGroupMenu");
        Assert.Contains("\"Roblox version\"", menu, StringComparison.Ordinal);
        Assert.Contains("\"Groups\"", menu, StringComparison.Ordinal);
        Assert.Contains("row.RemoveCommand", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_HaveNoLoadAnimation_SoScrollingStaysCheap()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));
        string list = Slice(xaml, "x:Name=\"AccountsList\"", "</ListBox>");

        Assert.DoesNotContain("RoutedEvent=\"Loaded\"", list, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", list, StringComparison.Ordinal);
    }

    [Fact]
    public void LaunchBar_IsOneRowWithModeSwitchProgressAndCancel()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Contains("Content=\"Game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Server\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding AccountList.LaunchButtonText}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding LaunchProgress}", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelLaunchCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StopInstanceComboBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop an instance", xaml, StringComparison.Ordinal);
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
        Assert.Equal("760", (string?)content.Attribute("MaxWidth"));
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
    public void AccountSelection_HeaderOffersSelectAllClearAndLaunch()
    {
        string xaml = File.ReadAllText(FindWorkspaceFile("src", "InstanceManager", "MainWindow.xaml"));

        Assert.Contains("AccountList.ToggleSelectAllCommand", xaml, StringComparison.Ordinal);
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

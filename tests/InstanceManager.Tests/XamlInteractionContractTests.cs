using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace InstanceManager.Tests;

public sealed class XamlInteractionContractTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void LaunchTargetInputs_DebounceSourceUpdates()
    {
        string xaml = Page("AccountsPage.xaml");

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
        string code = Source("MainWindow.xaml.cs");
        string mouseDown = Slice(code, "protected override void OnPreviewMouseDown", "base.OnPreviewMouseDown(e);");
        string release = Slice(code, "private void ReleaseTextFocus()", "}");

        Assert.Contains("Keyboard.FocusedElement is TextBoxBase editing", mouseDown, StringComparison.Ordinal);
        Assert.Contains("!TreeWalk.IsDescendantOrSelf(originalSource, editing)", mouseDown, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchTextBox", mouseDown, StringComparison.Ordinal);
        Assert.Contains("FocusManager.SetFocusedElement(this, null)", release, StringComparison.Ordinal);
        Assert.Contains("Keyboard.ClearFocus()", release, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_HasAnimatedSidebarWithoutBottomStatusBar()
    {
        string main = Source("MainWindow.xaml");

        foreach (string section in new[] { "Accounts", "Games", "Automation", "Settings" })
            Assert.Contains($"ConverterParameter={section}}}", main, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource NavButton}\"", main, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ToggleSidebarCommand}\"", main, StringComparison.Ordinal);
        Assert.Contains("Storyboard.TargetProperty=\"Width\" To=\"0\"", main, StringComparison.Ordinal);
        Assert.Contains("Storyboard.TargetProperty=\"Width\" To=\"224\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding ClientsRunningText}\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding StatusText}\"", main, StringComparison.Ordinal);
    }

    [Fact]
    public void ModernSlider_FollowsPressAndHoldAnywhereOnTheTrack()
    {
        string controls = Source("Themes", "Controls.xaml");
        string slider = Slice(controls, "x:Key=\"ModernSlider\"", "<Setter Property=\"Template\">");

        Assert.Contains("<Setter Property=\"behaviors:SliderPressDrag.Enabled\" Value=\"True\" />", slider, StringComparison.Ordinal);
        Assert.DoesNotContain("IsMoveToPointEnabled", slider, StringComparison.Ordinal);
    }

    [Fact]
    public void ModernSlider_UsesFullSizeThumbHitTarget()
    {
        XDocument document = XDocument.Load(Path("Themes", "Controls.xaml"));
        XElement sliderStyle = Assert.Single(document.Descendants(Presentation + "Style"),
            element => (string?)element.Attribute(Xaml + "Key") == "ModernSlider");
        XElement thumb = Assert.Single(sliderStyle.Descendants(Presentation + "Thumb"));

        Assert.Equal("28", (string?)thumb.Attribute("Width"));
        Assert.Equal("28", (string?)thumb.Attribute("Height"));
    }

    [Fact]
    public void NavButton_SelectedStateIsAPillWithAMarker()
    {
        XDocument document = XDocument.Load(Path("Themes", "Controls.xaml"));
        XElement navStyle = Assert.Single(document.Descendants(Presentation + "Style"),
            element => (string?)element.Attribute(Xaml + "Key") == "NavButton");

        Assert.Contains(navStyle.Descendants(Presentation + "Border"), e => (string?)e.Attribute(Xaml + "Name") == "Pill");
        Assert.Contains(navStyle.Descendants(Presentation + "Border"), e => (string?)e.Attribute(Xaml + "Name") == "Marker");
    }

    [Fact]
    public void SettingsSliders_UseHalfSecondSteps_AndMastersExplainTheLists()
    {
        string launching = SettingsView("LaunchSettings.xaml");
        string all = AllSettingsViews();

        Assert.DoesNotContain("PreviewMouseWheel=", all, StringComparison.Ordinal);
        Assert.Contains("SmallChange=\"500\" LargeChange=\"500\" TickFrequency=\"500\"", launching, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"30000\"", launching, StringComparison.Ordinal);
        Assert.Contains("When off, the list below decides.", SettingsView("GeneralSettings.xaml"), StringComparison.Ordinal);
        Assert.Contains("When off, the list below decides.", SettingsView("NotificationSettings.xaml"), StringComparison.Ordinal);
    }

    [Fact]
    public void FavoriteRows_ExposeDragReorderAndKeepArrowCommands()
    {
        string xaml = Page("AccountsPage.xaml");

        Assert.Contains("FavoriteRow_PreviewMouseLeftButtonDown", xaml, StringComparison.Ordinal);
        Assert.Contains("FavoriteRow_PreviewMouseMove", xaml, StringComparison.Ordinal);
        Assert.Contains("FavoriteRow_DragOver", xaml, StringComparison.Ordinal);
        Assert.Contains("FavoriteRow_Drop", xaml, StringComparison.Ordinal);
        Assert.Contains("LaunchPanel.MoveFavoriteUpCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("LaunchPanel.MoveFavoriteDownCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("TogglePrimaryCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_ShowCircularAvatar_WithoutAnAccountInfoLine()
    {
        string xaml = Page("AccountsPage.xaml");
        string row = Slice(xaml, "<DataTemplate DataType=\"{x:Type vm:AccountRowViewModel}\">", "</DataTemplate>");

        Assert.Contains("<ImageBrush ImageSource=\"{Binding AvatarImage}\" Stretch=\"UniformToFill\" />", row, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Initials}\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("LiveSummary", row, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"Row\" Height=\"54\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("AccountList.IsCompact", row, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_DoNotExposeAutoReconnectControl()
    {
        string xaml = Page("AccountsPage.xaml");

        Assert.DoesNotContain("AutoReconnectEnabled", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RejoinToggle", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsAutoReconnect_MergesKickAndErrorIntoOneToggle_AndNamesCrashPerInstance()
    {
        string xaml = SettingsView("ReconnectSettings.xaml");

        Assert.Contains("Reconnect after Kick/Error", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding Settings.AutoReconnectOnKickError}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Reconnect after Instance Crash", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Auto Rejoin", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("{Binding Settings.AutoReconnectOnError}", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ButtonStyles_HonorVisibleBorderContracts()
    {
        string controls = Source("Themes", "Controls.xaml");
        string main = Source("MainWindow.xaml");
        string addAccount = Source("Views", "AddAccountWindow.xaml");

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

        string action = Slice(controls, "x:Key=\"ActionButton\"", "</Style>");
        Assert.Contains("BorderBrush=", action, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=", action, StringComparison.Ordinal);

        Assert.Contains("Style=\"{StaticResource CaptionButton}\"", addAccount, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness=\"0\"", addAccount, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAccountWindow_HasNoBottomBar_AndCapturesTheLoginOnItsOwn()
    {
        string xaml = Source("Views", "AddAccountWindow.xaml");
        string code = Source("Views", "AddAccountWindow.xaml.cs");

        Assert.DoesNotContain("Use this account", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Capture_Click", xaml + code, StringComparison.Ordinal);
        Assert.Contains("_poll.Start()", code, StringComparison.Ordinal);
        Assert.Contains("_poll.Stop()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupEditor_UsesTheHexColorPicker()
    {
        string xaml = Source("Views", "GroupEditorDialog.xaml");

        Assert.Contains("<ui:ColorPicker", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorChoice", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_ShowOneActionPlusOverflowMenu_WithoutPerRowCombos()
    {
        string xaml = Page("AccountsPage.xaml");
        string code = Page("AccountsPage.xaml.cs");
        string row = Slice(xaml, "<DataTemplate DataType=\"{x:Type vm:AccountRowViewModel}\">", "</DataTemplate>");

        Assert.DoesNotContain("<ComboBox", row, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LaunchCommand}\"", row, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding StopCommand}\"", row, StringComparison.Ordinal);
        Assert.Contains("Click=\"AccountMenu_Click\"", row, StringComparison.Ordinal);
        Assert.Contains("MouseRightButtonUp=\"AccountRow_RightClick\"", row, StringComparison.Ordinal);

        string menu = Slice(code, "private void OpenAccountMenu", "private void OpenGroupMenu");
        Assert.Contains("\"Roblox version\"", menu, StringComparison.Ordinal);
        Assert.Contains("\"Groups\"", menu, StringComparison.Ordinal);
        Assert.Contains("\"Join another server\"", menu, StringComparison.Ordinal);
        Assert.Contains("row.RemoveCommand", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRows_HaveNoLoadAnimation_SoScrollingStaysCheap()
    {
        string list = Slice(Page("AccountsPage.xaml"), "x:Name=\"AccountsList\"", "</ListBox>");

        Assert.DoesNotContain("RoutedEvent=\"Loaded\"", list, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", list, StringComparison.Ordinal);
    }

    [Fact]
    public void LaunchBar_IsOneRowWithModeSwitchProgressAndCancel()
    {
        string xaml = Page("AccountsPage.xaml");

        Assert.Contains("Content=\"Game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Server\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding AccountList.LaunchButtonText}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding LaunchProgress}", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelLaunchCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemePicker_UsesOneListBoxAndKeepsMoreThemesToggle()
    {
        string xaml = SettingsView("AppearanceSettings.xaml");

        Assert.Equal(1, Count(xaml, "ItemsSource=\"{Binding Theme.Themes}\""));
        Assert.Equal(1, Count(xaml, "SelectedItem=\"{Binding Theme.SelectedTheme, Mode=TwoWay}\""));
        Assert.Contains("AlternationCount=\"2147483647\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"More themes\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding Theme.ShowAllThemes, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomColors_ListEveryTokenWithSwatchPickerAndHexField()
    {
        string xaml = SettingsView("AppearanceSettings.xaml");

        Assert.Contains("ItemsSource=\"{Binding Theme.ColorTokens}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ui:ColorPicker Hex=\"{Binding Hex, Mode=TwoWay}\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding Theme.CustomizeColorsCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_ScrollsVerticallyWithACappedContentWidth()
    {
        XDocument document = XDocument.Load(Path("Views", "Pages", "SettingsPage.xaml"));
        XDocument appearance = XDocument.Load(Path("Views", "Settings", "AppearanceSettings.xaml"));
        XElement scroller = Assert.Single(document.Descendants(Presentation + "ScrollViewer"),
            element => (string?)element.Attribute(Xaml + "Name") == "SettingsScroller");
        XElement column = scroller.Descendants(Presentation + "ColumnDefinition").First();
        XElement themeGrid = Assert.Single(appearance.Descendants(Presentation + "UniformGrid"),
            element => ((string?)element.Attribute("Columns"))?.Contains("ThemeGridColumns") == true);
        XElement actions = Assert.Single(appearance.Descendants(Presentation + "WrapPanel"),
            element => (string?)element.Attribute(Xaml + "Name") == "ThemeActions");

        Assert.Equal("Disabled", (string?)scroller.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("760", (string?)column.Attribute("MaxWidth"));
        Assert.Equal(
            "{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ListBox}, Converter={StaticResource ThemeGridColumns}}",
            (string?)themeGrid.Attribute("Columns"));
        Assert.Equal("0,2,0,0", (string?)actions.Attribute("Margin"));
    }

    [Fact]
    public void ThemeDrag_SwapsOnDropNotDuringDragOver()
    {
        string code = SettingsView("AppearanceSettings.xaml.cs");
        string dragOver = Slice(code, "private void ThemeCard_DragOver", "private void ThemeCard_DragLeave");
        string drop = Slice(code, "private void ThemeCard_Drop", "private static void SetThemeDropIndicator");

        Assert.Contains("ThemeSwap.SwapPair", drop, StringComparison.Ordinal);
        Assert.Contains("vm.Theme.ApplyOrder", drop, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemeSwap.SwapPair", dragOver, StringComparison.Ordinal);
        Assert.DoesNotContain("vm.Theme.ApplyOrder", dragOver, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeCard_PlaysActivationPulseOnEverySelection()
    {
        string xaml = SettingsView("AppearanceSettings.xaml");

        Assert.Contains("x:Name=\"ActivatePulse\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Storyboard.TargetName=\"ActivatePulse\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeDrag_ListAcceptsThemePayloadAcrossCardsAndGridGaps()
    {
        XDocument document = XDocument.Load(Path("Views", "Settings", "AppearanceSettings.xaml"));
        XElement themesList = Assert.Single(document.Descendants(Presentation + "ListBox"),
            element => (string?)element.Attribute(Xaml + "Name") == "ThemesList");

        Assert.Equal("True", (string?)themesList.Attribute("AllowDrop"));
        Assert.Equal("ThemesList_DragOver", (string?)themesList.Attribute("DragOver"));
        Assert.Equal("ThemesList_Drop", (string?)themesList.Attribute("Drop"));
    }

    [Fact]
    public void ThemeDrag_DimsDraggedCardForTheLifetimeOfTheDrag()
    {
        string dragStart = Slice(SettingsView("AppearanceSettings.xaml.cs"), "private void ThemeCard_PreviewMouseMove", "private Point SubtractThemeGrab");

        Assert.Contains("source.Opacity = 0.4", dragStart, StringComparison.Ordinal);
        Assert.Contains("source.Opacity = 1.0", dragStart, StringComparison.Ordinal);
    }

    [Fact]
    public void GamesLoadingIndicator_SitsNextToTheSearchInsteadOfOverTheGames()
    {
        XDocument document = XDocument.Load(Path("Views", "Pages", "GamesPage.xaml"));
        XElement loading = document.Descendants(Presentation + "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == "Loading games…");
        XElement row = loading.Ancestors(Presentation + "Grid").First();

        Assert.Contains(row.Descendants(Presentation + "TextBox"),
            element => (string?)element.Attribute(Xaml + "Name") == "GamesSearchBox");
        Assert.DoesNotContain(row.Descendants(Presentation + "ScrollViewer"), _ => true);
    }

    [Fact]
    public void AccountSelection_HeaderOffersSelectAllClearAndLaunch()
    {
        string xaml = Page("AccountsPage.xaml");

        Assert.Contains("AccountList.ToggleSelectAllCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("AccountList.ClearSelectionCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("LaunchSelectedCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("AccountList.CheckSessionsCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomationPage_IsOneListWithAccountsDestinationAndSchedule()
    {
        string xaml = Page("AutomationPage.xaml");

        foreach (string fragment in new[]
                 {
                     "Command=\"{Binding NewAutomationCommand}\"", "ItemsSource=\"{Binding Items}\"",
                     "Command=\"{Binding RunCommand}\"", "Command=\"{Binding StopNowCommand}\"",
                     "Command=\"{Binding DuplicateCommand}\"", "Command=\"{Binding DeleteCommand}\"",
                     "DataContext.AccountPicks", "DataContext.GroupPicks", "DataContext.AccountSearch",
                     "DataContext.UseFavoriteCommand", "DataContext.UseLaunchBarTargetCommand",
                     "IsChecked=\"{Binding ScheduleEnabled}\"", "ItemsSource=\"{Binding Days}\"",
                     "{Binding StartTime", "{Binding StopTime", "{Binding StopAfterMinutes",
                     "ConverterParameter=AfterMinutes", "ConverterParameter=AtTime", "Text=\"{Binding NextText}\""
                 })
        {
            Assert.Contains(fragment, xaml, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Templates", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Select an item on the left", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_BuildsOnlyTheAccountsPageUpFront()
    {
        string main = Source("MainWindow.xaml");

        Assert.Contains("<pages:AccountsPage x:Name=\"AccountsView\"", main, StringComparison.Ordinal);
        foreach (string page in new[] { "GamesPage", "AutomationPage", "SettingsPage" })
        {
            Assert.Contains($"ContentType=\"{{x:Type pages:{page}}}\"", main, StringComparison.Ordinal);
            Assert.DoesNotContain($"<pages:{page}", main, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RunTextBindings_AreOneWaySoReadOnlyPropertiesDoNotCrashStartup()
    {
        string root = System.IO.Path.GetDirectoryName(Path("App.xaml"))!;
        var offenders = Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !file.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => XDocument.Load(file).Descendants(Presentation + "Run")
                .Select(run => (string?)run.Attribute("Text"))
                .Where(text => text != null && text.StartsWith("{Binding", StringComparison.Ordinal) &&
                               !text.Contains("Mode=OneWay", StringComparison.Ordinal))
                .Select(text => $"{System.IO.Path.GetFileName(file)}: {text}"))
            .ToList();

        Assert.Empty(offenders);
    }

    private static string Page(string fileName) => Source("Views", "Pages", fileName);

    private static string SettingsView(string fileName) => Source("Views", "Settings", fileName);

    private static string AllSettingsViews() =>
        string.Concat(Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(Path("Views", "Settings", "GeneralSettings.xaml"))!, "*.xaml")
            .Select(File.ReadAllText));

    private static string Source(params string[] relativeParts) =>
        File.ReadAllText(Path(relativeParts));

    private static string Path(params string[] relativeParts) =>
        FindWorkspaceFile(new[] { "src", "InstanceManager" }.Concat(relativeParts).ToArray());

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
            string candidate = System.IO.Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate workspace file: {System.IO.Path.Combine(relativeParts)}");
    }
}

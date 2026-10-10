using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using InstanceManager.Models;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

[CollectionDefinition("WPF application", DisableParallelization = true)]
public sealed class WpfApplicationCollectionDefinition;

[Collection("WPF application")]
public sealed class ThemedDialogResourceTests
{
    [Fact]
    public void ThemedDialogs_InstantiateWithGloballyMergedResources()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            EnsureAppResources();

            CreatePrivate("InstanceManager.Views.ConfirmDialog", "Confirm action", "Delete group 'Farm'?", "Delete");
            CreatePrivate("InstanceManager.Views.DropActionDialog", "Tester", "Farm");
            CreatePrivate("InstanceManager.Views.GroupEditorDialog", (AccountGroup?)null, "#6E8FD9");
            CreatePrivate("InstanceManager.Views.FavoriteEditorDialog",
                new FavoriteGame { Name = "My server", PlaceId = 920587237, PrivateServerLinkCode = "123" }, "Edit favorite");
            CreatePrivate("InstanceManager.Controls.ColorPicker");
        });

        Assert.Null(failure);
    }

    [Fact]
    public void FavoritesComboBox_TemplateResolvesSharedResources()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            EnsureAppResources();

            var combo = new ComboBox
            {
                Style = (Style)Application.Current!.FindResource("FavoritesComboBox")
            };

            Assert.True(combo.ApplyTemplate());
        });

        Assert.Null(failure);
    }

    [Fact]
    public void GroupActionMenu_InstantiatesWithGloballyMergedResources()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            EnsureAppResources();

            var menu = new ContextMenu
            {
                Style = (Style)Application.Current!.FindResource("GroupActionMenu")
            };

            Assert.True(menu.ApplyTemplate());
        });

        Assert.Null(failure);
    }

    [Fact]
    public void EverySettingsCategoryAndTheReworkedPages_ResolveTheirResources()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            EnsureAppResources();
            EnsureAppConverters();

            foreach (InstanceManager.ViewModels.SettingsCategory category in Enum.GetValues<InstanceManager.ViewModels.SettingsCategory>())
                Assert.NotNull(InstanceManager.Views.Settings.SettingsCategoryHost.Create(category));

            CreatePrivate("InstanceManager.Views.Pages.AccountsPage");
            CreatePrivate("InstanceManager.Views.Pages.SettingsPage");
            CreatePrivate("InstanceManager.Views.Pages.AutomationPage");
            CreatePrivate("InstanceManager.Views.Pages.GamesPage");
        });

        Assert.Null(failure);
    }

    internal static void EnsureAppConverters()
    {
        ResourceDictionary resources = Application.Current!.Resources;
        if (resources.Contains("EnumVis"))
            return;

        string appXaml = System.IO.File.ReadAllText(FindSource("App.xaml"))
            .Replace("clr-namespace:InstanceManager.Converters\"", "clr-namespace:InstanceManager.Converters;assembly=InstanceManager\"", StringComparison.Ordinal);
        System.Xml.Linq.XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        System.Xml.Linq.XElement dictionary = System.Xml.Linq.XDocument.Parse(appXaml).Root!
            .Element(presentation + "Application.Resources")!.Element(presentation + "ResourceDictionary")!;
        dictionary.Element(presentation + "ResourceDictionary.MergedDictionaries")?.Remove();

        var converters = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
        foreach (object key in converters.Keys)
            resources[key] = converters[key];
    }

    private static string FindSource(string name)
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = System.IO.Path.Combine(directory.FullName, "src", "InstanceManager", name);
            if (System.IO.File.Exists(path))
                return path;
            directory = directory.Parent;
        }
        throw new System.IO.FileNotFoundException(name);
    }

    internal static void EnsureAppResources()
    {
        if (Application.Current == null)
            _ = new Application();

        ResourceDictionary resources = Application.Current!.Resources;
        if (resources.MergedDictionaries.Count > 0)
            return;

        resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Colors.xaml") });
        resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Controls.xaml") });
    }

    private static Uri Pack(string relative) =>
        new($"pack://application:,,,/InstanceManager;component/{relative}", UriKind.Absolute);

    private static void CreatePrivate(string typeName, params object?[] args)
    {
        Type type = typeof(InstanceManager.MainWindow).Assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"Type not found: {typeName}");

        _ = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
    }

    internal static Exception? RunOnStaThread(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return captured;
    }
}

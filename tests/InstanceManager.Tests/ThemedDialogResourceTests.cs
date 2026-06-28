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
            CreatePrivate("InstanceManager.Views.ThemeEditorDialog", (string?)null, "My theme", BuiltInThemes.Dark.Palette, "New theme");
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

    private static void EnsureAppResources()
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

        _ = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, args, null);
    }

    private static Exception? RunOnStaThread(Action action)
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

[Collection("WPF application")]
public sealed class ThemeServiceTests
{
    [Fact]
    public void Apply_ReplacesBrushResourcesAndDynamicConsumersRefresh()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            EnsureAppResources();

            var service = new ThemeService(new FakeThemeRepository(), new FakeSettingsService());
            var probe = new Border();
            probe.SetResourceReference(Border.BackgroundProperty, "Brush.Window");

            var host = new Window
            {
                Content = probe,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 1,
                Height = 1,
                Left = -10_000,
                Top = -10_000
            };

            host.Show();
            try
            {
                var originalBrush = Assert.IsType<SolidColorBrush>(probe.Background);

                service.Apply(BuiltInThemes.Light.Palette);
                var lightBrush = Assert.IsType<SolidColorBrush>(probe.Background);
                Assert.NotSame(originalBrush, lightBrush);
                Assert.Equal((Color)ColorConverter.ConvertFromString(BuiltInThemes.Light.Palette.Window), lightBrush.Color);

                service.Apply(BuiltInThemes.Dracula.Palette);
                var draculaBrush = Assert.IsType<SolidColorBrush>(probe.Background);
                Assert.NotSame(lightBrush, draculaBrush);
                Assert.Equal((Color)ColorConverter.ConvertFromString(BuiltInThemes.Dracula.Palette.Window), draculaBrush.Color);
            }
            finally
            {
                host.Close();
                service.Apply(BuiltInThemes.Dark.Palette);
            }
        });

        Assert.Null(failure);
    }
    [Fact]
    public void ApplyFromSettings_FallsBackToDarkForUnknownId()
    {
        Exception? failure = RunOnStaThread(() =>
        {
            EnsureAppResources();
            var settings = new FakeSettingsService();
            settings.Settings.ThemeId = "does-not-exist";

            var service = new ThemeService(new FakeThemeRepository(), settings);
            service.ApplyFromSettings();

            var windowBrush = (SolidColorBrush)Application.Current!.FindResource("Brush.Window");
            Assert.Equal((Color)ColorConverter.ConvertFromString(BuiltInThemes.Dark.Palette.Window), windowBrush.Color);
        });

        Assert.Null(failure);
    }

    private static void EnsureAppResources()
    {
        if (Application.Current == null)
            _ = new Application();

        ResourceDictionary resources = Application.Current!.Resources;
        if (resources.MergedDictionaries.Count == 0)
        {
            resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Colors.xaml") });
            resources.MergedDictionaries.Add(new ResourceDictionary { Source = Pack("Themes/Controls.xaml") });
        }
    }

    private static Uri Pack(string relative) =>
        new($"pack://application:,,,/InstanceManager;component/{relative}", UriKind.Absolute);

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

    private sealed class FakeThemeRepository : IThemeRepository
    {
        public IReadOnlyList<ThemeDefinition> All { get; } = new List<ThemeDefinition>();
        public void Add(ThemeDefinition theme) { }
        public void Update(ThemeDefinition theme) { }
        public void Remove(string id) { }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public void Save() { }
    }
}

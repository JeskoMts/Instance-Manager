using System;
using System.Windows;
using InstanceManager.Composition;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace InstanceManager;

public partial class App : Application
{
    private ServiceProvider? _provider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.CleanupLegacyWebViewData();

        var services = new ServiceCollection();
        services.AddInstanceManager();
        _provider = services.BuildServiceProvider();

        _provider.GetRequiredService<ThemeService>().ApplyFromSettings();

        var settings = _provider.GetRequiredService<ISettingsService>();
        var multiInstance = _provider.GetRequiredService<MultiInstanceManager>();
        multiInstance.TryApply(settings.Settings.MultiInstanceEnabled);

        var shell = _provider.GetRequiredService<ShellViewModel>();
        var window = _provider.GetRequiredService<MainWindow>();

        window.Loaded += async (_, _) => await shell.InitializeAsync();
        MainWindow = window;

#if DEBUG
        System.Diagnostics.Trace.AutoFlush = true;
        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(
            new System.Diagnostics.TextWriterTraceListener("binding-errors.log"));
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
            System.Diagnostics.SourceLevels.Warning;
#endif

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _provider?.Dispose();
        base.OnExit(e);
    }
}

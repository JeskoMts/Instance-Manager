using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using InstanceManager.Composition;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace InstanceManager;

public partial class App : Application
{
    private ServiceProvider? _provider;
    private UpdateService? _updater;
    private bool _installUpdateOnExit;
    private bool _restartAfterExit;

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

        _updater = _provider.GetRequiredService<UpdateService>();
        _updater.CleanupPreviousUpdate();
        if (Array.IndexOf(e.Args, UpdateService.UpdatedArgument) >= 0)
        {
            shell.Notify(NotificationId.AppUpdated, NotificationKind.Success, "Instance Manager updated",
                $"You're now on version {_updater.CurrentVersion.ToString(3)}.");
        }

#if !DEBUG
        _ = InstallUpdateAsync(_updater, shell);
#endif
    }

    private async Task InstallUpdateAsync(UpdateService updater, ShellViewModel shell)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            Version? next = await updater.DownloadAsync(timeout.Token);
            if (next is null)
                return;

            string version = next.ToString(3);
            if (!IsIdle(shell))
            {
                _installUpdateOnExit = true;
                shell.Notify(NotificationId.AppUpdated, NotificationKind.Info, "Update ready",
                    $"Version {version} is downloaded and installs when you close Instance Manager.");
                return;
            }

            if (updater.Apply())
            {
                _restartAfterExit = true;
                Shutdown();
                return;
            }

            shell.Notify(NotificationId.AppUpdated, NotificationKind.Error, "Update failed",
                $"Version {version} couldn't be installed. Check that the Instance Manager folder isn't read-only, or download the new version from GitHub.");
        }
        catch (Exception)
        {
        }
    }

    private bool IsIdle(ShellViewModel shell) =>
        !shell.IsBusy &&
        Windows.Count == 1 &&
        _provider?.GetRequiredService<InstanceTracker>().RunningCount == 0;

    protected override void OnExit(ExitEventArgs e)
    {
        _provider?.Dispose();

        if (_installUpdateOnExit)
            _updater?.Apply();

        if (_restartAfterExit && _updater != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_updater.ExecutablePath)
                {
                    UseShellExecute = false,
                    ArgumentList = { UpdateService.UpdatedArgument }
                });
            }
            catch (Win32Exception)
            {
            }
        }

        base.OnExit(e);
    }
}

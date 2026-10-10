using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using InstanceManager.Composition;
using InstanceManager.Controls;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace InstanceManager;

public partial class App : Application, IAppLifetime
{
    private ServiceProvider? _provider;
    private SingleInstance? _instance;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private ShellViewModel? _shell;
    private ISettingsService? _settings;
    private UpdateService? _updater;
    private bool _installUpdateOnExit;
    private bool _restartAfterExit;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool restarted = Array.IndexOf(e.Args, UpdateService.UpdatedArgument) >= 0;
        _instance = SingleInstance.TryAcquire(SingleInstance.DefaultName, restarted ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
        if (_instance == null)
        {
            SingleInstance.SignalExisting(SingleInstance.DefaultName);
            Shutdown();
            return;
        }

        AppPaths.CleanupLegacyWebViewData();
        AppPaths.CleanupRemovedFeatureFiles();

        var services = new ServiceCollection();
        services.AddInstanceManager();
        _provider = services.BuildServiceProvider();

        _provider.GetRequiredService<ThemeService>().ApplyFromSettings();

        _settings = _provider.GetRequiredService<ISettingsService>();
        var multiInstance = _provider.GetRequiredService<MultiInstanceManager>();
        multiInstance.TryApply(_settings.Settings.MultiInstanceEnabled);

        _shell = _provider.GetRequiredService<ShellViewModel>();
        _shell.Operations.Start();
        _window = _provider.GetRequiredService<MainWindow>();
        MainWindow = _window;

        _window.CloseToTray = () => _settings.Settings.CloseToTray;
        _window.HiddenToTray += UpdateTray;
        _window.IsVisibleChanged += (_, _) => UpdateTray();
        _shell.Settings.TrayPreferenceChanged += UpdateTray;
        _instance.ListenForActivation(() => Dispatcher.BeginInvoke(new Action(ShowMainWindow)));

        _provider.GetRequiredService<StartupService>().RefreshPath();

#if DEBUG
        System.Diagnostics.Trace.AutoFlush = true;
        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(
            new System.Diagnostics.TextWriterTraceListener("binding-errors.log"));
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
            System.Diagnostics.SourceLevels.Warning;
#endif

        bool startHidden = Array.IndexOf(e.Args, StartupService.StartupArgument) >= 0 && _settings.Settings.StartInTray;
        if (startHidden)
        {
            _ = _shell.InitializeAsync();
        }
        else
        {
            _window.Loaded += async (_, _) => await _shell.InitializeAsync();
            _window.Show();
        }
        UpdateTray();

        _updater = _provider.GetRequiredService<UpdateService>();
        _updater.CleanupPreviousUpdate();
        if (restarted)
        {
            _shell.Notify(NotificationId.AppUpdated, NotificationKind.Success, "Instance Manager updated",
                $"You're on version {_updater.CurrentVersion.ToString(3)} now.");
        }

        _shell.Updates.Start(this);
        _shell.Automation.Start();
    }

    public bool IsIdle =>
        _shell is { IsBusy: false } &&
        Windows.Count <= 1 &&
        _provider?.GetRequiredService<InstanceTracker>().RunningCount == 0;

    public void InstallUpdateOnExit() => _installUpdateOnExit = true;

    public void RestartAfterUpdate()
    {
        _installUpdateOnExit = false;
        _restartAfterExit = true;
        if (_window != null)
            _window.Quit();
        else
            Shutdown();
    }

    private void ShowMainWindow() => _window?.ShowFromTray();

    private void UpdateTray()
    {
        if (_window == null || _settings == null)
            return;

        bool needed = _settings.Settings.CloseToTray || !_window.IsVisible;
        if (needed && _tray == null)
        {
            _tray = new TrayIcon("Instance Manager");
            _tray.Activated += ShowMainWindow;
            _tray.MenuRequested += ShowTrayMenu;
        }
        if (_tray != null)
            _tray.Visible = needed;
    }

    private void ShowTrayMenu()
    {
        if (_shell == null || _window == null)
            return;

        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        menu.Items.Add(TrayItem("Open Instance Manager", IconKind.Monitor, ShowMainWindow, bold: true));
        MenuItem close = TrayItem("Close every Roblox window", IconKind.Stop, _shell.CloseAllClients);
        close.IsEnabled = _shell.AccountList.HasRunning;
        menu.Items.Add(close);
        menu.Items.Add(new Separator());
        menu.Items.Add(TrayItem("Quit", IconKind.Close, () => _window.Quit()));
        menu.Opened += (_, _) =>
        {
            if (PresentationSource.FromVisual(menu) is HwndSource source)
                SetForegroundWindow(source.Handle);
        };
        menu.IsOpen = true;
    }

    private static MenuItem TrayItem(string header, IconKind icon, Action action, bool bold = false)
    {
        var item = new MenuItem { Header = header, Icon = new Icon { Kind = icon, Size = 15 } };
        if (bold)
            item.FontWeight = FontWeights.SemiBold;
        item.Click += (_, _) => action();
        return item;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _shell?.Automation.Stop();
        _shell?.Operations.Stop();

        if (_installUpdateOnExit)
            _shell?.Updates.ApplyOnExit();

        _provider?.Dispose();
        _instance?.Release();

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

        _instance?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}

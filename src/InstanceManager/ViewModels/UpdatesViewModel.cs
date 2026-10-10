using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class UpdatesViewModel : ObservableObject
{
    public const string ReleasesPage = "https://github.com/JeskoMts/Instance-Manager/releases/latest";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly UpdateService _updater;
    private readonly ISettingsService _settings;
    private readonly IShellCoordinator _shell;
    private readonly string _previousDirectory;
    private IAppLifetime? _lifetime;
    private DispatcherTimer? _timer;
    private UpdateService.ReleaseInfo? _ready;

    public UpdatesViewModel(UpdateService updater, ISettingsService settings, IShellCoordinator shell, string? previousDirectory = null)
    {
        _updater = updater;
        _settings = settings;
        _shell = shell;
        _previousDirectory = previousDirectory ?? AppPaths.PreviousVersionDirectory;
        updateAutomatically = settings.Settings.UpdateCheckAutomatically;

        statusText = _updater.CanUpdate
            ? "Not checked yet."
            : "Updates install only in the downloaded app, not in a build from source.";
        RefreshPrevious();
    }

    public string VersionTitle => "Version " + _updater.CurrentVersion.ToString(3);

    public bool CanUpdate => _updater.CanUpdate;

    [ObservableProperty] private string statusText;
    [ObservableProperty] private bool isChecking;
    [ObservableProperty] private bool isUpdateReady;
    [ObservableProperty] private string? readyVersion;
    [ObservableProperty] private string previousText = string.Empty;
    [ObservableProperty] private bool canRollBack;
    [ObservableProperty] private bool updateAutomatically;

    partial void OnUpdateAutomaticallyChanged(bool value)
    {
        if (_settings.Settings.UpdateCheckAutomatically != value)
        {
            _settings.Settings.UpdateCheckAutomatically = value;
            _settings.Save();
        }
        RestartTimer();
    }

    public void Start(IAppLifetime lifetime)
    {
        _lifetime = lifetime;
        if (_updater.CanUpdate && UpdateAutomatically)
            _ = CheckAsync(startup: true, manual: false);
        RestartTimer();
    }

    private void RestartTimer()
    {
        _timer?.Stop();
        if (_lifetime == null || !_updater.CanUpdate || !UpdateAutomatically)
            return;
        _timer ??= new DispatcherTimer();
        _timer.Tick -= Timer_Tick;
        _timer.Tick += Timer_Tick;
        _timer.Interval = CheckInterval;
        _timer.Start();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (!IsUpdateReady && !IsChecking)
            await CheckAsync(startup: false, manual: false);
    }

    [RelayCommand]
    private Task CheckNow() => CheckAsync(startup: false, manual: true);

    public async Task CheckAsync(bool startup, bool manual)
    {
        if (IsChecking || !_updater.CanUpdate)
            return;
        if (IsUpdateReady)
        {
            StatusText = $"Version {ReadyVersion} is ready. Restart Instance Manager to install it.";
            return;
        }

        IsChecking = true;
        StatusText = "Asking GitHub for the latest version…";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            UpdateService.ReleaseInfo? release = await _updater.CheckAsync(timeout.Token);
            string checkedAt = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (release == null)
            {
                StatusText = _updater.UnavailableVersion is { } unavailable
                    ? $"Version {unavailable.ToString(3)} is on GitHub, but its download can't be verified yet. Try again later or open What's new. Checked at {checkedAt}."
                    : $"v{_updater.CurrentVersion.ToString(3)} is the latest version. Checked at {checkedAt}.";
                return;
            }

            string version = release.Version.ToString(3);
            StatusText = $"Downloading version {version}…";
            if (!await _updater.DownloadAndStageAsync(release, timeout.Token))
            {
                StatusText = $"Version {version} couldn't be downloaded or didn't match its checksum. Checked at {checkedAt}.";
                return;
            }

            _ready = release;
            ReadyVersion = version;
            IsUpdateReady = true;

            if ((startup || manual) && _lifetime?.IsIdle == true)
            {
                InstallAndRestart();
                return;
            }

            _lifetime?.InstallUpdateOnExit();
            StatusText = $"Version {version} is ready. Restart Instance Manager to install it.";
            _shell.Notify(NotificationId.AppUpdated, NotificationKind.Info, "Update ready",
                $"Version {version} installs when you restart Instance Manager.", RestartNow, "Restart now");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.IO.IOException)
        {
            StatusText = "Couldn't reach GitHub. Check your connection and try again.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void RestartNow()
    {
        if (!IsUpdateReady)
            return;
        InstallAndRestart();
    }

    public bool ApplyOnExit()
    {
        if (!_updater.HasStagedUpdate)
            return false;
        _updater.KeepCurrentVersion(_previousDirectory);
        return _updater.Apply();
    }

    private void InstallAndRestart()
    {
        if (ApplyOnExit())
        {
            _lifetime?.RestartAfterUpdate();
            return;
        }

        IsUpdateReady = false;
        StatusText = $"Version {ReadyVersion} couldn't be installed. Make sure the folder with Instance Manager isn't read-only.";
        _shell.Notify(NotificationId.AppUpdated, NotificationKind.Error, "Update failed",
            $"Version {ReadyVersion} couldn't be installed. Check that the folder isn't read-only, or download it from GitHub.");
    }

    [RelayCommand]
    private void WhatsNew() => OpenUrl(_ready?.PageUrl ?? ReleasesPage);

    [RelayCommand]
    private void RollBack()
    {
        if (UpdateService.FindKeptVersion(_previousDirectory) is not { } kept || _lifetime == null)
            return;

        if (!_updater.StageLocal(kept.Path) || !_updater.Apply())
        {
            _shell.Notify(NotificationId.AppUpdated, NotificationKind.Error, "Rollback failed",
                "The previous version couldn't be restored. Check that the folder with Instance Manager isn't read-only.");
            return;
        }

        UpdateAutomatically = false;
        _lifetime.RestartAfterUpdate();
    }

    private void RefreshPrevious()
    {
        var kept = UpdateService.FindKeptVersion(_previousDirectory);
        CanRollBack = _updater.CanUpdate && kept is { } k && k.Version != _updater.CurrentVersion;
        PreviousText = kept is { } found
            ? $"Version {found.Version.ToString(3)} is kept. Rolling back also turns Update automatically off, so the old version stays."
            : "No previous version is kept yet. The app keeps a copy of the old version at every update.";
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}

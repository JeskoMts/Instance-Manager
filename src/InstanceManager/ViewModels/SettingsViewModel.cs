using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly MultiInstanceManager _multiInstance;

    public SettingsViewModel(ISettingsService settings, IDialogService dialogs, MultiInstanceManager multiInstance)
    {
        _settings = settings;
        _dialogs = dialogs;
        _multiInstance = multiInstance;

        AppSettings s = settings.Settings;
        multiInstanceEnabled = s.MultiInstanceEnabled;
        launchDelayMs = s.LaunchDelayMs;
        versionsPathOverride = s.VersionsPathOverride ?? string.Empty;

        switchToAccountsOnGameSelect = s.SwitchToAccountsOnGameSelect;

        confirmBypassMaster = s.ConfirmBypassMaster;
        confirmBypassRemoveAccount = s.ConfirmBypassRemoveAccount;
        confirmBypassDeleteGroup = s.ConfirmBypassDeleteGroup;
        confirmBypassDeleteFavorite = s.ConfirmBypassDeleteFavorite;
        confirmBypassStopAllInstances = s.ConfirmBypassStopAllInstances;
        confirmBypassClearAccountGroups = s.ConfirmBypassClearAccountGroups;
        confirmBypassDeleteTheme = s.ConfirmBypassDeleteTheme;

        notifyMuteMaster = s.NotifyMuteMaster;
        toastDurationMs = s.ToastDurationMs;

        autoReconnectMaster = s.AutoReconnectMaster;
        autoReconnectOnKickError = s.AutoReconnectOnKickError;
        autoReconnectOnCrash = s.AutoReconnectOnCrash;
        autoReconnectMaxAttempts = s.AutoReconnectMaxAttempts;

        BuildNotificationMutes();

        _multiInstance.TryApply(multiInstanceEnabled);
    }

    [ObservableProperty] private bool multiInstanceEnabled;
    [ObservableProperty] private int launchDelayMs;
    [ObservableProperty] private string versionsPathOverride;
    [ObservableProperty] private bool switchToAccountsOnGameSelect;

    [ObservableProperty] private bool confirmBypassMaster;
    [ObservableProperty] private bool confirmBypassRemoveAccount;
    [ObservableProperty] private bool confirmBypassDeleteGroup;
    [ObservableProperty] private bool confirmBypassDeleteFavorite;
    [ObservableProperty] private bool confirmBypassStopAllInstances;
    [ObservableProperty] private bool confirmBypassClearAccountGroups;
    [ObservableProperty] private bool confirmBypassDeleteTheme;

    [ObservableProperty] private bool notifyMuteMaster;
    [ObservableProperty] private int toastDurationMs;

    [ObservableProperty] private bool autoReconnectMaster;
    [ObservableProperty] private bool autoReconnectOnKickError;
    [ObservableProperty] private bool autoReconnectOnCrash;
    [ObservableProperty] private int autoReconnectMaxAttempts;

    public ObservableCollection<NotificationMuteOption> NotificationMutes { get; } = new();

    public string MultiInstanceStatusLabel =>
        MultiInstanceEnabled ? "Multi-instance active" : "Multi-instance off";

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string DataFolder => AppPaths.DataDirectory;

    partial void OnMultiInstanceEnabledChanged(bool value)
    {
        _settings.Settings.MultiInstanceEnabled = value;
        _settings.Save();
        _multiInstance.TryApply(value);
        OnPropertyChanged(nameof(MultiInstanceStatusLabel));
    }

    partial void OnLaunchDelayMsChanged(int value)
    {
        int normalized = AppSettings.NormalizeLaunchDelay(value);
        if (normalized != value)
        {
            LaunchDelayMs = normalized;
            return;
        }

        _settings.Settings.LaunchDelayMs = normalized;
        _settings.Save();
    }

    partial void OnVersionsPathOverrideChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _settings.Settings.VersionsPathOverride = null;
            _settings.Save();
            return;
        }

        if (!LocalPathPolicy.TryNormalizeFixedLocalDirectory(value, out string normalized))
        {
            VersionsPathOverride = string.Empty;
            return;
        }

        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            VersionsPathOverride = normalized;
            return;
        }

        _settings.Settings.VersionsPathOverride = normalized;
        _settings.Save();
    }

    partial void OnSwitchToAccountsOnGameSelectChanged(bool value)
    {
        _settings.Settings.SwitchToAccountsOnGameSelect = value;
        _settings.Save();
    }

    partial void OnConfirmBypassMasterChanged(bool value)
    {
        _settings.Settings.ConfirmBypassMaster = value;
        _settings.Save();
    }

    partial void OnConfirmBypassRemoveAccountChanged(bool value)
    {
        _settings.Settings.ConfirmBypassRemoveAccount = value;
        _settings.Save();
    }

    partial void OnConfirmBypassDeleteGroupChanged(bool value)
    {
        _settings.Settings.ConfirmBypassDeleteGroup = value;
        _settings.Save();
    }

    partial void OnConfirmBypassDeleteFavoriteChanged(bool value)
    {
        _settings.Settings.ConfirmBypassDeleteFavorite = value;
        _settings.Save();
    }

    partial void OnConfirmBypassStopAllInstancesChanged(bool value)
    {
        _settings.Settings.ConfirmBypassStopAllInstances = value;
        _settings.Save();
    }

    partial void OnConfirmBypassClearAccountGroupsChanged(bool value)
    {
        _settings.Settings.ConfirmBypassClearAccountGroups = value;
        _settings.Save();
    }

    partial void OnConfirmBypassDeleteThemeChanged(bool value)
    {
        _settings.Settings.ConfirmBypassDeleteTheme = value;
        _settings.Save();
    }

    partial void OnNotifyMuteMasterChanged(bool value)
    {
        _settings.Settings.NotifyMuteMaster = value;
        _settings.Save();
    }

    partial void OnAutoReconnectMasterChanged(bool value)
    {
        _settings.Settings.AutoReconnectMaster = value;
        _settings.Save();
    }

    partial void OnAutoReconnectOnKickErrorChanged(bool value)
    {
        _settings.Settings.AutoReconnectOnKickError = value;
        _settings.Save();
    }

    partial void OnAutoReconnectOnCrashChanged(bool value)
    {
        _settings.Settings.AutoReconnectOnCrash = value;
        _settings.Save();
    }

    partial void OnAutoReconnectMaxAttemptsChanged(int value)
    {
        int normalized = AppSettings.NormalizeAutoReconnectAttempts(value);
        if (normalized != value)
        {
            AutoReconnectMaxAttempts = normalized;
            return;
        }

        _settings.Settings.AutoReconnectMaxAttempts = normalized;
        _settings.Save();
    }

    partial void OnToastDurationMsChanged(int value)
    {
        int rounded = (int)Math.Round(value / 100.0, MidpointRounding.AwayFromZero) * 100;
        int normalized = Math.Clamp(rounded, AppSettings.MinToastDurationMs, AppSettings.MaxToastDurationMs);
        if (normalized != value)
        {
            ToastDurationMs = normalized;
            return;
        }

        _settings.Settings.ToastDurationMs = normalized;
        _settings.Save();
    }

    private void BuildNotificationMutes()
    {
        (NotificationId Id, string Label)[] rows =
        {
            (NotificationId.AccountAdded, "Account added"),
            (NotificationId.AccountUpdated, "Account updated"),
            (NotificationId.AccountRemoved, "Account removed"),
            (NotificationId.GroupCreated, "Group created"),
            (NotificationId.GroupUpdated, "Group updated"),
            (NotificationId.GroupDeleted, "Group deleted"),
            (NotificationId.GroupEmpty, "Group is empty"),
            (NotificationId.LaunchComplete, "Launch complete"),
            (NotificationId.GameSelected, "Game selected"),
            (NotificationId.NothingToLaunch, "Nothing to launch"),
            (NotificationId.InvalidLaunchTarget, "Invalid launch target"),
            (NotificationId.RobloxNotFound, "Roblox not found"),
            (NotificationId.FavoriteApplied, "Favorite applied"),
            (NotificationId.FavoriteSaved, "Favorite saved"),
            (NotificationId.FavoriteUpdated, "Favorite updated"),
            (NotificationId.FavoriteDeleted, "Favorite deleted"),
            (NotificationId.FavoriteNotSaved, "Favorite not saved"),
            (NotificationId.AccountRenamed, "Account renamed"),
            (NotificationId.AccountGroupsUpdated, "Account groups updated"),
            (NotificationId.GroupRenamed, "Group renamed"),
            (NotificationId.ThemeApplied, "Theme applied"),
            (NotificationId.ThemeCreated, "Theme created"),
            (NotificationId.ThemeUpdated, "Theme updated"),
            (NotificationId.ThemeDeleted, "Theme deleted"),
            (NotificationId.ThemeExported, "Theme exported"),
            (NotificationId.ThemeImported, "Theme imported"),
            (NotificationId.ThemeImportFailed, "Theme import failed")
        };

        foreach ((NotificationId id, string label) in rows)
            NotificationMutes.Add(new NotificationMuteOption(_settings, id, label));
    }

    [RelayCommand]
    private void BrowseVersionsPath()
    {
        string? path = _dialogs.PickFolder("Choose Roblox versions folder");
        if (path != null)
            VersionsPathOverride = path;
    }

    [RelayCommand]
    private void ResetVersionsPath() => VersionsPathOverride = string.Empty;

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            AppPaths.EnsureDataDirectory();
            Process.Start(new ProcessStartInfo { FileName = AppPaths.DataDirectory, UseShellExecute = true });
        }
        catch
        {
        }
    }
}

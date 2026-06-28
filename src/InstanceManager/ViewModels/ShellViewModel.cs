using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class ShellViewModel : ObservableObject, IShellCoordinator
{
    private readonly LaunchService _launch;
    private readonly ISettingsService _settings;
    private readonly MultiInstanceManager _multiInstance;

    public ShellViewModel(
        IAccountRepository accounts,
        IGroupRepository groups,
        IFavoriteRepository favorites,
        IThemeRepository themes,
        ISettingsService settings,
        VersionService versions,
        LaunchService launch,
        IDialogService dialogs,
        IServerLinkResolver serverLinks,
        IRobloxAvatarService avatars,
        IRobloxGamesService games,
        InstanceTracker tracker,
        MultiInstanceManager multiInstance,
        AutoReconnectService autoReconnect,
        ThemeService themeService)
    {
        _launch = launch;
        _settings = settings;
        _multiInstance = multiInstance;

        VersionBar = new VersionBarViewModel(versions, settings);
        LaunchPanel = new LaunchPanelViewModel(favorites, settings, dialogs, this, serverLinks);
        AccountList = new AccountListViewModel(accounts, groups, dialogs, this, tracker, VersionBar, avatars, autoReconnect);
        Games = new GamesViewModel(games, this);
        Settings = new SettingsViewModel(settings, dialogs, multiInstance);
        Theme = new ThemeViewModel(themeService, themes, settings, dialogs, this);
        Notifications = new NotificationCenterViewModel(settings);
    }

    public VersionBarViewModel VersionBar { get; }
    public AccountListViewModel AccountList { get; }
    public LaunchPanelViewModel LaunchPanel { get; }
    public GamesViewModel Games { get; }
    public SettingsViewModel Settings { get; }
    public ThemeViewModel Theme { get; }
    public NotificationCenterViewModel Notifications { get; }

    [ObservableProperty] private string statusText = "Ready.";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private AppSection selectedSection = AppSection.Accounts;

    partial void OnSelectedSectionChanged(AppSection oldValue, AppSection newValue)
    {
        if (oldValue == AppSection.Settings && newValue != AppSection.Settings)
        {
            VersionBar.RefreshVersions();
            AccountList.RebuildGroups();
        }

        if (newValue == AppSection.Games)
            _ = Games.EnsureLoadedAsync();
    }

    public void ApplyGameTarget(long placeId, string? gameName)
    {
        LaunchPanel.ApplyGameTargetFromGames(placeId);
        string label = string.IsNullOrWhiteSpace(gameName) ? placeId.ToString() : gameName.Trim();
        Notify(NotificationId.GameSelected, NotificationKind.Info, "Game selected", $"Selected '{label}' as launch target.");

        if (_settings.Settings.SwitchToAccountsOnGameSelect)
            SelectedSection = AppSection.Accounts;
    }

    public async Task InitializeAsync()
    {
        await VersionBar.InitializeAsync();
        AccountList.RebuildGroups();

        _ = Games.EnsureLoadedAsync();
    }


    public void SetStatus(string message) => StatusText = message;

    public void Notify(NotificationId id, NotificationKind kind, string title, string message, Action? undoAction = null)
    {
        StatusText = message;
        Notifications.Show(id, kind, title, message, undoAction: undoAction);
    }

    public async Task LaunchAsync(IReadOnlyList<Account> accounts)
    {
        if (IsBusy) return;

        if (accounts.Count == 0)
        {
            Notify(NotificationId.NothingToLaunch, NotificationKind.Error, "Nothing to launch", "Select at least one account.");
            return;
        }

        IsBusy = true;
        try
        {
            ServerTargetResolution targetResult = await LaunchPanel.ResolveTargetAsync();
            if (!targetResult.IsSuccess)
            {
                Notify(NotificationId.InvalidLaunchTarget, NotificationKind.Error, "Invalid launch target", targetResult.Error);
                return;
            }

            if (VersionBar.Versions.Count == 0)
            {
                Notify(NotificationId.RobloxNotFound, NotificationKind.Error, "Roblox not found", "No installed Roblox version was found.");
                return;
            }

            if (accounts.Count > 1 && _settings.Settings.MultiInstanceEnabled
                && !_multiInstance.TryApply(true))
            {
                Notify(NotificationId.MultiInstanceUnavailable, NotificationKind.Error,
                    "Multi-instance unavailable",
                    "A Roblox client is already running, so extra accounts can't open separately. Close every Roblox window, then launch again.");
            }

            var progress = new Progress<string>(s => StatusText = s);
            LaunchSummary summary = await _launch.LaunchAsync(accounts, targetResult.Target!, ResolveVersion, progress);
            string message = summary.Failed == 0
                ? $"Started {summary.Started} instance(s)."
                : $"Started {summary.Started}, {summary.Failed} failed.";
            Notify(NotificationId.LaunchComplete, summary.Failed == 0 ? NotificationKind.Success : NotificationKind.Error,
                summary.Failed == 0 ? "Launch complete" : "Launch completed with errors", message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private RobloxVersion? ResolveVersion(Account account)
    {
        if (!string.IsNullOrEmpty(account.PreferredVersionGuid))
        {
            foreach (RobloxVersion v in VersionBar.Versions)
            {
                if (v.VersionGuid == account.PreferredVersionGuid)
                    return v;
            }
        }
        return VersionBar.SelectedVersion;
    }


    [RelayCommand]
    private Task LaunchSelected() => LaunchAsync(AccountList.SelectedAccounts());
}

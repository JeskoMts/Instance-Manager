using System;
using System.Collections.Generic;
using System.Threading;
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
    private CancellationTokenSource? _launchCts;

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
        LaunchPanel = new LaunchPanelViewModel(favorites, settings, dialogs, this, serverLinks, games);
        AccountList = new AccountListViewModel(accounts, groups, dialogs, this, tracker, VersionBar, avatars, autoReconnect);
        Games = new GamesViewModel(games, this);
        Settings = new SettingsViewModel(settings, dialogs, multiInstance);
        Theme = new ThemeViewModel(themeService, themes, settings, dialogs, this);
        Notifications = new NotificationCenterViewModel(settings);

        AccountList.RebuildGroups();
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
    [ObservableProperty] private double launchProgress;
    [ObservableProperty] private string launchProgressText = string.Empty;

    partial void OnSelectedSectionChanged(AppSection oldValue, AppSection newValue)
    {
        if (oldValue == AppSection.Settings && newValue != AppSection.Settings)
        {
            _ = VersionBar.RefreshVersionsAsync();
            AccountList.RebuildGroups();
        }

        if (newValue == AppSection.Games)
            _ = Games.EnsureLoadedAsync();
    }

    public void ApplyGameTarget(long placeId, string? gameName)
    {
        LaunchPanel.ApplyGameTargetFromGames(placeId);
        LaunchPanel.RememberGameName(placeId, gameName);
        string label = string.IsNullOrWhiteSpace(gameName) ? placeId.ToString() : gameName.Trim();
        Notify(NotificationId.GameSelected, NotificationKind.Info, "Game selected", $"Selected '{label}' as launch target.");

        if (_settings.Settings.SwitchToAccountsOnGameSelect)
            SelectedSection = AppSection.Accounts;
    }

    public async Task InitializeAsync()
    {
        _ = Games.EnsureLoadedAsync();
        await VersionBar.InitializeAsync();
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
        LaunchProgress = 0;
        LaunchProgressText = "Preparing…";
        using var cts = new CancellationTokenSource();
        _launchCts = cts;
        try
        {
            ServerTargetResolution targetResult = await LaunchPanel.ResolveTargetAsync();
            if (!targetResult.IsSuccess)
            {
                Notify(NotificationId.InvalidLaunchTarget, NotificationKind.Error, "Invalid launch target", targetResult.Error);
                return;
            }

            if (VersionBar.Versions.Count == 0)
                await VersionBar.RefreshVersionsAsync();
            if (VersionBar.Versions.Count == 0)
            {
                Notify(NotificationId.RobloxNotFound, NotificationKind.Error, "Roblox not found", "No installed Roblox client was found. Install Roblox, or set its folder under Settings → Roblox version.");
                return;
            }

            if (accounts.Count > 1 && _settings.Settings.MultiInstanceEnabled
                && !_multiInstance.TryApply(true))
            {
                Notify(NotificationId.MultiInstanceUnavailable, NotificationKind.Error,
                    "Multi-instance unavailable",
                    "A Roblox client is already running, so extra accounts can't open separately. Close every Roblox window, then launch again.");
            }

            AccountList.ClearLaunchStages();
            var progress = new InlineProgress<LaunchProgress>(OnLaunchProgress);
            LaunchSummary summary = await _launch.LaunchAsync(accounts, targetResult.Target!, ResolveVersion, progress, cts.Token);
            Notify(NotificationId.LaunchComplete, summary.Failed == 0 ? NotificationKind.Success : NotificationKind.Error,
                summary.Failed == 0 ? "Launch complete" : "Launch completed with errors", Describe(summary));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            AccountList.ClearPendingLaunchStages();
            Notify(NotificationId.LaunchComplete, NotificationKind.Info, "Launch cancelled", "The remaining accounts were not started.");
        }
        finally
        {
            _launchCts = null;
            IsBusy = false;
            LaunchProgress = 0;
            LaunchProgressText = string.Empty;
        }
    }

    internal static string Describe(LaunchSummary summary)
    {
        var parts = new List<string> { $"Started {summary.Started}" };
        if (summary.Failed > 0) parts.Add($"{summary.Failed} failed");
        if (summary.Skipped > 0) parts.Add($"{summary.Skipped} already running");
        return string.Join(" · ", parts) + ".";
    }

    private void OnLaunchProgress(LaunchProgress p)
    {
        StatusText = p.Message;
        AccountList.SetLaunchStage(p.AccountId, p.Stage, p.Stage == LaunchStage.Failed ? p.Message : null, p.RequiresSignIn);
        if (p.Stage == LaunchStage.Queued)
            return;

        bool done = p.Stage is LaunchStage.Started or LaunchStage.Failed or LaunchStage.Skipped;
        LaunchProgress = (p.Index + (done ? 1.0 : 0.5)) / Math.Max(1, p.Total);
        LaunchProgressText = done && p.Index + 1 < p.Total
            ? $"Next account… ({p.Index + 2}/{p.Total})"
            : $"Launching {p.Index + 1} of {p.Total}…";
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

    [RelayCommand]
    private void CancelLaunch() => _launchCts?.Cancel();

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}

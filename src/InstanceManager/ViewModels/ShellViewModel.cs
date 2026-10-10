using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private readonly InstanceTracker _tracker;
    private readonly AutoReconnectService _autoReconnect;
    private readonly IRobloxGamesService _games;
    private readonly IAccountRepository _accounts;
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
        ThemeService themeService,
        IAutomationRepository automation,
        UpdateService updater,
        RobloxGraphicsService graphics,
        StartupService startup,
        ISessionChecker sessions,
        AntiAfkService antiAfk)
    {
        _launch = launch;
        _settings = settings;
        _multiInstance = multiInstance;
        _tracker = tracker;
        _autoReconnect = autoReconnect;
        _games = games;
        _accounts = accounts;

        VersionBar = new VersionBarViewModel(versions, settings);
        LaunchPanel = new LaunchPanelViewModel(favorites, settings, dialogs, this, serverLinks, games);
        AccountList = new AccountListViewModel(accounts, groups, dialogs, this, tracker, VersionBar, avatars, autoReconnect, settings, sessions);
        Games = new GamesViewModel(games, this);
        Settings = new SettingsViewModel(settings, dialogs, multiInstance, startup);
        Theme = new ThemeViewModel(themeService, themes, settings, dialogs, this);
        Notifications = new NotificationCenterViewModel(settings);
        Automation = new AutomationViewModel(automation, accounts, this, serverLinks,
            groups: groups,
            favorites: favorites,
            isRunning: tracker.IsRunning,
            lookUpGameName: games.GetGameNameAsync,
            seed: () => new AutomationSeed(
                AccountList.SelectedAccounts().Select(a => a.Id).ToList(),
                LaunchPanel.SelectedMode,
                LaunchPanel.SelectedMode == JoinMode.PrivateByJobId ? LaunchPanel.JobIdInput : LaunchPanel.TargetInput));
        Updates = new UpdatesViewModel(updater, settings, this);
        Graphics = new GraphicsViewModel(settings, graphics, () => VersionBar.Versions.Select(v => v.FolderPath));
        Operations = new OperationsViewModel(settings, antiAfk);
        Operations.AvatarPreferenceChanged += AccountList.RefreshAvatarPreference;
        Operations.MessagePosted += (message, error) =>
            Notify(NotificationId.ToolMessage, error ? NotificationKind.Error : NotificationKind.Success,
                error ? "Didn't work" : "Done", message);

        sidebarCollapsed = settings.Settings.SidebarCollapsed;
        AccountList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountListViewModel.ClientsRunningText))
            {
                OnPropertyChanged(nameof(ClientsRunningText));
                if (SelectedSection == AppSection.Automation)
                    Automation.RefreshRunning();
            }
        };

        AccountList.RebuildGroups();
    }

    public VersionBarViewModel VersionBar { get; }
    public AccountListViewModel AccountList { get; }
    public LaunchPanelViewModel LaunchPanel { get; }
    public GamesViewModel Games { get; }
    public SettingsViewModel Settings { get; }
    public ThemeViewModel Theme { get; }
    public NotificationCenterViewModel Notifications { get; }
    public AutomationViewModel Automation { get; }
    public UpdatesViewModel Updates { get; }
    public GraphicsViewModel Graphics { get; }
    public OperationsViewModel Operations { get; }

    public string AppVersion =>
        "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.3");

    public string ClientsRunningText => AccountList.ClientsRunningText;

    [ObservableProperty] private string statusText = "Ready.";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private AppSection selectedSection = AppSection.Accounts;
    [ObservableProperty] private SettingsCategory selectedSettingsCategory = SettingsCategory.General;
    [ObservableProperty] private double launchProgress;
    [ObservableProperty] private string launchProgressText = string.Empty;
    [ObservableProperty] private bool sidebarCollapsed;

    partial void OnSidebarCollapsedChanged(bool value)
    {
        _settings.Settings.SidebarCollapsed = value;
        _settings.Save();
    }

    [RelayCommand]
    private void ToggleSidebar() => SidebarCollapsed = !SidebarCollapsed;

    partial void OnSelectedSectionChanged(AppSection oldValue, AppSection newValue)
    {
        if (oldValue == AppSection.Settings && newValue != AppSection.Settings)
        {
            _ = VersionBar.RefreshVersionsAsync();
            AccountList.RebuildGroups();
        }

        if (newValue == AppSection.Games)
            _ = Games.EnsureLoadedAsync();
        if (newValue == AppSection.Automation)
            Automation.Refresh();
        if (newValue == AppSection.Settings)
            Graphics.RefreshSummary();
    }

    public void ApplyGameTarget(long placeId, string? gameName)
    {
        LaunchPanel.ApplyGameTargetFromGames(placeId);
        LaunchPanel.RememberGameName(placeId, gameName);
        string label = string.IsNullOrWhiteSpace(gameName) ? placeId.ToString() : gameName.Trim();
        Notify(NotificationId.GameSelected, NotificationKind.Info, "Game selected", $"'{label}' is your launch target now.");

        if (_settings.Settings.SwitchToAccountsOnGameSelect)
            SelectedSection = AppSection.Accounts;
    }

    public async Task InitializeAsync()
    {
        _ = PreloadGamesAsync();
        await VersionBar.InitializeAsync();
        StatusText = VersionBar.SelectedVersion is { } version
            ? $"Ready. Roblox {version.DisplayLabel} found."
            : "Roblox wasn't found. Install it, or pick its folder in Settings.";
    }

    private async Task PreloadGamesAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        await Games.EnsureLoadedAsync();
    }

    public void SetStatus(string message) => StatusText = message;

    public void Notify(NotificationId id, NotificationKind kind, string title, string message, Action? undoAction = null, string? actionLabel = null)
    {
        StatusText = message;
        Notifications.Show(id, kind, title, message, undoAction: undoAction, actionLabel: actionLabel);
    }

    public Task LaunchAsync(IReadOnlyList<Account> accounts) => LaunchAsync(accounts, null);

    Task IShellCoordinator.LaunchAsync(IReadOnlyList<Account> accounts, ServerTarget target) => LaunchAsync(accounts, target);

    public async Task LaunchAsync(IReadOnlyList<Account> accounts, ServerTarget? targetOverride)
    {
        if (IsBusy) return;

        if (accounts.Count == 0)
        {
            Notify(NotificationId.NothingToLaunch, NotificationKind.Error, "Nothing to launch", "Select at least one account first.");
            return;
        }

        IsBusy = true;
        LaunchProgress = 0;
        LaunchProgressText = "Preparing…";
        using var cts = new CancellationTokenSource();
        _launchCts = cts;
        try
        {
            ServerTarget target;
            if (targetOverride != null)
            {
                target = targetOverride;
            }
            else
            {
                ServerTargetResolution targetResult = await LaunchPanel.ResolveTargetAsync(accounts, cts.Token);
                if (!targetResult.IsSuccess)
                {
                    Notify(NotificationId.InvalidLaunchTarget, NotificationKind.Error, "Can't launch", targetResult.Error);
                    return;
                }
                target = targetResult.Target!;
            }

            if (VersionBar.Versions.Count == 0)
                await VersionBar.RefreshVersionsAsync();
            if (VersionBar.Versions.Count == 0)
            {
                Notify(NotificationId.RobloxNotFound, NotificationKind.Error, "Roblox not found",
                    "No installed Roblox was found. Install Roblox, or pick its folder under Settings, Launching.");
                return;
            }

            int starting = accounts.Count(account => !_tracker.IsRunning(account.Id));
            if (NeedsOwnWindows(starting, _tracker.RunningCount) && _settings.Settings.MultiInstanceEnabled
                && !_multiInstance.TryApply(true))
            {
                Notify(NotificationId.MultiInstanceUnavailable, NotificationKind.Error,
                    "Only one window at a time",
                    "Roblox was already running when Instance Manager started, so extra accounts can't get their own window. Close every Roblox window, quit Roblox from its tray icon if it has one, and launch again.");
            }

            AccountList.ClearLaunchStages();
            var progress = new InlineProgress<LaunchProgress>(OnLaunchProgress);
            LaunchSummary summary = await _launch.LaunchAsync(accounts, target, ResolveVersion, progress, cts.Token);
            Notify(NotificationId.LaunchComplete, summary.Failed == 0 ? NotificationKind.Success : NotificationKind.Error,
                summary.Failed == 0 ? "Launch complete" : "Launch finished with errors", Describe(summary));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            AccountList.ClearPendingLaunchStages();
            Notify(NotificationId.LaunchComplete, NotificationKind.Info, "Launch cancelled", "The remaining accounts weren't started.");
        }
        finally
        {
            _launchCts = null;
            IsBusy = false;
            LaunchProgress = 0;
            LaunchProgressText = string.Empty;
        }
    }

    public void StopAccounts(IEnumerable<Guid> accountIds)
    {
        foreach (Guid id in accountIds)
        {
            _autoReconnect.NotifyManualStop(id);
            _tracker.Stop(id);
        }
    }

    public void CloseAllClients()
    {
        _autoReconnect.NotifyManualStopAll();
        int stopped = _tracker.StopAll();
        StatusText = stopped switch
        {
            0 => "No Roblox windows were open.",
            1 => "Closed 1 Roblox window.",
            _ => $"Closed {stopped} Roblox windows."
        };
    }

    [RelayCommand]
    private Task ServerHopSelected() => ServerHopAsync(AccountList.SelectedAccounts());

    public async Task ServerHopAsync(IReadOnlyList<Account> accounts)
    {
        if (IsBusy)
            return;
        if (accounts.Count == 0)
        {
            Notify(NotificationId.ServerHop, NotificationKind.Error, "Nothing to move", "Select the accounts that should switch servers.");
            return;
        }

        ServerTargetResolution resolution = await LaunchPanel.ResolveTargetAsync(accounts);
        if (resolution.Target is not { Mode: not JoinMode.Home } current)
        {
            Notify(NotificationId.ServerHop, NotificationKind.Error, "Pick a game first",
                "Server hop needs a game in the launch bar to find another server of it.");
            return;
        }

        StatusText = "Looking for another server…";
        IReadOnlyList<PublicServer> servers = await _games.GetPublicServersAsync(current.PlaceId);
        PublicServer? next = PickServer(servers, accounts.Count, current.JobId);
        if (next == null)
        {
            Notify(NotificationId.ServerHop, NotificationKind.Error, "No other server found",
                "Roblox didn't list another public server with enough free slots. Try again in a moment.");
            return;
        }

        List<Guid> running = accounts.Where(a => _tracker.IsRunning(a.Id)).Select(a => a.Id).ToList();
        StopAccounts(running);
        for (int i = 0; i < 40 && running.Any(_tracker.IsRunning); i++)
            await Task.Delay(250);

        Notify(NotificationId.ServerHop, NotificationKind.Info, "Switching servers",
            accounts.Count == 1 ? "Moving 1 account to another server." : $"Moving {accounts.Count} accounts to another server.");
        await LaunchAsync(accounts, ServerTarget.ByJob(current.PlaceId, next.JobId));
    }

    internal static bool NeedsOwnWindows(int starting, int alreadyRunning) =>
        starting > 0 && starting + alreadyRunning > 1;

    internal static PublicServer? PickServer(IReadOnlyList<PublicServer> servers, int seats, string? currentJobId)
    {
        List<PublicServer> candidates = servers
            .Where(s => !string.Equals(s.JobId, currentJobId, StringComparison.OrdinalIgnoreCase))
            .Where(s => s.MaxPlayers - s.Playing >= seats)
            .ToList();
        if (candidates.Count == 0)
            return null;
        return candidates[Random.Shared.Next(candidates.Count)];
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

    [RelayCommand]
    private void StopAll()
    {
        _launchCts?.Cancel();
        _autoReconnect.NotifyManualStopAll();
        _tracker.StopAll();
    }

    [RelayCommand]
    private void OpenSettings(SettingsCategory category)
    {
        SelectedSettingsCategory = category;
        SelectedSection = AppSection.Settings;
    }

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}

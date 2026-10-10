using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public sealed record AutomationSeed(IReadOnlyList<Guid> AccountIds, JoinMode Mode, string Target);

public partial class AutomationViewModel : ObservableObject
{
    private readonly IAutomationRepository _repository;
    private readonly IAccountRepository _accounts;
    private readonly IShellCoordinator _shell;
    private readonly IServerLinkResolver? _serverLinks;
    private readonly Func<DateTime> _clock;
    private readonly IGroupRepository? _groups;
    private readonly IFavoriteRepository? _favorites;
    private readonly Func<Guid, bool> _isRunning;
    private readonly Func<long, CancellationToken, Task<string?>>? _lookUpGameName;
    private readonly Func<AutomationSeed?>? _seed;
    private readonly Dictionary<long, string> _gameNames = new();
    private DispatcherTimer? _timer;
    private bool _ticking;

    public AutomationViewModel(
        IAutomationRepository repository,
        IAccountRepository accounts,
        IShellCoordinator shell,
        IServerLinkResolver? serverLinks = null,
        Func<DateTime>? clock = null,
        IGroupRepository? groups = null,
        IFavoriteRepository? favorites = null,
        Func<Guid, bool>? isRunning = null,
        Func<long, CancellationToken, Task<string?>>? lookUpGameName = null,
        Func<AutomationSeed?>? seed = null)
    {
        _repository = repository;
        _accounts = accounts;
        _shell = shell;
        _serverLinks = serverLinks;
        _clock = clock ?? (() => DateTime.Now);
        _groups = groups;
        _favorites = favorites;
        _isRunning = isRunning ?? (_ => false);
        _lookUpGameName = lookUpGameName;
        _seed = seed;

        foreach (Automation automation in repository.Automations)
            Items.Add(new AutomationItemViewModel(this, automation));

        selected = Items.FirstOrDefault();
        RebuildPicks();
        RefreshTimes();
    }

    public ObservableCollection<AutomationItemViewModel> Items { get; } = new();

    public BulkObservableCollection<AccountPickViewModel> AccountPicks { get; } = new();

    public BulkObservableCollection<GroupPickViewModel> GroupPicks { get; } = new();

    public BulkObservableCollection<FavoriteGame> Favorites { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private AutomationItemViewModel? selected;

    [ObservableProperty] private string accountSearch = string.Empty;

    public bool HasSelection => Selected != null;

    public bool IsEmpty => Items.Count == 0;

    public bool HasAccounts => AccountPicks.Count > 0;

    public bool HasGroups => GroupPicks.Count > 0;

    public bool HasFavorites => Favorites.Count > 0;

    public bool NoAccountMatches => AccountPicks.Count > 0 && AccountPicks.All(p => !p.IsVisible);

    public string PickSummary
    {
        get
        {
            int chosen = AccountPicks.Count(p => p.IsSelected);
            return AccountPicks.Count == 0 ? string.Empty : $"{chosen} of {AccountPicks.Count} selected";
        }
    }

    internal IAutomationRepository Repository => _repository;

    internal DateTime Now => _clock();

    public event Action<AutomationItemViewModel>? Created;

    partial void OnSelectedChanged(AutomationItemViewModel? value)
    {
        AccountSearch = string.Empty;
        RebuildPicks();
    }

    partial void OnAccountSearchChanged(string value) => ApplyAccountFilter();

    public void Refresh()
    {
        RebuildGroups();
        RebuildFavorites();
        RebuildPicks();
        foreach (AutomationItemViewModel item in Items)
            item.RefreshSummary();
        RefreshRunning();
        RefreshTimes();
    }

    public void RefreshRunning()
    {
        foreach (AutomationItemViewModel item in Items)
            item.RefreshRunning(_isRunning);
        foreach (AccountPickViewModel pick in AccountPicks)
            pick.IsRunning = _isRunning(pick.Id);
    }

    internal void RefreshTimes()
    {
        DateTime now = _clock();
        foreach (AutomationItemViewModel item in Items)
            item.RefreshTimes(now);
    }

    [RelayCommand]
    private void NewAutomation()
    {
        AutomationSeed? seed = _seed?.Invoke();
        var existing = new HashSet<Guid>(_accounts.All.Select(a => a.Id));
        string target = seed?.Target?.Trim() ?? string.Empty;
        var automation = new Automation
        {
            Name = UniqueName("Automation", Items.Select(i => i.Name)),
            AccountIds = seed?.AccountIds.Where(existing.Contains).Distinct().ToList() ?? new List<Guid>(),
            Mode = string.IsNullOrEmpty(target) ? JoinMode.Home : seed!.Mode,
            Target = target
        };
        Add(automation);
    }

    internal void Duplicate(AutomationItemViewModel source)
    {
        Automation from = source.Model;
        var copy = new Automation
        {
            Name = UniqueName(from.Name + " copy", Items.Select(i => i.Name)),
            AccountIds = from.AccountIds.ToList(),
            Mode = from.Mode,
            Target = from.Target,
            TargetName = from.TargetName,
            ScheduleEnabled = false,
            StartOnSchedule = from.StartOnSchedule,
            StartTime = from.StartTime,
            Days = from.Days.ToList(),
            Stop = from.Stop,
            StopAfterMinutes = from.StopAfterMinutes,
            StopTime = from.StopTime
        };
        Add(copy, Items.IndexOf(source) + 1);
    }

    internal void Delete(AutomationItemViewModel item)
    {
        int index = Items.IndexOf(item);
        if (index < 0)
            return;

        _repository.Remove(item.Model);
        Items.RemoveAt(index);
        if (ReferenceEquals(Selected, item))
            Selected = Items.Count == 0 ? null : Items[Math.Clamp(index, 0, Items.Count - 1)];
        OnPropertyChanged(nameof(IsEmpty));

        _shell.Notify(NotificationId.AutomationRun, NotificationKind.Info, "Automation deleted",
            $"Deleted \"{item.Model.Name}\".", () =>
            {
                _repository.Insert(index, item.Model);
                Items.Insert(Math.Clamp(index, 0, Items.Count), item);
                Selected = item;
                OnPropertyChanged(nameof(IsEmpty));
            });
    }

    private void Add(Automation automation, int? index = null)
    {
        int at = Math.Clamp(index ?? Items.Count, 0, Items.Count);
        _repository.Insert(at, automation);
        var item = new AutomationItemViewModel(this, automation);
        Items.Insert(at, item);
        item.RefreshRunning(_isRunning);
        item.RefreshTimes(_clock());
        Selected = item;
        OnPropertyChanged(nameof(IsEmpty));
        _ = LookUpNameAsync(item);
        Created?.Invoke(item);
    }

    [RelayCommand]
    private void SelectAllAccounts()
    {
        if (Selected is not { } item)
            return;
        List<AccountPickViewModel> visible = AccountPicks.Where(p => p.IsVisible).ToList();
        item.SetAccounts(visible.Select(p => p.Id), true);
        foreach (AccountPickViewModel pick in visible)
            pick.SetSilently(true);
        OnPicksChanged();
    }

    [RelayCommand]
    private void SelectNoAccounts()
    {
        if (Selected is not { } item)
            return;
        List<AccountPickViewModel> visible = AccountPicks.Where(p => p.IsVisible).ToList();
        item.SetAccounts(visible.Select(p => p.Id), false);
        foreach (AccountPickViewModel pick in visible)
            pick.SetSilently(false);
        OnPicksChanged();
    }

    [RelayCommand]
    private void ToggleGroup(GroupPickViewModel? group)
    {
        if (Selected is not { } item || group == null)
            return;

        bool select = !group.IsComplete;
        item.SetAccounts(group.MemberIds, select);
        foreach (AccountPickViewModel pick in AccountPicks.Where(p => group.MemberIds.Contains(p.Id)))
            pick.SetSilently(select);
        OnPicksChanged();
    }

    [RelayCommand]
    private void UseLaunchBarTarget()
    {
        if (Selected is not { } item || _seed?.Invoke() is not { } seed)
            return;
        string target = seed.Target?.Trim() ?? string.Empty;
        item.SetDestination(string.IsNullOrEmpty(target) ? JoinMode.Home : seed.Mode, target, null);
    }

    [RelayCommand]
    private void UseFavorite(FavoriteGame? favorite)
    {
        if (Selected is not { } item || favorite == null)
            return;

        if (favorite.ServerLink is { } serverLink)
            item.SetDestination(JoinMode.PrivateByJobId, serverLink, favorite.Name);
        else
            item.SetDestination(JoinMode.PublicByLink, favorite.PlaceId.ToString(CultureInfo.InvariantCulture), favorite.Name);
    }

    internal void OnPickChanged(AccountPickViewModel pick)
    {
        Selected?.SetAccounts(new[] { pick.Id }, pick.IsSelected);
        OnPicksChanged();
    }

    private void OnPicksChanged()
    {
        RefreshGroupStates();
        OnPropertyChanged(nameof(PickSummary));
    }

    private void RebuildPicks()
    {
        var picks = new List<AccountPickViewModel>();
        if (Selected is { } item)
        {
            var chosen = new HashSet<Guid>(item.Model.AccountIds);
            foreach (Account account in _accounts.All.OrderBy(a => a.SortOrder))
                picks.Add(new AccountPickViewModel(this, account, chosen.Contains(account.Id), _isRunning(account.Id)));
        }
        AccountPicks.Reset(picks);
        ApplyAccountFilter();
        RefreshGroupStates();
        OnPropertyChanged(nameof(HasAccounts));
        OnPropertyChanged(nameof(PickSummary));
    }

    private void ApplyAccountFilter()
    {
        string query = AccountSearch?.Trim() ?? string.Empty;
        foreach (AccountPickViewModel pick in AccountPicks)
            pick.IsVisible = query.Length == 0
                             || pick.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                             || pick.Username.Contains(query, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(NoAccountMatches));
    }

    private void RebuildGroups()
    {
        var groups = new List<GroupPickViewModel>();
        if (_groups != null)
        {
            foreach (AccountGroup group in _groups.All.OrderBy(g => g.SortOrder))
            {
                Guid[] members = _accounts.All.Where(a => a.BelongsTo(group.Id)).Select(a => a.Id).ToArray();
                if (members.Length > 0)
                    groups.Add(new GroupPickViewModel(group, members));
            }
        }
        GroupPicks.Reset(groups);
        RefreshGroupStates();
        OnPropertyChanged(nameof(HasGroups));
    }

    private void RefreshGroupStates()
    {
        var chosen = Selected == null ? new HashSet<Guid>() : new HashSet<Guid>(Selected.Model.AccountIds);
        foreach (GroupPickViewModel group in GroupPicks)
            group.IsComplete = group.MemberIds.All(chosen.Contains);
    }

    private void RebuildFavorites()
    {
        Favorites.Reset(_favorites?.All
            .OrderByDescending(f => f.IsPrimary)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<FavoriteGame>());
        OnPropertyChanged(nameof(HasFavorites));
    }

    internal int CountExisting(IEnumerable<Guid> ids)
    {
        var existing = new HashSet<Guid>(_accounts.All.Select(a => a.Id));
        return ids.Count(existing.Contains);
    }

    public void Start()
    {
        if (_timer != null)
            return;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
    }

    public void Stop() => _timer?.Stop();

    internal async Task TickAsync()
    {
        if (_ticking)
            return;
        _ticking = true;
        try
        {
            DateTime now = _clock();
            foreach (AutomationItemViewModel item in Items.ToList())
            {
                if (ScheduleClock.DueStop(item.Model, now) != null)
                    StopAutomation(item, scheduled: true);

                if (ScheduleClock.DueStart(item.Model, now) != null && await RunAsync(item, manual: false))
                {
                    item.Model.LastStartLocal = now;
                    _repository.Save();
                }
            }

            RefreshTimes();
        }
        finally
        {
            _ticking = false;
        }
    }

    internal async Task<bool> RunAsync(AutomationItemViewModel item, bool manual)
    {
        Automation model = item.Model;
        IReadOnlyList<Account> accounts = AccountsFor(model.AccountIds);
        if (accounts.Count == 0)
        {
            Report(model.Name, "Pick at least one account for this automation.", error: true);
            return true;
        }

        if (_shell.IsBusy)
        {
            if (manual)
                Report(model.Name, "Another launch is still running. Try again when it's done.", error: true);
            return manual;
        }

        ServerTargetResolution resolution = await ResolveAsync(model.Mode, model.Target, accounts);
        if (resolution.Target is not { } target)
        {
            Report(model.Name, resolution.Error, error: true);
            return true;
        }

        model.LastStartLocal = _clock();
        _repository.Save();
        item.RefreshTimes(model.LastStartLocal.Value);

        Report(model.Name, accounts.Count == 1 ? "Starting 1 account." : $"Starting {accounts.Count} accounts.", error: false);
        _ = _shell.LaunchAsync(accounts, target);
        return true;
    }

    internal void StopAutomation(AutomationItemViewModel item, bool scheduled)
    {
        Automation model = item.Model;
        Guid[] ids = model.AccountIds.ToArray();
        int running = ids.Count(_isRunning);
        _shell.StopAccounts(ids);

        DateTime now = _clock();
        model.LastStopLocal = now;
        if (model.Stop == AutomationStop.AfterMinutes && model.LastStartLocal is DateTime started
            && started.AddMinutes(model.StopAfterMinutes) > now)
            model.LastStopLocal = started.AddMinutes(model.StopAfterMinutes);
        _repository.Save();
        item.RefreshTimes(now);

        if (running > 0)
            Report(model.Name, running == 1 ? "Closed 1 account." : $"Closed {running} accounts.", error: false);
        else if (!scheduled)
            Report(model.Name, "None of its accounts were running.", error: false);
    }

    private IReadOnlyList<Account> AccountsFor(IEnumerable<Guid> ids)
    {
        var wanted = new HashSet<Guid>(ids);
        return _accounts.All.Where(a => wanted.Contains(a.Id)).OrderBy(a => a.SortOrder).ToList();
    }

    private async Task<ServerTargetResolution> ResolveAsync(JoinMode mode, string? input, IReadOnlyList<Account> accounts)
    {
        const string unreadable = "The game or server couldn't be read. Check the link or Place ID.";

        if (mode == JoinMode.Home || string.IsNullOrWhiteSpace(input))
            return ServerTargetResolution.Success(ServerTarget.Home());
        if (mode == JoinMode.PublicByLink)
            return GameLinkParser.TryParsePlaceId(input, out long placeId)
                ? ServerTargetResolution.Success(ServerTarget.Public(placeId))
                : ServerTargetResolution.Failure(unreadable);
        if (_serverLinks == null)
            return ServerTargetResolution.Failure(unreadable);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            return await _serverLinks.ResolveAsync(input, accounts, timeout.Token);
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or OperationCanceledException)
        {
            return ServerTargetResolution.Failure(unreadable);
        }
    }

    internal static long? PlaceIdOf(JoinMode mode, string? target) =>
        mode != JoinMode.Home && GameLinkParser.TryParsePlaceId(target, out long id) ? id : null;

    internal async Task LookUpNameAsync(AutomationItemViewModel item)
    {
        if (PlaceIdOf(item.Mode, item.Target) is not long placeId)
            return;

        string? known = _favorites?.All.FirstOrDefault(f => f.PlaceId == placeId)?.Name
                        ?? _gameNames.GetValueOrDefault(placeId);
        if (known != null)
        {
            item.SetTargetName(placeId, known);
            return;
        }
        if (_lookUpGameName == null)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string? name = await _lookUpGameName(placeId, timeout.Token);
            if (string.IsNullOrWhiteSpace(name))
                return;
            name = LaunchPanelViewModel.CleanGameName(name);
            _gameNames[placeId] = name;
            item.SetTargetName(placeId, name);
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or OperationCanceledException)
        {
        }
    }

    private void Report(string name, string message, bool error) =>
        _shell.Notify(NotificationId.AutomationRun, error ? NotificationKind.Error : NotificationKind.Info,
            error ? $"\"{name}\" didn't run" : $"\"{name}\"", message);

    internal static string UniqueName(string baseName, IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseName))
            return baseName;
        for (int i = 2; ; i++)
        {
            string candidate = $"{baseName} {i}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }
}

public sealed partial class AutomationItemViewModel : ObservableObject
{
    private static readonly (DayOfWeek Day, string Label)[] Week =
    {
        (DayOfWeek.Monday, "Mon"), (DayOfWeek.Tuesday, "Tue"), (DayOfWeek.Wednesday, "Wed"),
        (DayOfWeek.Thursday, "Thu"), (DayOfWeek.Friday, "Fri"), (DayOfWeek.Saturday, "Sat"), (DayOfWeek.Sunday, "Sun")
    };

    private static readonly DayOfWeek[] Weekdays =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

    private static readonly DayOfWeek[] Weekend = { DayOfWeek.Saturday, DayOfWeek.Sunday };

    private readonly AutomationViewModel _owner;
    private bool _loading;

    public AutomationItemViewModel(AutomationViewModel owner, Automation model)
    {
        _owner = owner;
        Model = model;
        _loading = true;
        name = model.Name;
        mode = model.Mode;
        target = model.Target;
        scheduleEnabled = model.ScheduleEnabled;
        startOnSchedule = model.StartOnSchedule;
        startTime = model.StartTime;
        stopMode = model.Stop;
        stopAfterMinutes = model.StopAfterMinutes;
        stopTime = model.StopTime;
        startTimeIsValid = ScheduleClock.TryParseTime(model.StartTime, out _);
        stopTimeIsValid = ScheduleClock.TryParseTime(model.StopTime, out _);
        foreach ((DayOfWeek day, string label) in Week)
            Days.Add(new DayToggleViewModel(this, day, label, model.Days.Contains(day)));
        _loading = false;
    }

    public Automation Model { get; }

    public ObservableCollection<DayToggleViewModel> Days { get; } = new();

    [ObservableProperty] private string name;
    [ObservableProperty] private JoinMode mode;
    [ObservableProperty] private string target;
    [ObservableProperty] private bool scheduleEnabled;
    [ObservableProperty] private bool startOnSchedule;
    [ObservableProperty] private string startTime;
    [ObservableProperty] private AutomationStop stopMode;
    [ObservableProperty] private int stopAfterMinutes;
    [ObservableProperty] private string stopTime;
    [ObservableProperty] private bool startTimeIsValid;
    [ObservableProperty] private bool stopTimeIsValid;
    [ObservableProperty] private string nextText = string.Empty;
    [ObservableProperty] private int runningCount;

    public bool NeedsTarget => Mode != JoinMode.Home;

    public string TargetHint => Mode == JoinMode.PrivateByJobId
        ? "Server link, private server link or Job ID link"
        : "Game link or Place ID";

    public string? TargetProblem
    {
        get
        {
            string input = Target?.Trim() ?? string.Empty;
            if (Mode == JoinMode.Home || input.Length == 0)
                return null;
            if (Mode == JoinMode.PublicByLink)
                return GameLinkParser.TryParsePlaceId(input, out _) ? null : "That's not a game link or Place ID.";
            return GameLinkParser.LooksLikeServerLink(input) || GameLinkParser.TryParsePlaceId(input, out _)
                ? null
                : "Paste a server link, a private server link or a link with a Job ID.";
        }
    }

    public bool HasTargetProblem => TargetProblem != null;

    public string DestinationText
    {
        get
        {
            string input = Target?.Trim() ?? string.Empty;
            if (Mode == JoinMode.Home || input.Length == 0)
                return "Roblox home screen";
            if (!string.IsNullOrWhiteSpace(Model.TargetName))
                return Mode == JoinMode.PrivateByJobId ? Model.TargetName + " (server)" : Model.TargetName;
            if (Mode == JoinMode.PrivateByJobId)
                return "A specific server";
            return GameLinkParser.TryParsePlaceId(input, out long id) ? $"Place {id}" : "Unknown game";
        }
    }

    public string AccountSummary
    {
        get
        {
            int count = _owner.CountExisting(Model.AccountIds);
            return count switch { 0 => "No accounts", 1 => "1 account", _ => $"{count} accounts" };
        }
    }

    public string ListSubtitle => $"{AccountSummary} · {DestinationText}";

    public string ScheduleText
    {
        get
        {
            if (!ScheduleEnabled)
                return "Runs when you start it";

            string days = DaysText(Model.Days);
            string? start = StartOnSchedule && StartTimeIsValid ? Model.StartTime : null;
            string? stop = StopMode switch
            {
                AutomationStop.AtTime when StopTimeIsValid => start != null ? "–" + Model.StopTime : "closes at " + Model.StopTime,
                AutomationStop.AfterMinutes when start != null => " for " + Duration(Model.StopAfterMinutes),
                _ => null
            };
            if (start == null && stop == null)
                return "Schedule incomplete";
            return $"{days} · {start}{stop}";
        }
    }

    public bool HasScheduleProblem => ScheduleEnabled && !StartOnSchedule && StopMode != AutomationStop.AtTime;

    public bool IsStopAfter => StopMode == AutomationStop.AfterMinutes;

    public bool IsStopAt => StopMode == AutomationStop.AtTime;

    public bool IsRunning => RunningCount > 0;

    public string RunningText => RunningCount == 1 ? "1 running" : $"{RunningCount} running";

    partial void OnNameChanged(string value)
    {
        Model.Name = string.IsNullOrWhiteSpace(value) ? "Automation" : value.Trim();
        Save();
    }

    partial void OnModeChanged(JoinMode value)
    {
        Model.Mode = value;
        OnPropertyChanged(nameof(NeedsTarget));
        OnPropertyChanged(nameof(TargetHint));
        OnTargetDetailsChanged();
        Save();
    }

    partial void OnTargetChanged(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (AutomationViewModel.PlaceIdOf(Mode, trimmed) != AutomationViewModel.PlaceIdOf(Mode, Model.Target))
            Model.TargetName = null;
        Model.Target = trimmed;
        OnTargetDetailsChanged();
        Save();
        if (!_loading)
            _ = _owner.LookUpNameAsync(this);
    }

    partial void OnScheduleEnabledChanged(bool value)
    {
        Model.ScheduleEnabled = value;
        Save();
    }

    partial void OnStartOnScheduleChanged(bool value)
    {
        Model.StartOnSchedule = value;
        OnPropertyChanged(nameof(HasScheduleProblem));
        Save();
    }

    partial void OnStartTimeChanged(string value)
    {
        StartTimeIsValid = ScheduleClock.TryParseTime(value, out TimeSpan parsed);
        if (StartTimeIsValid)
            Model.StartTime = Format(parsed);
        Save();
    }

    partial void OnStopModeChanged(AutomationStop value)
    {
        Model.Stop = value;
        OnPropertyChanged(nameof(IsStopAfter));
        OnPropertyChanged(nameof(IsStopAt));
        OnPropertyChanged(nameof(HasScheduleProblem));
        Save();
    }

    partial void OnStopAfterMinutesChanged(int value)
    {
        int normalized = Math.Clamp(value, 1, 24 * 60);
        if (normalized != value)
        {
            StopAfterMinutes = normalized;
            return;
        }
        Model.StopAfterMinutes = normalized;
        Save();
    }

    partial void OnStopTimeChanged(string value)
    {
        StopTimeIsValid = ScheduleClock.TryParseTime(value, out TimeSpan parsed);
        if (StopTimeIsValid)
            Model.StopTime = Format(parsed);
        Save();
    }

    [RelayCommand]
    private Task Run() => _owner.RunAsync(this, manual: true);

    [RelayCommand]
    private void StopNow() => _owner.StopAutomation(this, scheduled: false);

    [RelayCommand]
    private void Duplicate() => _owner.Duplicate(this);

    [RelayCommand]
    private void Delete() => _owner.Delete(this);

    [RelayCommand]
    private void PickEveryDay() => SetDays(Array.Empty<DayOfWeek>());

    [RelayCommand]
    private void PickWeekdays() => SetDays(Weekdays);

    [RelayCommand]
    private void PickWeekends() => SetDays(Weekend);

    internal void SetDay(DayOfWeek day, bool on)
    {
        if (_loading)
            return;
        if (on && !Model.Days.Contains(day))
            Model.Days.Add(day);
        else if (!on)
            Model.Days.Remove(day);
        Save();
    }

    private void SetDays(IReadOnlyCollection<DayOfWeek> days)
    {
        _loading = true;
        foreach (DayToggleViewModel toggle in Days)
            toggle.IsChecked = days.Contains(toggle.Day);
        _loading = false;
        Model.Days = days.ToList();
        Save();
    }

    internal void SetAccounts(IEnumerable<Guid> ids, bool on)
    {
        foreach (Guid id in ids)
        {
            bool contains = Model.AccountIds.Contains(id);
            if (on && !contains)
                Model.AccountIds.Add(id);
            else if (!on && contains)
                Model.AccountIds.Remove(id);
        }
        Save();
    }

    internal void SetDestination(JoinMode newMode, string newTarget, string? knownName)
    {
        Mode = newMode;
        Target = newTarget;
        if (knownName != null && AutomationViewModel.PlaceIdOf(newMode, newTarget) is long id)
            SetTargetName(id, knownName);
    }

    internal void SetTargetName(long placeId, string name)
    {
        if (AutomationViewModel.PlaceIdOf(Mode, Target) != placeId || Model.TargetName == name)
            return;
        Model.TargetName = name;
        OnTargetDetailsChanged();
        Save();
    }

    internal void RefreshSummary()
    {
        OnPropertyChanged(nameof(AccountSummary));
        OnPropertyChanged(nameof(DestinationText));
        OnPropertyChanged(nameof(ListSubtitle));
        OnPropertyChanged(nameof(ScheduleText));
        OnPropertyChanged(nameof(HasScheduleProblem));
    }

    internal void RefreshRunning(Func<Guid, bool> isRunning)
    {
        RunningCount = Model.AccountIds.Count(isRunning);
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(RunningText));
    }

    internal void RefreshTimes(DateTime now)
    {
        if (!Model.ScheduleEnabled)
            NextText = "Runs only when you click Run.";
        else if ((Model.StartOnSchedule && !StartTimeIsValid) || (Model.Stop == AutomationStop.AtTime && !StopTimeIsValid))
            NextText = "Enter times like 18:30.";
        else if (HasScheduleProblem)
            NextText = "Turn on the start time, or choose a stop time.";
        else
        {
            var parts = new List<string>();
            if (ScheduleClock.NextStart(Model, now) is DateTime start)
                parts.Add("Starts " + ScheduleClock.Describe(start, now));
            if (ScheduleClock.NextStop(Model, now) is DateTime stop)
                parts.Add((parts.Count == 0 ? "Closes " : "closes ") + ScheduleClock.Describe(stop, now));
            NextText = parts.Count == 0 ? "Nothing scheduled." : string.Join(", ", parts) + ".";
        }
    }

    public override string ToString() => Name;

    private void OnTargetDetailsChanged()
    {
        OnPropertyChanged(nameof(TargetProblem));
        OnPropertyChanged(nameof(HasTargetProblem));
        OnPropertyChanged(nameof(DestinationText));
        OnPropertyChanged(nameof(ListSubtitle));
    }

    private void Save()
    {
        if (_loading)
            return;
        _owner.Repository.Save();
        RefreshSummary();
        RefreshTimes(_owner.Now);
    }

    private static string Format(TimeSpan time) => $"{time.Hours:00}:{time.Minutes:00}";

    internal static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes} min"
        : minutes % 60 == 0 ? $"{minutes / 60} h"
        : $"{minutes / 60} h {minutes % 60} min";

    internal static string DaysText(List<DayOfWeek> days)
    {
        if (days.Count is 0 or 7)
            return "Every day";
        if (days.Count == 5 && Weekdays.All(days.Contains))
            return "Weekdays";
        if (days.Count == 2 && Weekend.All(days.Contains))
            return "Weekends";
        return string.Join(", ", Week.Where(w => days.Contains(w.Day)).Select(w => w.Label));
    }
}

public sealed partial class DayToggleViewModel : ObservableObject
{
    private readonly AutomationItemViewModel _owner;

    public DayToggleViewModel(AutomationItemViewModel owner, DayOfWeek day, string label, bool isChecked)
    {
        _owner = owner;
        Day = day;
        Label = label;
        this.isChecked = isChecked;
    }

    public DayOfWeek Day { get; }

    public string Label { get; }

    [ObservableProperty] private bool isChecked;

    partial void OnIsCheckedChanged(bool value) => _owner.SetDay(Day, value);
}

public sealed partial class AccountPickViewModel : ObservableObject
{
    private readonly AutomationViewModel _owner;
    private bool _silent;

    public AccountPickViewModel(AutomationViewModel owner, Account account, bool selected, bool running)
    {
        _owner = owner;
        Id = account.Id;
        Label = account.DisplayLabel;
        Username = string.IsNullOrWhiteSpace(account.Username) ? string.Empty : "@" + account.Username;
        string source = string.IsNullOrWhiteSpace(Label) ? account.Username : Label;
        Initials = string.IsNullOrWhiteSpace(source) ? "?" : source.Trim()[..1].ToUpperInvariant();
        isSelected = selected;
        isRunning = running;
    }

    public Guid Id { get; }

    public string Label { get; }

    public string Username { get; }

    public string Initials { get; }

    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isVisible = true;
    [ObservableProperty] private bool isRunning;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_silent)
            _owner.OnPickChanged(this);
    }

    internal void SetSilently(bool value)
    {
        _silent = true;
        IsSelected = value;
        _silent = false;
    }
}

public sealed partial class GroupPickViewModel : ObservableObject
{
    public GroupPickViewModel(AccountGroup group, IReadOnlyCollection<Guid> memberIds)
    {
        Name = group.Name;
        ColorHex = group.ColorHex;
        MemberIds = new HashSet<Guid>(memberIds);
    }

    public string Name { get; }

    public string ColorHex { get; }

    public HashSet<Guid> MemberIds { get; }

    public string CountText => MemberIds.Count.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty] private bool isComplete;
}

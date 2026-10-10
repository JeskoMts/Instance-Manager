using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AutomationViewModelTests
{
    private static readonly DateTime Monday = new(2026, 9, 28);

    private readonly Account _alpha = new() { UserId = 1, Username = "alpha", DisplayName = "Alpha", SortOrder = 0 };
    private readonly Account _beta = new() { UserId = 2, Username = "beta", DisplayName = "Beta", SortOrder = 1 };
    private readonly FakeAutomationRepository _repository = new();
    private readonly FakeShell _shell = new();
    private readonly HashSet<Guid> _running = new();
    private DateTime _now = Monday.AddHours(9);
    private AutomationSeed? _seed;

    [Fact]
    public async Task ScheduledStart_LaunchesOnceAndRemembersTheRun()
    {
        Automation automation = Add(JoinMode.PublicByLink, "606849621", _beta);
        automation.ScheduleEnabled = true;
        automation.StartTime = "09:00";
        AutomationViewModel vm = Create();

        await vm.TickAsync();
        await vm.TickAsync();

        (IReadOnlyList<Account> accounts, ServerTarget target) = Assert.Single(_shell.Launches);
        Assert.Equal(new[] { _beta.Id }, accounts.Select(a => a.Id));
        Assert.Equal(JoinMode.PublicByLink, target.Mode);
        Assert.Equal(606849621, target.PlaceId);
        Assert.Equal(_now, automation.LastStartLocal);
        Assert.True(_repository.SaveCount > 0);
    }

    [Fact]
    public async Task StopAfterMinutes_ClosesTheSameAccountsLater()
    {
        Automation automation = Add(JoinMode.Home, "", _alpha, _beta);
        automation.ScheduleEnabled = true;
        automation.StartTime = "09:00";
        automation.Stop = AutomationStop.AfterMinutes;
        automation.StopAfterMinutes = 30;
        AutomationViewModel vm = Create();

        await vm.TickAsync();
        Assert.Equal(JoinMode.Home, Assert.Single(_shell.Launches).Target.Mode);
        Assert.Empty(_shell.Stops);

        _now = _now.AddMinutes(29);
        await vm.TickAsync();
        Assert.Empty(_shell.Stops);

        _now = _now.AddMinutes(2);
        await vm.TickAsync();
        Assert.Equal(new[] { _alpha.Id, _beta.Id }, Assert.Single(_shell.Stops));

        _now = _now.AddMinutes(1);
        await vm.TickAsync();
        Assert.Single(_shell.Stops);
    }

    [Fact]
    public async Task StopAtTime_WithoutAStart_OnlyClosesTheAccounts()
    {
        Automation automation = Add(JoinMode.PublicByLink, "1", _alpha);
        automation.ScheduleEnabled = true;
        automation.StartOnSchedule = false;
        automation.Stop = AutomationStop.AtTime;
        automation.StopTime = "09:00";
        _running.Add(_alpha.Id);
        AutomationViewModel vm = Create();

        await vm.TickAsync();

        Assert.Empty(_shell.Launches);
        Assert.Equal(new[] { _alpha.Id }, Assert.Single(_shell.Stops));
        Assert.Contains(_shell.Notices, n => n.Kind == NotificationKind.Info && n.Message == "Closed 1 account.");
    }

    [Fact]
    public async Task BusyShell_RetriesTheScheduledStartOnTheNextTick()
    {
        Automation automation = Add(JoinMode.Home, "", _alpha);
        automation.ScheduleEnabled = true;
        automation.StartTime = "09:00";
        AutomationViewModel vm = Create();
        _shell.IsBusy = true;

        await vm.TickAsync();
        Assert.Empty(_shell.Launches);
        Assert.Null(automation.LastStartLocal);

        _shell.IsBusy = false;
        _now = _now.AddMinutes(1);
        await vm.TickAsync();
        Assert.Single(_shell.Launches);
    }

    [Fact]
    public async Task ScheduleWithoutAccounts_ReportsOnceAndDoesNotLaunch()
    {
        Automation automation = Add(JoinMode.Home, "");
        automation.Name = "Empty";
        automation.ScheduleEnabled = true;
        automation.StartTime = "09:00";
        AutomationViewModel vm = Create();

        await vm.TickAsync();
        _now = _now.AddSeconds(15);
        await vm.TickAsync();

        Assert.Empty(_shell.Launches);
        (NotificationKind kind, string title, _) = Assert.Single(_shell.Notices);
        Assert.Equal(NotificationKind.Error, kind);
        Assert.Contains("Empty", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_StartsTheAccountsInListOrderWithTheGameTarget()
    {
        Add(JoinMode.PublicByLink, "https://www.roblox.com/games/920587237/Adopt-Me", _beta, _alpha);
        AutomationViewModel vm = Create();

        await vm.Items.Single().RunCommand.ExecuteAsync(null);

        (IReadOnlyList<Account> accounts, ServerTarget target) = Assert.Single(_shell.Launches);
        Assert.Equal(new[] { _alpha.Id, _beta.Id }, accounts.Select(a => a.Id));
        Assert.Equal(920587237, target.PlaceId);
    }

    [Fact]
    public async Task ManualClose_SettlesAPendingStopAfter()
    {
        Automation automation = Add(JoinMode.Home, "", _alpha);
        automation.ScheduleEnabled = true;
        automation.StartTime = "09:00";
        automation.Stop = AutomationStop.AfterMinutes;
        automation.StopAfterMinutes = 30;
        AutomationViewModel vm = Create();
        await vm.TickAsync();

        _now = _now.AddMinutes(5);
        vm.Items.Single().StopNowCommand.Execute(null);
        _now = _now.AddMinutes(26);
        await vm.TickAsync();

        Assert.Single(_shell.Stops);
    }

    [Fact]
    public void NewAutomation_TakesTheSelectedAccountsAndTheLaunchBarTarget()
    {
        _seed = new AutomationSeed(new[] { _beta.Id, Guid.NewGuid() }, JoinMode.PublicByLink, "606849621");
        AutomationViewModel vm = Create();

        vm.NewAutomationCommand.Execute(null);
        vm.NewAutomationCommand.Execute(null);

        Assert.Equal(new[] { "Automation", "Automation 2" }, vm.Items.Select(i => i.Name));
        Automation created = _repository.Automations[0];
        Assert.Equal(new[] { _beta.Id }, created.AccountIds);
        Assert.Equal("606849621", created.Target);
        Assert.Same(vm.Items[1], vm.Selected);
    }

    [Fact]
    public void NewAutomation_WithAnEmptyLaunchBar_GoesToTheHomeScreen()
    {
        _seed = new AutomationSeed(Array.Empty<Guid>(), JoinMode.PublicByLink, "  ");
        AutomationViewModel vm = Create();

        vm.NewAutomationCommand.Execute(null);

        Assert.Equal(JoinMode.Home, vm.Items.Single().Mode);
        Assert.Equal("Roblox home screen", vm.Items.Single().DestinationText);
    }

    [Fact]
    public void Delete_CanBeUndoneFromTheNotification()
    {
        Add(JoinMode.Home, "", _alpha).Name = "First";
        Add(JoinMode.Home, "", _beta).Name = "Second";
        AutomationViewModel vm = Create();

        vm.Items[0].DeleteCommand.Execute(null);
        Assert.Equal(new[] { "Second" }, vm.Items.Select(i => i.Name));
        Assert.Equal(new[] { "Second" }, _repository.Automations.Select(a => a.Name));

        _shell.LastUndo!.Invoke();
        Assert.Equal(new[] { "First", "Second" }, vm.Items.Select(i => i.Name));
        Assert.Equal(new[] { "First", "Second" }, _repository.Automations.Select(a => a.Name));
    }

    [Fact]
    public void Duplicate_CopiesEverythingButStartsWithTheScheduleOff()
    {
        Automation source = Add(JoinMode.PublicByLink, "42", _alpha, _beta);
        source.Name = "Farm";
        source.ScheduleEnabled = true;
        source.Days = new List<DayOfWeek> { DayOfWeek.Monday };
        AutomationViewModel vm = Create();

        vm.Items[0].DuplicateCommand.Execute(null);

        Automation copy = _repository.Automations[1];
        Assert.Equal("Farm copy", copy.Name);
        Assert.Equal(source.AccountIds, copy.AccountIds);
        Assert.Equal(new[] { DayOfWeek.Monday }, copy.Days);
        Assert.False(copy.ScheduleEnabled);
        Assert.NotEqual(source.Id, copy.Id);
    }

    [Fact]
    public void AccountPicks_FollowTheSelectedAutomationAndWriteThrough()
    {
        Add(JoinMode.Home, "", _alpha);
        AutomationViewModel vm = Create();

        Assert.Equal(new[] { true, false }, vm.AccountPicks.Select(p => p.IsSelected));
        Assert.Equal("1 of 2 selected", vm.PickSummary);

        vm.AccountPicks[1].IsSelected = true;
        Assert.Equal(new[] { _alpha.Id, _beta.Id }, _repository.Automations[0].AccountIds);

        vm.AccountSearch = "bet";
        Assert.Equal(new[] { false, true }, vm.AccountPicks.Select(p => p.IsVisible));
        vm.SelectNoAccountsCommand.Execute(null);
        Assert.Equal(new[] { _alpha.Id }, _repository.Automations[0].AccountIds);
    }

    [Fact]
    public void SchedulePresetsAndTexts_DescribeWhatHappens()
    {
        Automation automation = Add(JoinMode.Home, "", _alpha);
        AutomationViewModel vm = Create();
        AutomationItemViewModel item = vm.Items.Single();

        Assert.Equal("Runs when you start it", item.ScheduleText);

        item.ScheduleEnabled = true;
        item.StartTime = "18:00";
        foreach (DayToggleViewModel day in item.Days.Where(d => d.Day is not (DayOfWeek.Saturday or DayOfWeek.Sunday)))
            day.IsChecked = true;
        item.StopMode = AutomationStop.AtTime;
        item.StopTime = "23:00";

        Assert.Equal(5, automation.Days.Count);
        Assert.Equal("Weekdays · 18:00–23:00", item.ScheduleText);
        Assert.Equal("Starts today at 18:00, closes today at 23:00.", item.NextText);

        item.StopMode = AutomationStop.AfterMinutes;
        item.StopAfterMinutes = 90;
        Assert.Equal("Weekdays · 18:00 for 1 h 30 min", item.ScheduleText);

        item.StartTime = "25:00";
        Assert.False(item.StartTimeIsValid);
        Assert.Equal("18:00", automation.StartTime);
        Assert.Equal("Enter times like 18:30.", item.NextText);
    }

    [Fact]
    public void TargetProblems_AreShownForTheWrongKindOfLink()
    {
        Add(JoinMode.PublicByLink, "", _alpha);
        AutomationViewModel vm = Create();
        AutomationItemViewModel item = vm.Items.Single();

        item.Target = "not a game";
        Assert.True(item.HasTargetProblem);

        item.Target = "https://www.roblox.com/games/606849621/Jailbreak";
        Assert.False(item.HasTargetProblem);
        Assert.Equal("Place 606849621", item.DestinationText);

        item.Mode = JoinMode.PrivateByJobId;
        item.Target = "https://www.roblox.com/games/start?placeId=1&gameInstanceId=0f0b0a0e-1111-2222-3333-444455556666";
        Assert.False(item.HasTargetProblem);
    }

    [Fact]
    public void RunningCounts_ComeFromTheTracker()
    {
        Add(JoinMode.Home, "", _alpha, _beta);
        AutomationViewModel vm = Create();
        _running.Add(_beta.Id);

        vm.RefreshRunning();

        Assert.Equal(1, vm.Items[0].RunningCount);
        Assert.True(vm.Items[0].IsRunning);
        Assert.Equal(new[] { false, true }, vm.AccountPicks.Select(p => p.IsRunning));
    }

    private AutomationViewModel Create() =>
        new(_repository, new FakeAccountRepository(_alpha, _beta), _shell,
            clock: () => _now, isRunning: _running.Contains, seed: () => _seed);

    private Automation Add(JoinMode mode, string target, params Account[] accounts)
    {
        var automation = new Automation { Mode = mode, Target = target, AccountIds = accounts.Select(a => a.Id).ToList() };
        _repository.Automations.Add(automation);
        return automation;
    }

    private sealed class FakeAutomationRepository : IAutomationRepository
    {
        public List<Automation> Automations { get; } = new();
        public int SaveCount { get; private set; }

        IReadOnlyList<Automation> IAutomationRepository.Automations => Automations;

        public void Insert(int index, Automation automation) => Automations.Insert(Math.Clamp(index, 0, Automations.Count), automation);
        public void Remove(Automation automation) => Automations.Remove(automation);
        public void Save() => SaveCount++;
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> _accounts;

        public FakeAccountRepository(params Account[] accounts) => _accounts = accounts.ToList();

        public IReadOnlyList<Account> All => _accounts;
        public Account? FindByUserId(long userId) => _accounts.FirstOrDefault(a => a.UserId == userId);
        public void Upsert(Account account) { }
        public void Remove(Account account) { }
    }

    private sealed class FakeShell : IShellCoordinator
    {
        public List<(IReadOnlyList<Account> Accounts, ServerTarget Target)> Launches { get; } = new();
        public List<Guid[]> Stops { get; } = new();
        public List<(NotificationKind Kind, string Title, string Message)> Notices { get; } = new();
        public Action? LastUndo { get; private set; }
        public bool IsBusy { get; set; }

        public Task LaunchAsync(IReadOnlyList<Account> accounts) => Task.CompletedTask;

        public Task LaunchAsync(IReadOnlyList<Account> accounts, ServerTarget target)
        {
            Launches.Add((accounts, target));
            return Task.CompletedTask;
        }

        public void StopAccounts(IEnumerable<Guid> accountIds) => Stops.Add(accountIds.ToArray());

        public void SetStatus(string message) { }

        public void Notify(NotificationId id, NotificationKind kind, string title, string message, Action? undoAction = null, string? actionLabel = null)
        {
            if (undoAction != null)
                LastUndo = undoAction;
            else
                Notices.Add((kind, title, message));
        }
    }
}

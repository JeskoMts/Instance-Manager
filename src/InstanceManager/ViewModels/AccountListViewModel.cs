using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class AccountListViewModel : ObservableObject
{
    private static readonly string[] GroupPalette =
    {
        "#D9646C", "#D98E5A", "#D9C56A", "#A7C25E", "#6FB08A", "#5BAFA8",
        "#5AA6C9", "#6E8FD9", "#8A82D9", "#A56FC0", "#CF6FA8", "#8A8F98"
    };

    private readonly IAccountRepository _accounts;
    private readonly IGroupRepository _groups;
    private readonly IDialogService _dialogs;
    private readonly IShellCoordinator _shell;
    private readonly InstanceTracker _tracker;
    private readonly VersionBarViewModel? _versionBar;
    private readonly IRobloxAvatarService? _avatars;
    private readonly AutoReconnectService? _autoReconnect;

    private readonly Dictionary<Guid, AccountRowViewModel> _allRows = new();
    private readonly Dictionary<Guid, GroupViewModel> _groupRows = new();
    private GroupViewModel? _ungroupedRow;

    public AccountListViewModel(
        IAccountRepository accounts,
        IGroupRepository groups,
        IDialogService dialogs,
        IShellCoordinator shell,
        InstanceTracker tracker,
        VersionBarViewModel? versionBar = null,
        IRobloxAvatarService? avatars = null,
        AutoReconnectService? autoReconnect = null)
    {
        _accounts = accounts;
        _groups = groups;
        _dialogs = dialogs;
        _shell = shell;
        _tracker = tracker;
        _versionBar = versionBar;
        _avatars = avatars;
        _autoReconnect = autoReconnect;

        _tracker.RunningChanged += OnRunningChanged;

        if (_versionBar != null)
            _versionBar.Versions.CollectionChanged += (_, _) => RebuildVersionChoices();
        RebuildVersionChoices();
    }

    public ObservableCollection<GroupViewModel> Groups { get; } = new();

    public BulkObservableCollection<object> Rows { get; } = new();

    public ObservableCollection<AccountRowViewModel> RunningInstances { get; } = new();

    [ObservableProperty] private AccountRowViewModel? selectedInstanceToStop;

    partial void OnSelectedInstanceToStopChanged(AccountRowViewModel? value)
    {
        if (value == null) return;
        StopAccount(value);
        SelectedInstanceToStop = null;
    }

    private void RefreshRunningInstances()
    {
        var running = _allRows.Values
            .Where(r => r.IsRunning)
            .OrderBy(r => r.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
        RunningInstances.Clear();
        foreach (var row in running)
            RunningInstances.Add(row);
    }

    public ObservableCollection<VersionChoiceViewModel> VersionChoices { get; } = new();

    private void RebuildVersionChoices()
    {
        VersionChoices.Clear();
        VersionChoices.Add(new VersionChoiceViewModel(null));
        if (_versionBar != null)
        {
            foreach (RobloxVersion v in _versionBar.Versions)
                VersionChoices.Add(new VersionChoiceViewModel(v));
        }

        foreach (AccountRowViewModel row in _allRows.Values)
            row.RefreshVersionChoice();
    }

    internal void SaveAccountVersion(Account account) => _accounts.Upsert(account);

    internal void PersistGroupState(AccountGroup group) => _groups.Update(group);

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private int selectedCount;
    [ObservableProperty] private int accountCount;

    partial void OnSearchTextChanged(string value) => RebuildGroups();


    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);
    public bool ShowList => Groups.Count > 0;
    public bool ShowEmptyState => Groups.Count == 0 && !IsSearching;
    public bool ShowNoResults => Groups.Count == 0 && IsSearching;
    public bool HasSelection => SelectedCount > 0;

    public int RunningCount => _tracker.RunningCount;
    public bool HasRunning => RunningCount > 0;

    public IReadOnlyList<AccountGroup> GroupModels => _groups.All;

    public IReadOnlyList<Account> SelectedAccounts() =>
        _allRows.Values.Where(r => r.IsSelected).Select(r => r.Account).ToList();

    public void RecountSelection()
    {
        SelectedCount = _allRows.Values.Count(r => r.IsSelected);
        OnPropertyChanged(nameof(HasSelection));
    }

    private object? _openMenuOwner;

    public void NotifyMenuOpened(object owner)
    {
        if (ReferenceEquals(_openMenuOwner, owner)) return;
        (_openMenuOwner as AccountRowViewModel)?.CloseMenu();
        (_openMenuOwner as GroupViewModel)?.CloseMenu();
        _openMenuOwner = owner;
    }

    public void NotifyMenuClosed(object owner)
    {
        if (ReferenceEquals(_openMenuOwner, owner))
            _openMenuOwner = null;
    }

    public void CloseOpenMenu()
    {
        (_openMenuOwner as AccountRowViewModel)?.CloseMenu();
        (_openMenuOwner as GroupViewModel)?.CloseMenu();
        _openMenuOwner = null;
    }

    [RelayCommand]
    private void SelectAllVisible()
    {
        foreach (var row in Groups.SelectMany(g => g.Accounts))
            row.IsSelected = true;
        RecountSelection();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in _allRows.Values)
            row.IsSelected = false;
        RecountSelection();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;


    public Task LaunchAccountAsync(AccountRowViewModel row) =>
        _shell.LaunchAsync(new[] { row.Account });

    public void StopAccount(AccountRowViewModel row)
    {
        _autoReconnect?.NotifyManualStop(row.Account.Id);
        _tracker.Stop(row.Account.Id);
    }

    [RelayCommand]
    private void StopAllInstances()
    {
        int n = _tracker.RunningCount;
        if (n <= 0)
            return;
        if (!_dialogs.Confirm(ConfirmAction.StopAllInstances, $"Close {n} running instance{(n == 1 ? "" : "s")}?"))
            return;
        _autoReconnect?.NotifyManualStopAll();
        _tracker.StopAll();
    }

    public Task LaunchGroupAsync(GroupViewModel group)
    {
        var accounts = group.Group == null
            ? _accounts.All.Where(account => account.GroupIds.Count == 0).ToList()
            : _accounts.All.Where(account => account.BelongsTo(group.Group.Id))
                .GroupBy(account => account.Id).Select(items => items.First()).ToList();
        if (accounts.Count == 0)
        {
            _shell.Notify(NotificationId.GroupEmpty, NotificationKind.Error, "Group is empty", $"Group '{group.Name}' has no accounts.");
            return Task.CompletedTask;
        }
        return _shell.LaunchAsync(accounts);
    }


    public void RebuildGroups()
    {
        EnsureRows();
        Groups.Clear();

        bool searching = IsSearching;
        string needle = SearchText.Trim();
        var liveGroupIds = new HashSet<Guid>();

        bool Matches(AccountRowViewModel r) =>
            !searching ||
            r.DisplayLabel.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            r.Username.Contains(needle, StringComparison.OrdinalIgnoreCase);

        foreach (var group in _groups.All.OrderBy(g => g.SortOrder).ThenBy(g => g.Name))
        {
            liveGroupIds.Add(group.Id);
            if (!_groupRows.TryGetValue(group.Id, out GroupViewModel? gvm))
            {
                gvm = new GroupViewModel(group, this);
                gvm.PropertyChanged += OnGroupExpandedChanged;
                _groupRows[group.Id] = gvm;
            }
            else
                gvm.Accounts.Clear();
            foreach (var acc in _accounts.All.Where(a => a.BelongsTo(group.Id)).OrderBy(a => a.SortOrder))
            {
                var row = _allRows[acc.Id];
                row.IsRunning = _tracker.IsRunning(acc.Id);
                if (Matches(row))
                    gvm.Accounts.Add(row);
            }
            if (!searching || gvm.Accounts.Count > 0)
            {
            gvm.NotifyCountChanged();
                Groups.Add(gvm);
            }
        }

        foreach (Guid staleId in _groupRows.Keys.Where(id => !liveGroupIds.Contains(id)).ToList())
        {
            if (_groupRows.TryGetValue(staleId, out GroupViewModel? stale))
                stale.PropertyChanged -= OnGroupExpandedChanged;
            _groupRows.Remove(staleId);
        }

        if (_ungroupedRow == null)
        {
            _ungroupedRow = new GroupViewModel(null, this);
            _ungroupedRow.PropertyChanged += OnGroupExpandedChanged;
        }
        var ungrouped = _ungroupedRow;
        ungrouped.Accounts.Clear();
        foreach (var acc in _accounts.All.Where(a => a.GroupIds.Count == 0).OrderBy(a => a.SortOrder))
        {
            var row = _allRows[acc.Id];
            row.IsRunning = _tracker.IsRunning(acc.Id);
            if (Matches(row))
                ungrouped.Accounts.Add(row);
        }
        if (ungrouped.Accounts.Count > 0)
            Groups.Add(ungrouped);

        AccountCount = _accounts.All.Count;
        RecountSelection();
        ungrouped.NotifyCountChanged();
        NotifyStateFlags();
        RefreshRunningInstances();
        RebuildRows();
    }

    private void RebuildRows()
    {
        var rows = new List<object>(_allRows.Count + Groups.Count);
        foreach (var group in Groups)
        {
            if (group.HasGroup)
                rows.Add(group);
            if (group.IsExpanded)
                rows.AddRange(group.Accounts);
        }
        Rows.Reset(rows);
    }

    private void OnGroupExpandedChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupViewModel.IsExpanded))
            RebuildRows();
    }

    private void NotifyStateFlags()
    {
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private void EnsureRows()
    {
        var liveIds = new HashSet<Guid>(_accounts.All.Count);
        foreach (var acc in _accounts.All)
        {
            acc.NormalizeGroupMemberships();
            liveIds.Add(acc.Id);
            if (!_allRows.ContainsKey(acc.Id))
            {
                var row = new AccountRowViewModel(acc, this, _avatars);
                _allRows[acc.Id] = row;
                _ = row.LoadAvatarAsync();
            }
        }

        if (_allRows.Count != liveIds.Count)
        {
            var stale = _allRows.Keys.Where(id => !liveIds.Contains(id)).ToList();
            foreach (var id in stale)
                _allRows.Remove(id);
        }
    }


    [RelayCommand]
    private async Task AddAccount()
    {
        Account? account = await _dialogs.ShowAddAccountAsync();
        if (account == null) return;

        Account? existing = _accounts.FindByUserId(account.UserId);
        if (existing != null)
        {
            existing.EncryptedCookie = account.EncryptedCookie;
            existing.Username = account.Username;
            existing.DisplayName = account.DisplayName;
            _accounts.Upsert(existing);
            _shell.Notify(NotificationId.AccountUpdated, NotificationKind.Success, "Account updated", $"Updated account '{existing.DisplayLabel}'.");
        }
        else
        {
            account.SortOrder = _accounts.All.Count;
            _accounts.Upsert(account);
            _shell.Notify(NotificationId.AccountAdded, NotificationKind.Success, "Account added", $"Added account '{account.DisplayLabel}'.");
        }

        RebuildGroups();
    }

    public void RemoveAccount(AccountRowViewModel row)
    {
        if (!_dialogs.Confirm(ConfirmAction.RemoveAccount, $"Remove account '{row.DisplayLabel}'?"))
            return;

        var account = row.Account;
        _accounts.Remove(account);
        _allRows.Remove(account.Id);
        RebuildGroups();
        _shell.Notify(NotificationId.AccountRemoved, NotificationKind.Success, "Account removed", "The account was removed from InstanceManager.", () =>
        {
            _accounts.Upsert(account);
            RebuildGroups();
        });
    }

    public void RenameAccount(AccountRowViewModel row)
    {
        string? alias = _dialogs.Prompt("Rename account (alias)", row.Account.Alias ?? row.DisplayLabel);
        if (alias == null) return;

        string oldLabel = row.DisplayLabel;
        row.Account.Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        _accounts.Upsert(row.Account);
        row.RefreshLabel();
        RebuildGroups();
        if (!string.Equals(oldLabel, row.DisplayLabel, StringComparison.Ordinal))
        {
            _shell.Notify(NotificationId.AccountRenamed, NotificationKind.Success,
                "Account renamed", $"Renamed '{oldLabel}' to '{row.DisplayLabel}'.");
        }
    }

    public void MoveToGroup(AccountRowViewModel row, AccountGroup? group)
    {
        row.Account.GroupIds.Clear();
        if (group != null)
            row.Account.GroupIds.Add(group.Id);
        row.Account.GroupId = null;
        _accounts.Upsert(row.Account);
        row.RefreshGroupState();
        RebuildGroups();
        _shell.Notify(NotificationId.AccountGroupsUpdated, NotificationKind.Success,
            "Account groups updated", $"Moved '{row.DisplayLabel}' to {(group?.Name ?? "No group")}.");
        _shell.SetStatus($"'{row.DisplayLabel}' → {(group?.Name ?? "No group")}");
    }


    public void ClearGroups(AccountRowViewModel row)
    {
        if (!row.IsGrouped)
            return;

        if (!_dialogs.Confirm(ConfirmAction.ClearAccountGroups, $"Clear all groups from '{row.DisplayLabel}'?"))
            return;

        MoveToGroup(row, null);
    }


    public void ToggleGroupMembership(AccountRowViewModel row, AccountGroup group)
    {
        bool removing = row.Account.BelongsTo(group.Id);
        if (removing)
            row.Account.GroupIds.Remove(group.Id);
        else
            row.Account.GroupIds.Add(group.Id);

        _accounts.Upsert(row.Account);
        row.RefreshGroupState();
        RebuildGroups();
        _shell.Notify(NotificationId.AccountGroupsUpdated, NotificationKind.Success,
            "Account groups updated",
            $"{(removing ? "Removed" : "Added")} '{row.DisplayLabel}' {(removing ? "from" : "to")} '{group.Name}'.");
    }

    public void SetGroupMembers(AccountGroup group, IReadOnlyCollection<Guid> selectedAccountIds)
    {
        var selected = new HashSet<Guid>(selectedAccountIds);
        var changed = new List<Account>();
        foreach (Account account in _accounts.All)
        {
            bool shouldBelong = selected.Contains(account.Id);
            bool belongs = account.BelongsTo(group.Id);
            if (shouldBelong == belongs)
                continue;

            if (shouldBelong)
                account.GroupIds.Add(group.Id);
            else
                account.GroupIds.Remove(group.Id);
            changed.Add(account);
        }

        if (changed.Count > 0)
            _accounts.UpsertMany(changed);
        RebuildGroups();
        _shell.Notify(NotificationId.GroupUpdated, NotificationKind.Success, "Group updated", $"Updated group '{group.Name}'.");
    }

    [RelayCommand]
    private void CreateGroup()
    {
        string color = GroupPalette[_groups.All.Count % GroupPalette.Length];
        AccountGroup? group = _dialogs.EditGroup(null, color);
        if (group == null) return;

        _groups.Add(group.Name, group.ColorHex);
        RebuildGroups();
        _shell.Notify(NotificationId.GroupCreated, NotificationKind.Success, "Group created", $"Created group '{group.Name}'.");
    }

    public void RenameGroup(GroupViewModel groupVm)
    {
        if (groupVm.Group == null) return;
        AccountGroup? edited = _dialogs.EditGroup(groupVm.Group, groupVm.Group.ColorHex);
        if (edited == null) return;

        groupVm.Group.Name = edited.Name;
        groupVm.Group.ColorHex = edited.ColorHex;
        _groups.Update(groupVm.Group);
        RebuildGroups();
        _shell.Notify(NotificationId.GroupRenamed, NotificationKind.Success,
            "Group renamed", $"Updated group '{groupVm.Group.Name}'.");
    }

    public void DropAccountOnGroup(AccountRowViewModel row, GroupViewModel targetVm)
    {
        AccountGroup? target = targetVm.Group;
        if (target == null) return;

        if (row.Account.BelongsTo(target.Id))
        {
            _shell.SetStatus($"'{row.DisplayLabel}' is already in {target.Name}.");
            return;
        }

        if (row.Account.GroupIds.Count > 0)
        {
            GroupDropChoice choice = _dialogs.AskGroupDrop(row.DisplayLabel, target.Name);
            if (choice == GroupDropChoice.Cancel) return;
            if (choice == GroupDropChoice.Move) row.Account.GroupIds.Clear();
        }

        row.Account.GroupIds.Add(target.Id);
        row.Account.GroupId = null;
        _accounts.Upsert(row.Account);
        row.RefreshGroupState();
        RebuildGroups();
        _shell.Notify(NotificationId.AccountGroupsUpdated, NotificationKind.Success,
            "Account groups updated", $"Added '{row.DisplayLabel}' to '{target.Name}'.");
        _shell.SetStatus($"'{row.DisplayLabel}' → {target.Name}");
    }

    public void ReorderAccount(AccountRowViewModel dragged, AccountRowViewModel target)
    {
        if (dragged == target) return;

        var ordered = _accounts.All.OrderBy(a => a.SortOrder).ToList();
        int from = ordered.FindIndex(a => a.Id == dragged.Account.Id);
        int to = ordered.FindIndex(a => a.Id == target.Account.Id);
        if (from < 0 || to < 0 || from == to) return;

        Account moved = ordered[from];
        ordered.RemoveAt(from);
        ordered.Insert(to, moved);

        for (int i = 0; i < ordered.Count; i++)
            ordered[i].SortOrder = i;

        _accounts.UpsertMany(ordered);
        RebuildGroups();
    }

    public void DeleteGroup(GroupViewModel groupVm)
    {
        if (groupVm.Group == null) return;
        if (!_dialogs.Confirm(ConfirmAction.DeleteGroup, $"Delete group '{groupVm.Group.Name}'? Its accounts become ungrouped."))
            return;

        var group = groupVm.Group;
        var affectedAccounts = _accounts.All.Where(a => a.BelongsTo(group.Id)).ToList();

        var changed = new List<Account>();
        foreach (var acc in affectedAccounts)
        {
            acc.GroupIds.Remove(group.Id);
            changed.Add(acc);
        }
        if (changed.Count > 0)
            _accounts.UpsertMany(changed);
        _groups.Remove(group);
        RebuildGroups();
        _shell.Notify(NotificationId.GroupDeleted, NotificationKind.Success, "Group deleted", "The group was deleted; its accounts were kept.", () =>
        {
            _groups.Add(group);
            var restoreChanged = new List<Account>();
            foreach (var acc in affectedAccounts)
            {
                acc.GroupIds.Add(group.Id);
                restoreChanged.Add(acc);
            }
            if (restoreChanged.Count > 0)
                _accounts.UpsertMany(restoreChanged);
            RebuildGroups();
        });
    }


    private void OnRunningChanged(Guid accountId, bool isRunning)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => OnRunningChanged(accountId, isRunning));
            return;
        }

        if (_allRows.TryGetValue(accountId, out AccountRowViewModel? row))
            row.IsRunning = isRunning;

        RefreshRunningInstances();
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(HasRunning));
    }
}

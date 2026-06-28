using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class MultiGroupAccountListTests
{
    [Fact]
    public void RebuildGroups_ShowsAccountInEveryAssignedGroup()
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        AccountGroup first = groups.Add("First", "#777777");
        AccountGroup second = groups.Add("Second", "#888888");
        accounts.Upsert(new Account { UserId = 1, Username = "Multi", GroupIds = new() { first.Id, second.Id } });
        AccountListViewModel vm = Create(accounts, groups, out _);

        vm.RebuildGroups();

        Assert.Single(vm.Groups.Single(group => group.Group?.Id == first.Id).Accounts);
        Assert.Single(vm.Groups.Single(group => group.Group?.Id == second.Id).Accounts);
        Assert.Equal(2, vm.Rows.OfType<AccountRowViewModel>().Count());
        Assert.Same(vm.Rows.OfType<AccountRowViewModel>().First(), vm.Rows.OfType<AccountRowViewModel>().Last());
    }

    [Fact]
    public void SetGroupMembers_UpdatesMembershipsInOneBatch()
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        AccountGroup first = groups.Add("First", "#777777");
        AccountGroup second = groups.Add("Second", "#888888");
        var one = new Account { UserId = 1, Username = "One", GroupIds = new() { first.Id, second.Id } };
        var two = new Account { UserId = 2, Username = "Two", GroupIds = new() { second.Id } };
        accounts.Upsert(one);
        accounts.Upsert(two);
        AccountListViewModel vm = Create(accounts, groups, out _);

        vm.SetGroupMembers(first, new[] { two.Id });

        Assert.DoesNotContain(first.Id, one.GroupIds);
        Assert.Contains(second.Id, one.GroupIds);
        Assert.Contains(first.Id, two.GroupIds);
        Assert.Equal(1, accounts.BatchCalls);
    }

    [Fact]
    public async Task LaunchGroup_SearchActiveStillLaunchesEveryMember()
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        AccountGroup group = groups.Add("Main", "#777777");
        accounts.Upsert(new Account { UserId = 1, Username = "Matches", GroupIds = new() { group.Id } });
        accounts.Upsert(new Account { UserId = 2, Username = "Hidden", GroupIds = new() { group.Id } });
        AccountListViewModel vm = Create(accounts, groups, out FakeShell shell);
        vm.RebuildGroups();
        vm.SearchText = "Matches";

        await vm.LaunchGroupAsync(vm.Groups.Single());

        Assert.Equal(2, shell.LastLaunched.Count);
    }

    [Fact]
    public void DropAccountOnGroup_UngroupedAccount_AddsWithoutAsking()
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        AccountGroup target = groups.Add("Target", "#777777");
        var account = new Account { UserId = 1, Username = "Solo" };
        accounts.Upsert(account);
        var dialogs = new FakeDialogService();
        var vm = new AccountListViewModel(accounts, groups, dialogs, new FakeShell(), new InstanceTracker());
        vm.RebuildGroups();

        AccountRowViewModel row = vm.Rows.OfType<AccountRowViewModel>().Single();
        GroupViewModel targetVm = vm.Groups.Single(group => group.Group?.Id == target.Id);
        vm.DropAccountOnGroup(row, targetVm);

        Assert.Contains(target.Id, account.GroupIds);
        Assert.Equal(0, dialogs.AskGroupDropCalls);
    }

    [Fact]
    public void DropAccountOnGroup_Conflict_Move_ClearsOtherGroups()
    {
        var account = DropOntoSecondGroup(GroupDropChoice.Move, out AccountGroup first, out AccountGroup second, out int asks);

        Assert.DoesNotContain(first.Id, account.GroupIds);
        Assert.Contains(second.Id, account.GroupIds);
        Assert.Equal(1, asks);
    }

    [Fact]
    public void DropAccountOnGroup_Conflict_Add_KeepsBothGroups()
    {
        var account = DropOntoSecondGroup(GroupDropChoice.Add, out AccountGroup first, out AccountGroup second, out int asks);

        Assert.Contains(first.Id, account.GroupIds);
        Assert.Contains(second.Id, account.GroupIds);
        Assert.Equal(1, asks);
    }

    private static Account DropOntoSecondGroup(GroupDropChoice choice, out AccountGroup first, out AccountGroup second, out int asks)
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        first = groups.Add("First", "#777777");
        second = groups.Add("Second", "#888888");
        var account = new Account { UserId = 1, Username = "X", GroupIds = new() { first.Id } };
        accounts.Upsert(account);
        var dialogs = new FakeDialogService { DropChoice = choice };
        AccountGroup secondLocal = second;
        var vm = new AccountListViewModel(accounts, groups, dialogs, new FakeShell(), new InstanceTracker());
        vm.RebuildGroups();

        AccountRowViewModel row = vm.Rows.OfType<AccountRowViewModel>().First();
        GroupViewModel targetVm = vm.Groups.Single(group => group.Group?.Id == secondLocal.Id);
        vm.DropAccountOnGroup(row, targetVm);

        asks = dialogs.AskGroupDropCalls;
        return account;
    }

    [Theory]
    [InlineData("A", "C", new[] { "B", "C", "A" })]
    [InlineData("C", "A", new[] { "C", "A", "B" })]
    public void ReorderAccount_MovesDraggedToTargetSlot(string dragName, string targetName, string[] expected)
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        var a = new Account { UserId = 1, Username = "A", SortOrder = 0 };
        var b = new Account { UserId = 2, Username = "B", SortOrder = 1 };
        var c = new Account { UserId = 3, Username = "C", SortOrder = 2 };
        accounts.Upsert(a);
        accounts.Upsert(b);
        accounts.Upsert(c);
        AccountListViewModel vm = Create(accounts, groups, out _);
        vm.RebuildGroups();

        AccountRowViewModel Row(string name) =>
            vm.Rows.OfType<AccountRowViewModel>().Single(r => r.Username == name);
        vm.ReorderAccount(Row(dragName), Row(targetName));

        string[] order = accounts.All.OrderBy(x => x.SortOrder).Select(x => x.Username).ToArray();
        Assert.Equal(expected, order);
    }

    [Fact]
    public void RebuildGroups_UnsubscribesRemovedGroupFromRowRebuilds()
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        AccountGroup group = groups.Add("Removed", "#777777");
        accounts.Upsert(new Account { UserId = 1, Username = "One", GroupIds = new() { group.Id } });
        AccountListViewModel vm = Create(accounts, groups, out _);
        vm.RebuildGroups();
        GroupViewModel stale = vm.Groups.Single(item => item.Group?.Id == group.Id);

        groups.Remove(group);
        vm.RebuildGroups();
        int rowResets = 0;
        vm.Rows.CollectionChanged += (_, _) => rowResets++;

        stale.IsExpanded = !stale.IsExpanded;

        Assert.Equal(0, rowResets);
    }

    private static AccountListViewModel Create(
        FakeAccountRepository accounts,
        FakeGroupRepository groups,
        out FakeShell shell)
    {
        shell = new FakeShell();
        return new AccountListViewModel(accounts, groups, new FakeDialogService(), shell, new InstanceTracker());
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> _items = new();
        public IReadOnlyList<Account> All => _items;
        public int BatchCalls { get; private set; }
        public Account? FindByUserId(long userId) => _items.Find(item => item.UserId == userId);
        public void Upsert(Account account)
        {
            int index = _items.FindIndex(item => item.Id == account.Id);
            if (index >= 0) _items[index] = account; else _items.Add(account);
        }
        public void UpsertMany(IEnumerable<Account> accounts)
        {
            BatchCalls++;
            foreach (Account account in accounts) Upsert(account);
        }
        public void Remove(Account account) => _items.RemoveAll(item => item.Id == account.Id);
    }

    private sealed class FakeGroupRepository : IGroupRepository
    {
        private readonly List<AccountGroup> _items = new();
        public IReadOnlyList<AccountGroup> All => _items;
        public AccountGroup Add(string name, string colorHex)
        {
            var group = new AccountGroup { Name = name, ColorHex = colorHex, SortOrder = _items.Count };
            _items.Add(group);
            return group;
        }
        public void Update(AccountGroup group) { }
        public void Remove(AccountGroup group) => _items.Remove(group);
    }

    private sealed class FakeDialogService : IDialogService
    {
        public GroupDropChoice DropChoice { get; set; } = GroupDropChoice.Cancel;
        public int AskGroupDropCalls { get; private set; }

        public Task<Account?> ShowAddAccountAsync() => Task.FromResult<Account?>(null);
        public string? Prompt(string title, string initialValue) => null;
        public FavoriteGame? EditFavorite(FavoriteGame existing) => null;
        public GroupDropChoice AskGroupDrop(string accountLabel, string groupName)
        {
            AskGroupDropCalls++;
            return DropChoice;
        }
        public bool Confirm(string message) => true;
        public string? PickFolder(string title) => null;
    }

    private sealed class FakeShell : IShellCoordinator
    {
        public IReadOnlyList<Account> LastLaunched { get; private set; } = Array.Empty<Account>();
        public Task LaunchAsync(IReadOnlyList<Account> accounts)
        {
            LastLaunched = accounts;
            return Task.CompletedTask;
        }
        public void SetStatus(string message) { }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class GroupExpansionPersistenceTests
{
    [Fact]
    public void GroupViewModel_ReadsAndPersistsExpansionState()
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        AccountGroup group = groups.Add("Collapsed", "#777777");
        group.IsExpanded = false;
        accounts.Upsert(new Account { UserId = 1, GroupIds = new() { group.Id } });
        var vm = new AccountListViewModel(accounts, groups, new FakeDialogService(), new FakeShell(), new InstanceTracker());

        vm.RebuildGroups();
        GroupViewModel row = Assert.Single(vm.Groups);
        Assert.False(row.IsExpanded);

        row.IsExpanded = true;

        Assert.True(group.IsExpanded);
        Assert.Equal(1, groups.UpdateCalls);
    }

    [Fact]
    public void RebuildGroups_ReusesGroupViewModel()
    {
        var accounts = new FakeAccountRepository();
        var groups = new FakeGroupRepository();
        AccountGroup group = groups.Add("Cached", "#777777");
        accounts.Upsert(new Account { UserId = 1, Username = "Account", GroupIds = new() { group.Id } });
        var vm = new AccountListViewModel(accounts, groups, new FakeDialogService(), new FakeShell(), new InstanceTracker());

        vm.RebuildGroups();
        GroupViewModel first = Assert.Single(vm.Groups);

        vm.RebuildGroups();
        GroupViewModel second = Assert.Single(vm.Groups);

        Assert.Same(first, second);
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> _items = new();
        public IReadOnlyList<Account> All => _items;
        public Account? FindByUserId(long userId) => _items.Find(item => item.UserId == userId);
        public void Upsert(Account account) => _items.Add(account);
        public void Remove(Account account) => _items.Remove(account);
    }

    private sealed class FakeGroupRepository : IGroupRepository
    {
        private readonly List<AccountGroup> _items = new();
        public IReadOnlyList<AccountGroup> All => _items;
        public int UpdateCalls { get; private set; }
        public AccountGroup Add(string name, string colorHex)
        {
            var group = new AccountGroup { Name = name, ColorHex = colorHex };
            _items.Add(group);
            return group;
        }
        public void Update(AccountGroup group) => UpdateCalls++;
        public void Remove(AccountGroup group) => _items.Remove(group);
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<Account?> ShowAddAccountAsync() => Task.FromResult<Account?>(null);
        public string? Prompt(string title, string initialValue) => null;
        public FavoriteGame? EditFavorite(FavoriteGame existing) => null;
        public bool Confirm(string message) => true;
        public string? PickFolder(string title) => null;
    }

    private sealed class FakeShell : IShellCoordinator
    {
        public Task LaunchAsync(IReadOnlyList<Account> accounts) => Task.CompletedTask;
        public void SetStatus(string message) { }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public class AccountListViewModelTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeGroupRepository _groups = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeShell _shell = new();
    private readonly InstanceTracker _tracker = new();

    private AccountListViewModel Create()
    {
        var vm = new AccountListViewModel(_accounts, _groups, _dialogs, _shell, _tracker);
        vm.RebuildGroups();
        return vm;
    }

    [Fact]
    public void InitialState_NoAccounts_NoGroups_ShowsEmptyStateOnly()
    {
        var vm = Create();

        Assert.True(vm.ShowEmptyState);
        Assert.False(vm.ShowList);
        Assert.False(vm.ShowNoResults);
    }

    [Fact]
    public void WithGroups_ButNoAccounts_ShowsGroupHeaders()
    {
        _groups.Add("Main Group", "#FF0000");
        var vm = Create();

        Assert.False(vm.ShowEmptyState);
        Assert.True(vm.ShowList);
        Assert.False(vm.ShowNoResults);
    }

    [Fact]
    public void WithAccounts_NoSearch_ShowsListOnly()
    {
        var group = _groups.Add("Main Group", "#FF0000");
        _accounts.Upsert(new Account
        {
            Id = Guid.NewGuid(),
            UserId = 12345,
            Username = "TestUser",
            DisplayName = "Test Display",
            GroupId = group.Id
        });

        var vm = Create();

        Assert.False(vm.ShowEmptyState);
        Assert.True(vm.ShowList);
        Assert.False(vm.ShowNoResults);
    }

    [Fact]
    public void WithAccounts_SearchMatches_ShowsListOnly()
    {
        var group = _groups.Add("Main Group", "#FF0000");
        _accounts.Upsert(new Account
        {
            Id = Guid.NewGuid(),
            UserId = 12345,
            Username = "TestUser",
            DisplayName = "Test Display",
            GroupId = group.Id
        });

        var vm = Create();
        vm.SearchText = "Test";

        Assert.False(vm.ShowEmptyState);
        Assert.True(vm.ShowList);
        Assert.False(vm.ShowNoResults);
    }

    [Fact]
    public void WithAccounts_SearchDoesNotMatch_ShowsNoResultsOnly()
    {
        var group = _groups.Add("Main Group", "#FF0000");
        _accounts.Upsert(new Account
        {
            Id = Guid.NewGuid(),
            UserId = 12345,
            Username = "TestUser",
            DisplayName = "Test Display",
            GroupId = group.Id
        });

        var vm = Create();
        vm.SearchText = "NoMatch";

        Assert.False(vm.ShowEmptyState);
        Assert.False(vm.ShowList);
        Assert.True(vm.ShowNoResults);
    }

    [Fact]
    public void Rows_FlattensHeaderAndAccounts()
    {
        var group = _groups.Add("Main Group", "#FF0000");
        _accounts.Upsert(new Account
        {
            Id = Guid.NewGuid(), UserId = 1, Username = "U", DisplayName = "D", GroupId = group.Id
        });

        var vm = Create();

        Assert.Contains(vm.Rows, r => r is GroupViewModel);
        Assert.Contains(vm.Rows, r => r is AccountRowViewModel);
    }

    [Fact]
    public void CollapsingGroup_RemovesAccountRows_KeepsHeader()
    {
        var group = _groups.Add("Main Group", "#FF0000");
        _accounts.Upsert(new Account
        {
            Id = Guid.NewGuid(), UserId = 1, Username = "U", DisplayName = "D", GroupId = group.Id
        });

        var vm = Create();
        GroupViewModel gvm = vm.Groups.First(g => g.HasGroup);
        gvm.IsExpanded = false;

        Assert.Contains(vm.Rows, r => r is GroupViewModel);
        Assert.DoesNotContain(vm.Rows, r => r is AccountRowViewModel);

        gvm.IsExpanded = true;
        Assert.Contains(vm.Rows, r => r is AccountRowViewModel);
    }

    [Fact]
    public void UngroupedAccounts_AppearInRows_WithoutHeader()
    {
        _accounts.Upsert(new Account
        {
            Id = Guid.NewGuid(), UserId = 2, Username = "Solo", DisplayName = "Solo", GroupId = null
        });

        var vm = Create();

        Assert.Contains(vm.Rows, r => r is AccountRowViewModel);
        Assert.DoesNotContain(vm.Rows, r => r is GroupViewModel);
    }


    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> _items = new();
        public IReadOnlyList<Account> All => _items;

        public Account? FindByUserId(long userId) => _items.Find(a => a.UserId == userId);

        public void Upsert(Account account)
        {
            int idx = _items.FindIndex(a => a.Id == account.Id);
            if (idx >= 0)
                _items[idx] = account;
            else
                _items.Add(account);
        }

        public void Remove(Account account) => _items.RemoveAll(a => a.Id == account.Id);
    }

    private sealed class FakeGroupRepository : IGroupRepository
    {
        private readonly List<AccountGroup> _items = new();
        public IReadOnlyList<AccountGroup> All => _items;

        public AccountGroup Add(string name, string colorHex)
        {
            var g = new AccountGroup
            {
                Id = Guid.NewGuid(),
                Name = name,
                ColorHex = colorHex,
                SortOrder = _items.Count
            };
            _items.Add(g);
            return g;
        }

        public void Update(AccountGroup group)
        {
            int idx = _items.FindIndex(g => g.Id == group.Id);
            if (idx >= 0) _items[idx] = group;
        }

        public void Remove(AccountGroup group) => _items.RemoveAll(g => g.Id == group.Id);
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
        public string? LastStatus { get; private set; }
        public Task LaunchAsync(IReadOnlyList<Account> accounts) => Task.CompletedTask;
        public void SetStatus(string message) => LastStatus = message;
    }
}

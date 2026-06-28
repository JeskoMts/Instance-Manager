using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class LaunchPanelPrimaryFavoriteTests
{
    [Fact]
    public void Constructor_DoesNotAutoApplyFavorite_OpensOnLastUsedTarget()
    {
        var favorites = new FakeFavoriteRepository();
        favorites.Add(new FavoriteGame { Name = "One", PlaceId = 111 });
        favorites.Add(new FavoriteGame { Name = "Two", PlaceId = 222 });
        var settings = new FakeSettingsService();
        settings.Settings.LastTargetInput = "999";

        var vm = Create(favorites, settings);

        Assert.Null(vm.SelectedFavorite);
        Assert.Equal("999", vm.TargetInput);
    }

    [Fact]
    public void Constructor_MigratesLegacyPrimaryFavorite()
    {
        var favorites = new FakeFavoriteRepository();
        favorites.Add(new FavoriteGame { Name = "One", PlaceId = 111 });
        var legacy = new FavoriteGame { Name = "Two", PlaceId = 222 };
        favorites.Add(legacy);
        var settings = new FakeSettingsService();
        settings.Settings.PrimaryFavoriteId = legacy.Id;

        var vm = Create(favorites, settings);

        Assert.True(legacy.IsPrimary);
        Assert.Null(settings.Settings.PrimaryFavoriteId);
        Assert.Equal(legacy.Id, vm.Favorites.First().Id);
    }

    [Fact]
    public void TogglePrimary_PinsFavoriteToTopAndPersists()
    {
        var favorites = new FakeFavoriteRepository();
        favorites.Add(new FavoriteGame { Name = "One", PlaceId = 111 });
        var second = new FavoriteGame { Name = "Two", PlaceId = 222 };
        favorites.Add(second);
        var settings = new FakeSettingsService();
        var vm = Create(favorites, settings);

        INotifyPropertyChanged observable = Assert.IsAssignableFrom<INotifyPropertyChanged>(second);
        string? changed = null;
        observable.PropertyChanged += (_, args) => changed = args.PropertyName;

        vm.TogglePrimaryCommand.Execute(second);

        Assert.True(second.IsPrimary);
        Assert.Equal(second.Id, vm.Favorites.First().Id);
        Assert.True(favorites.All.Single(f => f.Id == second.Id).IsPrimary);
        Assert.Equal(nameof(FavoriteGame.IsPrimary), changed);
    }

    [Fact]
    public void TogglePrimary_SupportsMultiplePinnedSortedAbove()
    {
        var favorites = new FakeFavoriteRepository();
        var a = new FavoriteGame { Name = "A", PlaceId = 1 };
        var b = new FavoriteGame { Name = "B", PlaceId = 2 };
        var c = new FavoriteGame { Name = "C", PlaceId = 3 };
        favorites.Add(a);
        favorites.Add(b);
        favorites.Add(c);
        var vm = Create(favorites, new FakeSettingsService());

        vm.TogglePrimaryCommand.Execute(a);
        vm.TogglePrimaryCommand.Execute(c);

        Assert.True(a.IsPrimary);
        Assert.True(c.IsPrimary);
        Assert.Equal(new[] { a.Id, c.Id, b.Id }, vm.Favorites.Select(f => f.Id));
    }

    [Fact]
    public void MoveFavoriteDown_SwapsOrderWithinPartition()
    {
        var favorites = new FakeFavoriteRepository();
        var a = new FavoriteGame { Name = "A", PlaceId = 1 };
        var b = new FavoriteGame { Name = "B", PlaceId = 2 };
        var c = new FavoriteGame { Name = "C", PlaceId = 3 };
        favorites.Add(a);
        favorites.Add(b);
        favorites.Add(c);
        var vm = Create(favorites, new FakeSettingsService());

        vm.MoveFavoriteDownCommand.Execute(a);

        Assert.Equal(new[] { b.Id, a.Id, c.Id }, vm.Favorites.Select(f => f.Id));
    }

    [Fact]
    public void ReorderFavorite_MovesDraggedToTargetAndPersistsContiguousOrder()
    {
        var favorites = new FakeFavoriteRepository();
        var a = new FavoriteGame { Name = "A", PlaceId = 1, SortOrder = 0 };
        var b = new FavoriteGame { Name = "B", PlaceId = 2, SortOrder = 1 };
        var c = new FavoriteGame { Name = "C", PlaceId = 3, SortOrder = 2 };
        favorites.Add(a);
        favorites.Add(b);
        favorites.Add(c);
        var vm = Create(favorites, new FakeSettingsService());

        vm.ReorderFavorite(c, a);

        Assert.Equal(new[] { c.Id, a.Id, b.Id }, vm.Favorites.Select(f => f.Id));
        Assert.Equal(new[] { 0, 1, 2 }, favorites.All.Select(f => f.SortOrder));
    }

    [Fact]
    public void ReorderFavorite_RejectsCrossPartitionDrop()
    {
        var favorites = new FakeFavoriteRepository();
        var pinned = new FavoriteGame { Name = "Pinned", PlaceId = 1, IsPrimary = true, SortOrder = 0 };
        var normal = new FavoriteGame { Name = "Normal", PlaceId = 2, SortOrder = 1 };
        favorites.Add(pinned);
        favorites.Add(normal);
        var vm = Create(favorites, new FakeSettingsService());

        vm.ReorderFavorite(normal, pinned);

        Assert.Equal(new[] { pinned.Id, normal.Id }, vm.Favorites.Select(f => f.Id));
        Assert.Equal(1, normal.SortOrder);
    }

    [Fact]
    public void ReorderFavorite_PreservesSelectedFavorite()
    {
        var favorites = new FakeFavoriteRepository();
        var a = new FavoriteGame { Name = "A", PlaceId = 1, SortOrder = 0 };
        var b = new FavoriteGame { Name = "B", PlaceId = 2, SortOrder = 1 };
        var c = new FavoriteGame { Name = "C", PlaceId = 3, SortOrder = 2 };
        favorites.Add(a);
        favorites.Add(b);
        favorites.Add(c);
        var vm = Create(favorites, new FakeSettingsService());
        vm.SelectedFavorite = b;

        vm.ReorderFavorite(c, a);

        Assert.Equal(b.Id, vm.SelectedFavorite?.Id);
    }

    [Fact]
    public void ReorderFavorite_WhileFilteredUsesFullPartitionOrder()
    {
        var favorites = new FakeFavoriteRepository();
        var apple = new FavoriteGame { Name = "Apple", PlaceId = 1, SortOrder = 0 };
        var banana = new FavoriteGame { Name = "Banana", PlaceId = 2, SortOrder = 1 };
        var apricot = new FavoriteGame { Name = "Apricot", PlaceId = 3, SortOrder = 2 };
        favorites.Add(apple);
        favorites.Add(banana);
        favorites.Add(apricot);
        var vm = Create(favorites, new FakeSettingsService());
        vm.FavoriteSearchText = "ap";

        vm.ReorderFavorite(apricot, apple);

        Assert.Equal(new[] { apricot.Id, apple.Id }, vm.FilteredFavorites.Select(f => f.Id));
        Assert.Equal(new[] { apricot.Id, apple.Id, banana.Id }, vm.Favorites.Select(f => f.Id));
    }

    [Fact]
    public void Search_FiltersFilteredFavoritesByName()
    {
        var favorites = new FakeFavoriteRepository();
        favorites.Add(new FavoriteGame { Name = "Brookhaven", PlaceId = 1 });
        favorites.Add(new FavoriteGame { Name = "Adopt Me", PlaceId = 2 });
        favorites.Add(new FavoriteGame { Name = "Jailbreak", PlaceId = 3 });
        var vm = Create(favorites, new FakeSettingsService());

        vm.FavoriteSearchText = "ado";

        FavoriteGame match = Assert.Single(vm.FilteredFavorites);
        Assert.Equal("Adopt Me", match.Name);
    }


    [Fact]
    public void RemoveFavorite_DoesNotPromoteAnotherToPrimary()
    {
        var favorites = new FakeFavoriteRepository();
        var a = new FavoriteGame { Name = "A", PlaceId = 1 };
        var b = new FavoriteGame { Name = "B", PlaceId = 2 };
        favorites.Add(a);
        favorites.Add(b);
        var vm = Create(favorites, new FakeSettingsService());
        vm.TogglePrimaryCommand.Execute(a);

        vm.RemoveFavoriteCommand.Execute(a);

        FavoriteGame remaining = Assert.Single(vm.Favorites);
        Assert.Equal(b.Id, remaining.Id);
        Assert.False(remaining.IsPrimary);
    }

    [Fact]
    public void AddFavorite_AddsUnpinnedEntry()
    {
        var favorites = new FakeFavoriteRepository();
        var settings = new FakeSettingsService();
        var dialogs = new FakeDialogService { PromptResult = "First" };
        var vm = new LaunchPanelViewModel(favorites, settings, dialogs, new FakeShell(), new FakeServerLinkResolver());
        vm.TargetInput = "123";

        vm.AddFavoriteCommand.Execute(null);

        FavoriteGame first = Assert.Single(vm.Favorites);
        Assert.Equal("First", first.Name);
        Assert.Equal(123, first.PlaceId);
        Assert.False(first.IsPrimary);
    }

    [Fact]
    public async Task ResolveTargetAsync_JobModeUsesOnlyServerLinkField()
    {
        var resolver = new FakeServerLinkResolver
        {
            Result = ServerTargetResolution.Success(ServerTarget.ByJob(77,
                "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab"))
        };
        var vm = Create(new FakeFavoriteRepository(), new FakeSettingsService(), resolver);
        vm.SelectedMode = JoinMode.PrivateByJobId;
        vm.TargetInput = "not-used";
        vm.JobIdInput = "https://www.roblox.com/share?code=test&type=Server";

        ServerTargetResolution result = await vm.ResolveTargetAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(vm.JobIdInput, resolver.LastInput);
        Assert.Equal(77, result.Target!.PlaceId);
    }

    private static LaunchPanelViewModel Create(
        FakeFavoriteRepository favorites,
        FakeSettingsService settings,
        FakeServerLinkResolver? resolver = null) =>
        new(favorites, settings, new FakeDialogService(), new FakeShell(), resolver ?? new FakeServerLinkResolver());

    private sealed class FakeFavoriteRepository : IFavoriteRepository
    {
        private readonly List<FavoriteGame> _items = new();
        public IReadOnlyList<FavoriteGame> All => _items;
        public void Add(FavoriteGame favorite) => _items.Add(favorite);
        public void Update(FavoriteGame favorite)
        {
            int index = _items.FindIndex(item => item.Id == favorite.Id);
            if (index >= 0) _items[index] = favorite;
        }
        public void Remove(FavoriteGame favorite) => _items.RemoveAll(item => item.Id == favorite.Id);
        public void Replace(IEnumerable<FavoriteGame> favorites)
        {
            _items.Clear();
            _items.AddRange(favorites);
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public int SaveCalls { get; private set; }
        public void Save() => SaveCalls++;
    }

    private sealed class FakeServerLinkResolver : IServerLinkResolver
    {
        public string? LastInput { get; private set; }
        public ServerTargetResolution Result { get; set; } = ServerTargetResolution.Failure("Missing target");
        public Task<ServerTargetResolution> ResolveAsync(string input, CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? PromptResult { get; init; }
        public Task<Account?> ShowAddAccountAsync() => Task.FromResult<Account?>(null);
        public string? Prompt(string title, string initialValue) => PromptResult;
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

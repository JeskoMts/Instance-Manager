using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public class LaunchPanelViewModelTests
{
    private static LaunchPanelViewModel Create() =>
        new(new FakeFavoriteRepository(), new FakeSettingsService(), new FakeDialogService(), new FakeShell());

    [Fact]
    public void TryBuildTarget_PublicMode_FromGameLink_BuildsPublicTarget()
    {
        var vm = Create();
        vm.SelectedMode = JoinMode.PublicByLink;
        vm.TargetInput = "https://www.roblox.com/de/games/126884695634066/Grow-a-Garden";

        bool ok = vm.TryBuildTarget(out ServerTarget target, out string error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal(JoinMode.PublicByLink, target.Mode);
        Assert.Equal(126884695634066, target.PlaceId);
        Assert.Null(target.JobId);
    }

    [Fact]
    public void TryBuildTarget_JobIdMode_WithValidJob_BuildsJobTarget()
    {
        var vm = Create();
        vm.SelectedMode = JoinMode.PrivateByJobId;
        vm.TargetInput = "920587237";
        vm.JobIdInput = "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab";

        bool ok = vm.TryBuildTarget(out ServerTarget target, out _);

        Assert.True(ok);
        Assert.Equal(JoinMode.PrivateByJobId, target.Mode);
        Assert.Equal(920587237, target.PlaceId);
        Assert.Equal("ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab", target.JobId);
    }

    [Fact]
    public void TryBuildTarget_JobIdMode_WithoutJob_Fails()
    {
        var vm = Create();
        vm.SelectedMode = JoinMode.PrivateByJobId;
        vm.TargetInput = "920587237";
        vm.JobIdInput = "";

        Assert.False(vm.TryBuildTarget(out _, out string error));
        Assert.Contains("Job ID", error);
    }

    [Fact]
    public void TryBuildTarget_InvalidTarget_Fails()
    {
        var vm = Create();
        vm.SelectedMode = JoinMode.PublicByLink;
        vm.TargetInput = "kein-link";

        Assert.False(vm.TryBuildTarget(out _, out string error));
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void SelectedMode_IsJobIdMode_ReflectsMode()
    {
        var vm = Create();
        vm.SelectedMode = JoinMode.PublicByLink;
        Assert.False(vm.IsJobIdMode);
        vm.SelectedMode = JoinMode.PrivateByJobId;
        Assert.True(vm.IsJobIdMode);
    }


    private static LaunchPanelViewModel CreateWith(FakeFavoriteRepository favs, FakeDialogService dialogs) =>
        new(favs, new FakeSettingsService(), dialogs, new FakeShell());

    [Fact]
    public void SettingSelectedFavorite_AppliesTarget_AndKeepsSelectionVisible()
    {
        var favs = new FakeFavoriteRepository();
        favs.Add(new FavoriteGame { Name = "Grow a Garden", PlaceId = 126884695634066 });
        var vm = CreateWith(favs, new FakeDialogService());

        FavoriteGame fav = vm.Favorites.First();
        vm.SelectedFavorite = fav;

        Assert.Equal("126884695634066", vm.TargetInput);
        Assert.Same(fav, vm.SelectedFavorite);
    }

    [Fact]
    public void GameTargetFromGames_ClearsFavoriteSoSameFavoriteCanBeReapplied()
    {
        var favs = new FakeFavoriteRepository();
        favs.Add(new FavoriteGame { Name = "Favorite", PlaceId = 111 });
        var vm = CreateWith(favs, new FakeDialogService());

        FavoriteGame fav = vm.Favorites.First();
        vm.SelectedFavorite = fav;

        MethodInfo? method = typeof(LaunchPanelViewModel).GetMethod("ApplyGameTargetFromGames");
        Assert.NotNull(method);
        method!.Invoke(vm, new object[] { 222L });

        Assert.Null(vm.SelectedFavorite);
        Assert.Equal("222", vm.TargetInput);

        vm.SelectedFavorite = fav;

        Assert.Same(fav, vm.SelectedFavorite);
        Assert.Equal("111", vm.TargetInput);
    }

    [Fact]
    public void SelectedFavorite_WithJobId_SwitchesToJobIdMode()
    {
        var favs = new FakeFavoriteRepository();
        favs.Add(new FavoriteGame
        {
            Name = "Private",
            PlaceId = 920587237,
            DefaultJobId = "ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab"
        });
        var vm = CreateWith(favs, new FakeDialogService());

        vm.SelectedFavorite = vm.Favorites.First();

        Assert.True(vm.IsJobIdMode);
        Assert.Equal("https://www.roblox.com/games/start?placeId=920587237&gameInstanceId=ec1c8e3d-1c2b-4c3d-9e2a-1234567890ab", vm.JobIdInput);
    }

    [Fact]
    public void RemoveFavorite_DeletesAndClearsSelection()
    {
        var favs = new FakeFavoriteRepository();
        favs.Add(new FavoriteGame { Name = "Doomed", PlaceId = 111 });
        var vm = CreateWith(favs, new FakeDialogService());

        FavoriteGame fav = vm.Favorites.First();
        vm.SelectedFavorite = fav;
        vm.RemoveFavoriteCommand.Execute(fav);

        Assert.Empty(vm.Favorites);
        Assert.Null(vm.SelectedFavorite);
        Assert.False(vm.HasFavorites);
    }

    [Fact]
    public void EditFavorite_UpdatesNameAndTarget()
    {
        var favs = new FakeFavoriteRepository();
        var original = new FavoriteGame { Name = "Old", PlaceId = 111 };
        favs.Add(original);
        var edited = new FavoriteGame { Id = original.Id, Name = "New", PlaceId = 222 };
        var vm = CreateWith(favs, new FakeDialogService { EditResult = edited });

        vm.EditFavoriteCommand.Execute(vm.Favorites.First());

        FavoriteGame stored = favs.All.Single();
        Assert.Equal("New", stored.Name);
        Assert.Equal(222, stored.PlaceId);
    }


    private sealed class FakeFavoriteRepository : IFavoriteRepository
    {
        private readonly List<FavoriteGame> _items = new();
        public IReadOnlyList<FavoriteGame> All => _items;
        public void Add(FavoriteGame favorite) => _items.Add(favorite);
        public void Update(FavoriteGame favorite)
        {
            int i = _items.FindIndex(f => f.Id == favorite.Id);
            if (i >= 0) _items[i] = favorite;
        }
        public void Remove(FavoriteGame favorite) => _items.RemoveAll(f => f.Id == favorite.Id);
        public void Replace(IEnumerable<FavoriteGame> favorites)
        {
            _items.Clear();
            _items.AddRange(favorites);
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public void Save() { }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public FavoriteGame? EditResult { get; set; }
        public Task<Account?> ShowAddAccountAsync() => Task.FromResult<Account?>(null);
        public string? Prompt(string title, string initialValue) => null;
        public FavoriteGame? EditFavorite(FavoriteGame existing) => EditResult;
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

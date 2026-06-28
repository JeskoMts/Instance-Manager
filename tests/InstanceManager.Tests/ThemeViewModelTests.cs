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

public sealed class ThemeViewModelTests
{
    [Fact]
    public void MoveTheme_PersistsOneOrderedCollectionWithoutChangingSelection()
    {
        var settings = new FakeSettingsService();
        var repository = new FakeThemeRepository();
        var viewModel = CreateViewModel(settings, repository);
        ThemeDefinition selected = viewModel.SelectedTheme!;
        ThemeDefinition moved = viewModel.Themes.Single(theme => theme.Id == BuiltInThemes.Sand.Id);
        ThemeDefinition target = viewModel.Themes[0];

        viewModel.MoveTheme(moved, target);

        Assert.Same(moved, viewModel.Themes[0]);
        Assert.Same(selected, viewModel.SelectedTheme);
        Assert.Equal(viewModel.Themes.Select(theme => theme.Id), settings.Settings.ThemeOrder);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public void ApplyOrder_SwapsTwoThemesWithoutMovingOthers_PreservesSelection_AndPersistsOnce()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings, new FakeThemeRepository());
        ThemeDefinition selected = viewModel.SelectedTheme!;
        var original = viewModel.Themes.ToList();

        var desired = original.ToList();
        (desired[1], desired[4]) = (desired[4], desired[1]);

        viewModel.ApplyOrder(desired);

        Assert.Same(original[4], viewModel.Themes[1]);
        Assert.Same(original[1], viewModel.Themes[4]);
        Assert.Same(original[0], viewModel.Themes[0]);
        Assert.Same(original[2], viewModel.Themes[2]);
        Assert.Same(original[3], viewModel.Themes[3]);
        Assert.Same(selected, viewModel.SelectedTheme);
        Assert.Equal(viewModel.Themes.Select(theme => theme.Id), settings.Settings.ThemeOrder);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public void ApplyOrder_WhenOrderIsUnchanged_DoesNotPersist()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings, new FakeThemeRepository());
        var unchanged = viewModel.Themes.ToList();

        viewModel.ApplyOrder(unchanged);

        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void SelectingTheme_ReplacesHighlightWithoutActivating()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings, new FakeThemeRepository());
        string original = settings.Settings.ThemeId;
        ThemeDefinition overflow = viewModel.Themes[ThemeViewModel.PrimaryThemeCount];
        ThemeDefinition primary = viewModel.Themes[0];

        viewModel.SelectedTheme = overflow;
        Assert.Same(overflow, viewModel.SelectedTheme);

        viewModel.SelectedTheme = primary;

        Assert.Same(primary, viewModel.SelectedTheme);
        Assert.Equal(original, settings.Settings.ThemeId);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void Activate_AppliesAndPersistsTheTheme()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings, new FakeThemeRepository());
        ThemeDefinition target = viewModel.Themes[ThemeViewModel.PrimaryThemeCount];

        viewModel.Activate(target);

        Assert.Same(target, viewModel.SelectedTheme);
        Assert.Equal(target.Id, settings.Settings.ThemeId);
    }

    private static ThemeViewModel CreateViewModel(FakeSettingsService settings, FakeThemeRepository repository)
    {
        var themeService = new ThemeService(repository, settings);
        return new ThemeViewModel(themeService, repository, settings, new FakeDialogService(), new FakeShell());
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public int SaveCount { get; private set; }
        public void Save() => SaveCount++;
    }

    private sealed class FakeThemeRepository : IThemeRepository
    {
        public IReadOnlyList<ThemeDefinition> All { get; } = Array.Empty<ThemeDefinition>();
        public void Add(ThemeDefinition theme) { }
        public void Update(ThemeDefinition theme) { }
        public void Remove(string id) { }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<Account?> ShowAddAccountAsync() => Task.FromResult<Account?>(null);
        public string? Prompt(string title, string initialValue) => null;
        public FavoriteGame? EditFavorite(FavoriteGame existing) => null;
        public bool Confirm(string message) => false;
        public string? PickFolder(string title) => null;
    }

    private sealed class FakeShell : IShellCoordinator
    {
        public Task LaunchAsync(IReadOnlyList<Account> accounts) => Task.CompletedTask;
        public void SetStatus(string message) { }
    }
}

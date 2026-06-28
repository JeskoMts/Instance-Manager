using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    private readonly IRobloxGamesService _games;
    private readonly ShellViewModel _shell;

    private CancellationTokenSource? _searchCts;
    private bool _loadedOnce;

    public GamesViewModel(IRobloxGamesService games, ShellViewModel shell)
    {
        _games = games;
        _shell = shell;
    }

    public ObservableCollection<GameCardViewModel> Games { get; } = new();

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isEmpty;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        _ = RunSearchAsync(value);
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loadedOnce)
            return;
        _loadedOnce = true;
        await LoadPopularAsync();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void SelectGame(GameCardViewModel? card)
    {
        if (card == null)
            return;

        foreach (GameCardViewModel c in Games)
            c.IsSelected = false;
        card.IsSelected = true;

        _shell.ApplyGameTarget(card.PlaceId, card.Name);
    }

    private async Task RunSearchAsync(string query)
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        CancellationToken token = cts.Token;

        if (string.IsNullOrWhiteSpace(query))
        {
            await LoadPopularAsync(token);
            return;
        }

        IsLoading = true;
        try
        {
            IReadOnlyList<GameInfo> results = await _games.SearchAsync(query, token);
            if (token.IsCancellationRequested)
                return;
            Populate(results);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private async Task LoadPopularAsync(CancellationToken token = default)
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<GameInfo> results = await _games.GetPopularAsync(token);
            if (token.IsCancellationRequested)
                return;
            Populate(results);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private void Populate(IReadOnlyList<GameInfo> infos)
    {
        Games.Clear();
        long? selectedPlaceId = CurrentTargetPlaceId();

        foreach (GameInfo info in infos)
        {
            var card = new GameCardViewModel(info, _games);
            if (selectedPlaceId == info.PlaceId)
                card.IsSelected = true;
            Games.Add(card);
            _ = card.LoadThumbnailAsync();
        }

        IsEmpty = Games.Count == 0;
    }

    private long? CurrentTargetPlaceId() =>
        GameLinkParser.TryParsePlaceId(_shell.LaunchPanel.TargetInput, out long id) ? id : null;
}

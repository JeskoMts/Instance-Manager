using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using InstanceManager.Models;
using InstanceManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    private const string PopularKey = "\0popular";
    private const int ThumbnailWidth = 480;
    private const int MaxCachedThumbnails = 150;
    private const int MaxCachedLists = 12;
    private static readonly TimeSpan ListLifetime = TimeSpan.FromMinutes(5);

    private readonly IRobloxGamesService _games;
    private readonly ShellViewModel _shell;
    private readonly Func<DateTime> _clock;

    private readonly Dictionary<string, ImageSource> _thumbnails = new(StringComparer.Ordinal);
    private readonly Queue<string> _thumbnailOrder = new();
    private readonly Dictionary<string, (DateTime At, IReadOnlyList<GameInfo> Games)> _lists = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _searchCts;
    private bool _loadedOnce;

    public GamesViewModel(IRobloxGamesService games, ShellViewModel shell, Func<DateTime>? clock = null)
    {
        _games = games;
        _shell = shell;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public BulkObservableCollection<GameCardViewModel> Games { get; } = new();

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
        await LoadAsync(PopularKey, _games.GetPopularAsync);
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

    private Task RunSearchAsync(string query) =>
        string.IsNullOrWhiteSpace(query)
            ? LoadAsync(PopularKey, _games.GetPopularAsync)
            : LoadAsync(query.Trim(), token => _games.SearchAsync(query, token));

    private async Task LoadAsync(string key, Func<CancellationToken, Task<IReadOnlyList<GameInfo>>> load)
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        CancellationToken token = cts.Token;

        if (_lists.TryGetValue(key, out var cached) && _clock() - cached.At < ListLifetime)
        {
            IsLoading = false;
            Populate(cached.Games, token);
            return;
        }

        IsLoading = true;
        try
        {
            IReadOnlyList<GameInfo> results = await load(token);
            if (token.IsCancellationRequested)
                return;
            if (results.Count > 0)
                RememberList(key, results);
            Populate(results, token);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private void RememberList(string key, IReadOnlyList<GameInfo> games)
    {
        if (_lists.Count >= MaxCachedLists && !_lists.ContainsKey(key))
        {
            string? oldest = null;
            DateTime oldestAt = DateTime.MaxValue;
            foreach ((string k, (DateTime At, IReadOnlyList<GameInfo> _) entry) in _lists)
            {
                if (entry.At < oldestAt)
                {
                    oldestAt = entry.At;
                    oldest = k;
                }
            }
            if (oldest != null)
                _lists.Remove(oldest);
        }
        _lists[key] = (_clock(), games);
    }

    private void Populate(IReadOnlyList<GameInfo> infos, CancellationToken token)
    {
        long? selectedPlaceId = CurrentTargetPlaceId();
        var cards = new List<GameCardViewModel>(infos.Count);

        foreach (GameInfo info in infos)
        {
            var card = new GameCardViewModel(info, LoadThumbnailAsync);
            if (selectedPlaceId == info.PlaceId)
                card.IsSelected = true;
            if (info.ThumbnailUrl != null && _thumbnails.TryGetValue(info.ThumbnailUrl, out ImageSource? image))
                card.Thumbnail = image;
            else
                _ = card.LoadThumbnailAsync(token);
            cards.Add(card);
        }

        Games.Reset(cards);
        IsEmpty = Games.Count == 0;
    }

    internal async Task<ImageSource?> LoadThumbnailAsync(string? url, CancellationToken token)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        if (_thumbnails.TryGetValue(url, out ImageSource? cached))
            return cached;

        byte[]? bytes = await _games.GetThumbnailAsync(url, token);
        if (bytes is not { Length: > 0 })
            return null;

        ImageSource image = await Task.Run(() => AccountRowViewModel.DecodeFrozen(bytes, ThumbnailWidth), token);
        if (!_thumbnails.ContainsKey(url))
        {
            _thumbnails[url] = image;
            _thumbnailOrder.Enqueue(url);
            while (_thumbnailOrder.Count > MaxCachedThumbnails)
                _thumbnails.Remove(_thumbnailOrder.Dequeue());
        }
        return image;
    }

    private long? CurrentTargetPlaceId() =>
        GameLinkParser.TryParsePlaceId(_shell.LaunchPanel.TargetInput, out long id) ? id : null;
}

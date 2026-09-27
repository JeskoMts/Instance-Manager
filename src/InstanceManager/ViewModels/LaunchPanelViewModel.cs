using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class LaunchPanelViewModel : ObservableObject
{
    private readonly IFavoriteRepository _favorites;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IShellCoordinator _shell;
    private static readonly TimeSpan GameNameLookupTimeout = TimeSpan.FromSeconds(4);
    private static readonly Regex LeadingTags = new(@"^\s*(?:[\[(【][^\])】]{0,40}[\])】]\s*)+", RegexOptions.Compiled);

    private readonly IServerLinkResolver? _serverLinks;
    private readonly IRobloxGamesService? _games;
    private readonly Dictionary<long, string> _gameNames = new();

    public LaunchPanelViewModel(
        IFavoriteRepository favorites,
        ISettingsService settings,
        IDialogService dialogs,
        IShellCoordinator shell,
        IServerLinkResolver? serverLinks = null,
        IRobloxGamesService? games = null)
    {
        _favorites = favorites;
        _settings = settings;
        _dialogs = dialogs;
        _shell = shell;
        _serverLinks = serverLinks;
        _games = games;

        selectedMode = _settings.Settings.LastJoinMode;
        targetInput = _settings.Settings.LastTargetInput ?? string.Empty;
        jobIdInput = _settings.Settings.LastJobIdInput ?? string.Empty;

        MigrateLegacyFavorites();
        RebuildFavorites();
        RestoreLastSelectedFavorite();
    }

    private bool _restoringFavorite;

    public ObservableCollection<FavoriteGame> Favorites { get; } = new();

    public ObservableCollection<FavoriteGame> FilteredFavorites { get; } = new();

    [ObservableProperty] private JoinMode selectedMode;
    [ObservableProperty] private string targetInput = string.Empty;
    [ObservableProperty] private string jobIdInput = string.Empty;
    [ObservableProperty] private string favoriteSearchText = string.Empty;

    [ObservableProperty] private FavoriteGame? selectedFavorite;

    public bool HasFavorites => Favorites.Count > 0;

    public bool IsJobIdMode => SelectedMode == JoinMode.PrivateByJobId;

    partial void OnSelectedFavoriteChanged(FavoriteGame? value)
    {
        _settings.Settings.LastSelectedFavoriteId = value?.Id;
        _settings.ScheduleSave();
        if (value != null)
            ApplyFavorite(value);
    }

    private void RestoreLastSelectedFavorite()
    {
        if (_settings.Settings.LastSelectedFavoriteId is not Guid id) return;
        FavoriteGame? match = Favorites.FirstOrDefault(f => f.Id == id);
        if (match == null) return;

        _restoringFavorite = true;
        try { SelectedFavorite = match; }
        finally { _restoringFavorite = false; }
    }

    partial void OnFavoriteSearchTextChanged(string value) => RebuildFiltered();

    partial void OnSelectedModeChanged(JoinMode value)
    {
        _settings.Settings.LastJoinMode = value;
        _settings.ScheduleSave();
        OnPropertyChanged(nameof(IsJobIdMode));
    }

    partial void OnTargetInputChanged(string value)
    {
        _settings.Settings.LastTargetInput = string.IsNullOrWhiteSpace(value) ? null : value;
        _settings.ScheduleSave();
    }

    partial void OnJobIdInputChanged(string value)
    {
        _settings.Settings.LastJobIdInput = string.IsNullOrWhiteSpace(value) ? null : value;
        _settings.ScheduleSave();
    }

    [RelayCommand]
    private void TogglePrimary(FavoriteGame? favorite)
    {
        if (favorite == null) return;
        favorite.IsPrimary = !favorite.IsPrimary;
        PersistAndRebuild();
    }

    [RelayCommand]
    private void MoveFavoriteUp(FavoriteGame? favorite) => Move(favorite, -1);

    [RelayCommand]
    private void MoveFavoriteDown(FavoriteGame? favorite) => Move(favorite, +1);

    public void ReorderFavorite(FavoriteGame dragged, FavoriteGame target)
    {
        if (ReferenceEquals(dragged, target) || dragged.IsPrimary != target.IsPrimary)
            return;

        List<FavoriteGame> partition = Favorites
            .Where(favorite => favorite.IsPrimary == dragged.IsPrimary)
            .ToList();
        int from = partition.IndexOf(dragged);
        int to = partition.IndexOf(target);
        if (from < 0 || to < 0 || from == to)
            return;

        partition.RemoveAt(from);
        partition.Insert(to, dragged);
        for (int i = 0; i < partition.Count; i++)
            partition[i].SortOrder = i;

        PersistAndRebuild();
    }

    private void Move(FavoriteGame? favorite, int direction)
    {
        if (favorite == null) return;

        List<FavoriteGame> partition = Favorites.Where(f => f.IsPrimary == favorite.IsPrimary).ToList();
        int index = partition.IndexOf(favorite);
        int target = index + direction;
        if (index < 0 || target < 0 || target >= partition.Count)
            return;

        (partition[index].SortOrder, partition[target].SortOrder) =
            (partition[target].SortOrder, partition[index].SortOrder);

        PersistAndRebuild();
    }

    public Task<ServerTargetResolution> ResolveTargetAsync(CancellationToken cancellationToken = default)
    {
        string input = SelectedMode == JoinMode.PrivateByJobId ? JobIdInput : TargetInput;
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(ServerTargetResolution.Success(ServerTarget.Home()));

        if (SelectedMode == JoinMode.PrivateByJobId)
        {
            return _serverLinks != null
                ? _serverLinks.ResolveAsync(JobIdInput, cancellationToken)
                : Task.FromResult(ServerTargetResolution.Failure("Server links can't be checked right now."));
        }

        if (!GameLinkParser.TryParsePlaceId(TargetInput, out long placeId))
            return Task.FromResult(ServerTargetResolution.Failure("Enter a valid game link or Place ID."));

        return Task.FromResult(ServerTargetResolution.Success(ServerTarget.Public(placeId)));
    }

    public bool TryBuildTarget(out ServerTarget target, out string error)
    {
        target = null!;
        error = string.Empty;

        if (!GameLinkParser.TryParsePlaceId(TargetInput, out long placeId))
        {
            error = "Enter a valid game link or Place ID.";
            return false;
        }

        if (SelectedMode == JoinMode.PrivateByJobId)
        {
            if (!GameLinkParser.TryParseJobId(JobIdInput, out string jobId))
            {
                error = "Enter a valid Job ID (GUID).";
                return false;
            }
            target = ServerTarget.ByJob(placeId, jobId);
        }
        else
        {
            target = ServerTarget.Public(placeId);
        }
        return true;
    }

    public void ApplyGameTargetFromGames(long placeId)
    {
        SelectedFavorite = null;
        JobIdInput = string.Empty;
        TargetInput = placeId.ToString();
        SelectedMode = JoinMode.PublicByLink;
    }

    public void RememberGameName(long placeId, string? name)
    {
        if (placeId > 0 && !string.IsNullOrWhiteSpace(name))
            _gameNames[placeId] = name.Trim();
    }

    [RelayCommand]
    private void ApplyFavorite(FavoriteGame? favorite)
    {
        if (favorite == null) return;

        if (!string.IsNullOrWhiteSpace(favorite.DefaultJobId))
        {
            JobIdInput = $"https://www.roblox.com/games/start?placeId={favorite.PlaceId}" +
                         $"&gameInstanceId={favorite.DefaultJobId}";
            SelectedMode = JoinMode.PrivateByJobId;
        }
        else
        {
            TargetInput = favorite.PlaceId.ToString();
            SelectedMode = JoinMode.PublicByLink;
        }
        if (!_restoringFavorite)
            _shell.Notify(NotificationId.FavoriteApplied, NotificationKind.Info, "Favorite applied", $"Applied favorite '{favorite.Name}'.");
    }

    [RelayCommand]
    private async Task AddFavoriteAsync()
    {
        ServerTargetResolution resolution;
        try
        {
            resolution = await ResolveTargetAsync();
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or OperationCanceledException)
        {
            resolution = ServerTargetResolution.Failure("The server link can't be checked right now.");
        }

        if (resolution.Target is not { Mode: not JoinMode.Home } target)
        {
            string hint = resolution.IsSuccess
                ? SelectedMode == JoinMode.PrivateByJobId
                    ? "Paste a server link first."
                    : "Enter a game link or Place ID first."
                : resolution.Error;
            _shell.Notify(NotificationId.FavoriteNotSaved, NotificationKind.Error, "Favorite not saved", hint);
            return;
        }

        string? gameName = await LookUpGameNameAsync(target.PlaceId);
        string suggestion = gameName != null ? CleanGameName(gameName) : $"Game {target.PlaceId}";
        string? name = _dialogs.Prompt("Name this favorite", suggestion);
        if (string.IsNullOrWhiteSpace(name)) return;

        var favorite = new FavoriteGame { Name = name.Trim(), PlaceId = target.PlaceId, SortOrder = NextSortOrder() };
        if (target.Mode == JoinMode.PrivateByJobId && !string.IsNullOrWhiteSpace(target.JobId))
            favorite.DefaultJobId = target.JobId;

        _favorites.Add(favorite);
        RebuildFavorites();
        _shell.Notify(NotificationId.FavoriteSaved, NotificationKind.Success, "Favorite saved", $"Saved favorite '{favorite.Name}'.");
    }

    [RelayCommand]
    private void EditFavorite(FavoriteGame? favorite)
    {
        if (favorite == null) return;

        FavoriteGame? edited = _dialogs.EditFavorite(favorite);
        if (edited == null) return;

        edited.IsPrimary = favorite.IsPrimary;
        edited.SortOrder = favorite.SortOrder;

        bool wasSelected = SelectedFavorite?.Id == edited.Id;
        _favorites.Update(edited);
        RebuildFavorites();

        if (wasSelected)
            SelectedFavorite = Favorites.FirstOrDefault(f => f.Id == edited.Id);

        _shell.Notify(NotificationId.FavoriteUpdated, NotificationKind.Success, "Favorite updated", $"Updated favorite '{edited.Name}'.");
    }

    [RelayCommand]
    private void RemoveFavorite(FavoriteGame? favorite)
    {
        if (favorite == null) return;
        if (!_dialogs.Confirm(ConfirmAction.DeleteFavorite, $"Delete favorite '{favorite.Name}'?")) return;

        bool wasSelected = SelectedFavorite?.Id == favorite.Id;
        _favorites.Remove(favorite);
        RebuildFavorites();

        if (wasSelected)
            SelectedFavorite = null;

        _shell.Notify(NotificationId.FavoriteDeleted, NotificationKind.Success, "Favorite deleted", $"Deleted favorite '{favorite.Name}'.", () =>
        {
            _favorites.Add(favorite);
            RebuildFavorites();
            if (wasSelected)
                SelectedFavorite = Favorites.FirstOrDefault(f => f.Id == favorite.Id);
        });
    }

    internal static string CleanGameName(string gameName)
    {
        string name = LeadingTags.Replace(gameName, string.Empty);
        int start = 0;
        int end = name.Length;
        while (start < end && IsDecoration(name[start]))
            start++;
        while (end > start && IsDecoration(name[end - 1]))
            end--;
        name = name[start..end];
        return name.Length > 0 ? name : gameName.Trim();
    }

    private static bool IsDecoration(char c) =>
        char.IsWhiteSpace(c) || char.IsSurrogate(c) || CharUnicodeInfo.GetUnicodeCategory(c) is
            UnicodeCategory.OtherSymbol or UnicodeCategory.Format or
            UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark;

    private async Task<string?> LookUpGameNameAsync(long placeId)
    {
        if (_gameNames.TryGetValue(placeId, out string? known))
            return known;
        if (_games == null)
            return null;

        using var timeout = new CancellationTokenSource(GameNameLookupTimeout);
        string? name = await _games.GetGameNameAsync(placeId, timeout.Token);
        RememberGameName(placeId, name);
        return name;
    }

    private void PersistAndRebuild()
    {
        FavoriteGame? selected = SelectedFavorite;
        _favorites.Replace(SortedFavorites().ToList());
        RebuildFavorites();
        if (selected != null)
            SelectedFavorite = Favorites.FirstOrDefault(f => f.Id == selected.Id);
    }

    private int NextSortOrder() =>
        _favorites.All.Count == 0 ? 0 : _favorites.All.Max(f => f.SortOrder) + 1;

    private void RebuildFavorites()
    {
        Favorites.Clear();
        foreach (FavoriteGame f in SortedFavorites())
            Favorites.Add(f);
        OnPropertyChanged(nameof(HasFavorites));
        RebuildFiltered();
    }

    private void RebuildFiltered()
    {
        string query = FavoriteSearchText?.Trim() ?? string.Empty;
        FilteredFavorites.Clear();
        foreach (FavoriteGame f in Favorites)
        {
            if (query.Length == 0 ||
                f.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                FilteredFavorites.Add(f);
        }
    }

    private IEnumerable<FavoriteGame> SortedFavorites() =>
        _favorites.All
            .OrderByDescending(f => f.IsPrimary)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);

    private void MigrateLegacyFavorites()
    {
        List<FavoriteGame> all = _favorites.All.ToList();
        bool changed = false;

        if (all.Count > 0 && all.All(f => f.SortOrder == 0))
        {
            for (int i = 0; i < all.Count; i++)
                all[i].SortOrder = i;
            changed = true;
        }

        Guid? legacyPrimary = _settings.Settings.PrimaryFavoriteId;
        if (legacyPrimary != null)
        {
            if (!all.Any(f => f.IsPrimary))
            {
                FavoriteGame? match = all.FirstOrDefault(f => f.Id == legacyPrimary);
                if (match != null)
                {
                    match.IsPrimary = true;
                    changed = true;
                }
            }
            _settings.Settings.PrimaryFavoriteId = null;
            _settings.Save();
        }

        if (changed)
            _favorites.Replace(all);
    }
}

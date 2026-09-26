using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.ViewModels;

public partial class GameCardViewModel : ObservableObject
{
    private readonly IRobloxGamesService _games;

    public GameCardViewModel(GameInfo info, IRobloxGamesService games)
    {
        Info = info;
        _games = games;
    }

    public GameInfo Info { get; }

    public long PlaceId => Info.PlaceId;
    public string Name => Info.Name;
    public string CreatorName => Info.CreatorName;
    public string PlayerCountText => Info.PlayerCount.ToString("N0", CultureInfo.CurrentCulture);

    [ObservableProperty] private ImageSource? thumbnail;
    [ObservableProperty] private bool isSelected;

    public async Task LoadThumbnailAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            byte[]? bytes = await _games.GetThumbnailAsync(Info.ThumbnailUrl, cancellationToken);
            if (bytes is not { Length: > 0 })
                return;

            Thumbnail = await Task.Run(() => AccountRowViewModel.DecodeFrozen(bytes, 360), cancellationToken);
        }
        catch
        {
            Thumbnail = null;
        }
    }
}

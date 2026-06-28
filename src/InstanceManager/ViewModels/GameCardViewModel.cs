using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 360;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            Thumbnail = image;
        }
        catch
        {
            Thumbnail = null;
        }
    }
}

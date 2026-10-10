using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using InstanceManager.Models;

namespace InstanceManager.ViewModels;

public partial class GameCardViewModel : ObservableObject
{
    private readonly Func<string?, CancellationToken, Task<ImageSource?>> _loadThumbnail;

    public GameCardViewModel(GameInfo info, Func<string?, CancellationToken, Task<ImageSource?>> loadThumbnail)
    {
        Info = info;
        _loadThumbnail = loadThumbnail;
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
            ImageSource? image = await _loadThumbnail(Info.ThumbnailUrl, cancellationToken);
            if (image != null)
                Thumbnail = image;
        }
        catch
        {
            Thumbnail = null;
        }
    }
}

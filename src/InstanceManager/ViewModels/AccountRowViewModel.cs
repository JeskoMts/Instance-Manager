using System.IO;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InstanceManager.Models;
using InstanceManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class AccountRowViewModel : ObservableObject
{
    private readonly AccountListViewModel _parent;
    private readonly IRobloxAvatarService? _avatars;

    public AccountRowViewModel(
        Account account,
        AccountListViewModel parent,
        IRobloxAvatarService? avatars = null)
    {
        Account = account;
        _parent = parent;
        _avatars = avatars;
    }

    public Account Account { get; }

    public string DisplayLabel => Account.DisplayLabel;
    public string Username => Account.Username;
    public long UserId => Account.UserId;

    public bool IsGrouped => Account.GroupIds.Count > 0;

    public string Initials
    {
        get
        {
            string label = (DisplayLabel ?? string.Empty).Trim();
            if (label.Length == 0) return "?";
            string[] parts = label.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]));
            return char.ToUpperInvariant(label[0]).ToString();
        }
    }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isIndented;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(HasStatus))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(HasStatus), nameof(IsLaunching))]
    private LaunchStage? launchStage;

    [ObservableProperty]
    private string? launchError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool requiresSignIn;

    [ObservableProperty]
    private ImageSource? avatarImage;

    [RelayCommand]
    private void ClearGroups() => _parent.ClearGroups(this);

    partial void OnIsSelectedChanged(bool value) => _parent.RecountSelection();

    public void RefreshGroupState() => OnPropertyChanged(nameof(IsGrouped));

    public bool IsLaunching => LaunchStage is Services.LaunchStage.Starting or Services.LaunchStage.WaitingForWindow or Services.LaunchStage.Joining;

    public bool HasStatus => LaunchStage != null || IsRunning;

    public string StatusText => LaunchStage switch
    {
        Services.LaunchStage.Queued => "Queued",
        Services.LaunchStage.Starting => "Starting…",
        Services.LaunchStage.WaitingForWindow => "Opening Roblox…",
        Services.LaunchStage.Joining => "Joining game…",
        Services.LaunchStage.Failed => RequiresSignIn ? "Login expired" : "Failed",
        _ => IsRunning ? "Running" : "Idle"
    };

    public async Task LoadAvatarAsync()
    {
        if (_avatars is null)
            return;

        byte[]? cached = null;
        try
        {
            cached = await _avatars.GetCachedAvatarAsync(UserId);
            if (cached is { Length: > 0 })
                AvatarImage = await Task.Run(() => DecodeFrozen(cached, 72));
        }
        catch
        {
            cached = null;
        }

        try
        {
            byte[]? bytes = await _avatars.GetAvatarAsync(UserId);
            if (bytes is not { Length: > 0 } || (cached != null && bytes.AsSpan().SequenceEqual(cached)))
                return;

            AvatarImage = await Task.Run(() => DecodeFrozen(bytes, 72));
        }
        catch
        {
        }
    }

    internal static BitmapImage DecodeFrozen(byte[] bytes, int decodePixelWidth)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = decodePixelWidth;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public ObservableCollection<VersionChoiceViewModel> VersionChoices => _parent.VersionChoices;

    public VersionChoiceViewModel? SelectedVersionChoice
    {
        get
        {
            ObservableCollection<VersionChoiceViewModel> choices = _parent.VersionChoices;
            if (!string.IsNullOrEmpty(Account.PreferredVersionGuid))
            {
                foreach (VersionChoiceViewModel c in choices)
                {
                    if (c.VersionGuid == Account.PreferredVersionGuid)
                        return c;
                }
            }
            foreach (VersionChoiceViewModel c in choices)
            {
                if (c.IsDefault)
                    return c;
            }
            return null;
        }
        set
        {
            Account.PreferredVersionGuid = value?.VersionGuid;
            _parent.SaveAccountVersion(Account);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PinnedVersionLabel));
        }
    }

    public string? PinnedVersionLabel =>
        SelectedVersionChoice is { IsDefault: false } choice ? choice.Label : null;

    public void RefreshVersionChoice()
    {
        OnPropertyChanged(nameof(VersionChoices));
        OnPropertyChanged(nameof(SelectedVersionChoice));
        OnPropertyChanged(nameof(PinnedVersionLabel));
    }

    [RelayCommand]
    private Task Launch() => _parent.LaunchAccountAsync(this);

    [RelayCommand]
    private void Stop() => _parent.StopAccount(this);

    [RelayCommand]
    private void Remove() => _parent.RemoveAccount(this);

    [RelayCommand]
    private void Rename() => _parent.RenameAccount(this);

    public void RefreshLabel()
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(Initials));
    }
}

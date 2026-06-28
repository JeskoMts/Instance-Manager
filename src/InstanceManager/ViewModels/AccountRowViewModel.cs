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
    private bool isRunning;

    [ObservableProperty]
    private ImageSource? avatarImage;

    [ObservableProperty]
    private bool isMenuOpen;

    public ObservableCollection<GroupMembershipViewModel> GroupMemberships { get; } = new();

    public bool HasGroups => _parent.GroupModels.Count > 0;

    partial void OnIsMenuOpenChanged(bool value)
    {
        if (value)
        {
            RebuildMemberships();
            _parent.NotifyMenuOpened(this);
        }
        else
        {
            _parent.NotifyMenuClosed(this);
        }
    }

    public void CloseMenu() => IsMenuOpen = false;
    public void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    private void RebuildMemberships()
    {
        GroupMemberships.Clear();
        foreach (AccountGroup g in _parent.GroupModels)
            GroupMemberships.Add(new GroupMembershipViewModel(g, this, _parent));
        OnPropertyChanged(nameof(HasGroups));
    }

    [RelayCommand]
    private void ClearGroups() => _parent.ClearGroups(this);

    partial void OnIsSelectedChanged(bool value) => _parent.RecountSelection();

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(StatusText));

    public void RefreshGroupState() => OnPropertyChanged(nameof(IsGrouped));

    public string StatusText => IsRunning ? "Running" : "Idle";

    public async Task LoadAvatarAsync()
    {
        if (_avatars is null)
            return;

        try
        {
            byte[]? bytes = await _avatars.GetAvatarAsync(UserId);
            if (bytes is not { Length: > 0 })
                return;

            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 72;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            AvatarImage = image;
        }
        catch
        {
            AvatarImage = null;
        }
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
        }
    }

    public void RefreshVersionChoice()
    {
        OnPropertyChanged(nameof(VersionChoices));
        OnPropertyChanged(nameof(SelectedVersionChoice));
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

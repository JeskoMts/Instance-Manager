using System.Collections.ObjectModel;
using System.Threading.Tasks;
using InstanceManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class GroupViewModel : ObservableObject
{
    private readonly AccountListViewModel _parent;

    public GroupViewModel(AccountGroup? group, AccountListViewModel parent)
    {
        Group = group;
        _parent = parent;
        isExpanded = group?.IsExpanded ?? true;
    }

    public AccountGroup? Group { get; }

    public bool HasGroup => Group != null;

    public string Name => Group?.Name ?? "Ungrouped";

    public string ColorHex => Group?.ColorHex ?? "#6B7280";

    public ObservableCollection<AccountRowViewModel> Accounts { get; } = new();

    [ObservableProperty]
    private bool isExpanded = true;

    partial void OnIsExpandedChanged(bool value)
    {
        if (Group == null) return;
        Group.IsExpanded = value;
        _parent.PersistGroupState(Group);
    }

    [ObservableProperty]
    private bool isMenuOpen;

    partial void OnIsMenuOpenChanged(bool value)
    {
        if (value)
            _parent.NotifyMenuOpened(this);
        else
            _parent.NotifyMenuClosed(this);
    }

    public void CloseMenu() => IsMenuOpen = false;
    public void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    public int Count => Accounts.Count;

    public void NotifyCountChanged() => OnPropertyChanged(nameof(Count));

    [RelayCommand]
    private Task LaunchGroup() => _parent.LaunchGroupAsync(this);

    [RelayCommand]
    private void RenameGroup() => _parent.RenameGroup(this);

    [RelayCommand]
    private void DeleteGroup() => _parent.DeleteGroup(this);
}

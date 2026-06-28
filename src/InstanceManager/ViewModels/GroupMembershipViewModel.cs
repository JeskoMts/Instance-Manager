using InstanceManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class GroupMembershipViewModel : ObservableObject
{
    private readonly AccountRowViewModel _row;
    private readonly AccountListViewModel _parent;

    public GroupMembershipViewModel(AccountGroup group, AccountRowViewModel row, AccountListViewModel parent)
    {
        Group = group;
        _row = row;
        _parent = parent;
        isMember = row.Account.BelongsTo(group.Id);
    }

    public AccountGroup Group { get; }
    public string Name => Group.Name;
    public string ColorHex => Group.ColorHex;

    [ObservableProperty]
    private bool isMember;

    [RelayCommand]
    private void Toggle()
    {
        _parent.ToggleGroupMembership(_row, Group);
        IsMember = _row.Account.BelongsTo(Group.Id);
    }
}

using System.Collections.Generic;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Storage;

public sealed class GroupRepository : IGroupRepository
{
    private readonly JsonFileStore _file = new(AppPaths.GroupsFile);
    private readonly List<AccountGroup> _items;

    public GroupRepository()
    {
        AppPaths.EnsureDataDirectory();
        _items = _file.Load(() => new List<AccountGroup>());
    }

    public IReadOnlyList<AccountGroup> All => _items;

    public AccountGroup Add(string name, string colorHex)
    {
        var group = new AccountGroup
        {
            Name = name,
            ColorHex = colorHex,
            SortOrder = _items.Count == 0 ? 0 : _items.Max(g => g.SortOrder) + 1
        };
        Add(group);
        return group;
    }

    public void Add(AccountGroup group)
    {
        _items.Add(group);
        Save();
    }

    public void Update(AccountGroup group)
    {
        int idx = _items.FindIndex(g => g.Id == group.Id);
        if (idx >= 0) _items[idx] = group;
        Save();
    }

    public void Remove(AccountGroup group)
    {
        _items.RemoveAll(g => g.Id == group.Id);
        Save();
    }

    private void Save() => _file.Save(_items);
}

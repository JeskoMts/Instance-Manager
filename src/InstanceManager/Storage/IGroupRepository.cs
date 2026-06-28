using System.Collections.Generic;
using InstanceManager.Models;

namespace InstanceManager.Storage;

public interface IGroupRepository
{
    IReadOnlyList<AccountGroup> All { get; }
    AccountGroup Add(string name, string colorHex);
    void Add(AccountGroup group) { }
    void Update(AccountGroup group);
    void Remove(AccountGroup group);
}

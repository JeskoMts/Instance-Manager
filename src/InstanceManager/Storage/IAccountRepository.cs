using System.Collections.Generic;
using InstanceManager.Models;

namespace InstanceManager.Storage;

public interface IAccountRepository
{
    IReadOnlyList<Account> All { get; }
    Account? FindByUserId(long userId);
    void Upsert(Account account);
    void UpsertMany(IEnumerable<Account> accounts)
    {
        foreach (Account account in accounts)
            Upsert(account);
    }
    void Remove(Account account);
}

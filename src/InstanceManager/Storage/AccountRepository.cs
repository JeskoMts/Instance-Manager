using System.Collections.Generic;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Storage;

public sealed class AccountRepository : IAccountRepository
{
    private readonly JsonFileStore _file;
    private readonly List<Account> _items;

    public AccountRepository() : this(AppPaths.AccountsFile)
    {
        AppPaths.EnsureDataDirectory();
    }

    public AccountRepository(string path)
    {
        _file = new JsonFileStore(path);
        _items = _file.Load(() => new List<Account>());

        bool changed = false;
        foreach (var acc in _items)
        {
            changed |= acc.NormalizeGroupMemberships();
            if (acc.BrowserTrackerId == 0)
            {
                acc.BrowserTrackerId = RobloxLauncher.GenerateBrowserTrackerId();
                changed = true;
            }
        }
        if (changed)
        {
            Save();
        }
    }

    public IReadOnlyList<Account> All => _items;

    public Account? FindByUserId(long userId) => _items.FirstOrDefault(a => a.UserId == userId);

    public void Upsert(Account account)
    {
        int idx = _items.FindIndex(a => a.Id == account.Id);
        if (idx >= 0) _items[idx] = account;
        else _items.Add(account);
        Save();
    }

    public void UpsertMany(IEnumerable<Account> accounts)
    {
        foreach (Account account in accounts)
        {
            account.NormalizeGroupMemberships();
            int idx = _items.FindIndex(a => a.Id == account.Id);
            if (idx >= 0) _items[idx] = account;
            else _items.Add(account);
        }
        Save();
    }

    public void Remove(Account account)
    {
        _items.RemoveAll(a => a.Id == account.Id);
        Save();
    }

    private void Save() => _file.Save(_items);
}

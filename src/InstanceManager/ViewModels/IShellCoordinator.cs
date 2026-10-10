using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.ViewModels;

public interface IShellCoordinator
{
    Task LaunchAsync(IReadOnlyList<Account> accounts);

    Task LaunchAsync(IReadOnlyList<Account> accounts, ServerTarget target) => LaunchAsync(accounts);

    void StopAccounts(IEnumerable<Guid> accountIds)
    {
    }

    bool IsBusy => false;

    void SetStatus(string message);

    void Notify(NotificationId id, NotificationKind kind, string title, string message, Action? undoAction = null, string? actionLabel = null) => SetStatus(message);
}

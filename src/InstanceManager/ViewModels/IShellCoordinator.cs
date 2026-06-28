using System.Collections.Generic;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.ViewModels;

public interface IShellCoordinator
{
    Task LaunchAsync(IReadOnlyList<Account> accounts);

    void SetStatus(string message);

    void Notify(NotificationId id, NotificationKind kind, string title, string message, Action? undoAction = null) => SetStatus(message);
}

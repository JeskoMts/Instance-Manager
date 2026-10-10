using System.Collections.Generic;
using System.Threading.Tasks;
using InstanceManager.Models;

namespace InstanceManager.Services;

public interface IDialogService
{
    Task<Account?> ShowAddAccountAsync();

    string? Prompt(string title, string initialValue);

    FavoriteGame? EditFavorite(FavoriteGame existing);

    AccountGroup? EditGroup(AccountGroup? existing, string suggestedColor) => null;

    GroupDropChoice AskGroupDrop(string accountLabel, string groupName) => GroupDropChoice.Cancel;

    bool Confirm(string message);

    bool Confirm(ConfirmAction action, string message) => Confirm(message);

    string? PickFolder(string title);
}

using System.Threading.Tasks;
using System.Windows;
using InstanceManager.Models;
using InstanceManager.Storage;
using InstanceManager.Views;

namespace InstanceManager.Services;

public sealed class WpfDialogService : IDialogService
{
    private readonly RobloxAuthService _auth;
    private readonly DpapiSecureStore _secure;
    private readonly ISettingsService _settings;

    public WpfDialogService(RobloxAuthService auth, DpapiSecureStore secure, ISettingsService settings)
    {
        _auth = auth;
        _secure = secure;
        _settings = settings;
    }

    private static Window Owner => Application.Current.MainWindow;

    public Task<Account?> ShowAddAccountAsync() => AddAccountWindow.RunAsync(Owner, _auth, _secure);

    public string? Prompt(string title, string initialValue) => PromptDialog.Show(Owner, title, initialValue);

    public FavoriteGame? EditFavorite(FavoriteGame existing) => FavoriteEditorDialog.Show(Owner, existing);

    public ThemeDefinition? EditTheme(string? id, string name, ThemePalette palette, string title) =>
        ThemeEditorDialog.Show(Owner, id, name, palette, title);

    public AccountGroup? EditGroup(AccountGroup? existing, string suggestedColor) =>
        GroupEditorDialog.Show(Owner, existing, suggestedColor);

    public GroupDropChoice AskGroupDrop(string accountLabel, string groupName) =>
        DropActionDialog.Show(Owner, accountLabel, groupName);

    public bool Confirm(string message) => ConfirmDialog.Show(Owner, message);

    public bool Confirm(ConfirmAction action, string message) =>
        _settings.Settings.IsConfirmBypassed(action) || ConfirmDialog.Show(Owner, message);

    public string? PickFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }
}

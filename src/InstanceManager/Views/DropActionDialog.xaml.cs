using System.Windows;
using InstanceManager.Services;

namespace InstanceManager.Views;

public partial class DropActionDialog : Window
{
    private DropActionDialog(string accountLabel, string groupName)
    {
        InitializeComponent();
        TitleText.Text = $"Add '{accountLabel}' to {groupName}";
        MessageText.Text =
            $"'{accountLabel}' already belongs to other groups. Move it here (remove it from its other groups), " +
            "or add it to this group as well?";
    }

    public GroupDropChoice Choice { get; private set; } = GroupDropChoice.Cancel;

    public static GroupDropChoice Show(Window owner, string accountLabel, string groupName)
    {
        var dialog = new DropActionDialog(accountLabel, groupName) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private void Move_Click(object sender, RoutedEventArgs e) { Choice = GroupDropChoice.Move; Close(); }
    private void Add_Click(object sender, RoutedEventArgs e) { Choice = GroupDropChoice.Add; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { Choice = GroupDropChoice.Cancel; Close(); }
}

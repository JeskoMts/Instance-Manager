using System.Windows;

namespace InstanceManager.Views;

public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string confirmLabel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    public static bool Show(Window owner, string message, string title = "Confirm action", string confirmLabel = "Delete")
    {
        var dialog = new ConfirmDialog(title, message, confirmLabel) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}

using System.Windows;
using System.Windows.Input;

namespace InstanceManager.Views;

public partial class PromptDialog : Window
{
    private PromptDialog(string title, string initialValue)
    {
        InitializeComponent();
        TitleText.Text = title;
        InputBox.Text = initialValue;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string? Value { get; private set; }

    public static string? Show(Window owner, string title, string initialValue)
    {
        var dialog = new PromptDialog(title, initialValue) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Value = InputBox.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ok_Click(sender, e);
        else if (e.Key == Key.Escape) Cancel_Click(sender, e);
    }
}

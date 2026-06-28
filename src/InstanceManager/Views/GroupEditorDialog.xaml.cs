using System;
using System.Windows;
using System.Windows.Controls;
using InstanceManager.Models;

namespace InstanceManager.Views;

public partial class GroupEditorDialog : Window
{
    private readonly AccountGroup? _existing;
    private string _selectedColor;

    private GroupEditorDialog(AccountGroup? existing, string suggestedColor)
    {
        _existing = existing;
        _selectedColor = existing?.ColorHex ?? suggestedColor;
        InitializeComponent();
        TitleText.Text = existing == null ? "New group" : "Edit group";
        NameBox.Text = existing?.Name ?? "New group";
        Loaded += (_, _) => { SelectColorButton(); NameBox.Focus(); NameBox.SelectAll(); };
    }

    public AccountGroup? Result { get; private set; }

    public static AccountGroup? Show(Window owner, AccountGroup? existing, string suggestedColor)
    {
        var dialog = new GroupEditorDialog(existing, suggestedColor) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0) { ErrorText.Text = "Enter a group name."; ErrorText.Visibility = Visibility.Visible; return; }
        Result = new AccountGroup
        {
            Id = _existing?.Id ?? Guid.NewGuid(), Name = name, ColorHex = _selectedColor,
            SortOrder = _existing?.SortOrder ?? 0, IsExpanded = _existing?.IsExpanded ?? true
        };
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void Color_Checked(object sender, RoutedEventArgs e) { if (sender is RadioButton button && button.Tag is string color) _selectedColor = color; }
    private void SelectColorButton()
    {
        foreach (object child in ColorGrid.Children)
            if (child is RadioButton button && string.Equals(button.Tag as string, _selectedColor, StringComparison.OrdinalIgnoreCase)) button.IsChecked = true;
    }
}

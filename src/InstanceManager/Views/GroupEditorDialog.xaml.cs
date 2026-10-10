using System;
using System.Windows;
using InstanceManager.Controls;
using InstanceManager.Models;

namespace InstanceManager.Views;

public partial class GroupEditorDialog : Window
{
    private readonly AccountGroup? _existing;

    private GroupEditorDialog(AccountGroup? existing, string suggestedColor)
    {
        _existing = existing;
        InitializeComponent();
        TitleText.Text = existing == null ? "New group" : "Edit group";
        NameBox.Text = existing?.Name ?? "New group";
        Picker.Hex = ColorMath.NormalizeHex(existing?.ColorHex) ?? ColorMath.NormalizeHex(suggestedColor) ?? "#6E8FD9";
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
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
        if (name.Length == 0)
        {
            ErrorText.Text = "Give the group a name.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = new AccountGroup
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Name = name,
            ColorHex = ColorMath.NormalizeHex(Picker.Hex) ?? "#6E8FD9",
            SortOrder = _existing?.SortOrder ?? 0,
            IsExpanded = _existing?.IsExpanded ?? true
        };
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

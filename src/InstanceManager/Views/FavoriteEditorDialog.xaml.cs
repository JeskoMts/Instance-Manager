using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Views;

public partial class FavoriteEditorDialog : Window
{
    private readonly Guid _id;

    private FavoriteEditorDialog(FavoriteGame existing, string title)
    {
        InitializeComponent();
        TitleText.Text = title;
        _id = existing.Id;
        NameBox.Text = existing.Name;
        TargetBox.Text = existing.PlaceId > 0 ? existing.PlaceId.ToString(CultureInfo.InvariantCulture) : string.Empty;
        JobBox.Text = existing.DefaultJobId ?? string.Empty;

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public FavoriteGame? Result { get; private set; }

    public static FavoriteGame? Show(Window owner, FavoriteGame existing, string title = "Edit favorite")
    {
        var dialog = new FavoriteEditorDialog(existing, title) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Enter a name.");
            return;
        }

        if (!GameLinkParser.TryParsePlaceId(TargetBox.Text, out long placeId))
        {
            ShowError("Enter a valid game link or PlaceId.");
            return;
        }

        string? jobId = null;
        if (!string.IsNullOrWhiteSpace(JobBox.Text))
        {
            if (!GameLinkParser.TryParseJobId(JobBox.Text, out string parsed))
            {
                ShowError("Enter a valid Job ID (GUID) or leave it empty.");
                return;
            }
            jobId = parsed;
        }

        Result = new FavoriteGame { Id = _id, Name = name, PlaceId = placeId, DefaultJobId = jobId };
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Box_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ok_Click(sender, e);
        else if (e.Key == Key.Escape) Cancel_Click(sender, e);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

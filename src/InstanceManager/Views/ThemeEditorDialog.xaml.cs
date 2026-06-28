using System;
using System.Windows;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Views;

public partial class ThemeEditorDialog : Window
{
    private readonly string _id;

    private ThemeEditorDialog(string? id, string name, ThemePalette palette, string title)
    {
        InitializeComponent();
        _id = id ?? Guid.NewGuid().ToString("N");
        TitleText.Text = title;
        NameBox.Text = name;
        LoadPalette(palette);

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public ThemeDefinition? Result { get; private set; }

    public static ThemeDefinition? Show(Window owner, string? id, string name, ThemePalette palette, string title)
    {
        var dialog = new ThemeEditorDialog(id, name, palette, title) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void LoadPalette(ThemePalette p)
    {
        WindowBox.Text = p.Window;
        SurfaceBox.Text = p.Surface;
        SurfaceHoverBox.Text = p.SurfaceHover;
        SurfaceActiveBox.Text = p.SurfaceActive;
        ElevatedBox.Text = p.Elevated;
        ElevatedHoverBox.Text = p.ElevatedHover;
        BorderBox.Text = p.Border;
        BorderStrongBox.Text = p.BorderStrong;
        AccentBox.Text = p.Accent;
        OnAccentBox.Text = p.OnAccent;
        TextPrimaryBox.Text = p.TextPrimary;
        TextSecondaryBox.Text = p.TextSecondary;
        TextMutedBox.Text = p.TextMuted;
        SuccessBox.Text = p.Success;
        DangerBox.Text = p.Danger;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("Enter a theme name.");
            return;
        }

        var fields = new (string Label, string Value)[]
        {
            ("Window / App", WindowBox.Text), ("Surface", SurfaceBox.Text),
            ("Surface hover", SurfaceHoverBox.Text), ("Surface active", SurfaceActiveBox.Text),
            ("Elevated", ElevatedBox.Text), ("Elevated hover", ElevatedHoverBox.Text),
            ("Primary text", TextPrimaryBox.Text), ("Secondary text", TextSecondaryBox.Text),
            ("Muted text", TextMutedBox.Text), ("Border", BorderBox.Text),
            ("Border strong", BorderStrongBox.Text), ("Accent", AccentBox.Text),
            ("On accent", OnAccentBox.Text), ("Success", SuccessBox.Text), ("Danger", DangerBox.Text)
        };

        foreach ((string label, string value) in fields)
        {
            if (!ThemeService.TryParse(value, out _))
            {
                ShowError($"'{label}' is not a valid color (e.g. #1A1A1E).");
                return;
            }
        }

        Result = new ThemeDefinition
        {
            Id = _id,
            Name = name,
            IsBuiltIn = false,
            Palette = new ThemePalette
            {
                Window = WindowBox.Text.Trim(), Surface = SurfaceBox.Text.Trim(),
                SurfaceHover = SurfaceHoverBox.Text.Trim(), SurfaceActive = SurfaceActiveBox.Text.Trim(),
                Elevated = ElevatedBox.Text.Trim(), ElevatedHover = ElevatedHoverBox.Text.Trim(),
                Border = BorderBox.Text.Trim(), BorderStrong = BorderStrongBox.Text.Trim(),
                Accent = AccentBox.Text.Trim(), OnAccent = OnAccentBox.Text.Trim(),
                TextPrimary = TextPrimaryBox.Text.Trim(), TextSecondary = TextSecondaryBox.Text.Trim(),
                TextMuted = TextMutedBox.Text.Trim(), Success = SuccessBox.Text.Trim(),
                Danger = DangerBox.Text.Trim()
            }
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

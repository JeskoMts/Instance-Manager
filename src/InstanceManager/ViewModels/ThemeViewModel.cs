using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InstanceManager.ViewModels;

public partial class ThemeViewModel : ObservableObject
{
    private readonly ThemeService _themeService;
    private readonly IThemeRepository _repository;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IShellCoordinator _shell;

    public ThemeViewModel(ThemeService themeService, IThemeRepository repository, ISettingsService settings, IDialogService dialogs, IShellCoordinator shell)
    {
        _themeService = themeService;
        _repository = repository;
        _settings = settings;
        _dialogs = dialogs;
        _shell = shell;

        RefreshThemes();
        selectedTheme = Themes.FirstOrDefault(t => t.Id == _settings.Settings.ThemeId) ?? Themes.FirstOrDefault();
    }

    public const int PrimaryThemeCount = 8;

    public ObservableCollection<ThemeDefinition> Themes { get; } = new();

    public bool HasMoreThemes => Themes.Count > PrimaryThemeCount;

    [ObservableProperty] private bool showAllThemes;

    [ObservableProperty] private ThemeDefinition? selectedTheme;

    public bool IsCustomSelected => SelectedTheme is { IsBuiltIn: false };

    partial void OnSelectedThemeChanged(ThemeDefinition? value)
    {
        OnPropertyChanged(nameof(IsCustomSelected));
    }

    public void Activate(ThemeDefinition? theme)
    {
        if (theme == null)
            return;

        SelectedTheme = theme;
        _themeService.Apply(theme.Palette);
        _settings.Settings.ThemeId = theme.Id;
        _settings.Save();
    }

    [RelayCommand]
    private void ApplyTheme(ThemeDefinition? theme)
    {
        if (theme == null)
            return;

        Activate(theme);
        _shell.Notify(NotificationId.ThemeApplied, NotificationKind.Success,
            "Theme applied", $"Applied '{theme.Name}'.");
    }

    [RelayCommand]
    private void CreateTheme()
    {
        ThemePalette seed = (SelectedTheme?.Palette ?? BuiltInThemes.Dark.Palette).Clone();
        ThemeDefinition? created = _dialogs.EditTheme(null, "My theme", seed, "New theme");
        if (created == null)
            return;

        _repository.Add(created);
        RefreshThemes();
        SelectById(created.Id);
        _shell.Notify(NotificationId.ThemeCreated, NotificationKind.Success,
            "Theme created", $"Created '{created.Name}'.");
    }

    [RelayCommand]
    private void EditTheme(ThemeDefinition? theme)
    {
        if (theme is not { IsBuiltIn: false })
            return;

        ThemeDefinition? edited = _dialogs.EditTheme(theme.Id, theme.Name, theme.Palette.Clone(), "Edit theme");
        if (edited == null)
            return;

        _repository.Update(edited);
        RefreshThemes();
        SelectById(edited.Id);
        _shell.Notify(NotificationId.ThemeUpdated, NotificationKind.Success,
            "Theme updated", $"Updated '{edited.Name}'.");
    }

    [RelayCommand]
    private void DeleteTheme(ThemeDefinition? theme)
    {
        if (theme is not { IsBuiltIn: false })
            return;
        if (!_dialogs.Confirm(ConfirmAction.DeleteTheme, $"Delete theme '{theme.Name}'?"))
            return;

        bool wasSelected = SelectedTheme?.Id == theme.Id;
        _repository.Remove(theme.Id);
        RefreshThemes();

        if (wasSelected)
            SelectById(BuiltInThemes.Dark.Id);

        _shell.Notify(NotificationId.ThemeDeleted, NotificationKind.Success,
            "Theme deleted", $"Deleted '{theme.Name}'.");
    }

    private void SelectById(string id) => Activate(Themes.FirstOrDefault(t => t.Id == id));

    [RelayCommand]
    private void ExportTheme(ThemeDefinition? theme)
    {
        theme ??= SelectedTheme;
        if (theme is not { IsBuiltIn: false })
            return;

        try
        {
            Clipboard.SetText(ThemeCodec.Encode(theme));
        }
        catch (Exception)
        {
            _shell.Notify(NotificationId.ThemeExported, NotificationKind.Error, "Copy failed",
                "Could not copy the theme code to the clipboard.");
            return;
        }

        _shell.Notify(NotificationId.ThemeExported, NotificationKind.Success, "Theme code copied",
            $"Copied '{theme.Name}' — paste it to share.");
    }

    [RelayCommand]
    private void ImportTheme()
    {
        string code = BoundedClipboardTextReader.TryRead(
            ThemeCodec.MaxCodeCharacters,
            out string clipboardText)
            ? clipboardText
            : string.Empty;

        if (!ThemeCodec.TryDecode(code, out ThemeDefinition? imported))
        {
            _shell.Notify(NotificationId.ThemeImportFailed, NotificationKind.Error, "Import failed",
                "The clipboard doesn't contain a valid theme code.");
            return;
        }

        imported.Id = Guid.NewGuid().ToString("N");
        imported.IsBuiltIn = false;
        imported.Name = MakeUniqueName(imported.Name.Trim());

        _repository.Add(imported);
        RefreshThemes();
        Activate(Themes.FirstOrDefault(t => t.Id == imported.Id));

        _shell.Notify(NotificationId.ThemeImported, NotificationKind.Success, "Theme imported",
            $"Imported '{imported.Name}'.");
    }

    private string MakeUniqueName(string name)
    {
        string baseName = string.IsNullOrWhiteSpace(name) ? "Imported theme" : name;
        if (Themes.All(t => !string.Equals(t.Name, baseName, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        for (int i = 2; ; i++)
        {
            string candidate = $"{baseName} ({i})";
            if (Themes.All(t => !string.Equals(t.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    private void RefreshThemes()
    {
        string? selectedId = SelectedTheme?.Id;
        Themes.Clear();
        foreach (ThemeDefinition t in OrderByPreference(_themeService.AllThemes()))
            Themes.Add(t);
        OnPropertyChanged(nameof(HasMoreThemes));

        SelectedTheme = Themes.FirstOrDefault(t => t.Id == selectedId);
    }

    private IEnumerable<ThemeDefinition> OrderByPreference(IEnumerable<ThemeDefinition> themes)
    {
        List<string> order = _settings.Settings.ThemeOrder;
        return themes
            .Select((theme, index) => (theme, index))
            .OrderBy(x =>
            {
                int rank = order.IndexOf(x.theme.Id);
                return rank >= 0 ? rank : order.Count + x.index;
            })
            .Select(x => x.theme);
    }

    public void MoveTheme(ThemeDefinition dragged, ThemeDefinition target)
    {
        int from = Themes.IndexOf(dragged);
        int to = Themes.IndexOf(target);
        if (from < 0 || to < 0 || from == to)
            return;

        Themes.Move(from, to);
        _settings.Settings.ThemeOrder = Themes.Select(t => t.Id).ToList();
        _settings.Save();
    }

    public void ApplyOrder(IReadOnlyList<ThemeDefinition> order)
    {
        bool changed = false;
        for (int i = 0; i < order.Count; i++)
        {
            int current = Themes.IndexOf(order[i]);
            if (current >= 0 && current != i)
            {
                Themes.Move(current, i);
                changed = true;
            }
        }

        if (!changed)
            return;

        _settings.Settings.ThemeOrder = Themes.Select(t => t.Id).ToList();
        _settings.Save();
    }
}

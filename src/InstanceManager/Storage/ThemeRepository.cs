using System.Collections.Generic;
using InstanceManager.Models;
using InstanceManager.Services;

namespace InstanceManager.Storage;

public sealed class ThemeRepository : IThemeRepository
{
    private readonly JsonFileStore _file = new(AppPaths.ThemesFile);
    private readonly List<ThemeDefinition> _items;

    public ThemeRepository()
    {
        AppPaths.EnsureDataDirectory();
        _items = _file.Load(() => new List<ThemeDefinition>());
        foreach (ThemeDefinition t in _items)
            t.IsBuiltIn = false;
    }

    public IReadOnlyList<ThemeDefinition> All => _items;

    public void Add(ThemeDefinition theme)
    {
        theme.IsBuiltIn = false;
        _items.Add(theme);
        _file.Save(_items);
    }

    public void Update(ThemeDefinition theme)
    {
        int index = _items.FindIndex(t => t.Id == theme.Id);
        if (index < 0)
            return;

        theme.IsBuiltIn = false;
        _items[index] = theme;
        _file.Save(_items);
    }

    public void Remove(string id)
    {
        _items.RemoveAll(t => t.Id == id);
        _file.Save(_items);
    }
}

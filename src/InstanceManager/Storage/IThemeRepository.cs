using System.Collections.Generic;
using InstanceManager.Models;

namespace InstanceManager.Storage;

public interface IThemeRepository
{
    IReadOnlyList<ThemeDefinition> All { get; }

    void Add(ThemeDefinition theme);
    void Update(ThemeDefinition theme);
    void Remove(string id);
}

namespace InstanceManager.Models;

public sealed class ThemeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public ThemePalette Palette { get; set; } = new();
}

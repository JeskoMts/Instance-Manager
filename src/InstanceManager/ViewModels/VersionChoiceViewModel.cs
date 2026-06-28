using InstanceManager.Models;

namespace InstanceManager.ViewModels;

public sealed class VersionChoiceViewModel
{
    public VersionChoiceViewModel(RobloxVersion? version) => Version = version;

    public RobloxVersion? Version { get; }

    public bool IsDefault => Version == null;

    public string? VersionGuid => Version?.VersionGuid;

    public string Label => IsDefault ? "Default (global)" : Version!.DisplayLabel;
}

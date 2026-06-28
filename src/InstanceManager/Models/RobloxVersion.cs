using System.IO;

namespace InstanceManager.Models;

public sealed class RobloxVersion
{
    public required string FolderPath { get; init; }

    public required string VersionGuid { get; init; }

    public required string PlayerExePath { get; init; }

    public required string FileVersion { get; init; }

    public long BuildNumber { get; init; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(FileVersion) ? VersionGuid : FileVersion;

    public bool IsValid => File.Exists(PlayerExePath);
}

using System;
using System.IO;

namespace InstanceManager.Services;

internal static class LocalPathPolicy
{
    public static bool TryNormalizeFixedLocalDirectory(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string candidate = value.Trim();
        if (!Path.IsPathFullyQualified(candidate) ||
            candidate.StartsWith(@"\\", StringComparison.Ordinal) ||
            candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(candidate);
            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root) || new DriveInfo(root).DriveType != DriveType.Fixed)
                return false;

            path = Path.TrimEndingDirectorySeparator(fullPath);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or
            NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

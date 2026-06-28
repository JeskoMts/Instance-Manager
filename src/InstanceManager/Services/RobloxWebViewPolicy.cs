using System;

namespace InstanceManager.Services;

internal static class RobloxWebViewPolicy
{
    public static bool IsAllowedTopLevelNavigation(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (!uri.IsDefaultPort && uri.Port != 443))
        {
            return false;
        }

        string host = uri.IdnHost;
        return string.Equals(host, "roblox.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase);
    }
}

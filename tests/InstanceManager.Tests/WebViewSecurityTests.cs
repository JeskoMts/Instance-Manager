using System;
using System.IO;
using System.Linq;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public sealed class WebViewSecurityTests
{
    [Theory]
    [InlineData("https://www.roblox.com/login")]
    [InlineData("https://auth.roblox.com/v2/login")]
    [InlineData("https://roblox.com/home")]
    public void LoginNavigationPolicy_AllowsOnlyRobloxHttpsOrigins(string uri)
    {
        Assert.True(RobloxWebViewPolicy.IsAllowedTopLevelNavigation(uri));
    }

    [Theory]
    [InlineData("http://www.roblox.com/login")]
    [InlineData("https://roblox.com.evil.example/login")]
    [InlineData("https://evil.example/")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("roblox-player:1+launchmode:play")]
    [InlineData("")]
    public void LoginNavigationPolicy_RejectsForeignAndExternalUris(string uri)
    {
        Assert.False(RobloxWebViewPolicy.IsAllowedTopLevelNavigation(uri));
    }

    [Fact]
    public void AddAccountWindow_UsesInPrivateProfileAndClosesBrowserEscapeRoutes()
    {
        string code = File.ReadAllText(FindWorkspaceFile(
            "src", "InstanceManager", "Views", "AddAccountWindow.xaml.cs"));

        Assert.Contains("IsInPrivateModeEnabled = true", code, StringComparison.Ordinal);
        Assert.Contains("NavigationStarting +=", code, StringComparison.Ordinal);
        Assert.Contains("NewWindowRequested +=", code, StringComparison.Ordinal);
        Assert.Contains("DownloadStarting +=", code, StringComparison.Ordinal);
        Assert.Contains("LaunchingExternalUriScheme +=", code, StringComparison.Ordinal);
        Assert.Contains("DeleteAllCookies", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WebViewProfile_UsesLocalNonRoamingStorage()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.StartsWith(local, AppPaths.WebViewDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.False(AppPaths.WebViewDirectory.StartsWith(roaming, StringComparison.OrdinalIgnoreCase));
    }

    private static string FindWorkspaceFile(params string[] relativeParts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}

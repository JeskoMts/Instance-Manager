using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InstanceManager.Tests;

public sealed class LifecycleContractTests
{
    [Fact]
    public void BindingTrace_IsDebugOnly()
    {
        string app = Read("src", "InstanceManager", "App.xaml.cs");

        Assert.Contains("#if DEBUG", app, StringComparison.Ordinal);
        Assert.Contains("PresentationTraceSources", app, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAccountWindow_UnsubscribesAndDisposesWebViewOnClose()
    {
        string window = Read("src", "InstanceManager", "Views", "AddAccountWindow.xaml.cs");

        Assert.Contains("Closed += AddAccountWindow_Closed", window, StringComparison.Ordinal);
        Assert.Contains("NavigationCompleted -= WebView_NavigationCompleted", window, StringComparison.Ordinal);
        Assert.Contains("WebView.Dispose()", window, StringComparison.Ordinal);
    }

    [Fact]
    public void InstanceTracker_DisposesExitedProcesses()
    {
        string tracker = Read("src", "InstanceManager", "Services", "InstanceTracker.cs");

        Assert.Contains("DisposeTrackedProcess", tracker, StringComparison.Ordinal);
        Assert.Contains("process.Dispose()", tracker, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_AppliesSavedMultiInstanceSetting()
    {
        string app = Read("src", "InstanceManager", "App.xaml.cs");

        Assert.Contains("GetRequiredService<ISettingsService>()", app, StringComparison.Ordinal);
        Assert.Contains("multiInstance.TryApply(settings.Settings.MultiInstanceEnabled)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("multiInstance.Apply(settings.Settings.MultiInstanceEnabled)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRequiredService<MultiInstanceManager>().EnsureHeld()", app, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(parts));
    }
}

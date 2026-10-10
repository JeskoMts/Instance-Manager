using System;
using System.Threading;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.ViewModels;
using Microsoft.Win32;
using Xunit;

namespace InstanceManager.Tests;

public sealed class DesktopIntegrationTests
{
    [Fact]
    public void Startup_WritesQuotedPathWithStartupFlagAndCanBeTurnedOff()
    {
        string key = @"Software\InstanceManager.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            var startup = new StartupService(key, @"C:\Apps\Instance Manager.exe");
            Assert.False(startup.IsEnabled);

            Assert.True(startup.SetEnabled(true));
            Assert.True(startup.IsEnabled);
            Assert.Equal("\"C:\\Apps\\Instance Manager.exe\" --startup", ReadValue(key));

            new StartupService(key, @"D:\Moved\Instance Manager.exe").RefreshPath();
            Assert.Equal("\"D:\\Moved\\Instance Manager.exe\" --startup", ReadValue(key));

            Assert.True(startup.SetEnabled(false));
            Assert.False(startup.IsEnabled);
            new StartupService(key, @"D:\Moved\Instance Manager.exe").RefreshPath();
            Assert.Null(ReadValue(key));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Startup_WithoutAKnownExecutableDoesNothing()
    {
        string key = @"Software\InstanceManager.Tests." + Guid.NewGuid().ToString("N");
        try
        {
            Assert.False(new StartupService(key, string.Empty).SetEnabled(true));
            Assert.Null(ReadValue(key));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void SingleInstance_SecondCopyIsRefusedAndWakesTheFirstOne()
    {
        string name = "InstanceManager.Tests." + Guid.NewGuid().ToString("N");
        SingleInstance first = SingleInstance.TryAcquire(name, TimeSpan.Zero)
                               ?? throw new InvalidOperationException("first acquire failed");
        using var activated = new ManualResetEventSlim();
        try
        {
            Assert.Null(AcquireOnOtherThread(name));

            first.ListenForActivation(activated.Set);
            Assert.True(SingleInstance.SignalExisting(name));
            Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            first.Dispose();
        }

        Assert.NotNull(AcquireOnOtherThread(name));
    }

    [Fact]
    public void ServerHop_PicksAnotherServerWithEnoughFreeSlots()
    {
        var servers = new[]
        {
            new PublicServer("current", 2, 20),
            new PublicServer("full", 19, 20),
            new PublicServer("roomy", 10, 20)
        };

        for (int i = 0; i < 20; i++)
            Assert.Equal("roomy", ShellViewModel.PickServer(servers, 3, "CURRENT")?.JobId);
        Assert.Null(ShellViewModel.PickServer(servers, 11, "current"));
        Assert.Null(ShellViewModel.PickServer(Array.Empty<PublicServer>(), 1, null));
    }

    private static string? ReadValue(string key)
    {
        using RegistryKey? opened = Registry.CurrentUser.OpenSubKey(key);
        return opened?.GetValue(StartupService.ValueName) as string;
    }

    private static object? AcquireOnOtherThread(string name)
    {
        object? result = null;
        var thread = new Thread(() =>
        {
            SingleInstance? instance = SingleInstance.TryAcquire(name, TimeSpan.Zero);
            result = instance == null ? null : new object();
            instance?.Dispose();
        });
        thread.Start();
        thread.Join();
        return result;
    }
}

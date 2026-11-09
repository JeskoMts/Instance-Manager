using InstanceManager.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace InstanceManager.Tests;

[CollectionDefinition("Roblox singleton", DisableParallelization = true)]
public sealed class RobloxSingletonCollectionDefinition
{
}

/// <summary>A manager wired to a fake process host, so tests never touch real Roblox processes.</summary>
internal static class TestMultiInstance
{
    public static MultiInstanceManager Create(FakeRobloxProcessHost? host = null) =>
        new(host ?? new FakeRobloxProcessHost(), () => FakeRobloxProcessHost.Now, _ => { }, TimeSpan.FromHours(1));
}

internal sealed class FakeRobloxProcessHost : IRobloxProcessHost
{
    public static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    public List<RobloxProcessInfo> Processes { get; } = new();
    public HashSet<int> Protected { get; } = new();
    public List<int> Terminated { get; } = new();
    public List<int[]> Sweeps { get; } = new();
    public Dictionary<int, int> HandlesPerProcess { get; } = new();

    public RobloxProcessInfo Add(int id, TimeSpan age, bool window, string? commandLine)
    {
        var info = new RobloxProcessInfo(id, Now - age, window, commandLine);
        Processes.Add(info);
        HandlesPerProcess[id] = 2;
        return info;
    }

    public IReadOnlyList<RobloxProcessInfo> List() => Processes.ToArray();

    public IReadOnlyList<NamedHandleSweep> CloseSingletonHandles(IReadOnlyCollection<int> processIds)
    {
        Sweeps.Add(processIds.ToArray());
        return processIds.Select(id =>
        {
            if (Protected.Contains(id))
                return new NamedHandleSweep(id, 0, 5);
            int closed = HandlesPerProcess.GetValueOrDefault(id);
            HandlesPerProcess[id] = 0;
            return new NamedHandleSweep(id, closed, 0);
        }).ToArray();
    }

    public bool Terminate(int processId)
    {
        if (Protected.Contains(processId))
            return false;
        Terminated.Add(processId);
        Processes.RemoveAll(p => p.Id == processId);
        return true;
    }
}

[Collection("Roblox singleton")]
public class MultiInstanceManagerTests
{
    private const string GameUrl = "\"C:\\Roblox\\RobloxPlayerBeta.exe\" roblox-player:1+launchmode:play+gameinfo:x";
    private const string TrayCommand = "\"C:\\Roblox\\RobloxPlayerBeta.exe\" --launch-to-tray";

    [Fact]
    public void PrepareForLaunch_EndsTrayProcessStartedWithWindows()
    {
        // Roblox autostarts "--launch-to-tray" with Windows. Since 0.746 a game launch is handed to that
        // process and the launched one exits, which is what users saw after a reboot.
        var host = new FakeRobloxProcessHost();
        host.Add(10, TimeSpan.FromMinutes(30), window: false, TrayCommand);
        using MultiInstanceManager manager = TestMultiInstance.Create(host);
        manager.Apply(true);

        MultiInstancePreparation result = manager.PrepareForLaunch();

        Assert.Equal(new[] { 10 }, host.Terminated);
        Assert.Equal(1, result.TrayProcessesClosed);
        Assert.True(result.IsReady);
    }

    [Fact]
    public void PrepareForLaunch_EndsTrayProcessEvenWhenMultiInstanceIsOff()
    {
        // Otherwise even a single launch ends up in an untracked process.
        var host = new FakeRobloxProcessHost();
        host.Add(10, TimeSpan.FromMinutes(30), window: false, TrayCommand);
        using MultiInstanceManager manager = TestMultiInstance.Create(host);

        manager.PrepareForLaunch();

        Assert.Equal(new[] { 10 }, host.Terminated);
        Assert.Empty(host.Sweeps);
    }

    [Fact]
    public void PrepareForLaunch_ClosesSingletonHandlesInRunningGameClients()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(20, TimeSpan.FromMinutes(5), window: true, GameUrl);
        host.Add(21, TimeSpan.FromMinutes(1), window: true, GameUrl);
        using MultiInstanceManager manager = TestMultiInstance.Create(host);
        manager.Apply(true);

        MultiInstancePreparation result = manager.PrepareForLaunch();

        Assert.Empty(host.Terminated);
        Assert.Equal(new[] { 20, 21 }, Assert.Single(host.Sweeps));
        Assert.Equal(4, result.HandlesClosed);
        Assert.True(result.IsReady);
    }

    [Fact]
    public void PrepareForLaunch_LeavesClientsAloneWhenMultiInstanceIsOff()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(20, TimeSpan.FromMinutes(5), window: true, GameUrl);
        using MultiInstanceManager manager = TestMultiInstance.Create(host);

        MultiInstancePreparation result = manager.PrepareForLaunch();

        Assert.Empty(host.Sweeps);
        Assert.Empty(host.Terminated);
        Assert.True(result.IsReady);
    }

    [Fact]
    public void PrepareForLaunch_ReportsClientsThatCantBeOpened()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(30, TimeSpan.FromMinutes(5), window: true, GameUrl);
        host.Protected.Add(30);
        using MultiInstanceManager manager = TestMultiInstance.Create(host);
        manager.Apply(true);

        MultiInstancePreparation result = manager.PrepareForLaunch();

        Assert.False(result.IsReady);
        Assert.Equal(new[] { 30 }, result.BlockedProcessIds);
    }

    [Fact]
    public void PrepareForLaunch_ReportsTrayProcessThatCantBeEnded()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(31, TimeSpan.FromMinutes(5), window: false, TrayCommand);
        host.Protected.Add(31);
        using MultiInstanceManager manager = TestMultiInstance.Create(host);

        MultiInstancePreparation result = manager.PrepareForLaunch();

        Assert.Equal(new[] { 31 }, result.BlockedProcessIds);
    }

    [Fact]
    public void PrepareForLaunch_WaitsForAClientThatIsStillStarting()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(40, TimeSpan.FromSeconds(1), window: false, GameUrl);
        var waits = new List<TimeSpan>();
        using var manager = new MultiInstanceManager(host, () => FakeRobloxProcessHost.Now, waits.Add, TimeSpan.FromHours(1));
        manager.Apply(true);

        manager.PrepareForLaunch();

        Assert.Equal(MultiInstanceManager.StartingGrace - TimeSpan.FromSeconds(1), Assert.Single(waits));
        Assert.Empty(host.Terminated);
    }

    [Theory]
    [InlineData(false, TrayCommand, 1, true)]
    [InlineData(true, TrayCommand, 600, false)]          // Tray process opened into the full app: has a window.
    [InlineData(false, GameUrl, 600, false)]             // A game client is never treated as tray.
    [InlineData(false, "\"C:\\Roblox\\RobloxPlayerBeta.exe\"", 60, true)]   // Windowless without a launch URL.
    [InlineData(false, "\"C:\\Roblox\\RobloxPlayerBeta.exe\"", 2, false)]   // ...but give a fresh one time.
    [InlineData(false, null, 600, false)]                // Unknown command line: leave it.
    public void IsTrayOnly_RecognisesBackgroundTrayProcesses(bool window, string? commandLine, int ageSeconds, bool expected)
    {
        var info = new RobloxProcessInfo(1, FakeRobloxProcessHost.Now.AddSeconds(-ageSeconds), window, commandLine);

        Assert.Equal(expected, MultiInstanceManager.IsTrayOnly(info, FakeRobloxProcessHost.Now));
    }

    [Fact]
    public void BackgroundSweep_HandlesEachSettledClientOnce()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(50, TimeSpan.FromSeconds(30), window: true, GameUrl);
        host.Add(51, TimeSpan.FromSeconds(2), window: false, GameUrl); // still starting
        using MultiInstanceManager manager = TestMultiInstance.Create(host);

        manager.SweepSettledClients();
        manager.SweepSettledClients();

        Assert.Equal(new[] { 50 }, Assert.Single(host.Sweeps));
    }

    [Fact]
    public void BackgroundSweep_RetriesClientsThatWereBlocked()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(60, TimeSpan.FromSeconds(30), window: true, GameUrl);
        host.Protected.Add(60);
        using MultiInstanceManager manager = TestMultiInstance.Create(host);

        manager.SweepSettledClients();
        host.Protected.Clear();
        manager.SweepSettledClients();

        Assert.Equal(2, host.Sweeps.Count);
    }

    [Fact]
    public void Apply_TogglesActiveState()
    {
        using MultiInstanceManager manager = TestMultiInstance.Create();

        Assert.False(manager.IsActive);
        Assert.True(manager.TryApply(true));
        Assert.True(manager.IsActive);
        Assert.True(manager.TryApply(false));
        Assert.False(manager.IsActive);
        manager.Apply(true);
        Assert.True(manager.IsActive);
    }

    [Fact]
    public void AfterDispose_TryApplyFailsAndPrepareDoesNothing()
    {
        var host = new FakeRobloxProcessHost();
        host.Add(10, TimeSpan.FromMinutes(30), window: false, TrayCommand);
        MultiInstanceManager manager = TestMultiInstance.Create(host);
        manager.Dispose();

        Assert.False(manager.TryApply(true));
        Assert.True(manager.PrepareForLaunch().IsReady);
        Assert.Empty(host.Terminated);
    }

    [Fact]
    public void UsesTheObjectNamesRobloxCreates()
    {
        Assert.Equal(new[] { "ROBLOX_singletonEvent", "ROBLOX_singletonMutex" }, MultiInstanceManager.SingletonNames);
    }
}

/// <summary>Exercises the real Win32 handle closing against objects this test process creates.</summary>
[Collection("Roblox singleton")]
public class NamedObjectCloserTests
{
    [Fact]
    public void Close_RemovesNamedEventAndMutexHeldByAnotherProcess()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string eventName = $"INSTANCEMANAGER_TEST_singletonEvent_{suffix}";
        string mutexName = $"INSTANCEMANAGER_TEST_singletonMutex_{suffix}";
        using Holder holder = StartHolder(eventName, mutexName);

        IReadOnlyList<NamedHandleSweep> result = NamedObjectCloser.Close(new[] { holder.Process.Id }, new[] { eventName, mutexName });

        NamedHandleSweep sweep = Assert.Single(result);
        Assert.Equal(2, sweep.Closed);
        Assert.False(sweep.Blocked);
        Assert.False(Exists(eventName));
        Assert.False(Exists(mutexName));
        Assert.False(holder.Process.HasExited);
    }

    [Fact]
    public void Close_AfterwardsANewProcessCreatesFreshObjects()
    {
        string name = $"INSTANCEMANAGER_TEST_singletonEvent_{Guid.NewGuid():N}";
        using Holder holder = StartHolder(name, $"{name}_m");

        NamedObjectCloser.Close(new[] { holder.Process.Id }, new[] { name });
        using var fresh = new EventWaitHandle(false, EventResetMode.ManualReset, name, out bool createdNew);

        Assert.True(createdNew);
    }

    [Fact]
    public void Close_LeavesOtherNamesAlone()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string keep = $"INSTANCEMANAGER_TEST_keep_{suffix}";
        using Holder holder = StartHolder(keep, $"INSTANCEMANAGER_TEST_keepMutex_{suffix}");

        NamedHandleSweep sweep = Assert.Single(NamedObjectCloser.Close(new[] { holder.Process.Id }, new[] { $"INSTANCEMANAGER_TEST_other_{suffix}" }));

        Assert.Equal(0, sweep.Closed);
        Assert.True(Exists(keep));
    }

    [Fact]
    public void Close_TreatsAnExitedProcessAsClean()
    {
        using Process done = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true, UseShellExecute = false })!;
        done.WaitForExit();
        int pid = done.Id;

        NamedHandleSweep sweep = Assert.Single(NamedObjectCloser.Close(new[] { pid }, new[] { "x" }));

        Assert.False(sweep.Blocked);
    }

    [Fact]
    public void LastSegment_StripsTheKernelNamespace()
    {
        Assert.Equal("ROBLOX_singletonEvent", NamedObjectCloser.LastSegment(@"\Sessions\1\BaseNamedObjects\ROBLOX_singletonEvent"));
        Assert.Equal("plain", NamedObjectCloser.LastSegment("plain"));
    }

    [Fact]
    public void CommandLine_CanBeReadFromAnotherProcess()
    {
        using Holder holder = StartHolder($"INSTANCEMANAGER_TEST_cmd_{Guid.NewGuid():N}", $"INSTANCEMANAGER_TEST_cmdm_{Guid.NewGuid():N}");

        string? commandLine = SystemRobloxProcessHost.TryGetCommandLine(holder.Process.Id);

        Assert.NotNull(commandLine);
        Assert.Contains("INSTANCEMANAGER_TEST_cmd_", commandLine, StringComparison.Ordinal);
    }

    private static Holder StartHolder(string eventName, string mutexName)
    {
        string script =
            $"$e = New-Object System.Threading.EventWaitHandle($false, 'ManualReset', '{eventName}'); " +
            $"$m = New-Object System.Threading.Mutex($true, '{mutexName}'); " +
            "[Console]::Out.WriteLine('ready'); [Console]::Out.Flush(); Start-Sleep -Seconds 60";
        var holder = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true
        })!;

        string? line = holder.StandardOutput.ReadLine();
        Assert.Equal("ready", line);
        return new Holder(holder);
    }

    private static bool Exists(string name)
    {
        IntPtr handle = OpenEventW(0x00100000 /* SYNCHRONIZE */, false, name);
        if (handle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 6 /* ERROR_INVALID_HANDLE: exists with another type */)
                return true;
            return false;
        }

        CloseHandle(handle);
        return true;
    }

    private sealed class Holder(Process process) : IDisposable
    {
        public Process Process { get; } = process;

        public void Dispose()
        {
            try { Process.Kill(); } catch (InvalidOperationException) { }
            Process.Dispose();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenEventW(uint desiredAccess, bool inheritHandle, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

using System;
using System.Diagnostics;
using System.Threading;
using InstanceManager.Services;
using Xunit;

namespace InstanceManager.Tests;

public class InstanceTrackerTests
{
    [Fact]
    public void Stop_UnknownAccount_ReturnsFalse()
    {
        using var tracker = new InstanceTracker();
        Assert.False(tracker.Stop(Guid.NewGuid()));
    }

    [Fact]
    public void StopAll_NoInstances_ReturnsZero()
    {
        using var tracker = new InstanceTracker();
        Assert.Equal(0, tracker.StopAll());
    }

    [Fact]
    public void Stop_KillsTrackedProcess_AndClearsRunningState()
    {
        using var tracker = new InstanceTracker();
        var id = Guid.NewGuid();

        Process proc = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping 127.0.0.1 -n 30 > nul",
            CreateNoWindow = true,
            UseShellExecute = false
        })!;

        bool? lastRunning = null;
        tracker.RunningChanged += (gid, running) => { if (gid == id) lastRunning = running; };

        tracker.Track(id, proc);
        Assert.True(tracker.IsRunning(id));
        Assert.Equal(1, tracker.RunningCount);
        Assert.True(lastRunning);

        Assert.True(tracker.Stop(id));

        var sw = Stopwatch.StartNew();
        while (lastRunning != false && sw.ElapsedMilliseconds < 5000)
            Thread.Sleep(25);

        Assert.False(tracker.IsRunning(id));
        Assert.Equal(0, tracker.RunningCount);
        Assert.False(lastRunning);
    }

    [Fact]
    public void Track_ReplacingAccount_KillsPreviousProcess()
    {
        using var tracker = new InstanceTracker();
        var id = Guid.NewGuid();
        Process? first = null;
        Process? second = null;
        int firstId = 0;
        int secondId = 0;

        try
        {
            first = StartLongRunningProcess();
            second = StartLongRunningProcess();
            firstId = first.Id;
            secondId = second.Id;

            tracker.Track(id, first);
            tracker.Track(id, second);

            Assert.True(WaitUntilExited(firstId));
            Assert.True(tracker.IsRunning(id));
        }
        finally
        {
            tracker.Stop(id);
            KillIfRunning(firstId);
            KillIfRunning(secondId);
        }
    }

    [Fact]
    public void Stop_WithStaleExpectedProcess_DoesNotKillCurrentProcess()
    {
        using var tracker = new InstanceTracker();
        var id = Guid.NewGuid();
        Process? first = null;
        Process? second = null;
        int firstId = 0;
        int secondId = 0;

        try
        {
            first = StartLongRunningProcess();
            second = StartLongRunningProcess();
            firstId = first.Id;
            secondId = second.Id;

            tracker.Track(id, first);
            tracker.Track(id, second);

            var overload = typeof(InstanceTracker).GetMethod(
                "Stop",
                new[] { typeof(Guid), typeof(Process) });
            Assert.NotNull(overload);

            bool stopped = (bool)overload!.Invoke(tracker, new object[] { id, first })!;

            Assert.False(stopped);
            Assert.True(tracker.IsRunning(id));
            Assert.True(IsProcessRunning(secondId));
        }
        finally
        {
            tracker.Stop(id);
            KillIfRunning(firstId);
            KillIfRunning(secondId);
        }
    }

    private static Process StartLongRunningProcess() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -Command Start-Sleep -Seconds 30",
            CreateNoWindow = true,
            UseShellExecute = false
        })!;

    private static bool WaitUntilExited(int processId)
    {
        if (processId == 0)
            return true;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000)
        {
            if (!IsProcessRunning(processId))
                return true;

            Thread.Sleep(25);
        }

        return false;
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void KillIfRunning(int processId)
    {
        if (processId == 0)
            return;

        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}

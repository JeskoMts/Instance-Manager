using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace InstanceManager.Services;

/// <summary>Result of getting the running Roblox processes out of the way of a new launch.</summary>
/// <param name="TrayProcessesClosed">Background tray processes that were ended so they can't take over the launch.</param>
/// <param name="HandlesClosed">Single-instance handles that were closed inside running clients.</param>
/// <param name="BlockedProcessIds">Roblox processes that couldn't be inspected or ended (usually started as administrator).</param>
public readonly record struct MultiInstancePreparation(int TrayProcessesClosed, int HandlesClosed, IReadOnlyList<int> BlockedProcessIds)
{
    public static MultiInstancePreparation Nothing { get; } = new(0, 0, Array.Empty<int>());

    public bool IsReady => BlockedProcessIds.Count == 0;
}

/// <summary>
/// Lets several Roblox clients run side by side.
/// <para>
/// Roblox (0.74x) allows one client per user with two named objects: the event
/// <c>ROBLOX_singletonEvent</c> and the mutex <c>ROBLOX_singletonMutex</c>. A new client creates both, and when
/// they already exist it signals the event so the older client quits. Closing those handles inside every
/// running client removes the names, so the next client finds nothing and starts as if it were alone.
/// Holding the names ourselves (what 1.1.4 did) no longer works: a Roblox tray process that was already running
/// owns them, and since 0.746 a second client also gives up when it can't create the event.
/// </para>
/// <para>
/// Since 0.746 a game launch is also handed to a running background tray process ("WebPlay NoReload with Tray
/// only") and the launched process exits. Roblox starts such a tray process with Windows and whenever a game
/// window is closed. Tray processes hold no game, so they are ended right before every launch.
/// </para>
/// </summary>
public sealed class MultiInstanceManager : IDisposable
{
    internal const string EventName = "ROBLOX_singletonEvent";
    internal const string MutexName = "ROBLOX_singletonMutex";
    internal static readonly string[] SingletonNames = [EventName, MutexName];

    internal static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromSeconds(2);
    /// <summary>A client without a window is left alone this long so it can finish creating its singleton objects.</summary>
    internal static readonly TimeSpan SettleAge = TimeSpan.FromSeconds(10);
    /// <summary>Processes younger than this are given time to finish starting before a launch touches them.</summary>
    internal static readonly TimeSpan StartingGrace = TimeSpan.FromSeconds(4);

    private readonly IRobloxProcessHost _host;
    private readonly Func<DateTime> _utcNow;
    private readonly Action<TimeSpan> _sleep;
    private readonly TimeSpan _sweepInterval;
    private readonly object _gate = new();
    private readonly object _work = new();
    private readonly HashSet<(int Id, DateTime Started)> _swept = new();
    private Timer? _timer;
    private bool _active;
    private bool _disposed;

    public MultiInstanceManager()
        : this(new SystemRobloxProcessHost(), () => DateTime.UtcNow, Thread.Sleep, DefaultSweepInterval)
    {
    }

    internal MultiInstanceManager(IRobloxProcessHost host, Func<DateTime> utcNow, Action<TimeSpan> sleep, TimeSpan sweepInterval)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        _sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
        _sweepInterval = sweepInterval;
    }

    /// <summary>True while multi-instance is switched on and running clients are kept from blocking new ones.</summary>
    public bool IsActive
    {
        get
        {
            lock (_gate)
                return _active;
        }
    }

    public void Apply(bool enabled)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active == enabled)
                return;

            _active = enabled;
            if (enabled)
            {
                _timer = new Timer(_ => SweepInBackground(), null, TimeSpan.Zero, _sweepInterval);
            }
            else
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        if (!enabled)
        {
            lock (_work)
                _swept.Clear();
        }
    }

    /// <summary>Applies the setting; returns false only when the manager is already disposed.</summary>
    public bool TryApply(bool enabled)
    {
        try
        {
            Apply(enabled);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Call right before starting a Roblox client. Ends background tray processes (they would swallow the
    /// launch) and, while multi-instance is on, closes the single-instance handles in every running client.
    /// </summary>
    public MultiInstancePreparation PrepareForLaunch()
    {
        lock (_gate)
        {
            if (_disposed)
                return MultiInstancePreparation.Nothing;
        }

        lock (_work)
        {
            try
            {
                return PrepareCore();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return MultiInstancePreparation.Nothing;
            }
        }
    }

    private MultiInstancePreparation PrepareCore()
    {
        IReadOnlyList<RobloxProcessInfo> processes = WaitForStartingProcesses(_host.List());
        if (processes.Count == 0)
            return MultiInstancePreparation.Nothing;

        DateTime now = _utcNow();
        var blocked = new List<int>();
        var clients = new List<RobloxProcessInfo>();
        int trayClosed = 0;

        foreach (RobloxProcessInfo process in processes)
        {
            if (!IsTrayOnly(process, now))
            {
                clients.Add(process);
                continue;
            }

            if (_host.Terminate(process.Id))
                trayClosed++;
            else
                blocked.Add(process.Id);
        }

        int handlesClosed = 0;
        if (IsActive && clients.Count > 0)
        {
            foreach (NamedHandleSweep sweep in _host.CloseSingletonHandles(clients.Select(c => c.Id).ToArray()))
            {
                handlesClosed += sweep.Closed;
                if (sweep.Blocked)
                    blocked.Add(sweep.ProcessId);
                else
                    _swept.Add(Key(clients.First(c => c.Id == sweep.ProcessId)));
            }
        }

        return new MultiInstancePreparation(trayClosed, handlesClosed, blocked);
    }

    private IReadOnlyList<RobloxProcessInfo> WaitForStartingProcesses(IReadOnlyList<RobloxProcessInfo> processes)
    {
        DateTime now = _utcNow();
        TimeSpan wait = TimeSpan.Zero;
        foreach (RobloxProcessInfo process in processes)
        {
            TimeSpan age = now - process.StartedUtc;
            if (!process.HasVisibleWindow && age < StartingGrace && StartingGrace - age > wait)
                wait = StartingGrace - age;
        }

        if (wait <= TimeSpan.Zero)
            return processes;

        _sleep(wait);
        return _host.List();
    }

    /// <summary>
    /// A Roblox process with no visible window that was started for the tray (<c>--launch-to-tray</c>), or that
    /// has been around for a while without a launch URL, is the background tray helper and holds no game.
    /// </summary>
    internal static bool IsTrayOnly(RobloxProcessInfo process, DateTime utcNow)
    {
        if (process.HasVisibleWindow || process.CommandLine is not { } commandLine)
            return false;

        if (commandLine.Contains("--launch-to-tray", StringComparison.OrdinalIgnoreCase))
            return true;

        bool hasLaunchUrl = commandLine.Contains("roblox-player:", StringComparison.OrdinalIgnoreCase)
                            || commandLine.Contains("roblox:", StringComparison.OrdinalIgnoreCase);
        return !hasLaunchUrl && utcNow - process.StartedUtc >= SettleAge;
    }

    private void SweepInBackground()
    {
        if (!Monitor.TryEnter(_work))
            return;

        try
        {
            if (!IsActive)
                return;
            SweepSettledClients();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            Monitor.Exit(_work);
        }
    }

    /// <summary>
    /// Closes the singleton handles in clients that finished starting and weren't handled yet, so a client
    /// started from the browser or by Roblox itself can't make the others quit.
    /// </summary>
    internal void SweepSettledClients()
    {
        IReadOnlyList<RobloxProcessInfo> processes = _host.List();
        _swept.RemoveWhere(key => !processes.Any(p => Key(p) == key));
        if (processes.Count == 0)
            return;

        DateTime now = _utcNow();
        RobloxProcessInfo[] due = processes
            .Where(p => !_swept.Contains(Key(p)))
            .Where(p => p.HasVisibleWindow || now - p.StartedUtc >= SettleAge)
            .ToArray();
        if (due.Length == 0)
            return;

        foreach (NamedHandleSweep sweep in _host.CloseSingletonHandles(due.Select(p => p.Id).ToArray()))
        {
            if (!sweep.Blocked)
                _swept.Add(Key(due.First(p => p.Id == sweep.ProcessId)));
        }
    }

    private static (int, DateTime) Key(RobloxProcessInfo process) => (process.Id, process.StartedUtc);

    public void Dispose()
    {
        Timer? timer;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _active = false;
            timer = _timer;
            _timer = null;
        }

        timer?.Dispose();
    }
}

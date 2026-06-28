using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace InstanceManager.Services;

public sealed class InstanceTracker : IDisposable
{
    private readonly ConcurrentDictionary<Guid, Process> _byAccount = new();

    public event Action<Guid, bool>? RunningChanged;

    public void Track(Guid accountId, Process proc)
    {
        if (_byAccount.TryRemove(accountId, out Process? old))
        {
            TerminateAndDispose(old);
        }

        _byAccount[accountId] = proc;
        RaiseChanged(accountId, true);

        try
        {
            proc.EnableRaisingEvents = true;
            proc.Exited += (_, _) => DisposeTrackedProcess(accountId, proc);
            if (proc.HasExited)
                DisposeTrackedProcess(accountId, proc);
        }
        catch (Exception)
        {
        }
    }

    public bool IsRunning(Guid accountId) =>
        _byAccount.TryGetValue(accountId, out Process? p) && IsAlive(p);

    public int RunningCount
    {
        get
        {
            int n = 0;
            foreach (var p in _byAccount.Values)
                if (IsAlive(p)) n++;
            return n;
        }
    }

    public bool Stop(Guid accountId)
    {
        if (!_byAccount.TryGetValue(accountId, out Process? p))
            return false;
        return TryKill(p);
    }

    public bool Stop(Guid accountId, Process expectedProcess)
    {
        if (expectedProcess == null)
            return false;

        if (!_byAccount.TryGetValue(accountId, out Process? p) || !ReferenceEquals(p, expectedProcess))
            return false;

        return TryKill(p);
    }

    private static bool TryKill(Process p)
    {
        try
        {
            p.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int StopAll()
    {
        int stopped = 0;
        foreach (Guid accountId in _byAccount.Keys.ToArray())
            if (Stop(accountId))
                stopped++;
        return stopped;
    }

    private static bool IsAlive(Process p)
    {
        try { return !p.HasExited; }
        catch { return false; }
    }

    private static void TerminateAndDispose(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        try { process.Dispose(); }
        catch { }
    }

    private void DisposeTrackedProcess(Guid accountId, Process process)
    {
        bool removed = ((ICollection<KeyValuePair<Guid, Process>>)_byAccount)
            .Remove(new KeyValuePair<Guid, Process>(accountId, process));

        try { process.Dispose(); }
        catch { }

        if (removed)
            RaiseChanged(accountId, false);
    }

    public void Dispose()
    {
        foreach (KeyValuePair<Guid, Process> item in _byAccount)
            DisposeTrackedProcess(item.Key, item.Value);
    }

    private void RaiseChanged(Guid accountId, bool isRunning) => RunningChanged?.Invoke(accountId, isRunning);
}

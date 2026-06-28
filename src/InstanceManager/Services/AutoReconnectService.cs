using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;

namespace InstanceManager.Services;

public sealed class AutoReconnectService : IDisposable
{
    private const int SignalSettleDelayMs = 1000;
    private const int ReconnectDelayMs = 3000;

    private const int CollateralReconnectWindowMs = 10_000;

    private readonly InstanceTracker _tracker;
    private readonly ISettingsService _settings;
    private readonly AutoReconnectLog _log;
    private readonly Func<DateTime, RobloxLogWatcher> _watcherFactory;
    private readonly Dictionary<Guid, Session> _sessions = new();
    private readonly object _gate = new();
    private bool _disposed;

    private Guid _lastReconnectAccount;
    private DateTime _lastReconnectUtc = DateTime.MinValue;

    public AutoReconnectService(InstanceTracker tracker, ISettingsService settings, AutoReconnectLog log)
        : this(tracker, settings, log, CreateSharedWatcherFactory())
    {
    }

    private static Func<DateTime, RobloxLogWatcher> CreateSharedWatcherFactory()
    {
        var registry = new LogSessionRegistry();
        return launchUtc => new RobloxLogWatcher(launchUtc, registry);
    }

    internal AutoReconnectService(
        InstanceTracker tracker,
        ISettingsService settings,
        AutoReconnectLog log,
        Func<DateTime, RobloxLogWatcher> watcherFactory)
    {
        _tracker = tracker;
        _settings = settings;
        _log = log;
        _watcherFactory = watcherFactory;
        _tracker.RunningChanged += OnRunningChanged;
    }

    public Func<Account, ServerTarget, RobloxVersion, CancellationToken, Task<Process?>>? Relaunch { get; set; }

    public void RegisterLaunch(Account account, ServerTarget target, RobloxVersion version, Process process)
    {
        if (account == null || target == null || version == null || process == null)
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            if (_sessions.TryGetValue(account.Id, out Session? existing))
                existing.DisposeWatcher();

            var session = new Session(account, target, version, process);
            _sessions[account.Id] = session;
            AttachWatcher(session);
        }
    }

    public void NotifyManualStop(Guid accountId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(accountId, out Session? session))
                session.ManuallyStopped = true;
        }
    }

    public void NotifyManualStopAll()
    {
        lock (_gate)
        {
            foreach (Session session in _sessions.Values)
                session.ManuallyStopped = true;
        }
    }

    private void AttachWatcher(Session session)
    {
        RobloxLogWatcher watcher = _watcherFactory(GetProcessStartUtc(session.Process));
        Guid runId = session.RunId;
        session.Watcher = watcher;
        watcher.Detected += signal => OnSignal(session.Account.Id, runId, signal);
    }

    private static DateTime GetProcessStartUtc(Process process)
    {
        try { return process.StartTime.ToUniversalTime(); }
        catch { return DateTime.UtcNow; }
    }

    private void OnSignal(Guid accountId, Guid runId, RobloxSessionSignal signal)
    {
        bool killStuckClient = false;
        Process? processToKill = null;
        Guid runIdToHandle = runId;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(accountId, out Session? session)
                || session.RunId != runId
                || session.ManuallyStopped)
            {
                return;
            }

            if (IsCollateralReconnect(accountId))
                return;

            switch (signal)
            {
                case RobloxSessionSignal.InGame:
                    session.InGame = true;
                    break;
                case RobloxSessionSignal.GracefulLeave:
                    if (session.InGame && !session.ActionTaken)
                    {
                        session.ActionTaken = true;
                        session.PendingTrigger = AutoReconnectTrigger.Error;
                        if (_settings.Settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Error))
                        {
                            killStuckClient = true;
                            processToKill = session.Process;
                            runIdToHandle = session.RunId;
                        }
                    }
                    else
                    {
                        session.GracefulLeave = true;
                    }
                    break;
                case RobloxSessionSignal.Kicked:
                case RobloxSessionSignal.Error:
                    if (session.ActionTaken)
                    {
                        if (signal == RobloxSessionSignal.Kicked)
                            session.PendingTrigger = AutoReconnectTrigger.Kick;
                        return;
                    }
                    session.ActionTaken = true;
                    session.GracefulLeave = false;
                    AutoReconnectTrigger dropTrigger = signal == RobloxSessionSignal.Kicked
                        ? AutoReconnectTrigger.Kick
                        : AutoReconnectTrigger.Error;
                    session.PendingTrigger = dropTrigger;
                    if (_settings.Settings.IsAutoReconnectEnabledFor(dropTrigger))
                    {
                        killStuckClient = true;
                        processToKill = session.Process;
                        runIdToHandle = session.RunId;
                    }
                    break;
            }
        }

        if (killStuckClient)
        {
            bool trackerWillRaiseExit = false;
            try
            {
                if (processToKill != null)
                    trackerWillRaiseExit = _tracker.Stop(accountId, processToKill);
            }
            catch { }

            bool fallbackHandled = TerminateProcess(processToKill);
            if (trackerWillRaiseExit || fallbackHandled)
                _ = HandleExitForRunAsync(accountId, runIdToHandle);
        }
    }

    private void OnRunningChanged(Guid accountId, bool isRunning)
    {
        if (isRunning)
            return;
        _ = HandleExitAsync(accountId);
    }

    private Task HandleExitAsync(Guid accountId) => HandleExitForRunAsync(accountId, expectedRunId: null);

    private async Task HandleExitForRunAsync(Guid accountId, Guid? expectedRunId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(accountId, out Session? found))
                return;

            if (expectedRunId.HasValue && found.RunId != expectedRunId.Value)
                return;

            if (found.ExitHandling)
                return;

            found.ExitHandling = true;
        }

        await Task.Delay(SignalSettleDelayMs).ConfigureAwait(false);

        Session session;
        AutoReconnectTrigger trigger;
        int attempt;
        int max;
        ServerTarget target;
        RobloxVersion version;
        Account account;

        lock (_gate)
        {
            if (!_sessions.TryGetValue(accountId, out Session? found))
                return;
            if (expectedRunId.HasValue && found.RunId != expectedRunId.Value)
                return;
            session = found;

            if (session.ManuallyStopped)
            {
                Remove(accountId);
                return;
            }

            if (IsCollateralReconnect(accountId))
            {
                _log.SkippedCollateral(session.Account);
                Remove(accountId);
                return;
            }

            AutoReconnectTrigger? resolved = session.PendingTrigger;
            if (resolved is null && session.GracefulLeave)
            {
                Remove(accountId);
                return;
            }

            resolved ??= session.InGame ? AutoReconnectTrigger.Crash : null;
            if (resolved is null)
            {
                Remove(accountId);
                return;
            }
            trigger = resolved.Value;

            if (!_settings.Settings.IsAutoReconnectEnabledFor(trigger))
            {
                Remove(accountId);
                return;
            }

            max = AppSettings.NormalizeAutoReconnectAttempts(_settings.Settings.AutoReconnectMaxAttempts);
            if (!AppSettings.IsUnlimitedAttempts(max) && session.Attempts >= max)
            {
                _log.GaveUp(session.Account, trigger, max);
                Remove(accountId);
                return;
            }

            session.Attempts++;
            attempt = session.Attempts;
            session.Account.BrowserTrackerId = GenerateFreshBrowserTrackerId(session.Account.BrowserTrackerId);
            account = session.Account;
            target = session.Target;
            version = session.Version;
            session.DisposeWatcher();
            session.PrepareForReconnect();
        }

        _log.Attempt(account, trigger, attempt, max, target);

        Func<Account, ServerTarget, RobloxVersion, CancellationToken, Task<Process?>>? relaunch = Relaunch;
        if (relaunch == null)
        {
            _log.Result(account, attempt, success: false, "relaunch unavailable");
            lock (_gate) Remove(accountId);
            return;
        }

        try
        {
            await Task.Delay(ReconnectDelayMs).ConfigureAwait(false);

            lock (_gate)
            {
                if (_disposed)
                    return;
                _lastReconnectAccount = accountId;
                _lastReconnectUtc = DateTime.UtcNow;
            }

            Process? process = await relaunch(account, target, version, CancellationToken.None).ConfigureAwait(false);
            if (process == null)
            {
                _log.Result(account, attempt, success: false, "launch returned no process");
                lock (_gate) Remove(accountId);
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return;
                }

                if (!_sessions.TryGetValue(accountId, out Session? current) || !ReferenceEquals(current, session))
                    return;

                session.BeginRun(process);
                AttachWatcher(session);
            }

            _log.Result(account, attempt, success: true);
        }
        catch (Exception ex)
        {
            _log.Result(account, attempt, success: false, ex.Message);
            lock (_gate) Remove(accountId);
        }
    }

    private void Remove(Guid accountId)
    {
        if (_sessions.Remove(accountId, out Session? session))
            session.DisposeWatcher();
    }

    private bool IsCollateralReconnect(Guid accountId) =>
        accountId != _lastReconnectAccount
        && (DateTime.UtcNow - _lastReconnectUtc).TotalMilliseconds < CollateralReconnectWindowMs;

    private static bool TerminateProcess(Process? process)
    {
        if (process == null)
            return false;

        try
        {
            if (process.Id == Environment.ProcessId)
                return false;

            if (process.HasExited)
                return true;

            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static long GenerateFreshBrowserTrackerId(long current)
    {
        long next;
        do
        {
            next = RobloxLauncher.GenerateBrowserTrackerId();
        }
        while (next == current);

        return next;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _tracker.RunningChanged -= OnRunningChanged;
            foreach (Session session in _sessions.Values)
                session.DisposeWatcher();
            _sessions.Clear();
        }
    }

    private sealed class Session
    {
        public Session(Account account, ServerTarget target, RobloxVersion version, Process process)
        {
            Account = account;
            Target = target;
            Version = version;
            Process = process;
        }

        public Account Account { get; }
        public ServerTarget Target { get; }
        public RobloxVersion Version { get; }
        public Process Process { get; set; }
        public Guid RunId { get; private set; } = Guid.NewGuid();

        public int Attempts { get; set; }
        public bool ManuallyStopped { get; set; }
        public bool ExitHandling { get; set; }
        public bool InGame { get; set; }
        public bool GracefulLeave { get; set; }
        public bool ActionTaken { get; set; }
        public AutoReconnectTrigger? PendingTrigger { get; set; }
        public RobloxLogWatcher? Watcher { get; set; }

        public void PrepareForReconnect()
        {
            InGame = false;
            GracefulLeave = false;
            ActionTaken = false;
            PendingTrigger = null;
        }

        public void BeginRun(Process process)
        {
            Process = process;
            RunId = Guid.NewGuid();
            ExitHandling = false;
            PrepareForReconnect();
        }

        public void DisposeWatcher()
        {
            Watcher?.Dispose();
            Watcher = null;
        }
    }
}

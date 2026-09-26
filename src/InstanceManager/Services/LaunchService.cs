using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;

namespace InstanceManager.Services;

public readonly record struct LaunchSummary(int Started, int Failed, int Skipped = 0)
{
    public int Total => Started + Failed;
}

public enum LaunchStage
{
    Queued,
    Starting,
    WaitingForWindow,
    Joining,
    Started,
    Failed,
    Skipped
}

public readonly record struct LaunchProgress(
    Guid AccountId, LaunchStage Stage, int Index, int Total, string Message, bool RequiresSignIn = false);

public sealed class LaunchService
{
    internal static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadyPollInterval = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(20);
    private const int RetryPauseMs = 2000;

    private readonly DpapiSecureStore _secure;
    private readonly RobloxAuthService _auth;
    private readonly RobloxLauncher _launcher;
    private readonly InstanceTracker _tracker;
    private readonly MultiInstanceManager _multiInstance;
    private readonly ISettingsService _settings;
    private readonly AutoReconnectService? _autoReconnect;

    private readonly SemaphoreSlim _launchSlot = new(1, 1);

    public LaunchService(
        DpapiSecureStore secure,
        RobloxAuthService auth,
        RobloxLauncher launcher,
        InstanceTracker tracker,
        MultiInstanceManager multiInstance,
        ISettingsService settings,
        AutoReconnectService? autoReconnect = null)
    {
        _secure = secure;
        _auth = auth;
        _launcher = launcher;
        _tracker = tracker;
        _multiInstance = multiInstance;
        _settings = settings;
        _autoReconnect = autoReconnect;

        if (_autoReconnect != null)
            _autoReconnect.Relaunch = (account, target, version, ct) => LaunchOneAsync(account, target, version, ct);
    }

    public async Task<LaunchSummary> LaunchAsync(
        IReadOnlyList<Account> accounts,
        ServerTarget target,
        Func<Account, RobloxVersion?> resolveVersion,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolveVersion);

        _multiInstance.TryApply(_settings.Settings.MultiInstanceEnabled);

        int total = accounts.Count;
        int started = 0, failed = 0, skipped = 0;

        for (int i = 0; i < total; i++)
            progress?.Report(new LaunchProgress(accounts[i].Id, LaunchStage.Queued, i, total, "Queued"));

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            Account account = accounts[i];
            string position = $"({i + 1}/{total})";

            if (_tracker.IsRunning(account.Id))
            {
                skipped++;
                progress?.Report(new LaunchProgress(account.Id, LaunchStage.Skipped, i, total,
                    $"{account.DisplayLabel} is already running {position}"));
                continue;
            }

            string? error = null;
            bool ok = false;
            bool requiresSignIn = false;
            for (int attempt = 1; attempt <= 2 && !ok; attempt++)
            {
                progress?.Report(new LaunchProgress(account.Id, LaunchStage.Starting, i, total,
                    attempt == 1
                        ? $"Starting {account.DisplayLabel} {position}…"
                        : $"Retrying {account.DisplayLabel} {position}…"));
                try
                {
                    RobloxVersion? version = resolveVersion(account);
                    if (version == null || !version.IsValid)
                        throw new InvalidOperationException("No valid Roblox version for this account.");

                    (Process proc, Task<bool> ready) = await StartInSlotAsync(account, target, version, ct);
                    _autoReconnect?.RegisterLaunch(account, target, version, proc);

                    progress?.Report(new LaunchProgress(account.Id, LaunchStage.WaitingForWindow, i, total,
                        $"Waiting for {account.DisplayLabel} to open {position}…"));
                    ok = await ready;
                    if (!ok)
                    {
                        error = "Roblox closed while starting.";
                        if (attempt == 1)
                            await Task.Delay(RetryPauseMs, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    requiresSignIn = ex is RobloxAuthException { RequiresSignIn: true };
                    break;
                }
            }

            if (ok && _autoReconnect != null && target.Mode != JoinMode.Home)
            {
                progress?.Report(new LaunchProgress(account.Id, LaunchStage.Joining, i, total,
                    $"{account.DisplayLabel} is joining the game {position}…"));
                await _autoReconnect.WaitForJoinAsync(account.Id, JoinTimeout, ct);
            }

            if (ok)
            {
                started++;
                progress?.Report(new LaunchProgress(account.Id, LaunchStage.Started, i, total,
                    $"{account.DisplayLabel} is running {position}"));
            }
            else
            {
                failed++;
                progress?.Report(new LaunchProgress(account.Id, LaunchStage.Failed, i, total,
                    $"{account.DisplayLabel}: {error}", requiresSignIn));
            }
        }

        return new LaunchSummary(started, failed, skipped);
    }

    public async Task<Process?> LaunchOneAsync(
        Account account,
        ServerTarget target,
        RobloxVersion version,
        CancellationToken ct = default)
    {
        try
        {
            if (version is null || !version.IsValid)
                return null;

            _multiInstance.TryApply(_settings.Settings.MultiInstanceEnabled);
            (Process proc, _) = await StartInSlotAsync(account, target, version, ct).ConfigureAwait(false);
            return proc;
        }
        catch
        {
            return null;
        }
    }

    private async Task<(Process Process, Task<bool> Ready)> StartInSlotAsync(
        Account account, ServerTarget target, RobloxVersion version, CancellationToken ct)
    {
        await _launchSlot.WaitAsync(ct);
        Process proc;
        try
        {
            proc = await LaunchCoreAsync(account, target, version, ct);
        }
        catch
        {
            _launchSlot.Release();
            throw;
        }

        return (proc, SettleThenReleaseAsync(proc, ct));
    }

    private async Task<bool> SettleThenReleaseAsync(Process proc, CancellationToken ct)
    {
        try
        {
            bool alive = await WaitForWindowAsync(proc, ReadyTimeout, ct).ConfigureAwait(false);
            int delay = Math.Max(0, _settings.Settings.LaunchDelayMs);
            if (alive && delay > 0)
                await Task.Delay(delay, ct).ConfigureAwait(false);
            return alive;
        }
        finally
        {
            _launchSlot.Release();
        }
    }

    internal static async Task<bool> WaitForWindowAsync(Process process, TimeSpan timeout, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                if (process.HasExited)
                    return false;
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                    return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return true;
            }

            if (clock.Elapsed >= timeout)
                return true;
            await Task.Delay(ReadyPollInterval, ct).ConfigureAwait(false);
        }
    }

    private async Task<Process> LaunchCoreAsync(Account account, ServerTarget target, RobloxVersion version, CancellationToken ct)
    {
        if (!_secure.TryUnprotect(account.EncryptedCookie, out string cookie))
            throw new RobloxAuthException(
                "This saved login can't be read here (it was saved under another Windows user). Add the account again to sign back in.",
                requiresSignIn: true);

        string ticket = await _auth.GetAuthTicketAsync(cookie, ct);
        long btid = account.BrowserTrackerId;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string url = RobloxLauncher.BuildLaunchUrl(ticket, target, btid, now);

        Process proc = await Task.Run(() => _launcher.Launch(version, url), ct);
        _tracker.Track(account.Id, proc);
        return proc;
    }
}

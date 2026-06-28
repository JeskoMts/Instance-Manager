using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;

namespace InstanceManager.Services;

public readonly record struct LaunchSummary(int Started, int Failed)
{
    public int Total => Started + Failed;
}

public sealed class LaunchService
{
    private readonly DpapiSecureStore _secure;
    private readonly RobloxAuthService _auth;
    private readonly RobloxLauncher _launcher;
    private readonly InstanceTracker _tracker;
    private readonly MultiInstanceManager _multiInstance;
    private readonly ISettingsService _settings;
    private readonly AutoReconnectService? _autoReconnect;

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
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolveVersion);

        if (!_multiInstance.TryApply(_settings.Settings.MultiInstanceEnabled)
            && _settings.Settings.MultiInstanceEnabled)
        {
            progress?.Report("Multi-instance couldn't be enabled (Roblox may already be running); launching anyway…");
        }

        int delay = Math.Max(0, _settings.Settings.LaunchDelayMs);
        int started = 0, failed = 0;

        for (int i = 0; i < accounts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            Account account = accounts[i];
            progress?.Report($"Launching {account.DisplayLabel} ({i + 1}/{accounts.Count})…");

            try
            {
                RobloxVersion? version = resolveVersion(account);
                if (version == null || !version.IsValid)
                    throw new InvalidOperationException("No valid Roblox version for this account.");

                Process proc = await LaunchCoreAsync(account, target, version, ct);
                _autoReconnect?.RegisterLaunch(account, target, version, proc);
                started++;
            }
            catch (Exception ex)
            {
                failed++;
                progress?.Report($"Error launching {account.DisplayLabel}: {ex.Message}");
            }

            if (i < accounts.Count - 1 && delay > 0)
                await Task.Delay(delay, ct);
        }

        return new LaunchSummary(started, failed);
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
            return await LaunchCoreAsync(account, target, version, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Process> LaunchCoreAsync(Account account, ServerTarget target, RobloxVersion version, CancellationToken ct)
    {
        if (!_secure.TryUnprotect(account.EncryptedCookie, out string cookie))
            throw new InvalidOperationException("Could not decrypt cookie (different Windows account?).");

        string ticket = await _auth.GetAuthTicketAsync(cookie, ct);
        long btid = account.BrowserTrackerId;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string url = RobloxLauncher.BuildLaunchUrl(ticket, target, btid, now);

        Process proc = _launcher.Launch(version, url);
        _tracker.Track(account.Id, proc);
        return proc;
    }
}

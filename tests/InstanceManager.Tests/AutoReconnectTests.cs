using System.Text.Json;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using Xunit;

namespace InstanceManager.Tests;

public sealed class AutoReconnectTests
{

    [Theory]
    [InlineData("2024-... [FLog::Output] Error Code: 267 You were kicked", RobloxSessionSignal.Kicked)]
    [InlineData("... kicked from this experience", RobloxSessionSignal.Kicked)]
    [InlineData("Verbindung getrennt (Fehlercode: 267)", RobloxSessionSignal.Kicked)]
    [InlineData("Du wurdest aus dieser Experience gekickt oder von den Moderatoren entfernt.", RobloxSessionSignal.Kicked)]
    [InlineData("Moderationsnachricht: Uh oh! Your save data didn't load right.", RobloxSessionSignal.Kicked)]
    [InlineData("You were kicked or removed by the experience moderator.", RobloxSessionSignal.Kicked)]
    [InlineData("... Error Code: 277 lost connection", RobloxSessionSignal.Error)]
    [InlineData("Connection lost while receiving data", RobloxSessionSignal.Error)]
    [InlineData("The game server has shut down", RobloxSessionSignal.Error)]
    [InlineData("[FLog::Network] Disconnect: Reason 4", RobloxSessionSignal.Error)]
    [InlineData("[FLog::SingleSurfaceApp] leaveUGCGameInternal", RobloxSessionSignal.GracefulLeave)]
    [InlineData("[FLog::Network] Replicator created", RobloxSessionSignal.InGame)]
    public void Classify_RecognizesKnownMarkers(string line, RobloxSessionSignal expected)
    {
        Assert.Equal(expected, RobloxLogClassifier.Classify(line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("[FLog::Output] some unrelated chatter")]
    public void Classify_IgnoresIrrelevantLines(string? line)
    {
        Assert.Null(RobloxLogClassifier.Classify(line));
    }

    [Fact]
    public void Classify_KickWins_OverGenericError()
    {
        Assert.Equal(RobloxSessionSignal.Kicked,
            RobloxLogClassifier.Classify("Error Code: 267 disconnect: reason kicked"));
    }

    [Theory]
    [InlineData("[FLog::Network] Disconnect reason received: 267")]
    [InlineData("[FLog::Network] Disconnection Notification. Reason: 267")]
    [InlineData("[FLog::Network] Sending disconnect with reason: 267")]
    public void Classify_Numeric267DisconnectReason_IsKick(string line)
    {
        Assert.Equal(RobloxSessionSignal.Kicked, RobloxLogClassifier.Classify(line));
    }

    [Fact]
    public void LogWatcher_ChoosesSessionCreatedNearLaunch_NotOlderRecentlyWrittenLog()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        DateTime launchUtc = new(2026, 6, 23, 10, 5, 15, DateTimeKind.Utc);
        string stalePath = Path.Combine(tempDir, "0.726.0.7261140_20260623T100000Z_Player_STALE_last.log");
        string sessionPath = Path.Combine(tempDir, "0.726.0.7261140_20260623T100515Z_Player_REAL_last.log");

        File.WriteAllText(stalePath, "[FLog::Network] Replicator created\n");
        File.WriteAllText(sessionPath, "[FLog::Network] Connection lost\n");

        File.SetLastWriteTimeUtc(stalePath, launchUtc.AddMinutes(5));
        File.SetLastWriteTimeUtc(sessionPath, launchUtc.AddSeconds(1));

        RobloxSessionSignal? detected = null;
        using var watcher = new RobloxLogWatcher(tempDir, launchUtc);
        watcher.Detected += signal => detected = signal;

        Poll(watcher);

        Assert.Equal(RobloxSessionSignal.Error, detected);
    }

    [Fact]
    public void TwoWatchers_BindDistinctLogs_WhenSecondLogIsCreatedLate()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var registry = new LogSessionRegistry();
        DateTime launchA = new(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc);
        DateTime launchB = launchA.AddSeconds(2);

        string logA = Path.Combine(tempDir, $"0.726.0.7261140_{Stamp(launchA)}_Player_A_last.log");
        File.WriteAllText(logA, "[FLog::Network] Replicator created\n");

        using var watcherA = new RobloxLogWatcher(tempDir, launchA, registry);
        using var watcherB = new RobloxLogWatcher(tempDir, launchB, registry);

        Poll(watcherA);
        Poll(watcherB);

        string logB = Path.Combine(tempDir, $"0.726.0.7261140_{Stamp(launchB)}_Player_B_last.log");
        File.WriteAllText(logB, "[FLog::Network] Replicator created\n");
        Poll(watcherB);

        Assert.Equal(logA, BoundLogPath(watcherA));
        Assert.Equal(logB, BoundLogPath(watcherB));
    }

    [Fact]
    public void TwoWatchers_BindDistinctLogs_WhenLogsShareFilenameSecond()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var registry = new LogSessionRegistry();
        DateTime second = new(2026, 6, 25, 10, 0, 0, DateTimeKind.Utc);
        DateTime launchA = second.AddMilliseconds(100);
        DateTime launchB = second.AddMilliseconds(200);
        string logA = Path.Combine(tempDir, $"0.726.0.7261140_{Stamp(second)}_Player_A_last.log");
        string logB = Path.Combine(tempDir, $"0.726.0.7261140_{Stamp(second)}_Player_B_last.log");

        File.WriteAllText(logA, "[FLog::Network] Replicator created\n");
        File.SetCreationTimeUtc(logA, second.AddMilliseconds(150));

        using var watcherA = new RobloxLogWatcher(tempDir, launchA, registry);
        using var watcherB = new RobloxLogWatcher(tempDir, launchB, registry);

        Poll(watcherB);
        Assert.Null(BoundLogPath(watcherB));

        Poll(watcherA);

        File.WriteAllText(logB, "[FLog::Network] Replicator created\n");
        File.SetCreationTimeUtc(logB, second.AddMilliseconds(250));
        Poll(watcherB);

        Assert.Equal(logA, BoundLogPath(watcherA));
        Assert.Equal(logB, BoundLogPath(watcherB));
    }

    [Fact]
    public void OlderWatcher_DoesNotStealYoungerLaunchLog_WhenOwnLogIsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var registry = new LogSessionRegistry();
        DateTime launchA = new(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc);
        DateTime launchB = launchA.AddSeconds(2);

        using var watcherA = new RobloxLogWatcher(tempDir, launchA, registry);
        using var watcherB = new RobloxLogWatcher(tempDir, launchB, registry);

        string logB = Path.Combine(tempDir, $"0.726.0.7261140_{Stamp(launchB)}_Player_B_last.log");
        File.WriteAllText(logB, "[FLog::Network] Replicator created\n");

        Poll(watcherA);
        Poll(watcherB);

        Assert.Null(BoundLogPath(watcherA));
        Assert.Equal(logB, BoundLogPath(watcherB));
    }

    [Fact]
    public void DisposedWatcher_ReleasesLog_SoAReconnectWatcherCanReclaimIt()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var registry = new LogSessionRegistry();
        DateTime launch = new(2026, 6, 23, 11, 0, 0, DateTimeKind.Utc);
        string log = Path.Combine(tempDir, $"0.726.0.7261140_{Stamp(launch)}_Player_X_last.log");
        File.WriteAllText(log, "[FLog::Network] Replicator created\n");

        var first = new RobloxLogWatcher(tempDir, launch, registry);
        Poll(first);
        Assert.Equal(log, BoundLogPath(first));

        first.Dispose();

        using var second = new RobloxLogWatcher(tempDir, launch, registry);
        Poll(second);

        Assert.Equal(log, BoundLogPath(second));
    }

    [Fact]
    public async Task KickSignalAfterLeaveMarker_ReconnectsInsteadOfBeingSuppressed()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var settings = new FakeSettingsService();

        using var service = new AutoReconnectService(
            tracker,
            settings,
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 42, Username = "tester" };
        var target = ServerTarget.Public(123);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        int relaunches = 0;
        service.Relaunch = (relaunchedAccount, relaunchedTarget, relaunchedVersion, _) =>
        {
            Assert.Same(account, relaunchedAccount);
            Assert.Same(target, relaunchedTarget);
            Assert.Same(version, relaunchedVersion);
            relaunches++;
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(account, target, version, Process.GetCurrentProcess());

        Signal(service, account.Id, RobloxSessionSignal.InGame);
        Signal(service, account.Id, RobloxSessionSignal.GracefulLeave);
        Signal(service, account.Id, RobloxSessionSignal.Kicked);
        await HandleExitAsync(service, account.Id);

        Assert.Equal(1, relaunches);
    }

    [Fact]
    public async Task KickSignalCanUpgradeLeaveMarkerBeforeSettingsGateRuns()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var settings = new FakeSettingsService();

        using var service = new AutoReconnectService(
            tracker,
            settings,
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 46, Username = "late-kick" };
        var target = ServerTarget.Public(321);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        int relaunches = 0;
        service.Relaunch = (_, _, _, _) =>
        {
            relaunches++;
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(account, target, version, Process.GetCurrentProcess());

        Signal(service, account.Id, RobloxSessionSignal.InGame);
        Signal(service, account.Id, RobloxSessionSignal.GracefulLeave);
        Task exitTask = BeginHandleExitAsync(service, account.Id);
        await Task.Delay(100);
        Signal(service, account.Id, RobloxSessionSignal.Kicked);
        await exitTask;

        Assert.Equal(1, relaunches);
    }

    [Fact]
    public async Task InGameMenuReturn_ReconnectsInsteadOfWaitingForAProcessCrash()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var service = new AutoReconnectService(
            tracker,
            new FakeSettingsService(),
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 43, Username = "menu-drop" };
        var target = ServerTarget.Public(456);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        int relaunches = 0;
        service.Relaunch = (_, _, _, _) =>
        {
            relaunches++;
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(account, target, version, Process.GetCurrentProcess());

        Signal(service, account.Id, RobloxSessionSignal.InGame);
        Signal(service, account.Id, RobloxSessionSignal.GracefulLeave);
        await HandleExitAsync(service, account.Id);

        Assert.Equal(1, relaunches);
    }

    [Fact]
    public async Task AccountDisabledFlag_NoLongerSuppressesAutoReconnect()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var service = new AutoReconnectService(
            tracker,
            new FakeSettingsService(),
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 44, Username = "legacy-disabled", AutoReconnectEnabled = false };
        var target = ServerTarget.Public(789);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        int relaunches = 0;
        service.Relaunch = (_, _, _, _) =>
        {
            relaunches++;
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(account, target, version, Process.GetCurrentProcess());

        Signal(service, account.Id, RobloxSessionSignal.InGame);
        Signal(service, account.Id, RobloxSessionSignal.Kicked);
        await HandleExitAsync(service, account.Id);

        Assert.Equal(1, relaunches);
    }

    [Fact]
    public async Task ErrorSignalForSecondTrackedSession_ReconnectsOnlySecondAccount()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var service = new AutoReconnectService(
            tracker,
            new FakeSettingsService(),
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var first = new Account { UserId = 101, Username = "first" };
        var second = new Account { UserId = 202, Username = "second" };
        var target = ServerTarget.Public(12345);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        var relaunched = new List<Guid>();
        service.Relaunch = (account, _, _, _) =>
        {
            relaunched.Add(account.Id);
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(first, target, version, Process.GetCurrentProcess());
        service.RegisterLaunch(second, target, version, Process.GetCurrentProcess());

        Signal(service, first.Id, RobloxSessionSignal.InGame);
        Signal(service, second.Id, RobloxSessionSignal.InGame);
        Signal(service, second.Id, RobloxSessionSignal.Error);
        await HandleExitAsync(service, second.Id);

        Assert.Equal(new[] { second.Id }, relaunched);
    }

    [Fact]
    public async Task SiblingDropRightAfterAnotherReconnect_IsSuppressedAsCollateral()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var service = new AutoReconnectService(
            tracker,
            new FakeSettingsService(),
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var crashed = new Account { UserId = 501, Username = "crashed" };
        var sibling = new Account { UserId = 502, Username = "sibling" };
        var target = ServerTarget.Public(555);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        var relaunched = new List<Guid>();
        service.Relaunch = (account, _, _, _) =>
        {
            relaunched.Add(account.Id);
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(crashed, target, version, Process.GetCurrentProcess());
        service.RegisterLaunch(sibling, target, version, Process.GetCurrentProcess());

        Signal(service, crashed.Id, RobloxSessionSignal.InGame);
        Signal(service, sibling.Id, RobloxSessionSignal.InGame);

        Signal(service, crashed.Id, RobloxSessionSignal.Error);
        await HandleExitAsync(service, crashed.Id);

        Signal(service, sibling.Id, RobloxSessionSignal.Error);
        await HandleExitAsync(service, sibling.Id);

        Assert.Equal(new[] { crashed.Id }, relaunched);
    }

    [Fact]
    public async Task StaleWatcherSignalAfterReconnect_IsIgnoredByRunId()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var service = new AutoReconnectService(
            tracker,
            new FakeSettingsService(),
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 303, Username = "stale-run" };
        var target = ServerTarget.Public(67890);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        int relaunches = 0;
        service.Relaunch = (_, _, _, _) =>
        {
            relaunches++;
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(account, target, version, Process.GetCurrentProcess());

        Signal(service, account.Id, RobloxSessionSignal.InGame);
        Guid staleRunId = CurrentRunId(service, account.Id);
        Signal(service, account.Id, RobloxSessionSignal.Error);
        await HandleExitAsync(service, account.Id);

        Assert.Equal(1, relaunches);
        Assert.NotEqual(staleRunId, CurrentRunId(service, account.Id));

        Signal(service, account.Id, staleRunId, RobloxSessionSignal.Error);
        await HandleExitAsync(service, account.Id);

        Assert.Equal(1, relaunches);
    }

    [Fact]
    public async Task CrashReconnect_RefreshesBrowserTrackerBeforeRelaunchingIntoOriginalTarget()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var service = new AutoReconnectService(
            tracker,
            new FakeSettingsService(),
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 404, Username = "crash", BrowserTrackerId = 12_345 };
        var target = ServerTarget.Public(920587237);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        long trackerAtReconnect = 0;
        ServerTarget? targetAtReconnect = null;
        service.Relaunch = (relaunchedAccount, relaunchedTarget, _, _) =>
        {
            trackerAtReconnect = relaunchedAccount.BrowserTrackerId;
            targetAtReconnect = relaunchedTarget;
            return Task.FromResult<Process?>(Process.GetCurrentProcess());
        };

        service.RegisterLaunch(account, target, version, Process.GetCurrentProcess());
        Signal(service, account.Id, RobloxSessionSignal.InGame);
        await HandleExitAsync(service, account.Id);

        Assert.NotEqual(12_345, trackerAtReconnect);
        Assert.Same(target, targetAtReconnect);
    }

    [Fact]
    public void KickSignal_KillsSessionProcessEvenWhenTrackerNoLongerOwnsIt()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var service = new AutoReconnectService(
            tracker,
            new FakeSettingsService(),
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 45, Username = "stuck-dialog" };
        var target = ServerTarget.Public(987);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };
        int relaunches = 0;
        service.Relaunch = (_, _, _, _) =>
        {
            relaunches++;
            return Task.FromResult<Process?>(null);
        };

        Process process = StartLongRunningProcess();
        int processId = process.Id;

        try
        {
            service.RegisterLaunch(account, target, version, process);

            Signal(service, account.Id, RobloxSessionSignal.InGame);
            Signal(service, account.Id, RobloxSessionSignal.Kicked);

            Assert.True(WaitUntilExited(processId));
            Assert.True(WaitUntil(() => relaunches == 1, 7000));
        }
        finally
        {
            KillIfRunning(processId);
        }
    }

    [Fact]
    public void DisabledAutoReconnect_DoesNotKillTheClientOnAKick()
    {
        using var tracker = new InstanceTracker();
        string tempDir = Path.Combine(Path.GetTempPath(), "instance-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var settings = new FakeSettingsService();
        settings.Settings.AutoReconnectMaster = false;

        using var service = new AutoReconnectService(
            tracker,
            settings,
            new AutoReconnectLog(Path.Combine(tempDir, "auto-reconnect.log")),
            launchUtc => new RobloxLogWatcher(tempDir, launchUtc));

        var account = new Account { UserId = 77, Username = "feature-off" };
        var target = ServerTarget.Public(111);
        var version = new RobloxVersion
        {
            FolderPath = AppContext.BaseDirectory,
            VersionGuid = "version-test",
            PlayerExePath = Environment.ProcessPath ?? "dotnet",
            FileVersion = "test"
        };

        int relaunches = 0;
        service.Relaunch = (_, _, _, _) =>
        {
            relaunches++;
            return Task.FromResult<Process?>(null);
        };

        Process process = StartLongRunningProcess();
        int processId = process.Id;

        try
        {
            service.RegisterLaunch(account, target, version, process);

            Signal(service, account.Id, RobloxSessionSignal.InGame);
            Signal(service, account.Id, RobloxSessionSignal.Kicked);

            Assert.False(WaitUntil(() => !IsProcessRunning(processId), 1500));
            Assert.Equal(0, relaunches);
        }
        finally
        {
            KillIfRunning(processId);
        }
    }

    [Fact]
    public void IsAutoReconnectEnabledFor_AllTriggers_DefaultOn()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Error));
        Assert.True(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Kick));
        Assert.True(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Crash));
    }

    [Fact]
    public void IsAutoReconnectEnabledFor_MasterOff_DisablesEveryTrigger()
    {
        var settings = new AppSettings { AutoReconnectMaster = false };

        Assert.False(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Error));
        Assert.False(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Kick));
        Assert.False(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Crash));
    }

    [Fact]
    public void IsAutoReconnectEnabledFor_PerTriggerSwitch_GatesOnlyThatTrigger()
    {
        var settings = new AppSettings { AutoReconnectOnCrash = false };

        Assert.True(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Kick));
        Assert.False(settings.IsAutoReconnectEnabledFor(AutoReconnectTrigger.Crash));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    [InlineData(20, 20)]
    [InlineData(30, 30)]
    [InlineData(31, 31)]
    [InlineData(100, 31)]
    public void NormalizeAutoReconnectAttempts_ClampsToRange(int input, int expected)
    {
        Assert.Equal(expected, AppSettings.NormalizeAutoReconnectAttempts(input));
    }

    [Fact]
    public void Normalize_ClampsStoredMaxAttempts()
    {
        var settings = new AppSettings { AutoReconnectMaxAttempts = 0 };

        settings.Normalize();

        Assert.Equal(AppSettings.MinAutoReconnectAttempts, settings.AutoReconnectMaxAttempts);
    }

    [Fact]
    public void Account_AutoReconnect_DefaultsOn()
    {
        Assert.True(new Account().AutoReconnectEnabled);
    }

    [Fact]
    public void Account_LegacyJsonWithoutField_DeserializesToEnabled()
    {
        const string legacy = "{\"UserId\":1,\"Username\":\"u\"}";

        Account? account = JsonSerializer.Deserialize<Account>(legacy,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(account);
        Assert.True(account!.AutoReconnectEnabled);
    }

    [Fact]
    public void Account_DisabledFlag_RoundTrips()
    {
        var account = new Account { UserId = 1, AutoReconnectEnabled = false };

        string json = JsonSerializer.Serialize(account);
        Account? back = JsonSerializer.Deserialize<Account>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.False(back!.AutoReconnectEnabled);
    }

    private static void Signal(AutoReconnectService service, Guid accountId, RobloxSessionSignal signal)
    {
        Signal(service, accountId, CurrentRunId(service, accountId), signal);
    }

    private static void Signal(AutoReconnectService service, Guid accountId, Guid runId, RobloxSessionSignal signal)
    {
        typeof(AutoReconnectService)
            .GetMethod("OnSignal", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, new object[] { accountId, runId, signal });
    }

    private static Guid CurrentRunId(AutoReconnectService service, Guid accountId)
    {
        object sessions = typeof(AutoReconnectService)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
        object session = ((IDictionary)sessions)[accountId]!;
        return (Guid)session.GetType()
            .GetProperty("RunId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(session)!;
    }

    private static void Poll(RobloxLogWatcher watcher)
    {
        typeof(RobloxLogWatcher)
            .GetMethod("Poll", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(watcher, null);
    }

    private static string Stamp(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'");

    private static string? BoundLogPath(RobloxLogWatcher watcher) =>
        (string?)typeof(RobloxLogWatcher)
            .GetField("_logPath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(watcher);

    private static async Task HandleExitAsync(AutoReconnectService service, Guid accountId)
    {
        Task task = BeginHandleExitAsync(service, accountId);

        await task;
    }

    private static Task BeginHandleExitAsync(AutoReconnectService service, Guid accountId)
    {
        return (Task)typeof(AutoReconnectService)
            .GetMethod("HandleExitAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, new object[] { accountId })!;
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
        => WaitUntil(() => !IsProcessRunning(processId), 5000);

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
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

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();

        public void Save()
        {
        }
    }
}

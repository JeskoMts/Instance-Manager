using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class LaunchServiceTests
{
    [Fact]
    public async Task WaitForWindow_ReturnsFalse_WhenTheClientDiesBeforeShowingAWindow()
    {
        using Process process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
        process.WaitForExit();

        Assert.False(await LaunchService.WaitForWindowAsync(process, TimeSpan.FromSeconds(5), CancellationToken.None));
    }

    [Fact]
    public async Task WaitForWindow_StopsWaitingAtTimeout_ButReportsALiveClientAsReady()
    {
        using Process process = StartSleeper();
        try
        {
            var clock = Stopwatch.StartNew();
            Assert.True(await LaunchService.WaitForWindowAsync(process, TimeSpan.FromMilliseconds(400), CancellationToken.None));
            Assert.InRange(clock.ElapsedMilliseconds, 300, 5000);
        }
        finally
        {
            process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task LaunchAsync_SkipsAccountsThatAreAlreadyRunning_InsteadOfKillingThem()
    {
        using var tracker = new InstanceTracker();
        LaunchService service = CreateService(tracker);
        var account = new Account { UserId = 1, Username = "running" };
        Process running = StartSleeper();
        try
        {
            tracker.Track(account.Id, running);
            var reports = new List<LaunchProgress>();

            LaunchSummary summary = await service.LaunchAsync(
                new[] { account }, ServerTarget.Public(1), _ => null, new SyncProgress(reports.Add));

            Assert.Equal(new LaunchSummary(0, 0, 1), summary);
            Assert.Contains(reports, r => r.Stage == LaunchStage.Skipped);
            Assert.True(tracker.IsRunning(account.Id));
        }
        finally
        {
            running.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task LaunchAsync_ReportsConfigurationErrorsOnce_WithoutRetrying()
    {
        using var tracker = new InstanceTracker();
        LaunchService service = CreateService(tracker);
        var account = new Account { UserId = 2, Username = "noversion" };
        var reports = new List<LaunchProgress>();

        LaunchSummary summary = await service.LaunchAsync(
            new[] { account }, ServerTarget.Public(1), _ => null, new SyncProgress(reports.Add));

        Assert.Equal(new LaunchSummary(0, 1), summary);
        Assert.Single(reports, r => r.Stage == LaunchStage.Starting);
        LaunchProgress failed = Assert.Single(reports, r => r.Stage == LaunchStage.Failed);
        Assert.Contains("No valid Roblox version", failed.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { LaunchStage.Queued, LaunchStage.Starting, LaunchStage.Failed }, reports.Select(r => r.Stage));
    }

    [Fact]
    public void LaunchSummaryText_ListsOnlyWhatHappened()
    {
        Assert.Equal("Started 3.", ShellViewModel.Describe(new LaunchSummary(3, 0)));
        Assert.Equal("Started 2 · 1 failed · 4 already running.", ShellViewModel.Describe(new LaunchSummary(2, 1, 4)));
    }

    private static LaunchService CreateService(InstanceTracker tracker)
    {
        var settings = new FakeSettingsService();
        settings.Settings.MultiInstanceEnabled = false;
        return new LaunchService(
            new DpapiSecureStore(),
            new RobloxAuthService(new HttpClient()),
            new RobloxLauncher(),
            tracker,
            new MultiInstanceManager(),
            settings);
    }

    private static Process StartSleeper() =>
        Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -Command Start-Sleep -Seconds 30")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;

    private sealed class SyncProgress(Action<LaunchProgress> onReport) : IProgress<LaunchProgress>
    {
        public void Report(LaunchProgress value) => onReport(value);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();

        public void Save()
        {
        }
    }
}

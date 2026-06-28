using System;
using System.Diagnostics;
using System.Threading.Tasks;
using InstanceManager.Models;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

public sealed class NotificationCenterTests
{
    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(20);
        Assert.True(condition());
    }

    [Fact]
    public void Show_MutedNotification_GoesToHistoryOnly_NotToToasts()
    {
        var settings = new FakeSettingsService();
        settings.Settings.NotifyMuteMaster = true;
        settings.Settings.MutedNotifications.Add(NotificationId.FavoriteSaved);
        var center = new NotificationCenterViewModel(settings);

        center.Show(NotificationId.FavoriteSaved, NotificationKind.Success, "Saved", "x", TimeSpan.Zero);

        Assert.Empty(center.Items);
        Assert.Single(center.History);
    }

    [Fact]
    public void Show_MasterMuteOff_StillMutesIdInMutedSet()
    {
        var settings = new FakeSettingsService();
        settings.Settings.NotifyMuteMaster = false;
        settings.Settings.MutedNotifications.Add(NotificationId.FavoriteSaved);
        var center = new NotificationCenterViewModel(settings);

        center.Show(NotificationId.FavoriteSaved, NotificationKind.Success, "Saved", "x", TimeSpan.Zero);

        Assert.Empty(center.Items);
        Assert.Single(center.History);
    }

    [Fact]
    public void Show_UsesConfiguredToastDuration()
    {
        var settings = new FakeSettingsService();
        settings.Settings.ToastDurationMs = 1234;
        var center = new NotificationCenterViewModel(settings);

        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "A", "1");

        Assert.Equal(1234, center.Items[0].LifetimeMs);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public void Save() { }
    }

    [Fact]
    public void Show_AddsToastWithRequestedKind()
    {
        var center = new NotificationCenterViewModel();

        center.Show(NotificationId.AccountAdded, NotificationKind.Error, "Launch failed", "Invalid server link", TimeSpan.Zero);

        ToastViewModel toast = Assert.Single(center.Items);
        Assert.Equal(NotificationKind.Error, toast.Kind);
        Assert.Equal("Launch failed", toast.Title);
        Assert.Equal("Invalid server link", toast.Message);
    }

    [Fact]
    public async Task Dismiss_RemovesToast()
    {
        var center = new NotificationCenterViewModel();
        center.Show(NotificationId.AccountAdded, NotificationKind.Success, "Saved", "Favorite saved", TimeSpan.Zero);
        ToastViewModel toast = Assert.Single(center.Items);

        center.DismissCommand.Execute(toast);

        Assert.True(toast.IsClosing);
        await WaitForAsync(() => center.Items.Count == 0);
    }

    [Fact]
    public void Show_SameToastTwice_CoalescesDuplicate()
    {
        var center = new NotificationCenterViewModel();

        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "Ready", "Done", TimeSpan.Zero);
        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "Ready", "Done", TimeSpan.Zero);

        Assert.Single(center.Items);
    }

    [Fact]
    public void Show_AddsToHistoryNewestFirst_AndIncrementsUnread()
    {
        var center = new NotificationCenterViewModel();

        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "A", "1", TimeSpan.Zero);
        center.Show(NotificationId.AccountAdded, NotificationKind.Success, "B", "2", TimeSpan.Zero);

        Assert.Equal(2, center.History.Count);
        Assert.Equal("B", center.History[0].Title);
        Assert.Equal(2, center.UnreadCount);
        Assert.True(center.HasUnread);
    }

    [Fact]
    public void OpeningCenter_ResetsUnread()
    {
        var center = new NotificationCenterViewModel();
        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "A", "1", TimeSpan.Zero);

        center.IsCenterOpen = true;

        Assert.Equal(0, center.UnreadCount);
        Assert.False(center.HasUnread);
    }

    [Fact]
    public void Show_WhileCenterOpen_DoesNotIncrementUnread()
    {
        var center = new NotificationCenterViewModel { IsCenterOpen = true };

        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "A", "1", TimeSpan.Zero);

        Assert.Equal(0, center.UnreadCount);
    }

    [Fact]
    public void ClearHistory_EmptiesHistoryAndClosesCenter()
    {
        var center = new NotificationCenterViewModel();
        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "A", "1", TimeSpan.Zero);
        center.IsCenterOpen = true;

        center.ClearHistoryCommand.Execute(null);

        Assert.Empty(center.History);
        Assert.False(center.HasHistory);
        Assert.False(center.IsCenterOpen);
    }

    [Fact]
    public void RemoveFromHistory_RemovesEntry()
    {
        var center = new NotificationCenterViewModel();
        center.Show(NotificationId.AccountAdded, NotificationKind.Info, "A", "1", TimeSpan.Zero);
        ToastViewModel entry = Assert.Single(center.History);

        center.RemoveFromHistoryCommand.Execute(entry);

        Assert.Empty(center.History);
    }

    [Fact]
    public async Task UndoCommand_InvokesActionAndDismissesToast()
    {
        var center = new NotificationCenterViewModel();
        bool undoCalled = false;
        Action undoAction = () => undoCalled = true;

        center.Show(NotificationId.AccountRemoved, NotificationKind.Success, "Removed", "msg", TimeSpan.Zero, undoAction);
        ToastViewModel toast = Assert.Single(center.Items);
        ToastViewModel historyEntry = Assert.Single(center.History);

        Assert.True(toast.CanUndo);
        Assert.True(historyEntry.CanUndo);
        Assert.False(undoCalled);

        center.UndoCommand.Execute(toast);

        Assert.True(undoCalled);
        Assert.Empty(center.History);
        await WaitForAsync(() => center.Items.Count == 0);
    }
}

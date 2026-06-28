using System.Threading.Tasks;
using System;
using System.Linq;
using InstanceManager.Models;
using InstanceManager.Services;
using InstanceManager.Storage;
using InstanceManager.ViewModels;
using Xunit;

namespace InstanceManager.Tests;

[Collection("Roblox singleton")]
public class SettingsViewModelTests
{
    private static SettingsViewModel Create(FakeSettingsService settings, MultiInstanceManager manager) =>
        new(settings, new FakeDialogService(), manager);

    [Fact]
    public void MultiInstanceEnabled_DefaultsToTrue()
    {
        using var manager = new MultiInstanceManager();
        var vm = Create(new FakeSettingsService(), manager);

        Assert.True(vm.MultiInstanceEnabled);
    }

    [Fact]
    public void DisablingMultiInstance_PersistsAndReleasesMutex()
    {
        var settings = new FakeSettingsService();
        using var manager = new MultiInstanceManager();
        var vm = Create(settings, manager);

        vm.MultiInstanceEnabled = false;

        Assert.False(settings.Settings.MultiInstanceEnabled);
        Assert.False(manager.IsHeld);
    }

    [Fact]
    public void EnablingMultiInstance_PersistsAndHoldsMutex()
    {
        var settings = new FakeSettingsService();
        settings.Settings.MultiInstanceEnabled = false;
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);
        var vm = Create(settings, manager);

        vm.MultiInstanceEnabled = true;

        Assert.True(settings.Settings.MultiInstanceEnabled);
        Assert.True(manager.IsHeld);
    }

    private static (string Current, string Legacy) UniqueNames()
    {
        string suffix = Guid.NewGuid().ToString("N");
        return ($"INSTANCEMANAGER_TEST_singletonEvent_{suffix}", $"INSTANCEMANAGER_TEST_singletonMutex_{suffix}");
    }

    [Fact]
    public void EnablingMasters_DoesNotRewriteIndividualChoices()
    {
        var settings = new FakeSettingsService();
        settings.Settings.ConfirmBypassDeleteFavorite = true;
        settings.Settings.MutedNotifications.Add(NotificationId.AccountAdded);
        using var manager = new MultiInstanceManager();
        var vm = Create(settings, manager);

        vm.ConfirmBypassMaster = true;
        vm.NotifyMuteMaster = true;

        Assert.True(settings.Settings.ConfirmBypassDeleteFavorite);
        Assert.Contains(NotificationId.AccountAdded, settings.Settings.MutedNotifications);
    }

    [Fact]
    public void NotificationMutes_ExposeAllPersistentNotificationIds()
    {
        using var manager = new MultiInstanceManager();
        var vm = Create(new FakeSettingsService(), manager);

        foreach (string name in new[]
        {
            "GameSelected",
            "AccountRenamed",
            "AccountGroupsUpdated",
            "GroupRenamed",
            "ThemeApplied",
            "ThemeCreated",
            "ThemeUpdated",
            "ThemeDeleted"
        })
        {
            NotificationId id = Enum.Parse<NotificationId>(name);
            Assert.Contains(vm.NotificationMutes, option => option.Id == id);
        }
    }

    [Theory]
    [InlineData("StopAllInstances")]
    [InlineData("ClearAccountGroups")]
    [InlineData("DeleteTheme")]
    public void NewConfirmationBypassActions_AreControlledByMaster(string actionName)
    {
        var settings = new AppSettings { ConfirmBypassMaster = true };
        ConfirmAction action = Enum.Parse<ConfirmAction>(actionName);

        Assert.True(settings.IsConfirmBypassed(action));
    }

    [Theory]
    [InlineData(250, 500)]
    [InlineData(750, 1000)]
    public void LaunchDelay_NormalizesLiveChanges(int input, int expected)
    {
        var settings = new FakeSettingsService();
        using var manager = new MultiInstanceManager();
        var vm = Create(settings, manager);

        vm.LaunchDelayMs = input;

        Assert.Equal(expected, vm.LaunchDelayMs);
        Assert.Equal(expected, settings.Settings.LaunchDelayMs);
    }

    [Theory]
    [InlineData(2349, 2300)]
    [InlineData(2350, 2400)]
    [InlineData(4999, 5000)]
    [InlineData(449, 500)]
    public void DisplayTime_RoundsToHundredAndClamps(int input, int expected)
    {
        var settings = new FakeSettingsService();
        using var manager = new MultiInstanceManager();
        var vm = Create(settings, manager);

        vm.ToastDurationMs = input;

        Assert.Equal(expected, vm.ToastDurationMs);
        Assert.Equal(expected, settings.Settings.ToastDurationMs);
    }


    [Fact]
    public void AutoReconnect_DefaultsOn()
    {
        using var manager = new MultiInstanceManager();
        var vm = Create(new FakeSettingsService(), manager);

        Assert.True(vm.AutoReconnectMaster);
        Assert.True(vm.AutoReconnectOnKickError);
        Assert.True(vm.AutoReconnectOnCrash);
        Assert.Equal(3, vm.AutoReconnectMaxAttempts);
    }

    [Fact]
    public void AutoReconnectToggles_Persist()
    {
        var settings = new FakeSettingsService();
        using var manager = new MultiInstanceManager();
        var vm = Create(settings, manager);

        vm.AutoReconnectMaster = false;
        vm.AutoReconnectOnKickError = false;

        Assert.False(settings.Settings.AutoReconnectMaster);
        Assert.False(settings.Settings.AutoReconnectOnKickError);
        Assert.True(settings.Settings.AutoReconnectOnCrash);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(35, 31)]
    [InlineData(31, 31)]
    [InlineData(5, 5)]
    public void AutoReconnectMaxAttempts_ClampsLiveChanges(int input, int expected)
    {
        var settings = new FakeSettingsService();
        using var manager = new MultiInstanceManager();
        var vm = Create(settings, manager);

        vm.AutoReconnectMaxAttempts = input;

        Assert.Equal(expected, vm.AutoReconnectMaxAttempts);
        Assert.Equal(expected, settings.Settings.AutoReconnectMaxAttempts);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public void Save() { }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<Account?> ShowAddAccountAsync() => Task.FromResult<Account?>(null);
        public string? Prompt(string title, string initialValue) => null;
        public FavoriteGame? EditFavorite(FavoriteGame existing) => null;
        public bool Confirm(string message) => true;
        public string? PickFolder(string title) => null;
    }
}

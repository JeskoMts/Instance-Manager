using InstanceManager.Models;
using Xunit;
using System;
using System.Linq;

namespace InstanceManager.Tests;

public sealed class AppSettingsBehaviorTests
{
    [Fact]
    public void ConfirmationBypass_MasterOverridesDisabledActionWithoutChangingIt()
    {
        var settings = new AppSettings
        {
            ConfirmBypassMaster = true,
            ConfirmBypassRemoveAccount = false
        };

        Assert.True(settings.IsConfirmBypassed(ConfirmAction.RemoveAccount));
        Assert.False(settings.ConfirmBypassRemoveAccount);
    }

    [Fact]
    public void ConfirmationBypass_IndividualStillWorksWithMasterOff()
    {
        var settings = new AppSettings
        {
            ConfirmBypassMaster = false,
            ConfirmBypassDeleteGroup = true
        };

        Assert.True(settings.IsConfirmBypassed(ConfirmAction.DeleteGroup));
    }

    [Fact]
    public void NotificationMute_MasterOverridesDisabledIndividualWithoutChangingIt()
    {
        var settings = new AppSettings { NotifyMuteMaster = true };

        Assert.True(settings.IsNotificationMuted(NotificationId.AccountAdded));
        Assert.DoesNotContain(NotificationId.AccountAdded, settings.MutedNotifications);
    }

    [Fact]
    public void NotificationMute_IndividualStillWorksWithMasterOff()
    {
        var settings = new AppSettings { NotifyMuteMaster = false };
        settings.MutedNotifications.Add(NotificationId.AccountAdded);

        Assert.True(settings.IsNotificationMuted(NotificationId.AccountAdded));
    }

    [Fact]
    public void AutoReconnectSettings_ExposeNewNames_AndMigrateLegacyAutoRejoinValues()
    {
        var settings = new AppSettings
        {
            AutoRejoinMaster = false,
            AutoRejoinOnKickError = false,
            AutoRejoinOnCrash = false,
            AutoRejoinMaxAttempts = 7
        };

        bool changed = settings.Normalize();

        Assert.True(changed);
        Assert.False(GetBool(settings, "AutoReconnectMaster"));
        Assert.False(GetBool(settings, "AutoReconnectOnKickError"));
        Assert.False(GetBool(settings, "AutoReconnectOnCrash"));
        Assert.Equal(7, GetInt(settings, "AutoReconnectMaxAttempts"));
        Assert.Null(settings.AutoRejoinMaster);
        Assert.Null(settings.AutoRejoinOnKickError);
        Assert.Null(settings.AutoRejoinOnCrash);
        Assert.Null(settings.AutoRejoinMaxAttempts);
    }

    [Fact]
    public void NotificationAndConfirmationEnums_IncludePersistentActionControls()
    {
        string[] notificationNames = Enum.GetNames(typeof(NotificationId));
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
            Assert.Contains(name, notificationNames);
        }

        string[] confirmNames = Enum.GetNames(typeof(ConfirmAction));
        foreach (string name in new[] { "StopAllInstances", "ClearAccountGroups", "DeleteTheme" })
            Assert.Contains(name, confirmNames);
    }

    private static bool GetBool(AppSettings settings, string propertyName) =>
        (bool)(typeof(AppSettings).GetProperty(propertyName)?.GetValue(settings)
            ?? throw new InvalidOperationException($"Missing {propertyName}"));

    private static int GetInt(AppSettings settings, string propertyName) =>
        (int)(typeof(AppSettings).GetProperty(propertyName)?.GetValue(settings)
            ?? throw new InvalidOperationException($"Missing {propertyName}"));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(250, 500)]
    [InlineData(750, 1000)]
    [InlineData(1249, 1000)]
    [InlineData(1250, 1500)]
    [InlineData(-1, 0)]
    [InlineData(29750, 30000)]
    [InlineData(40000, 30000)]
    public void Normalize_RoundsLaunchDelayToNearestFiveHundred(int input, int expected)
    {
        var settings = new AppSettings { LaunchDelayMs = input };

        settings.Normalize();

        Assert.Equal(expected, settings.LaunchDelayMs);
    }

    [Fact]
    public void Normalize_RepairsNullThemeOrderFromLegacySettings()
    {
        var settings = new AppSettings { ThemeOrder = null! };

        bool changed = settings.Normalize();

        Assert.True(changed);
        Assert.NotNull(settings.ThemeOrder);
        Assert.Empty(settings.ThemeOrder);
    }
}

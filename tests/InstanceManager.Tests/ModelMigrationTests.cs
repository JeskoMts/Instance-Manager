using System;
using InstanceManager.Models;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ModelMigrationTests
{
    [Fact]
    public void NormalizeGroupMemberships_MigratesLegacyGroupId()
    {
        Guid groupId = Guid.NewGuid();
        var account = new Account { GroupId = groupId };

        bool changed = account.NormalizeGroupMemberships();

        Assert.True(changed);
        Assert.Null(account.GroupId);
        Assert.Equal(new[] { groupId }, account.GroupIds);
    }

    [Fact]
    public void NormalizeGroupMemberships_RemovesDuplicateAndEmptyIds()
    {
        Guid groupId = Guid.NewGuid();
        var account = new Account { GroupIds = new() { Guid.Empty, groupId, groupId } };

        bool changed = account.NormalizeGroupMemberships();

        Assert.True(changed);
        Assert.Equal(new[] { groupId }, account.GroupIds);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(15_250, 15_500)]
    [InlineData(30_000, 30_000)]
    [InlineData(40_000, 30_000)]
    [InlineData(60_000, 30_000)]
    public void Normalize_ClampsAndRoundsLaunchDelay(int input, int expected)
    {
        var settings = new AppSettings { LaunchDelayMs = input };

        settings.Normalize();

        Assert.Equal(expected, settings.LaunchDelayMs);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Normalize_FoldsLegacyKickAndErrorToggles_IntoMergedSwitch(bool oldError, bool oldKick, bool expected)
    {
        var settings = new AppSettings
        {
            AutoReconnectOnKickError = !expected,
            AutoRejoinOnError = oldError,
            AutoRejoinOnKick = oldKick,
            AutoRejoinOnKickError = !expected
        };

        bool changed = settings.Normalize();

        Assert.True(changed);
        Assert.Equal(expected, settings.AutoReconnectOnKickError);
        Assert.Null(settings.AutoRejoinOnError);
        Assert.Null(settings.AutoRejoinOnKick);
        Assert.Null(settings.AutoRejoinOnKickError);
    }

    [Fact]
    public void Normalize_PostMergeSettings_LeaveMergedToggleUntouched()
    {
        var settings = new AppSettings { AutoReconnectOnKickError = false };

        settings.Normalize();

        Assert.False(settings.AutoReconnectOnKickError);
        Assert.Null(settings.AutoRejoinOnError);
        Assert.Null(settings.AutoRejoinOnKick);
        Assert.Null(settings.AutoRejoinOnKickError);
    }
}

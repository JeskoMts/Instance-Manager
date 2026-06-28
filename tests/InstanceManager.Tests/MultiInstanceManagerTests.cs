using InstanceManager.Services;
using System;
using System.Threading;
using Xunit;

namespace InstanceManager.Tests;

[CollectionDefinition("Roblox singleton", DisableParallelization = true)]
public sealed class RobloxSingletonCollectionDefinition
{
}

[Collection("Roblox singleton")]
public class MultiInstanceManagerTests
{
    [Fact]
    public void EnsureHeld_CreatesCurrentAndLegacyNamesAsMutexes()
    {
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);

        manager.EnsureHeld();

        using Mutex currentMutex = Mutex.OpenExisting(current);
        using Mutex legacyMutex = Mutex.OpenExisting(legacy);
    }

    [Fact]
    public void EnsureHeld_WhenCurrentNameIsAnEvent_ReportsTypeConflict()
    {
        (string current, string legacy) = UniqueNames();
        using var collision = new EventWaitHandle(false, EventResetMode.ManualReset, current);
        using var manager = new MultiInstanceManager(current, legacy);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(manager.EnsureHeld);

        Assert.Contains(current, error.Message, StringComparison.Ordinal);
        Assert.Contains("mutex", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryApply_WhenCurrentNameIsAnEvent_ReturnsFalseInsteadOfThrowing()
    {
        (string current, string legacy) = UniqueNames();
        using var collision = new EventWaitHandle(false, EventResetMode.ManualReset, current);
        using var manager = new MultiInstanceManager(current, legacy);

        bool applied = manager.TryApply(true);

        Assert.False(applied);
        Assert.False(manager.IsHeld);
    }

    [Fact]
    public void TryApply_WhenAvailable_HoldsMutexAndReportsSuccess()
    {
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);

        Assert.True(manager.TryApply(true));
        Assert.True(manager.IsHeld);
        Assert.True(manager.TryApply(false));
        Assert.False(manager.IsHeld);
    }

    [Fact]
    public void EnsureHeld_PreventsAnotherThreadFromAcquiringBothMutexes()
    {
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);
        manager.EnsureHeld();

        Assert.False(CanAcquireFromNewThread(current));
        Assert.False(CanAcquireFromNewThread(legacy));
    }

    [Fact]
    public void Apply_True_HoldsMutex()
    {
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);

        manager.Apply(true);

        Assert.True(manager.IsHeld);
    }

    [Fact]
    public void Apply_False_ReleasesHeldMutex()
    {
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);
        manager.EnsureHeld();
        Assert.True(manager.IsHeld);

        manager.Apply(false);

        Assert.False(manager.IsHeld);
        Assert.True(CanAcquireFromNewThread(current));
        Assert.True(CanAcquireFromNewThread(legacy));
    }

    [Fact]
    public void Release_WhenNotHeld_IsNoOp()
    {
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);

        manager.Release();

        Assert.False(manager.IsHeld);
    }

    [Fact]
    public void Apply_CanToggleBackAndForth()
    {
        (string current, string legacy) = UniqueNames();
        using var manager = new MultiInstanceManager(current, legacy);

        manager.Apply(true);
        Assert.True(manager.IsHeld);

        manager.Apply(false);
        Assert.False(manager.IsHeld);

        manager.Apply(true);
        Assert.True(manager.IsHeld);
    }

    [Fact]
    public void EnsureHeld_WhenMutexIsOwnedExternally_IsStillActive()
    {
        (string current, string legacy) = UniqueNames();
        using var externalReady = new ManualResetEventSlim();
        using var releaseExternal = new ManualResetEventSlim();
        var externalOwner = new Thread(() =>
        {
            using var mutex = new Mutex(false, current);
            mutex.WaitOne();
            externalReady.Set();
            releaseExternal.Wait();
            mutex.ReleaseMutex();
        });
        externalOwner.Start();
        Assert.True(externalReady.Wait(TimeSpan.FromSeconds(2)));

        using var manager = new MultiInstanceManager(current, legacy);
        manager.EnsureHeld();

        Assert.True(manager.IsHeld);
        releaseExternal.Set();
        Assert.True(externalOwner.Join(TimeSpan.FromSeconds(2)));
        Assert.True(SpinWait.SpinUntil(() => !CanAcquireFromNewThread(current), TimeSpan.FromSeconds(2)));
    }

    private static (string Current, string Legacy) UniqueNames()
    {
        string suffix = Guid.NewGuid().ToString("N");
        return ($"INSTANCEMANAGER_TEST_singletonEvent_{suffix}", $"INSTANCEMANAGER_TEST_singletonMutex_{suffix}");
    }

    private static bool CanAcquireFromNewThread(string name)
    {
        bool acquired = false;
        var thread = new Thread(() =>
        {
            using var mutex = new Mutex(false, name);
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            finally
            {
                if (acquired)
                    mutex.ReleaseMutex();
            }
        });

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        return acquired;
    }
}

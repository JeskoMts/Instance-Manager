# Multi-Instance Error Reconnect Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure an error restarts only the affected Roblox instance when several instances are open.

**Architecture:** Preserve the existing per-account reconnect flow and fix the shared log-allocation boundary. Track one claim per launch, remove already-bound launches from later matching, use registration order for deterministic ties, and retain subsecond filesystem precision when it corroborates the filename timestamp.

**Tech Stack:** .NET 8, C#, xUnit

---

### Task 1: Reproduce same-second multi-instance log starvation

**Files:**
- Modify: `tests/InstanceManager.Tests/AutoReconnectTests.cs`

- [x] **Step 1: Write the failing test**

Add `TwoWatchers_BindDistinctLogs_WhenLogsShareFilenameSecond`:

```csharp
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
```

- [x] **Step 2: Run the test to verify it fails**

Run:

```powershell
dotnet test tests\InstanceManager.Tests\InstanceManager.Tests.csproj --filter FullyQualifiedName~TwoWatchers_BindDistinctLogs_WhenLogsShareFilenameSecond --no-restore
```

Expected: FAIL because `watcherB` remains unbound.

### Task 2: Fix log ownership at the shared registry

**Files:**
- Modify: `src/InstanceManager/Services/LogSessionRegistry.cs`
- Modify: `src/InstanceManager/Services/RobloxLogWatcher.cs`

- [x] **Step 1: Track claims by launch**

Store each launch with its registration order. In `TryClaim`, ignore launches that already own a log and choose the earliest registration when distances tie. Record the winning launch-to-path claim atomically.

- [x] **Step 2: Release ownership through launch unregister**

Make `UnregisterLaunch` remove both the launch and its claimed path. Let `RobloxLogWatcher.Dispose` unregister once instead of separately releasing the path.

- [x] **Step 3: Preserve subsecond log creation precision**

In `GetSessionStartUtc`, parse the filename timestamp as before. If `File.GetCreationTimeUtc(path)` falls within that same UTC second, return the creation time; otherwise return the parsed filename timestamp.

- [x] **Step 4: Run the regression test**

Run:

```powershell
dotnet test tests\InstanceManager.Tests\InstanceManager.Tests.csproj --filter FullyQualifiedName~TwoWatchers_BindDistinctLogs_WhenLogsShareFilenameSecond --no-restore
```

Expected: PASS.

### Task 3: Verify behavior and regressions

**Files:**
- No additional changes

- [x] **Step 1: Run all Auto Reconnect tests**

```powershell
dotnet test tests\InstanceManager.Tests\InstanceManager.Tests.csproj --filter FullyQualifiedName~AutoReconnectTests --no-restore
```

Expected: all Auto Reconnect tests pass.

- [x] **Step 2: Run the complete test suite**

```powershell
dotnet test InstanceManager.sln --no-restore
```

Expected: all tests pass.

- [x] **Step 3: Build Release**

```powershell
dotnet build InstanceManager.sln --configuration Release --no-restore
```

Expected: build succeeds with zero errors.

# Multi-Instance Error Reconnect Design

## Goal

When several Roblox instances are open, an error in one instance must restart only that account and leave every other instance running.

## Root cause

`LogSessionRegistry` keeps every active launch in timestamp matching even after that launch already owns a log. When multiple Roblox logs are created in the same second, the first launch can remain the best timestamp match for later logs and prevent later watchers from binding. The affected account then never receives its error signal.

Roblox filenames expose timestamps only to whole seconds, while the filesystem creation time contains subsecond precision.

## Design

- Keep one log claim per launch in `LogSessionRegistry`.
- Exclude launches that already own a log when selecting the best launch for another log.
- Resolve equal timestamp matches by launch registration order, oldest first.
- Use filesystem creation time when it falls within the same second as the filename timestamp; otherwise retain the filename timestamp.
- Keep reconnect orchestration unchanged: once the correct watcher emits `Error`, `AutoReconnectService` already stops and relaunches only that account.

## Verification

Add one regression test with two launches and two logs sharing the same filename second. The first watcher claims the first log; after the second log appears, the second watcher must claim it rather than remain unbound. Existing orchestration tests continue to verify that an error reconnects only the affected account.

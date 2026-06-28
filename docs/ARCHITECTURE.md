# Architecture

Instance Manager is a single desktop application. It is built on .NET 8 and WPF, runs x64 only, and follows a fairly traditional MVVM layout. There is no server, no database, and no background Windows service. All state lives in the running process and in a handful of JSON files under `%APPDATA%\Instance Manager`. The only things it talks to are the official Roblox HTTPS endpoints and the Roblox client it launches on your machine.

This document explains how the pieces fit together, what happens during the two operations that matter most (launching and reconnecting), and why certain decisions were made.

## Design goals

A few goals shaped most of the structure:

- **Local and private.** No accounts of its own, no telemetry, no cloud. The user's machine is the whole world.
- **Hard to crash.** A locked file, an expired cookie, or an occupied Roblox singleton should degrade gracefully, not take the app down.
- **Testable without a UI.** The interesting logic lives in services and view models that can be exercised directly in unit tests. The test suite never opens a window.
- **One user, one dataset.** Because there is exactly one user, almost everything is a process-wide singleton and keeps its working copy in memory.

## Runtime shape

```
                        +-----------------------------------------+
                        |               MainWindow                |
                        |     WPF, drag and drop, theming         |
                        +--------------------+--------------------+
                                             | DataContext
                        +--------------------v--------------------+
                        |              ShellViewModel             |
                        |  coordinates: AccountList, LaunchPanel, |
                        |  VersionBar, Settings, Theme, Toasts    |
                        +------+-------------------------+--------+
                               |                         |
                    +----------v----------+   +----------v------------+
                    |       Services      |   |        Storage        |
                    |  LaunchService      |   |  AccountRepository    |
                    |  RobloxAuthService  |   |  GroupRepository      |
                    |  RobloxLauncher     |   |  FavoriteRepository   |
                    |  MultiInstanceMgr   |   |  ThemeRepository      |
                    |  InstanceTracker    |   |  SettingsService      |
                    |  AutoReconnectService  |   +-----------+-----------+
                    |  RobloxLogWatcher   |               |
                    |  VersionService     |        +------v-------+
                    |  RobloxAvatarSvc    |        | JsonFileStore|
                    |  ServerLinkResolver |        |  (atomic)    |
                    |  DpapiSecureStore   |        +------+-------+
                    |  ThemeService       |               |
                    +------+--------------+        +------v----------------+
                           |                       | %APPDATA%\Instance    |
                    +------v---------------+        | Manager\*.json,       |
                    | Roblox HTTPS APIs    |        | webview\, *.log       |
                    | auth, users,         |        +-----------------------+
                    | thumbnails, versions |
                    +------+---------------+
                           |
                    +------v---------------+
                    | RobloxPlayerBeta.exe |  one process per account
                    +----------------------+
```

## Layers

The code is organized into clear layers, top to bottom.

**Views.** XAML plus minimal code-behind. `MainWindow` carries the drag-and-drop logic for lists and themes. The dialogs (login, confirmations, editors) live under `Views/`. Views bind to view models and know nothing about services or file paths.

**ViewModels.** The MVVM layer, built on CommunityToolkit.Mvvm. `ShellViewModel` is the coordinator. It owns the sub view models (`AccountListViewModel`, `LaunchPanelViewModel`, `VersionBarViewModel`, `SettingsViewModel`, `ThemeViewModel`, `NotificationCenterViewModel`), exposes `LaunchAsync`, and implements `IShellCoordinator` for status and notification messages. View models depend only on repository and service interfaces.

**Services.** Domain logic and I/O. This is where the real work happens: authentication, launching, multi-instance, auto-reconnect, version discovery, avatar loading, link validation, encryption, and theming.

**Storage.** Four repositories and one settings service, each persisting one JSON file through `JsonFileStore`. The repositories keep their list in memory and are the source of truth at runtime.

**Models.** Plain data classes with almost no behavior. The exceptions are `Account.NormalizeGroupMemberships` (a small migration) and `AppSettings.Normalize` (value clamping and migration).

## Composition and lifecycle

Wiring lives in [ServiceCollectionExtensions](../src/InstanceManager/Composition/ServiceCollectionExtensions.cs). Every service and repository is registered as a singleton, along with one shared `HttpClient`.

On startup ([App.OnStartup](../src/InstanceManager/App.xaml.cs)):

1. Build the `ServiceProvider`.
2. Apply the saved theme before any window exists, so there is no flash of the default palette.
3. Best-effort enable multi-instance from the saved setting.
4. Resolve `ShellViewModel` and `MainWindow`, wire `MainWindow.Loaded` to `ShellViewModel.InitializeAsync`, and show the window.
5. In Debug builds only, route WPF data-binding warnings to `binding-errors.log`.

On exit ([App.OnExit](../src/InstanceManager/App.xaml.cs)) the provider is disposed. That single dispose releases the mutex grip and tears down tracked processes, since the relevant services implement `IDisposable`.

## Component catalog

The services, with the detail that matters when reading or changing them:

- [RobloxAuthService](../src/InstanceManager/Services/RobloxAuthService.cs) exchanges a `.ROBLOSECURITY` cookie for a short-lived authentication ticket and reads the authenticated user's info. It performs the CSRF handshake (a 403 with an `x-csrf-token` header, retried with that token) and backs off on HTTP 429 using the `Retry-After` header, clamped between 1 and 30 seconds, up to five attempts total.
- [RobloxLauncher](../src/InstanceManager/Services/RobloxLauncher.cs) builds the `PlaceLauncher` URL and the `roblox-player:` launch URI, then starts `RobloxPlayerBeta.exe` with that URI as its only argument. All URL assembly is static and side-effect free, which makes it easy to unit test.
- [MultiInstanceManager](../src/InstanceManager/Services/MultiInstanceManager.cs) holds the two Roblox singleton mutexes open. Covered in detail below.
- [InstanceTracker](../src/InstanceManager/Services/InstanceTracker.cs) maps each account to its running process, raises a `RunningChanged` event on start and exit, and can stop one instance or all of them. It hooks `Process.Exited` so an instance that dies on its own is noticed.
- [AutoReconnectService](../src/InstanceManager/Services/AutoReconnectService.cs) is the brain of Auto Reconnect. It listens to `InstanceTracker` for process exits and to a `RobloxLogWatcher` per instance for in-game state changes, decides whether a drop should reconnect, and delegates the actual relaunch back to `LaunchService`.
- [RobloxLogWatcher](../src/InstanceManager/Services/RobloxLogWatcher.cs) tails one Roblox client log on a timer and reports meaningful lines. It binds to exactly one log file and uses a shared registry so two watchers never follow the same file.
- [LogSessionRegistry](../src/InstanceManager/Services/LogSessionRegistry.cs) is the small shared set of claimed log paths that keeps watchers from colliding. One registry is shared by all watchers created from a single `AutoReconnectService`.
- [RobloxLogClassifier](../src/InstanceManager/Services/RobloxLogClassifier.cs) turns a single log line into a signal (in-game, kicked, error, graceful leave) by matching known markers. Markers are case-insensitive and ordered so the most specific signal wins.
- [LaunchService](../src/InstanceManager/Services/LaunchService.cs) orchestrates a launch run across many accounts and also provides the single-account relaunch primitive that Auto Reconnect calls back into.
- [VersionService](../src/InstanceManager/Services/VersionService.cs) finds installed Roblox versions on local fixed drives and optionally asks Roblox for the latest version. [RobloxExecutableValidator](../src/InstanceManager/Services/RobloxExecutableValidator.cs) canonicalizes paths, rejects reparse/network escapes, and verifies Windows trust plus the Roblox signer during discovery and again immediately before launch.
- [RobloxAvatarService](../src/InstanceManager/Services/RobloxAvatarService.cs) loads avatar headshots from the thumbnail API and deduplicates concurrent requests for the same user.
- [ServerLinkResolver](../src/InstanceManager/Services/ServerLinkResolver.cs) and [GameLinkParser](../src/InstanceManager/Services/GameLinkParser.cs) parse and validate user input: place ids, job ids, and server links.
- [DpapiSecureStore](../src/InstanceManager/Services/DpapiSecureStore.cs) encrypts and decrypts the cookie with Windows DPAPI.
- [ThemeService](../src/InstanceManager/Services/ThemeService.cs) and [ThemeCodec](../src/InstanceManager/Services/ThemeCodec.cs) apply palettes to the live application resources and encode or decode a theme as a shareable text code.

## A launch, end to end

Launching is the central operation. Traced through `ShellViewModel.LaunchAsync` into `LaunchService.LaunchAsync`:

1. **Resolve the target.** The launch panel produces a [ServerTarget](../src/InstanceManager/Models/ServerTarget.cs): either `PublicByLink` (place id only) or `PrivateByJobId` (place id plus job id). A typed server link is validated by `ServerLinkResolver` first, which only accepts HTTPS and only Roblox domains, follows at most five redirects, and re-checks each hop against the allowlist.
2. **Enable multi-instance, best-effort.** `LaunchService` calls `MultiInstanceManager.TryApply`. If the grip cannot be taken, it reports that through the progress callback and launches anyway.
3. **Per account, in order:**
   - Resolve the version: the account's preferred version if set, otherwise the global selection. A missing or invalid version counts the account as a failure and the run moves on.
   - Decrypt the cookie with `DpapiSecureStore.TryUnprotect`. A decryption failure (for example, the file came from a different Windows user) is a per-account failure, not a fatal error.
   - Fetch a fresh authentication ticket with `RobloxAuthService.GetAuthTicketAsync`, including the CSRF handshake and rate-limit backoff.
   - Build the `roblox-player:` URI with `RobloxLauncher.BuildLaunchUrl` (ticket, PlaceLauncher URL, browser tracker id, launch time).
   - Start `RobloxPlayerBeta.exe` and hand the process to `InstanceTracker.Track`.
   - Register the instance with `AutoReconnectService` so a later drop can reconnect it.
   - Wait `LaunchDelayMs` before the next account.
4. **Report the result.** A `LaunchSummary(Started, Failed)` flows back and becomes a toast and a status line.

The key property is that one account's failure never ends the run. Errors are caught per account and counted, so an expired cookie in the middle of a group does not strand the accounts after it.

## Auto Reconnect, end to end

Auto Reconnect sits on top of the launch primitive and adds a feedback loop. For each launched instance, `AutoReconnectService` keeps a small session with the account, target, version, current process, attempt count, and a flag for whether it ever reached in-game.

The signals come from two places:

- **The Roblox log,** through a `RobloxLogWatcher` per instance. The watcher tails the client log file and the classifier reports when the instance joined a game, got kicked or removed (error 267 and moderation messages), hit a disconnect or generic error, or left back to the menu.
- **The process,** through `InstanceTracker.RunningChanged`. When the process exits, the service decides what the exit meant.

The decision logic resolves a drop into one of three triggers:

- **Error** for a disconnect, server shutdown, generic error dialog, or menu return.
- **Kick** for a kick or removal (error 267).
- **Crash** when the process died while in-game with no clean leave recorded.

A drop only reconnects when its trigger is enabled in settings. The settings gate has one switch for Error and Kick together (`AutoReconnectOnKickError`) and one for Crash (`AutoReconnectOnCrash`), both behind the `AutoReconnectMaster` switch. A manual stop sets a flag that suppresses any reconnect for that instance. When a reconnect is warranted, the service closes the stuck client if needed, waits briefly, and calls back into `LaunchService.LaunchOneAsync` to start the same account again. Attempts are capped per run by `AutoReconnectMaxAttempts`, and every step is recorded in `auto-reconnect.log`.

### Why each instance needs its own log file

Roblox writes one log file per client run into `%LOCALAPPDATA%\Roblox\logs`, and the file name does not contain the process id, so there is no direct way to map a process to its log. The watcher picks the log whose timestamp is nearest to its own launch time.

With several instances launched close together, that pick alone is not enough. A Roblox client can take a few seconds to create its log file. During that gap, a younger instance's watcher would find only the older instance's log and bind to it, so two watchers would tail the same file. The result was that a kick on one account would either reconnect the wrong account or, when the affected account's log was the one nobody was watching, reconnect nobody at all. Running two accounts and seeing only "both errored" cases work was the visible symptom.

The fix is `LogSessionRegistry`. Each watcher claims its log file in a shared registry and skips any file another watcher already owns. A younger watcher that finds only a claimed log waits and retries on its next poll, then binds to its own log once it appears. The claim is released when the watcher is disposed, which is exactly what happens on a reconnect before the new watcher attaches, so a reconnected instance can reclaim its fresh log.

## Multi-instance in detail

A Windows mutex belongs to the thread that owns it. To hold a mutex for the lifetime of the app, `MultiInstanceManager` gives each mutex its own dedicated background thread (a `MutexSlot`). The thread opens or creates the named mutex, takes it, signals that it is ready, and then parks until a stop event is set. When the app shuts down or the user turns the feature off, the stop event is set, every slot releases its mutex and joins, and the hold session is torn down.

The design handles the awkward cases explicitly:

- If the named object exists but is not a mutex (something else grabbed the name), opening it throws a specific error and the feature reports as unavailable rather than crashing.
- If the previous owner abandoned the mutex (a `AbandonedMutexException`), that is treated as a successful acquisition, since an abandoned mutex is free.
- `TryApply` wraps the whole thing so the startup path, the settings toggle, and a launch can ask for multi-instance without having to handle failure. Callers that do want the detail can use `Apply` or `EnsureHeld`, which still throw.

## External integrations

Every network call goes through one shared `HttpClient` configured in composition: cookies off, automatic redirects off, decompression on, a 20 second timeout, and the user agent `Roblox/WinInet`. Turning redirects off is deliberate so that server-link resolution controls and checks each hop itself.

| Endpoint | Purpose |
|---|---|
| `auth.roblox.com/v1/authentication-ticket/` | Trade the cookie for a one-time auth ticket (POST, CSRF) |
| `users.roblox.com/v1/users/authenticated` | Confirm an account's identity when adding it |
| `thumbnails.roblox.com/v1/users/avatar-headshot` | Avatar headshot image |
| `clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer` | Latest client version |
| `assetgame.roblox.com/game/PlaceLauncher.ashx` | Part of the launch URI, embedded rather than fetched directly |

In addition the app starts `RobloxPlayerBeta.exe` locally and hosts a WebView2 browser for the login, which loads `https://www.roblox.com/login`. The login dialog creates its own WebView2 environment with an InPrivate profile and tears it down on close, so the WebView2 host processes (which dominate the app's footprint while sign-in is open) are released once the dialog goes away rather than lingering for the whole session.

## Threading and concurrency

The app is more concurrent than a small UI tool might suggest, so the boundaries are worth naming:

- **UI thread.** All view-model updates and WPF binding happen here.
- **Mutex owner threads.** One background thread per held mutex, parked on a stop event.
- **Log watcher timers.** Each `RobloxLogWatcher` runs a one-second timer. The classifier and the detection callback run on that timer thread.
- **Process exit callbacks.** `Process.Exited` fires on a thread-pool thread.
- **Settings save timer.** `SettingsService` saves on a debounced timer, also on a thread-pool thread.

Shared state is guarded by locks at the edges. `AutoReconnectService` keeps its sessions behind a single lock and does the actual relaunch outside the lock. `LogSessionRegistry` guards its claimed-paths set. `InstanceTracker` uses a concurrent dictionary. The general rule is that I/O and process work happen outside locks, and only the small bookkeeping is locked.

## Error handling philosophy

The app leans toward swallowing recoverable errors at the layer that owns them, rather than letting them bubble up into a crash:

- **Persistence** swallows `IOException`, `JsonException`, and `UnauthorizedAccessException`. This matters because saves can run on a thread-pool timer thread, where an unhandled exception would end the process.
- **Per-account launch failures** are caught and counted so the rest of a run continues.
- **Multi-instance** failures are turned into a best-effort no-op through `TryApply`.
- **Log watching and auto-reconnect logging** are best-effort. A locked or missing log file is retried on the next tick and never throws into the app.

The trade-off is that some failures are silent by design. Where that is true, the failure is still observable somewhere: a per-account error becomes a toast, a failed reconnect is written to `auto-reconnect.log`, and binding warnings go to a log file in Debug builds.

## Testing strategy

The test suite is xUnit and runs without a UI. It covers the service logic directly because the main project exposes its internals to the test assembly through `InternalsVisibleTo`. A few patterns recur:

- Services that do I/O take their dependencies through constructors, so tests can inject fakes (a fake settings service, a temp directory, a current process standing in for a Roblox client).
- `AutoReconnectService` takes a watcher factory, so tests can point watchers at a temp directory and drive signals directly.
- Some contracts are asserted against the XAML text itself (for example, that the settings page shows the merged Auto Reconnect toggle), which keeps the UI labels and bindings honest without a UI test harness.

## Extension points

- **Other persistence backends.** The repositories sit behind interfaces (`IAccountRepository` and friends). An alternative store would replace `JsonFileStore` and be registered in composition.
- **Other join modes.** `JoinMode`, `ServerTarget`, and the switch in `RobloxLauncher.BuildPlaceLauncherUrl` are the only places that produce launch URLs.
- **Other dialogs.** UI prompts go through `IDialogService`, so they are swappable and testable against the interface.
- **Other auto-reconnect signals.** New drop or join markers are added in `RobloxLogClassifier`. New trigger policy is added in `AppSettings.IsAutoReconnectEnabledFor` and the settings UI.
- **Themes.** Built-in palettes come from `BuiltInThemes` in code, user themes from `themes.json`, and sharing runs through `ThemeCodec`.

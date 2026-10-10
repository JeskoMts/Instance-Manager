# Architecture

[README](../README.md) · [Features](FEATURES.md) · [Troubleshooting](TROUBLESHOOTING.md) · [Data storage](DATA-STORAGE.md) · [Security](SECURITY.md) · Architecture · [Changelog](../CHANGELOG.md)

Instance Manager is a single WPF desktop app on .NET 8, built for x64 only, with a conventional MVVM layout. There is no server, database or Windows service. State lives in the running process and in a few JSON files under `%APPDATA%\Instance Manager`. The app talks to Roblox's HTTPS APIs, to GitHub for updates, and to the Roblox clients it starts on the same PC.

This page shows how the parts fit together and walks through the operations that matter most: launching, reconnecting, automation, Anti-AFK and updating.

## Design goals

- Local and private. No accounts of its own, no telemetry, no cloud.
- Hard to crash. A locked file, an expired cookie or a Roblox singleton held by someone else should break one feature, never the whole app.
- Testable without a UI. The logic sits in services and view models that unit tests call directly, and the test suite never opens a window.
- One user, one dataset. Almost everything is a process-wide singleton that keeps its working copy in memory.

## Runtime shape

```
                     +-------------------------------------------+
                     |                MainWindow                 |
                     |  sidebar, pages under Views/Pages, toasts |
                     +---------------------+---------------------+
                                           | DataContext
                     +---------------------v---------------------+
                     |               ShellViewModel              |
                     |  AccountList, LaunchPanel, VersionBar,    |
                     |  Games, Settings, Theme, Notifications,   |
                     |  Automation, Updates, Graphics,           |
                     |  Operations                               |
                     +-------+---------------------------+-------+
                             |                           |
                 +-----------v-----------+   +-----------v-----------+
                 |        Services       |   |        Storage        |
                 |  LaunchService        |   |  AccountRepository    |
                 |  RobloxAuthService    |   |  GroupRepository      |
                 |  SessionCheckService  |   |  FavoriteRepository   |
                 |  RobloxLauncher       |   |  ThemeRepository      |
                 |  MultiInstanceManager |   |  AutomationRepository |
                 |  InstanceTracker      |   |  SettingsService      |
                 |  AutoReconnectService |   +-----------+-----------+
                 |  RobloxLogWatcher     |               |
                 |  RobloxGraphicsService|        +------v-------+
                 |  VersionService       |        | JsonFileStore|
                 |  RobloxAvatarService  |        |  (atomic)    |
                 |  RobloxGamesService   |        +------+-------+
                 |  ServerLinkResolver   |               |
                 |  DpapiSecureStore     |    +----------v--------------+
                 |  ThemeService         |    | %APPDATA%\Instance      |
                 |  UpdateService        |    | Manager\*.json, *.log   |
                 |  TrayIcon             |    +-------------------------+
                 |  StartupService       |
                 |  SingleInstance       |
                 |  AntiAfkService       |
                 +-----------+-----------+
                             |
                 +-----------v-----------+
                 | Roblox HTTPS APIs     |
                 | GitHub releases API   |
                 +-----------+-----------+
                             |
                 +-----------v-----------+
                 | RobloxPlayerBeta.exe  |  one process per account
                 +-----------------------+
```

## Layers

Views are XAML with little code-behind. `MainWindow` is the frame: the sidebar, caption buttons, toasts and notification center. The four pages live under `Views/Pages/`. `AccountsPage` holds the drag-and-drop logic for accounts, groups and favorites. `SettingsPage` holds only the searchable category list; each category is its own view under `Views/Settings/` (for example `AppearanceSettings` with the theme drag and drop, and `ReconnectSettings` with the Anti-AFK key capture), and [SettingsCatalog](../src/InstanceManager/ViewModels/SettingsCatalog.cs) lists them with their group, icon, description and search words. Dialogs for login, confirmations and editors live under `Views/`, and `Controls/ColorPicker` is the color picker used by the group editor and the Custom colors list. Views bind to view models and know nothing about services or file paths.

Only the Accounts page is built with the window. Games, Automation and Settings sit in a [DeferredContent](../src/InstanceManager/Controls/DeferredContent.cs) that builds the page the first time it becomes visible and keeps it. [SettingsCategoryHost](../src/InstanceManager/Views/Settings/SettingsCategoryHost.cs) does the same per settings category and sets each cached view's `DataContext` directly, so a category taken out of the tree never detaches its bindings. `Icon` parses each vector path the first time it is drawn.

The view models use CommunityToolkit.Mvvm. `ShellViewModel` owns the others (`AccountListViewModel`, `LaunchPanelViewModel`, `VersionBarViewModel`, `GamesViewModel`, `SettingsViewModel`, `ThemeViewModel`, `NotificationCenterViewModel`, `AutomationViewModel`, `UpdatesViewModel`, `GraphicsViewModel` and `OperationsViewModel`). It runs launches through `LaunchAsync` and implements `IShellCoordinator`, so child view models can start launches, stop accounts and post notifications. View models depend only on repository and service interfaces.

Services hold the domain logic and all I/O: login, launching, multi-instance, Auto Reconnect, graphics settings, version discovery, avatars, link validation, encryption, themes, updates, the Windows integration (tray, autostart, single instance) and Anti-AFK.

Storage is five repositories plus a settings service. Each keeps one JSON file through `JsonFileStore` and holds its list in memory as the source of truth while the app runs.

Models are plain data classes. The only behavior in them is `Account.NormalizeGroupMemberships` and `AppSettings.Normalize`, both small migrations.

## Composition and lifecycle

[ServiceCollectionExtensions](../src/InstanceManager/Composition/ServiceCollectionExtensions.cs) registers every service and repository as a singleton, together with one shared `HttpClient`.

[App.OnStartup](../src/InstanceManager/App.xaml.cs) then:

1. Takes the `Local\JeskoMts.InstanceManager.Instance` mutex through [SingleInstance](../src/InstanceManager/Services/SingleInstance.cs). If another copy holds it, the new process sets the `.Activate` event, which makes the running copy show its window, and exits. After an update restart (`--updated`) it waits up to 15 seconds for the old process to let go.
2. Deletes an old roaming WebView2 folder and the leftover files of removed features (`playtime.json`, `diagnostics.log`), then builds the `ServiceProvider`.
3. Applies the saved theme before any window exists, so the default palette never flashes.
4. Takes the Roblox singletons if multi-instance is on, as far as possible.
5. Resolves `ShellViewModel`, starts Anti-AFK through `OperationsViewModel.Start`, resolves `MainWindow`, wires the tray icon and the activation event and refreshes the autostart entry in case the exe moved.
6. Shows the window and runs `ShellViewModel.InitializeAsync`, or only runs it when Windows started the app with `--startup` and Start in the tray is on.
7. Cleans up after the previous update, shows the "updated" notification after an update restart, and starts the update timer (with the startup check, if enabled) and the automation timer.

In Debug builds WPF binding warnings go to `binding-errors.log`.

[App.OnExit](../src/InstanceManager/App.xaml.cs) removes the tray icon, stops the automation timer and Anti-AFK, installs a waiting update, disposes the provider (which releases the Roblox singletons and lets go of tracked processes) and releases the single-instance mutex. If an update asked for a restart, the new exe is started last.

With Close to tray on, the window's close button only hides the window, and the app keeps running until Quit in the tray menu.

## Services

- [RobloxAuthService](../src/InstanceManager/Services/RobloxAuthService.cs) trades an account's session cookie for a one-time authentication ticket and reads the signed-in user. It handles the CSRF handshake (a 403 with an `x-csrf-token` header, retried with that token) and backs off on HTTP 429 using `Retry-After`, clamped to 1 to 30 seconds, for up to five attempts. `CheckSessionAsync` answers Valid, Expired (401 or 403, or a different user) or Unknown (no answer), which [SessionCheckService](../src/InstanceManager/Services/SessionCheckService.cs) uses for Check sessions.
- [RobloxLauncher](../src/InstanceManager/Services/RobloxLauncher.cs) builds the `PlaceLauncher` URL and the `roblox-player:` URI and starts `RobloxPlayerBeta.exe` with that URI as its only argument. URL building is static and has no side effects, which keeps it easy to test.
- [MultiInstanceManager](../src/InstanceManager/Services/MultiInstanceManager.cs) holds the Roblox singleton objects. See [Multi-instance](#multi-instance).
- [InstanceTracker](../src/InstanceManager/Services/InstanceTracker.cs) maps each account to its Roblox process, raises `RunningChanged` on start and exit, and stops one instance or all of them. It listens to `Process.Exited` so it notices clients that close on their own, and it lists the running processes for Anti-AFK.
- [AutoReconnectService](../src/InstanceManager/Services/AutoReconnectService.cs) decides whether a dropped instance comes back. It listens to `InstanceTracker` for exits and to one `RobloxLogWatcher` per instance for in-game events, and hands the relaunch back to `LaunchService`.
- [RobloxLogWatcher](../src/InstanceManager/Services/RobloxLogWatcher.cs) follows one Roblox client log and reports the lines that matter. [LogSessionRegistry](../src/InstanceManager/Services/LogSessionRegistry.cs) makes sure no two watchers follow the same file.
- [RobloxLogClassifier](../src/InstanceManager/Services/RobloxLogClassifier.cs) turns a log line into a signal (joined, kicked, error, left) by matching known markers. Matching ignores case, and the most specific marker wins. Lines from Roblox's HTTP, WebSocket and SignalR diagnostics and from the Creator channels, where a game's own scripts print, are never signals.
- [LaunchService](../src/InstanceManager/Services/LaunchService.cs) runs a launch across many accounts and provides the single-account relaunch that Auto Reconnect uses.
- [VersionService](../src/InstanceManager/Services/VersionService.cs) finds installed Roblox versions. [RobloxExecutableValidator](../src/InstanceManager/Services/RobloxExecutableValidator.cs) checks every `RobloxPlayerBeta.exe` during discovery and again right before launch (see [SECURITY.md](SECURITY.md#launching-roblox)).
- [RobloxAvatarService](../src/InstanceManager/Services/RobloxAvatarService.cs) loads avatar headshots, caches them on disk and merges concurrent requests for the same user. It looks up the picture addresses of up to 100 users in one request, sent when 100 are waiting or 20 ms after the first, and downloads a picture only when its address differs from the one saved next to the cached copy.
- [ServerLinkResolver](../src/InstanceManager/Services/ServerLinkResolver.cs) and [GameLinkParser](../src/InstanceManager/Services/GameLinkParser.cs) check what users type: Place IDs, Job IDs, private server link codes and server links. [RobloxShareLinkService](../src/InstanceManager/Services/RobloxShareLinkService.cs) looks up `roblox.com/share` links through Roblox's share link API. Roblox answers them only for a signed-in account and never puts the server into a redirect, so the code is sent with a saved login: first the accounts being launched, then the others, ten at most.
- [DpapiSecureStore](../src/InstanceManager/Services/DpapiSecureStore.cs) encrypts and decrypts cookies with Windows DPAPI.
- [ThemeService](../src/InstanceManager/Services/ThemeService.cs) and [ThemeCodec](../src/InstanceManager/Services/ThemeCodec.cs) apply palettes to the running app and turn a theme into a shareable code and back.
- [UpdateService](../src/InstanceManager/Services/UpdateService.cs) checks GitHub for a newer release, downloads and verifies it, and swaps the exe. See [Updates](#updates).
- [RobloxGraphicsService](../src/InstanceManager/Services/RobloxGraphicsService.cs) turns the graphics settings into FastFlags, writes them into each version's `ClientSettings\ClientAppSettings.json` and sets `FramerateCap` in `GlobalBasicSettings_13.xml`. `PrepareLaunch` does both right before every start.
- [RobloxGamesService](../src/InstanceManager/Services/RobloxGamesService.cs) loads the Games page, game names for favorites and automations, the public server list for server hops and the root place of a private server's game. `GamesViewModel` keeps the last 12 result lists for five minutes and up to 150 decoded thumbnails, so going back to a list needs no requests.
- [ScheduleClock](../src/InstanceManager/Services/ScheduleClock.cs) works out when an automation's start or close is due and when the next one happens.
- [TrayIcon](../src/InstanceManager/Services/TrayIcon.cs) wraps `Shell_NotifyIcon` and adds the icon again when Explorer restarts. [StartupService](../src/InstanceManager/Services/StartupService.cs) writes the `Instance Manager` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- [AntiAfkService](../src/InstanceManager/Services/AntiAfkService.cs) presses a key in every running client so idle accounts aren't kicked. See [Anti-AFK](#anti-afk).

## A launch, step by step

`ShellViewModel.LaunchAsync` hands off to `LaunchService.LaunchAsync`:

1. The launch panel turns the input into a [ServerTarget](../src/InstanceManager/Models/ServerTarget.cs): a public game (Place ID), a specific server (Place ID and Job ID), a private server (Place ID and link code) or the Roblox home screen when the field is empty. `ServerLinkResolver` checks server links first.
2. `MultiInstanceManager.TryApply` makes sure the singletons are held. If that fails, the user is told and the launch goes on anyway.
3. Then, for each account in order:
   - Pick the Roblox version: the account's pinned version if it is installed, otherwise the default one.
   - Decrypt the cookie with `DpapiSecureStore.TryUnprotect`. A cookie from another Windows user fails here and counts as one failed account.
   - Get a fresh authentication ticket from `RobloxAuthService`.
   - Let `RobloxGraphicsService.PrepareLaunch` write the FastFlags and FPS cap for that version.
   - Build the `roblox-player:` URI, start `RobloxPlayerBeta.exe`, hand the process to `InstanceTracker` and register it with `AutoReconnectService`.
   - Wait until the client has a window (up to 30 seconds), then the configured pause, then until its log reports the game join (up to 20 seconds). A client that exits before its window appears gets one more try, and accounts that are already running are skipped.
4. A `LaunchSummary` with started, failed and skipped counts becomes a notification.

Every start goes through one launch slot, including single launches, Auto Reconnect relaunches and schedules, so two clients never start at the same moment. Errors are caught and counted per account, so an expired cookie in the middle of a group doesn't stop the accounts after it.

## Auto Reconnect, step by step

For each instance launched into a game or server, `AutoReconnectService` keeps a small session: the account, target, version, current process, attempt count and whether it ever reached a game. An account opened on the Roblox home screen gets no session, because there is no game to bring it back to.

Signals come from the instance's Roblox log through its `RobloxLogWatcher`, and from the process exit through `InstanceTracker`. The service sorts a drop into one of three triggers:

- Kick, for a kick or removal (error 267, moderation messages).
- Error, for a disconnect, a server shutdown or a generic error dialog.
- Crash, when the process died in a game without a clean leave in the log.

A clean leave is the player leaving the game on purpose: the log shows `leaveUGCGameInternal` or a client-initiated disconnect (reason 285). The service then keeps the client open and ignores the disconnect errors Roblox logs while it tears the game down; only a kick that shows up afterwards still counts. The leave isn't remembered beyond that. If the process exits later, whether closed, crashed or ended in Task Manager, it is a Crash and reconnects like any other, and joining another game from the home screen clears the leave state.

A kick or error counts only after the log showed the client starting to join a game, so nothing during startup can pass for a drop. A drop reconnects only if its trigger is on. `AutoReconnectOnKickError` covers Kick and Error together and `AutoReconnectOnCrash` covers Crash, both under `AutoReconnectMaster`. A manual stop sets a flag that blocks any reconnect for that instance. When a reconnect is due, the service closes the stuck client if it is still open, waits briefly and calls `LaunchService.LaunchOneAsync` for the same account. Attempts per run are capped by `AutoReconnectMaxAttempts`, and each step goes to `auto-reconnect.log`. For ten seconds after a reconnect, drops of other accounts are not acted on, and a client that closes in that window is logged as `SKIP` and left closed.

### Why each instance needs its own log

Roblox writes one log file per client into `%LOCALAPPDATA%\Roblox\logs`, and the file name doesn't contain the process ID. Each watcher therefore picks the log whose timestamp is closest to its own launch time.

That alone breaks when several clients start close together, because a client can take a few seconds to create its log. In that gap a newer watcher would find only the older client's log and follow it too, so a kick on one account restarted the wrong account or none at all.

`LogSessionRegistry` prevents this. Each watcher claims its file in a shared registry and skips files another watcher owns. A watcher that finds only claimed logs tries again on its next poll and binds to its own log once it exists. The claim is released when the watcher is disposed, which happens on every reconnect before the new watcher starts, so the reconnected client can claim its fresh log.

## Multi-instance

A Windows mutex belongs to the thread that acquired it. To hold one for the app's whole lifetime, `MultiInstanceManager` gives each named object its own background thread (a `MutexSlot`). The thread opens or creates the mutex, acquires it, reports that it is ready and then waits on a stop event. When the app exits or the user turns the feature off, the stop event fires, each slot releases its mutex and its thread ends.

If the name exists but belongs to an object that isn't a mutex, opening it throws a specific error and the feature reports itself as unavailable. An `AbandonedMutexException` counts as a successful acquisition, because an abandoned mutex is free. `TryApply` wraps all of this so startup, the settings switch and a launch can ask for multi-instance without handling failures; `Apply` and `EnsureHeld` still throw for callers that want the details.

## Automation

`AutomationViewModel` owns the automations from `automations.json`. An automation is a list of account IDs, a join mode and a target, and an optional schedule: days, a start time that can be turned off, and a close rule (never, after a number of minutes, or at a time). Each automation has an `AutomationItemViewModel`. The account grid, group chips and favorites in the editor are built only for the selected automation. A new automation takes its accounts and target from an `AutomationSeed` that `ShellViewModel` fills from the account selection and the launch bar.

Every 15 seconds the view model asks `ScheduleClock.DueStop` and `DueStart` what is due. An occurrence counts for 10 minutes after its time, so a PC that wakes up a few minutes late still runs it, and `LastStartLocal` and `LastStopLocal` make sure each one happens only once. A close time that isn't after the start time belongs to the next day. "Close after" is due at `LastStartLocal` plus the minutes, so it survives a restart of the app, and closing by hand moves `LastStopLocal` past it. A start goes through `IShellCoordinator.LaunchAsync`, the same path as a click on Launch. While another launch is running, a scheduled start isn't marked as done, so it is tried again on the next tick. Closing goes through `IShellCoordinator.StopAccounts`. Running counts come from `InstanceTracker.IsRunning` and refresh while the page is open.

`AutomationRepository` converts the 1.1.2 preview format (separate templates and schedules) once when it loads it; see [DATA-STORAGE.md](DATA-STORAGE.md#migrations).

## Anti-AFK

`OperationsViewModel` holds the Anti-AFK switches and the avatar switch. It saves every change through `SettingsService` and restarts `AntiAfkService`, and `App` calls its `Start` and `Stop` at startup and exit. The service reads its settings when it restarts and does nothing while it is switched off. The message after Send now goes out through `MessagePosted`, which `ShellViewModel` turns into a notification with the `ToolMessage` ID.

- `AntiAfkService` waits for the interval, then goes through `InstanceTracker.RunningProcesses`. For each client it brings the main window to the front (with `AttachThreadInput` when Windows refuses the plain call), checks that the window really is in front and sends a key down and up with `keybd_event`, including the key's scan code and, for keys such as the arrows, the extended-key flag. Afterwards it gives focus back to the previous window if Switch back afterwards is on.
- [AntiAfkKeys](../src/InstanceManager/Models/AntiAfkKeys.cs) holds the rules for the key. `AppSettings.AntiAfkKey` is the name of a WPF `Key`, and `AntiAfkKeys` says which names are valid, refuses the keys that would act outside Roblox (Esc, the Windows keys, media, browser and launch keys), and turns a key into the label on the button ("Left Shift") and into its virtual key code. Choosing the key runs through `OperationsViewModel`: the Key button starts a capture, `ReconnectSettings` passes the next key from `PreviewKeyDown` to `CaptureAntiAfkKey`, and losing focus or Esc ends it.

## Updates

The app ships as one framework-dependent single-file exe, `Instance Manager.exe`. The project's assembly is still called `InstanceManager`, so `dotnet publish` writes `InstanceManager.exe`, and the release script gives it its shipped name. The native `WebView2Loader.dll` is bundled too, and the .NET host unpacks it to `%TEMP%\.net\<exe name>\` when the app starts. `UpdateService` works on the exe that is running, whatever its name, and only when it runs as a single-file bundle (an empty `Assembly.Location`), so builds from source never update themselves.

1. `CleanupPreviousUpdate` deletes `*.im-old` files and the `.im-update` folder that the previous update left in the app folder. See [Replacing a folder install](#replacing-a-folder-install) for the extra cleanup of old installs.
2. `CheckAsync` asks `api.github.com` for the latest release of `JeskoMts/Instance-Manager`, or for the 15 newest releases when pre-releases are included (drafts are ignored). The tag, for example `1.1.2`, is read as a version, and nothing happens unless it is newer than the running build. A GitHub error is reported as such instead of looking like "no update".
3. It looks for the asset `InstanceManager-<tag>.zip`. The parsed download URL must be exactly `https://github.com/JeskoMts/Instance-Manager/releases/download/<tag>/InstanceManager-<tag>.zip`, and the asset must have a `sha256:` digest in the API response.
4. The zip is downloaded with redirects followed by hand: at most five, HTTPS only, and only to `github.com`, `objects.githubusercontent.com` or `release-assets.githubusercontent.com`. The download is capped at 64 MB, and its SHA-256 must match the digest.
5. Every entry must be a plain file name with no folders, no invalid characters and no duplicates, and the unpacked size is capped. Only `Instance Manager.exe` is extracted, into `.im-update` inside the app folder.
6. `Apply` renames the running exe to `<name>.im-old` and moves the new one into its place under the same name, so shortcuts keep working. Windows allows renaming a running executable. If the move fails, the old exe is put back.

`UpdatesViewModel` decides when to call `Apply`. For the startup check, if no launch is running, no Roblox instance is tracked and no dialog is open, it applies the update right away, shuts down and starts the exe again with `--updated`, which shows a notification with the new version. Otherwise the update waits for Restart to update or for the app to close. Skipping a version deletes the staged exe, so nothing is installed on exit.

Before `Apply`, `KeepCurrentVersion` copies the running exe to `%LOCALAPPDATA%\Instance Manager\previous-version\Instance Manager <version>.exe`, replacing any older copy. Roll back stages that copy with `StageLocal`, applies it the same way and marks the version it left as skipped, so the timer doesn't install it again right away.

### Release assets

`scripts/build-release.ps1` publishes the exe and writes the two assets every release carries: `InstanceManager-<version>.zip` with `Instance Manager.exe` as its only entry, and `InstanceManager-Source-Code-<version>.zip`. In a git checkout the source zip holds the tracked files plus new files that aren't ignored; in an unpacked source archive it holds everything except build output. The same script is part of the source of every release since 1.0.0, so each one can be built again the same way.

### Replacing a folder install

Releases up to 1.1.1 were first published as a folder of files next to `InstanceManager.exe`. When the single-file exe finds an `InstanceManager.deps.json` beside it, it is sitting in such a folder, and `CleanupPreviousUpdate` deletes the files of the old version, including the old `InstanceManager.exe` unless that is the exe running. A file the old process still holds is renamed to `.im-old` instead and removed at the next start. The bundle never loads those loose files, so they can't get in the way before they are gone.

The 1.1.1 updater that shipped in the first 1.1.1 download expects a package with both `InstanceManager.exe` and `InstanceManager.dll` and ignores a one-file package. Those installs are updated once by hand; the rebuilt 1.1.1 accepts the one-file package.

## External services

All network calls go through one shared `HttpClient`: cookies off, automatic redirects off, decompression on, a 20-second timeout and the user agent `Roblox/WinInet`. Redirects are off so that server-link resolution and the update download can check every hop themselves. Requests to GitHub replace the user agent with `InstanceManager/<version>`.

| Endpoint | Purpose |
|---|---|
| `auth.roblox.com/v1/authentication-ticket/` | Trade the cookie for a one-time launch ticket (POST with CSRF handshake) |
| `users.roblox.com/v1/users/authenticated` | Confirm an account's identity when it is added, and Check sessions |
| `thumbnails.roblox.com/v1/users/avatar-headshot` | Avatar headshots |
| `games.roblox.com`, `apis.roblox.com`, `thumbnails.roblox.com/v1/games/...` | Game list, search and thumbnails for the Games page, game names for favorites and automations, the root place of a private server's game |
| `games.roblox.com/v1/games/<placeId>/servers/Public` | Public server list for moving accounts to another server |
| `apis.roblox.com/sharelinks/v1/resolve-link` | Look up a `roblox.com/share` link, sent with a saved login (POST with CSRF handshake) |
| `www.roblox.com` | Login page in the WebView2 window, server link resolution |
| `assetgame.roblox.com/game/PlaceLauncher.ashx` | Part of the launch URI, not fetched by the app |
| `api.github.com/repos/JeskoMts/Instance-Manager/releases/latest` | Update check |
| `api.github.com/repos/JeskoMts/Instance-Manager/releases?per_page=15` | Update check with pre-releases |
| `github.com/.../releases/download/...` and its CDN redirect | Update download |

The login dialog creates its own WebView2 environment with an InPrivate profile and disposes it on close. The WebView2 processes use most of the app's memory while the dialog is open and are released as soon as it closes.

## Threads

- The UI thread runs all view model updates and WPF binding.
- Each held singleton has its own background thread, parked on a stop event.
- Each `RobloxLogWatcher` runs a one-second timer; the classifier and the callback run on that timer thread.
- `Process.Exited` fires on a thread-pool thread.
- `SettingsService` saves on a debounced thread-pool timer.
- The automation and update timers are `DispatcherTimer`s on the UI thread.
- Anti-AFK runs as an async loop started from the UI thread. Its waits are asynchronous, so it doesn't block it.
- `TrayIcon` has a hidden message window on the UI thread. The single-instance activation event is waited on by the thread pool and handed to the UI thread.

Shared state is locked at the edges. `AutoReconnectService` keeps its sessions behind one lock and relaunches outside it, `LogSessionRegistry` locks its set of claimed paths, and `InstanceTracker` uses a concurrent dictionary. File and process work happens outside locks, and only bookkeeping is locked.

## Error handling

Recoverable errors are handled by the layer that owns them, so none of them ends the app:

- Persistence swallows `IOException`, `JsonException` and `UnauthorizedAccessException`, because saves can run on a timer thread where an unhandled exception would end the process.
- Launch failures are caught per account and counted.
- Multi-instance failures turn into a no-op through `TryApply`.
- Log watching tries a locked or missing log again on the next tick.
- The update check never crashes the app. No network or a GitHub rate limit shows "Couldn't reach GitHub" on the Updates page, and a bad package simply isn't installed.
- Writing FastFlags or the FPS cap skips files it can't open, so a locked Roblox folder never blocks a launch.
- A server link that can't be looked up, because Roblox can't be reached or accepts none of the saved logins, becomes a notification instead of an exception.

Most failures still leave a trace: a failed launch becomes a notification, a failed reconnect goes to `auto-reconnect.log`, and binding warnings go to a log file in Debug builds.

## Tests

The xUnit suite runs without a UI and reaches internal types through `InternalsVisibleTo`. Services that do I/O take their dependencies through constructors, so tests pass in fakes such as a stub `HttpMessageHandler`, a temporary folder or a fake settings service.

- `AutoReconnectService` takes a watcher factory, so tests can point watchers at a temporary folder.
- `UpdateService` takes the exe path, the current version and whether it runs as a single file, so the whole update path, including redirects, digest checks, renamed exes and rollback, runs against a temporary folder.
- `AutomationViewModel` takes a clock, a running check and a seed, so schedule tests move time forward without waiting. `AutomationRepositoryTests` convert a preview-format file in a temporary folder.
- `RobloxGraphicsService` and `StartupService` take a Roblox folder and a registry key, which tests point at a temporary folder and a throwaway `HKCU\Software\InstanceManager.Tests.<guid>` key that is deleted afterwards.
- `AntiAfkKeyTests` check which key names are valid or refused, how keys are shown and which virtual key code and extended flag each one gets. `AntiAfkKeyCaptureTests` drive the key capture through the view model and through the real Reconnect settings page, which is hosted in an `HwndSource` so that closing it doesn't end the shared `Application`.
- `RemovedFeaturesTests` load a settings file from a 1.1.2 preview with the fields of removed features, check that they are gone after the next save, check that the leftover files are deleted, and check that no keyboard shortcut and no account info member is left in the source.
- `ServerLinkResolverTests` answer the way Roblox does through a stub handler: share links that only redirect to a page, the CSRF handshake of the share link API and logins it no longer accepts.
- `SecurityHardeningTests` cover the exact Roblox signer name, the response size limits (with a stream that counts how much was read), the characters removed from log lines and the denied WebView2 permissions.
- Contract tests read the XAML directly to check that labels and bindings stay in place, for example that every `Run.Text` binding is one-way.

## Where to extend

- Persistence: the repositories sit behind interfaces such as `IAccountRepository`. Another store would replace `JsonFileStore` and be registered in composition.
- Join modes: `JoinMode`, `ServerTarget` and the switch in `RobloxLauncher.BuildPlaceLauncherUrl` are the only places that produce launch URLs.
- Dialogs: all prompts go through `IDialogService`.
- Auto Reconnect signals: new markers go into `RobloxLogClassifier`, new trigger rules into `AppSettings.IsAutoReconnectEnabledFor` and `ReconnectSettings`.
- Settings: a new category needs a `SettingsCategory` value, an entry in `SettingsCatalog` with its search words, a view under `Views/Settings/` and a line in `SettingsCategoryHost.Create`. A setting in an existing category only needs its row in that view and a few search words in the catalog.
- Themes: built-in palettes are defined in `BuiltInThemes`, your own themes are stored in `themes.json`, and sharing runs through `ThemeCodec`.
- FastFlags: a new graphics option needs a setting in `AppSettings`, a line in `RobloxGraphicsService.BuildPlan` and its flag names in `RobloxGraphicsService.Allowlist`.

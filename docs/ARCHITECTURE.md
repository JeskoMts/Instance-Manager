# Architecture

Instance Manager is a single WPF desktop app on .NET 8, built for x64 only, with a conventional MVVM layout. There is no server, database or Windows service. State lives in the running process and in a few JSON files under `%APPDATA%\Instance Manager`. The app talks to Roblox's HTTPS APIs, to GitHub for updates, and to the Roblox clients it starts on the same PC.

This document describes how the parts fit together and walks through the three operations that matter most: launching, reconnecting and updating.

## Design goals

- Local and private. No accounts of its own, no telemetry, no cloud.
- Hard to crash. A locked file, an expired cookie or a Roblox singleton held by someone else should degrade one feature, not take the app down.
- Testable without a UI. The logic sits in services and view models that unit tests call directly. The test suite never opens a window.
- One user, one dataset. Almost everything is a process-wide singleton that keeps its working copy in memory.

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
                    |  AutoReconnectSvc   |   +-----------+-----------+
                    |  RobloxLogWatcher   |               |
                    |  VersionService     |        +------v-------+
                    |  RobloxAvatarSvc    |        | JsonFileStore|
                    |  ServerLinkResolver |        |  (atomic)    |
                    |  DpapiSecureStore   |        +------+-------+
                    |  ThemeService       |               |
                    |  UpdateService      |        +------v----------------+
                    +------+--------------+        | %APPDATA%\Instance    |
                           |                       | Manager\*.json, *.log |
                    +------v---------------+       +-----------------------+
                    | Roblox HTTPS APIs    |
                    | GitHub releases API  |
                    +------+---------------+
                           |
                    +------v---------------+
                    | RobloxPlayerBeta.exe |  one process per account
                    +----------------------+
```

## Layers

Views are XAML with little code-behind. `MainWindow` holds the drag-and-drop logic for account lists and themes, and the dialogs (login, confirmations, editors) live under `Views/`. Views bind to view models and don't know about services or file paths.

The view models use CommunityToolkit.Mvvm. `ShellViewModel` owns the others (`AccountListViewModel`, `LaunchPanelViewModel`, `VersionBarViewModel`, `SettingsViewModel`, `ThemeViewModel`, `NotificationCenterViewModel`), runs launches through `LaunchAsync`, and implements `IShellCoordinator` so child view models can post status lines and notifications. View models depend only on repository and service interfaces.

Services hold the domain logic and all I/O: login, launching, multi-instance, Auto Reconnect, version discovery, avatars, link validation, encryption, themes and updates.

Storage is four repositories plus a settings service. Each persists one JSON file through `JsonFileStore` and keeps its list in memory as the source of truth at runtime.

Models are plain data classes. The only behavior in them is `Account.NormalizeGroupMemberships` and `AppSettings.Normalize`, both small migrations.

## Composition and lifecycle

[ServiceCollectionExtensions](../src/InstanceManager/Composition/ServiceCollectionExtensions.cs) registers every service and repository as a singleton, together with one shared `HttpClient`.

[App.OnStartup](../src/InstanceManager/App.xaml.cs) then:

1. Builds the `ServiceProvider`.
2. Applies the saved theme before any window exists, so the default palette never flashes.
3. Takes the Roblox singletons if multi-instance is enabled, best effort.
4. Resolves `ShellViewModel` and `MainWindow`, hooks `MainWindow.Loaded` to `ShellViewModel.InitializeAsync` and shows the window.
5. Deletes leftovers of the previous update and, in Release builds, starts the update check in the background.

In Debug builds WPF binding warnings go to `binding-errors.log`.

[App.OnExit](../src/InstanceManager/App.xaml.cs) disposes the provider, which releases the singletons and tears down tracked processes. If an update is waiting, it is installed at this point, and if the update asked for a restart, the new executable is started last.

## Services

- [RobloxAuthService](../src/InstanceManager/Services/RobloxAuthService.cs) trades a `.ROBLOSECURITY` cookie for a one-time authentication ticket and reads the signed-in user. It handles the CSRF handshake (a 403 carrying an `x-csrf-token` header, retried with that token) and backs off on HTTP 429 using `Retry-After`, clamped to 1 to 30 seconds, for up to five attempts.
- [RobloxLauncher](../src/InstanceManager/Services/RobloxLauncher.cs) builds the `PlaceLauncher` URL and the `roblox-player:` URI and starts `RobloxPlayerBeta.exe` with that URI as its only argument. URL building is static and side-effect free, which keeps it easy to test.
- [MultiInstanceManager](../src/InstanceManager/Services/MultiInstanceManager.cs) holds the Roblox singleton objects. See below.
- [InstanceTracker](../src/InstanceManager/Services/InstanceTracker.cs) maps each account to its Roblox process, raises `RunningChanged` on start and exit, and stops one instance or all of them. It listens to `Process.Exited` so it notices clients that close on their own.
- [AutoReconnectService](../src/InstanceManager/Services/AutoReconnectService.cs) decides whether a dropped instance should come back. It listens to `InstanceTracker` for exits and to one `RobloxLogWatcher` per instance for in-game events, and hands the relaunch back to `LaunchService`.
- [RobloxLogWatcher](../src/InstanceManager/Services/RobloxLogWatcher.cs) tails one Roblox client log and reports the lines that matter. [LogSessionRegistry](../src/InstanceManager/Services/LogSessionRegistry.cs) makes sure no two watchers follow the same file.
- [RobloxLogClassifier](../src/InstanceManager/Services/RobloxLogClassifier.cs) turns a log line into a signal (joined, kicked, error, left) by matching known markers. Matching ignores case, and the most specific marker wins.
- [LaunchService](../src/InstanceManager/Services/LaunchService.cs) runs a launch across many accounts and provides the single-account relaunch that Auto Reconnect uses.
- [VersionService](../src/InstanceManager/Services/VersionService.cs) finds installed Roblox versions. [RobloxExecutableValidator](../src/InstanceManager/Services/RobloxExecutableValidator.cs) checks every `RobloxPlayerBeta.exe` during discovery and again right before launch (see [SECURITY.md](SECURITY.md#launching-roblox)).
- [RobloxAvatarService](../src/InstanceManager/Services/RobloxAvatarService.cs) loads avatar headshots, caches them on disk and merges concurrent requests for the same user.
- [ServerLinkResolver](../src/InstanceManager/Services/ServerLinkResolver.cs) and [GameLinkParser](../src/InstanceManager/Services/GameLinkParser.cs) validate what users type: Place IDs, Job IDs and server links.
- [DpapiSecureStore](../src/InstanceManager/Services/DpapiSecureStore.cs) encrypts and decrypts cookies with Windows DPAPI.
- [ThemeService](../src/InstanceManager/Services/ThemeService.cs) and [ThemeCodec](../src/InstanceManager/Services/ThemeCodec.cs) apply palettes to the running app and turn a theme into a shareable code and back.
- [UpdateService](../src/InstanceManager/Services/UpdateService.cs) checks GitHub for a newer release, downloads and verifies it, and swaps the app's files. See below.

## A launch, step by step

`ShellViewModel.LaunchAsync` hands off to `LaunchService.LaunchAsync`:

1. The launch panel turns the input into a [ServerTarget](../src/InstanceManager/Models/ServerTarget.cs): a public game (Place ID), a specific server (Place ID and Job ID) or the Roblox home screen when the field is empty. `ServerLinkResolver` checks server links first.
2. `MultiInstanceManager.TryApply` makes sure the singletons are held. If that fails, the user is told and the launch continues anyway.
3. Then, for each account in order:
   - Pick the Roblox version: the account's pinned version if it is installed, otherwise the global one.
   - Decrypt the cookie with `DpapiSecureStore.TryUnprotect`. A cookie from another Windows user fails here and counts as one failed account.
   - Get a fresh authentication ticket from `RobloxAuthService`.
   - Build the `roblox-player:` URI and start `RobloxPlayerBeta.exe`, then hand the process to `InstanceTracker` and register it with `AutoReconnectService`.
   - Wait until the client has a window (up to 30 s), then the configured pause, then until its log reports the game join (up to 20 s). A client that exits before its window appears is retried once, and accounts that are already running are skipped.
4. A `LaunchSummary` with started, failed and skipped counts becomes a notification.

Every start goes through one launch slot, including single launches and Auto Reconnect relaunches, so two clients never start at the same moment. Errors are caught per account and counted, so an expired cookie in the middle of a group doesn't stop the accounts after it.

## Auto Reconnect, step by step

For each launched instance, `AutoReconnectService` keeps a small session: the account, target, version, current process, attempt count and whether it ever reached a game.

Signals arrive from the instance's Roblox log, through its `RobloxLogWatcher`, and from the process exit, through `InstanceTracker.RunningChanged`. The service sorts a drop into one of three triggers:

- Kick, for a kick or removal (error 267, moderation messages).
- Error, for a disconnect, a server shutdown or a generic error dialog.
- Crash, when the process died in a game without a clean leave in the log.

A clean leave is the player leaving the game on purpose: the log shows `leaveUGCGameInternal` or a client-initiated disconnect (reason 285). The service then keeps the client open, ignores the disconnect errors Roblox logs while it tears down the game, and doesn't reconnect when the window is closed later. Only a kick that shows up afterwards still counts.

A drop reconnects only if its trigger is enabled. `AutoReconnectOnKickError` covers Kick and Error together and `AutoReconnectOnCrash` covers Crash, both under `AutoReconnectMaster`. A manual stop sets a flag that blocks any reconnect for that instance. When a reconnect is due, the service closes the stuck client if it is still open, waits briefly and calls `LaunchService.LaunchOneAsync` for the same account. Attempts per run are capped by `AutoReconnectMaxAttempts`, and each step is written to `auto-reconnect.log`.

### Why each instance needs its own log

Roblox writes one log file per client into `%LOCALAPPDATA%\Roblox\logs`, and the file name doesn't contain the process ID. The watcher therefore picks the log whose timestamp is closest to its own launch time.

That alone breaks when several clients start close together, because a client can take a few seconds to create its log. In that gap, a newer watcher would find only the older client's log and follow it too, so a kick on one account restarted the wrong account or none at all.

`LogSessionRegistry` fixes this. Each watcher claims its file in a shared registry and skips files another watcher already owns. A watcher that finds only claimed logs retries on its next poll and binds to its own log once it exists. The claim is released when the watcher is disposed, which happens on every reconnect before the new watcher starts, so the reconnected client can claim its fresh log.

## Multi-instance

A Windows mutex belongs to the thread that acquired it. To hold one for the app's whole lifetime, `MultiInstanceManager` gives each named object its own background thread (a `MutexSlot`). The thread opens or creates the mutex, acquires it, reports that it is ready and then waits on a stop event. When the app exits or the user turns the feature off, the stop event fires, each slot releases its mutex and its thread ends.

If the name exists but belongs to an object that isn't a mutex, opening it throws a specific error and the feature reports as unavailable. An `AbandonedMutexException` counts as a successful acquisition, because an abandoned mutex is free. `TryApply` wraps all of this so startup, the settings switch and a launch can ask for multi-instance without handling failures; `Apply` and `EnsureHeld` still throw for callers that want the details.

## Updates

Since 1.1.2 the app ships as one framework-dependent single-file exe. The native `WebView2Loader.dll` is bundled too; the .NET host extracts it to `%TEMP%\.net\<exe name>\` when the app starts. `UpdateService` works on the exe that is running, whatever its name, and only when it runs as a single-file bundle (an empty `Assembly.Location`), so builds from source never update themselves.

1. `CleanupPreviousUpdate` deletes `*.im-old` files and the `.im-update` folder left in the app folder by the previous update. If the running exe is called `InstanceManager.exe` and an `InstanceManager.deps.json` sits next to it, the folder is an old 1.1.1 install that the 1.1.1 updater has just upgraded, and the loose files of that version are deleted as well (or renamed to `.im-old` while the old process still holds them).
2. `DownloadAsync` asks `api.github.com` for the latest release of `JeskoMts/Instance-Manager`. The tag (for example `1.1.2`) is parsed as a version, and nothing happens unless it is newer than the running build. Drafts and pre-releases never show up in this endpoint.
3. It looks for `Instance.Manager.<tag>.zip` and falls back to `InstanceManager-<tag>.zip`. The download URL must start with `https://github.com/JeskoMts/Instance-Manager/releases/download/`, and the asset must have a `sha256:` digest in the API response. If the first asset exists but fails these checks, there is no update and no fallback.
4. The zip is downloaded with redirects followed by hand: at most five, HTTPS only, and only to `github.com`, `objects.githubusercontent.com` or `release-assets.githubusercontent.com`. The download is capped at 64 MB, and its SHA-256 must match the digest.
5. Every entry must be a plain file name with no folders, no invalid characters and no duplicates, and the unpacked size is capped. Only the exe entry is extracted (`Instance Manager.exe`, or `InstanceManager.exe` in the fallback package) into `.im-update` inside the app folder.
6. `Apply` renames the running exe to `<name>.im-old` and moves the new one into its place under the same name, so shortcuts keep working. Windows allows renaming a running executable. If the move fails, the old exe is put back.

`App` decides when to call `Apply`. If no launch is running, no Roblox instance is tracked and no dialog is open, it applies the update right away, shuts down and starts the exe again with `--updated`, which shows a notification with the new version. Otherwise it waits and applies the update in `OnExit`.

### Upgrading from 1.1.1

The 1.1.1 updater only accepts `InstanceManager-<tag>.zip` with both `InstanceManager.exe` and `InstanceManager.dll` inside, copies every file of the zip over the install and restarts `InstanceManager.exe`. `scripts/build-release.ps1` therefore builds that package with the single-file exe as `InstanceManager.exe` and the matching `InstanceManager.dll`. After the restart the bundle ignores the loose files next to it, and step 1 above removes them. Every release has to carry this package for as long as 1.1.1 installs should keep updating.

## External services

All network calls go through one shared `HttpClient`: cookies off, automatic redirects off, decompression on, a 20-second timeout and the user agent `Roblox/WinInet`. Redirects are off so that server-link resolution and the update download can check every hop themselves. Requests to GitHub replace the user agent with `InstanceManager/<version>`.

| Endpoint | Purpose |
|---|---|
| `auth.roblox.com/v1/authentication-ticket/` | Trade the cookie for a one-time launch ticket (POST, CSRF) |
| `users.roblox.com/v1/users/authenticated` | Confirm an account's identity when it is added |
| `thumbnails.roblox.com/v1/users/avatar-headshot` | Avatar headshots |
| `games.roblox.com`, `apis.roblox.com`, `thumbnails.roblox.com/v1/games/...` | Game list, search and thumbnails for the Games tab |
| `www.roblox.com` | Login page in the WebView2 window, server link resolution |
| `assetgame.roblox.com/game/PlaceLauncher.ashx` | Embedded in the launch URI, not fetched by the app |
| `api.github.com/repos/JeskoMts/Instance-Manager/releases/latest` | Update check |
| `github.com/.../releases/download/...` and its CDN redirect | Update download |

The login dialog creates its own WebView2 environment with an InPrivate profile and disposes it on close. The WebView2 processes use most of the app's memory while the dialog is open and are released as soon as it closes.

## Threads

- The UI thread runs all view model updates and WPF binding.
- Each held singleton has its own background thread, parked on a stop event.
- Each `RobloxLogWatcher` runs a one-second timer; the classifier and the callback run on that timer thread.
- `Process.Exited` fires on a thread-pool thread.
- `SettingsService` saves on a debounced thread-pool timer.

Shared state is locked at the edges. `AutoReconnectService` keeps its sessions behind one lock and relaunches outside it, `LogSessionRegistry` locks its set of claimed paths, and `InstanceTracker` uses a concurrent dictionary. File and process work happens outside locks, and only bookkeeping is locked.

## Error handling

Recoverable errors are handled by the layer that owns them rather than crashing the app:

- Persistence swallows `IOException`, `JsonException` and `UnauthorizedAccessException`, because saves can run on a timer thread where an unhandled exception would end the process.
- Launch failures are caught per account and counted.
- Multi-instance failures turn into a no-op through `TryApply`.
- Log watching retries a locked or missing log on the next tick.
- The update check swallows every failure. No network, a GitHub rate limit or a bad package just means no update this time.

Silent failures still leave a trace somewhere: a failed launch becomes a notification, a failed reconnect goes to `auto-reconnect.log`, and binding warnings go to a log file in Debug builds.

## Tests

The xUnit suite runs without a UI and reaches internal types through `InternalsVisibleTo`. Services that do I/O take their dependencies through constructors, so tests pass in fakes such as a stub `HttpMessageHandler`, a temporary folder or a fake settings service. `AutoReconnectService` takes a watcher factory so tests can point watchers at a temporary folder. `UpdateService` takes the app folder and current version, so the full update path, including redirects, digest checks and rollback, runs against a temporary folder. A few tests read the XAML directly to check that labels and bindings stay in place.

## Where to extend

- Persistence: the repositories sit behind interfaces (`IAccountRepository` and the others). Another store would replace `JsonFileStore` and be registered in composition.
- Join modes: `JoinMode`, `ServerTarget` and the switch in `RobloxLauncher.BuildPlaceLauncherUrl` are the only places that produce launch URLs.
- Dialogs: all prompts go through `IDialogService`.
- Auto Reconnect signals: new markers go into `RobloxLogClassifier`, new trigger rules into `AppSettings.IsAutoReconnectEnabledFor` and the settings page.
- Themes: built-in palettes are defined in `BuiltInThemes`, user themes are stored in `themes.json`, and sharing runs through `ThemeCodec`.

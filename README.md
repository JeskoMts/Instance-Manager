# Instance Manager

Instance Manager is a local Windows app for people who run more than one Roblox account at a time. You add your accounts once, organize them however you like, and launch several of them into the same game or the same private server with a couple of clicks. Each account runs as its own Roblox window.

Everything stays on your machine. There is no server to sign in to, no account system of its own, and no telemetry. Sign-in happens through the real Roblox login page inside an embedded browser, and the session cookie that comes back is encrypted with Windows DPAPI before it ever touches the disk. The app never sees your Roblox password, and it never sends your data anywhere except to Roblox's own endpoints.

## Features

A quick map of what the app does and how to use each part. The full tour, with every option explained, is in [docs/FEATURES.md](docs/FEATURES.md).

| Feature | Details | How to use |
|---|---|---|
| Accounts | Store any number of Roblox accounts, each with its identity, an optional alias and notes, and an encrypted session cookie. Avatars load on demand. | Accounts tab, click Add Account, sign in to the real Roblox login. Re-adding an existing account refreshes its cookie. |
| Groups | Colored, collapsible groups that can be launched as a unit. An account can belong to more than one group. | Click Create Group, then drag account rows onto a group header. Launch from the group header. |
| Favorites | Saved games with a name, a place id, and an optional default private server. A primary favorite is pinned to the top and restored on startup. | Enter a target, use the save action, name it. Pick it later from the favorites dropdown. |
| Launch targets | Join a public game by link or place id, or a private server by full server link or job id. Server links are validated and kept on Roblox domains. | Type a game link or id in the launch box. Switch to Job ID mode for a private server. |
| Games tab | Browse a grid of popular games and turn one into your launch target with a click. | Open the Games tab and click a game. Optionally have it jump to Accounts. |
| Multi-instance launch | Run many accounts in parallel, each as its own client, spaced out by a delay you set. One bad account never stops the rest. | Select accounts or a group, click Launch. Set the launch delay in Settings. |
| Instance control | See which accounts are live and stop them one at a time or all at once. | Use the stop action on a running row, or Stop All. |
| Auto Reconnect | Bring an instance back after a kick, error, disconnect, or crash, up to a retry limit, each instance on its own log. | On by default. Tune it on the Settings tab under Auto Reconnect. A manual stop never reconnects. |
| Roblox versions | Pick a global client version, check the latest online, or pin a per-account version. | Choose a version in the version bar, or per account on its row. Set a custom folder in Settings. |
| Themes | Built-in color schemes plus an editor, shareable as a short text code, applied live. | Open the Themes tab to switch, edit, import, or export a theme. |
| Notifications | On-screen toasts with a duration you set and per-message muting. | Settings tab, Notifications section. |
| Confirmations | Skip confirmation prompts for actions you do often. | Settings tab, Confirmations section. |
| Undo | Removing an account or deleting a group or favorite can be undone. | Click Undo on the toast right after the action. |

## How multi-instance works

Roblox normally allows only one running client per machine. It enforces this with two named singleton objects, `ROBLOX_singletonEvent` (current) and `ROBLOX_singletonMutex` (legacy). Instance Manager holds both of those open for as long as it is running, each on its own background thread. While the app holds them, a second, third, or fourth Roblox launch no longer replaces the first one.

This feature is optional and best-effort. If something else already owns one of those names in a way the app cannot take over (Roblox is already running, for example), the grip simply is not established. Startup, the settings toggle, and a launch all continue to work in that case; you just do not get the parallel-instance behavior until the name is free again.

## Requirements

- Windows 10 or Windows 11, 64-bit. The app targets `net8.0-windows` and is built x64-only.
- The .NET 8 Desktop Runtime to run a published build, or the .NET 8 SDK to build from source.
- The Microsoft Edge WebView2 Runtime, which powers the Roblox login window. Most up-to-date Windows installs already have it. If it is missing, the Add Account dialog shows a download link.
- At least one officially signed Roblox client (`RobloxPlayerBeta.exe`). By default the app looks under `%LOCALAPPDATA%\Roblox\Versions`. A custom folder must be on a local fixed drive; network/UNC paths and unsigned clients are rejected.

No administrator rights are needed. The app manifest requests `asInvoker`, so it runs as your normal user.

## Building from source

Restore and build with the .NET SDK:

```bash
dotnet restore InstanceManager.sln
dotnet build InstanceManager.sln --configuration Release
```

Run it straight from source while developing:

```bash
dotnet run --project src/InstanceManager/InstanceManager.csproj
```

A Release build produces `InstanceManager.exe` under `src/InstanceManager/bin/Release/net8.0-windows/`.

## First run

1. Open the app and go to the Accounts tab.
2. Click Add Account. A window opens with the real Roblox login page. Sign in there as you normally would.
3. After a successful sign-in, the app reads the `.ROBLOSECURITY` cookie, checks it against Roblox to confirm the identity, stores the account encrypted, and clears the login browser session.
4. Repeat for each account you want to manage.
5. Pick a target (a favorite, a place link or id, or a private server), select the accounts or a whole group, and click Launch.

## Settings reference

All settings live on the Settings tab and are written to `settings.json` in the data directory. You never have to edit that file by hand. The values that matter most:

| Setting | What it does | Default |
|---|---|---|
| `MultiInstanceEnabled` | Holds the Roblox singleton objects so instances run in parallel | `true` |
| `LaunchDelayMs` | Pause between consecutive launches, 0 to 60000 ms in 500 ms steps | `1500` |
| `SelectedVersionGuid` | The globally selected Roblox version | none |
| `VersionsPathOverride` | An alternate local fixed-drive path containing Authenticode-signed Roblox versions | none |
| `CheckLatestVersionOnline` | Ask Roblox for the latest client version | `true` |
| `AutoReconnectMaster` | Master switch for the whole Auto Reconnect feature | `true` |
| `AutoReconnectOnKickError` | Reconnect after a kick, removal, disconnect, or error dialog | `true` |
| `AutoReconnectOnCrash` | Reconnect after the client process crashes while in a game | `true` |
| `AutoReconnectMaxAttempts` | How many automatic reconnects per run before giving up, 1 to 20 | `3` |
| `ThemeId` | The active color scheme | `dark` |
| `ToastDurationMs` | How long a notification stays on screen, 500 to 5000 ms | `4500` |
| `ConfirmBypass*` / `NotifyMute*` | Skip confirmation prompts or mute specific notifications | `false` |

The data directory is `%APPDATA%\Instance Manager`. See [docs/DATA-STORAGE.md](docs/DATA-STORAGE.md) for the full list of files and what is in them.

## Auto Reconnect

Auto Reconnect watches every instance you launch and brings it back when it drops out of its game. It reacts to three kinds of event, grouped behind two switches plus a master switch:

- **Reconnect after Kick/Error.** Covers being kicked or removed (Roblox error 267 and moderation messages), plus disconnects, lost connections, servers that shut down or go offline, generic error dialogs, and being dropped back to the Roblox menu.
- **Reconnect after Instance Crash.** Covers the client process dying while you were in a game, with no clean exit.

When a drop is detected, the app closes the stuck client if it is still hanging on a dialog, waits a moment, and relaunches the same account into the same target and version. Each instance gets up to `AutoReconnectMaxAttempts` tries per run before the app gives up on it. Stopping an instance yourself always wins: a manual stop never triggers a reconnect.

How the app knows what happened: it tails the per-instance Roblox client log in `%LOCALAPPDATA%\Roblox\logs` and classifies the lines it sees. Each running instance is bound to its own log file, so when you run several accounts at once, a kick on one of them only reconnects that one and leaves the others alone. Every attempt, success, failure, and give-up is written to `auto-reconnect.log` in the data directory if you ever want to see what happened.

The two switches and the master switch are all on by default, so Auto Reconnect works out of the box. Turn the master off to disable everything at once.

## Project layout

```
src/InstanceManager/
  App.xaml(.cs)         WPF entry point: builds the DI container, applies the theme, holds the singleton
  MainWindow.xaml(.cs)  The main window and its drag-and-drop logic
  Composition/          Dependency-injection registration
  Models/               Plain data types (Account, AccountGroup, FavoriteGame, AppSettings, ...)
  Services/             Domain logic and I/O (auth, launcher, multi-instance, auto-reconnect, versions, crypto)
  Storage/              Repositories built on JsonFileStore plus SettingsService
  ViewModels/           The MVVM layer, coordinated by ShellViewModel
  Views/                Dialogs (login, confirmations, editors)
  Behaviors/ Converters/ Themes/   WPF helpers and XAML resources
tests/InstanceManager.Tests/        The xUnit test suite
.github/workflows/ci.yml            Build and test on windows-latest
```

The full feature tour is in [docs/FEATURES.md](docs/FEATURES.md). The deeper design is documented in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), the data model in [docs/DATA-STORAGE.md](docs/DATA-STORAGE.md), and the security model in [docs/SECURITY.md](docs/SECURITY.md). When something goes wrong, [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) walks through the common causes and fixes.

## Building and testing

The tests use xUnit and live in `tests/InstanceManager.Tests/`. The main project exposes its internals to the test assembly through `InternalsVisibleTo`, so service-level logic can be tested directly.

```bash
dotnet test InstanceManager.sln
```

Continuous integration performs locked/audited restore, vulnerable-package reporting, a Release build, and the complete test suite. See [.github/workflows/ci.yml](.github/workflows/ci.yml).

## Troubleshooting

The most frequent issues are below. For the full guide, with version, multi-instance, Auto Reconnect, theme, and data problems all covered, see [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

**A launched account immediately fails.** The most common cause is an expired cookie. Remove the account and add it again. A failed account does not block the others in the same run; the rest still launch.

**Multi-instance is not working.** Something else may already hold the Roblox singleton. Close any running Roblox client and any older helper processes, then try again. The app reestablishes the grip on the next launch or settings change.

**The login window is blank or will not open.** Install or repair the WebView2 Runtime, then reopen the Add Account dialog.

**A cookie cannot be decrypted.** DPAPI ties the encrypted cookie to your Windows user on this machine. If you copied `accounts.json` from another user or another PC, those cookies cannot be read here and the affected accounts will fail at launch. Add them again on this machine.

## Privacy and security in short

Your accounts and their cookies never leave your machine except as normal Roblox traffic. The cookie is stored only in DPAPI-encrypted form, which is bound to your Windows user. Outbound connections go to Roblox endpoints only, all over HTTPS. The full model, including the parts with deliberate limits, is in [docs/SECURITY.md](docs/SECURITY.md).

## License

Proprietary. All rights reserved. The source is published for inspection only. Use, reproduction, modification, and distribution are not permitted without prior written permission.

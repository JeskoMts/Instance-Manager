# Instance Manager

Instance Manager is a Windows app for running several Roblox accounts at the same time. You add each account once, sort them into groups, and start as many as you want into the same game or private server. Every account gets its own Roblox window.

The app runs entirely on your PC. It has no account system of its own, no server and no telemetry. You sign in through the real Roblox login page, the app never sees your password, and the session cookie it keeps is encrypted with Windows DPAPI before it is written to disk.

## What it does

- Keeps any number of Roblox accounts, each with an optional nickname and notes.
- Sorts accounts into colored groups that you can start with one click. An account can be in several groups.
- Starts the selected accounts one after another, each in its own Roblox window, into a public game, a specific server or a private server.
- Saves the games you play most as favorites and shows a grid of popular games to pick from.
- Brings an account back automatically when it gets kicked, disconnected or crashes (Auto Reconnect).
- Lets you choose which installed Roblox version to use, globally or per account.
- Comes with several color themes and a theme editor. Themes can be shared as a short code.
- Updates itself. On every start it checks GitHub for a new release and installs it within a few seconds.

[docs/FEATURES.md](docs/FEATURES.md) explains each of these in more detail.

## Download

1. Download `InstanceManager-<version>.zip` from the [latest release](https://github.com/JeskoMts/Instance-Manager/releases/latest).
2. Unzip it into its own folder, for example `Documents\Instance Manager`.
3. Run `InstanceManager.exe`.

Give the app a folder of its own that your Windows user can write to. The updater replaces the files in that folder, so it can't work from a read-only location such as `C:\Program Files`.

## Requirements

- Windows 10 or 11, 64-bit.
- The [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64). Building from source needs the .NET 8 SDK instead.
- The Microsoft Edge WebView2 Runtime, which the login window uses. Windows 11 ships with it. If it's missing, the Add account window shows a download link.
- Roblox. The app launches the official, signed `RobloxPlayerBeta.exe` from `%LOCALAPPDATA%\Roblox\Versions` or from a folder you choose in Settings.

The app runs as your normal Windows user and never asks for administrator rights.

## First run

1. Open the Accounts tab and click Add account.
2. Sign in on the Roblox login page that opens. The window closes by itself once the login is saved.
3. Repeat for every account you want to use.
4. Type a game link or Place ID into the launch bar, or pick a favorite or a game from the Games tab.
5. Select accounts (click the rows or press Ctrl+A) and click Launch.

## Updates

Every time the app starts, it asks GitHub for the latest release. If there is a newer version, the app downloads it, checks its SHA-256 checksum against the one GitHub publishes, swaps the files and restarts. This usually takes a second or two and happens before you've done anything. If you are already launching accounts or have Roblox instances running, the update waits and installs when you close the app.

## Running several clients at once

Roblox normally allows only one client per PC. It enforces this with two named system objects, `ROBLOX_singletonEvent` and the older `ROBLOX_singletonMutex`. Instance Manager holds both of them for as long as it runs, so a second or third Roblox window no longer closes the first one.

If another program already holds one of those names, usually a Roblox client that was open before Instance Manager started, the app can't take it over. Launching still works in that case, but only one client at a time. Close every Roblox window and launch again.

## Auto Reconnect

Auto Reconnect watches each Roblox window the app started. When an account is kicked, loses its connection, lands back on the Roblox menu or the client crashes, the app starts that account again into the same game with the same Roblox version. It reads the Roblox log of each client to tell what happened, so a kick on one account never restarts the others.

It is on by default and gives up after 3 attempts per account; you can change the limit or turn it off in Settings. An instance you stop yourself is never restarted. Every attempt is written to `auto-reconnect.log` in `%APPDATA%\Instance Manager`.

## Building from source

```bash
dotnet restore InstanceManager.sln
dotnet build InstanceManager.sln --configuration Release
dotnet run --project src/InstanceManager/InstanceManager.csproj
```

The release folder is built with:

```bash
dotnet publish src/InstanceManager/InstanceManager.csproj -c Release -r win-x64 --self-contained false -o publish
```

The updater only runs in Release builds, so running a Debug build from source never replaces your build output.

## Tests

```bash
dotnet test InstanceManager.sln
```

The xUnit tests in `tests/InstanceManager.Tests/` run without opening a window. CI restores with locked package versions, reports vulnerable packages, scans for leaked secrets, builds Release and runs the tests; see [.github/workflows/ci.yml](.github/workflows/ci.yml).

## Project layout

```
src/InstanceManager/
  App.xaml(.cs)         Startup, dependency injection, theme, update check
  MainWindow.xaml(.cs)  Main window and drag and drop
  Composition/          Service registration
  Models/               Plain data types (Account, AccountGroup, AppSettings, ...)
  Services/             Login, launching, multi-instance, Auto Reconnect, updates, encryption
  Storage/              JSON repositories and the settings service
  ViewModels/           MVVM layer, coordinated by ShellViewModel
  Views/                Dialogs (login, confirmations, editors)
  Controls/             Vector icon control
  Behaviors/ Converters/ Themes/   WPF helpers and styles
tests/InstanceManager.Tests/        xUnit tests
```

## Documentation

- [FEATURES.md](docs/FEATURES.md): what each part of the app does and how to use it
- [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md): common problems and fixes
- [ARCHITECTURE.md](docs/ARCHITECTURE.md): how the code is put together
- [DATA-STORAGE.md](docs/DATA-STORAGE.md): which files the app writes and what is in them
- [SECURITY.md](docs/SECURITY.md): how logins are protected and what the limits are

## Help and feedback

Questions, ideas and bug reports go to the [Instance Manager Discord](https://discord.gg/8XyKcZdSGe). The Discord button next to the notification bell opens it too.

## License

Proprietary. All rights reserved. The source is published for inspection only. Use, reproduction, modification, and distribution are not permitted without prior written permission.

The interface icons are based on Lucide and Feather; their licenses are in [docs/THIRD-PARTY-NOTICES.md](docs/THIRD-PARTY-NOTICES.md).

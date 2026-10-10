<div align="center">

<img src="src/InstanceManager/Assets/Logo.png" width="64" height="64" alt="Instance Manager logo">

# Instance Manager

Run several Roblox accounts at the same time, each in its own window.

[![Latest release](https://img.shields.io/github/v/release/JeskoMts/Instance-Manager?label=release&color=2F6FEB)](https://github.com/JeskoMts/Instance-Manager/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/JeskoMts/Instance-Manager/total?color=2F6FEB)](https://github.com/JeskoMts/Instance-Manager/releases)
![Windows 10 and 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-2F6FEB)](LICENSE)
[![Discord](https://img.shields.io/badge/Discord-join-5865F2?logo=discord&logoColor=white)](https://discord.gg/8XyKcZdSGe)

[Download](https://github.com/JeskoMts/Instance-Manager/releases/latest) · [Features](docs/FEATURES.md) · [Changelog](CHANGELOG.md) · [Troubleshooting](docs/TROUBLESHOOTING.md) · [Discord](https://discord.gg/8XyKcZdSGe)

</div>

Instance Manager is a Windows app for people who play Roblox on more than one account. You add each account once, sort them into groups and send as many as you like into the same game or private server. Every account gets its own Roblox window.

The app runs entirely on your PC. It has no account system of its own, no server and no telemetry. You sign in on the real Roblox login page, the app never sees your password, and the session cookie it keeps is encrypted with Windows DPAPI before it is written to disk.

## Features

| Feature | What it does |
|---|---|
| Accounts and groups | Any number of accounts with nicknames and notes, colored groups that launch with one click, and a check that marks expired logins. |
| Launching | Accounts start one after another into a public game, a specific server or a private server, with live status for each row. |
| Several clients at once | Every account keeps its own Roblox window, because the app holds the objects Roblox uses to allow only one client. |
| Auto Reconnect | An account that is kicked, disconnected or crashes is started again in the same game. Leaving a game yourself doesn't count. |
| Automation | A saved set of accounts and a game starts with one click, or starts and closes on its own at the times and days you pick. |
| Favorites and games | Your most played games as favorites, and a grid of popular games to pick from. |
| Graphics | An FPS cap and FastFlags such as the graphics API and quality, applied to every client the app starts. |
| Tray | A tray icon, an option to close to the tray and one to start with Windows. |
| Anti-AFK | Presses a key you choose in every running Roblox window, so idle clients aren't kicked. It is off until you turn it on. |
| Themes | Built-in color themes, your own themes with a color picker, and short codes to share them. |
| Updates | The app updates itself from GitHub, checks every download against its SHA-256 checksum and can roll back one version. |

[docs/FEATURES.md](docs/FEATURES.md) explains every part in detail.

## Download

1. Download `InstanceManager-<version>.zip` from the [latest release](https://github.com/JeskoMts/Instance-Manager/releases/latest).
2. Unzip it. Inside is one file, `Instance Manager.exe`.
3. Put it in any folder you can write to, for example the desktop or `Documents`, and start it.

That file is the whole app. Accounts and settings are stored in `%APPDATA%\Instance Manager`, so you can move or rename the exe at any time. Only one copy runs at once, and starting it again brings the open window to the front. Avoid read-only places such as `C:\Program Files`, because the updater replaces the exe where it is.

Every release has the same two downloads: `InstanceManager-<version>.zip` with the app and `InstanceManager-Source-Code-<version>.zip` with its source code.

## Requirements

- Windows 10 or 11, 64-bit.
- The [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) for x64. Windows offers the download if it is missing.
- The Microsoft Edge WebView2 Runtime for the login window. Windows 11 already has it, and the Add account window links to it when it is missing.
- Roblox. The app starts the signed `RobloxPlayerBeta.exe` from `%LOCALAPPDATA%\Roblox\Versions` or from a folder you choose in Settings.

The app runs as your normal Windows user and never asks for administrator rights.

## First steps

1. Open Accounts and click Add account.
2. Sign in on the Roblox page that opens. The window closes by itself once the login is saved.
3. Repeat this for every account you want to use.
4. Type a game link or Place ID into the launch bar, or pick a favorite or a game from the Games page.
5. Select accounts by clicking their rows or by dragging a box over them, then click Launch.

## Updates

The app asks GitHub for a new release when it starts and every 60 minutes after that. A newer version is downloaded, checked against the SHA-256 checksum GitHub publishes, and installed by replacing the exe and restarting, which takes a second or two. If you are launching accounts or Roblox is running, the update waits until you close the app or click Restart to update.

Settings > Updates has one switch, Update automatically, plus Check now and Roll back to the version you had before.

## Building from source

You need the .NET 8 SDK.

```bash
dotnet restore InstanceManager.sln
dotnet build InstanceManager.sln --configuration Release
dotnet run --project src/InstanceManager/InstanceManager.csproj
```

The tests use xUnit and run without opening a window:

```bash
dotnet test InstanceManager.sln
```

A release is built with:

```powershell
./scripts/build-release.ps1
```

The script publishes the app as one framework-dependent exe for win-x64 and writes two zips to `release-assets/`: `InstanceManager-<version>.zip` with `Instance Manager.exe` and `InstanceManager-Source-Code-<version>.zip` with the source. Attach both to the GitHub release and use the plain version as the tag, for example `1.1.2`. The updater looks for exactly that asset name and only runs in the published single-file app, so a build from source never replaces itself.

CI restores with locked package versions, reports vulnerable packages, scans for leaked secrets, builds Release and runs the tests. See [.github/workflows/ci.yml](.github/workflows/ci.yml).

## Project layout

```
src/InstanceManager/
  App.xaml(.cs)          Startup, single instance, tray, theme and update check
  MainWindow.xaml(.cs)   Window frame with the sidebar, pages and notifications
  Composition/           Service registration
  Models/                Plain data types such as Account, AppSettings and Automation
  Services/              Login, launching, multi-instance, Auto Reconnect, updates and Anti-AFK
  Storage/               JSON repositories and the settings service
  ViewModels/            MVVM layer, coordinated by ShellViewModel
  Views/Pages/           Accounts, Games, Automation and Settings
  Views/Settings/        One view per settings category, built when it is first opened
  Views/                 Dialogs for login, confirmations and editors
  Controls/              Vector icons, the color picker and layout helpers
  Assets/                App icon and logo
  Behaviors/ Converters/ Themes/   WPF helpers and styles
tests/InstanceManager.Tests/       xUnit tests
scripts/                           Release build and secret scan
```

## Documentation

| Document | What it covers |
|---|---|
| [FEATURES.md](docs/FEATURES.md) | Every page and setting and how to use it |
| [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) | Common problems and how to fix them |
| [DATA-STORAGE.md](docs/DATA-STORAGE.md) | Which files the app writes and what is in them |
| [SECURITY.md](docs/SECURITY.md) | How logins are protected and where the limits are |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | How the code fits together |
| [CHANGELOG.md](CHANGELOG.md) | What changed in each release |

## Help and feedback

Questions, ideas and bug reports go to the [Instance Manager Discord](https://discord.gg/8XyKcZdSGe). The Discord button next to the notification bell in the app opens it as well.

## License

Instance Manager is licensed under the [Apache License 2.0](LICENSE).

The interface icons are based on Lucide and Feather. Their licenses are in [docs/THIRD-PARTY-NOTICES.md](docs/THIRD-PARTY-NOTICES.md).

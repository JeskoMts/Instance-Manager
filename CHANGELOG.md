# Changelog

All notable changes to Instance Manager are listed here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.1.2] - 2026-09-27

### Highlights

- Instance Manager is now a single file, `Instance Manager.exe`, that you can put anywhere and start from there.
- Leaving a game no longer closes Roblox.
- New app icon and logo.
- A cleaner header above the account list.

### Added

- A new logo. You see it in the title bar, on the taskbar and on the exe itself, so a desktop shortcut shows it too.
- When you save a favorite, the name field is filled with the game's real name from Roblox, for example "Grow a Garden 2", where it used to say "Game 126884695634066". Update tags like "[UPD]" and emoji at the start or end are left out, and you can still change the name before saving.

### Changed

- The download is one file. `Instance.Manager.1.1.2.zip` contains only `Instance Manager.exe`, which you can move to any folder, the desktop included, and run from there. Accounts and settings stay in `%APPDATA%\Instance Manager` as before. You still need the .NET 8 Desktop Runtime.
- The header above the account list was redesigned. It says "1 account" instead of "1 accounts", turns into a "2 selected" chip with an × that clears the selection, and shows running instances as one "3 running | Stop all" pill. The separate Select all button is gone, since the checkbox and Ctrl+A do the same thing.
- On the Games tab, the loading indicator now sits next to the search box instead of covering the games.
- Saving a favorite in Server mode takes the game and server from the server link. It used to read the hidden Game field, which often held something else.
- New installs get new defaults: a 2 second pause between launches (was 1.5), notifications that stay for 3 seconds (was 4.5), and Auto Reconnect turned off (was on). Settings you already have are kept.

### Fixed

- Leaving a game through the Roblox menu closed the client. Auto Reconnect read the leave as a lost connection, killed Roblox and started it again. Roblox now goes back to its home screen and stays open, and closing it later doesn't trigger a reconnect either. Kicks still reconnect as before.

### Updates

- The updater replaces the running exe under its current name, so shortcuts keep working.
- Copies of 1.1.1 update to 1.1.2 on their own. On its first start, 1.1.2 deletes the files the old version needed and leaves only `InstanceManager.exe`.
- Each release now carries `Instance.Manager.<version>.zip` for downloads and `InstanceManager-<version>.zip` for the 1.1.1 updater. `scripts/build-release.ps1` builds both.

### Internal

- The test suite grew from 383 to 408 tests. The new ones cover leaving a game, the new defaults, game names for favorites, the list header, the Games tab loading indicator and the single-file updater, including the move from 1.1.1.
- The move from 1.1.1 was also tried by hand. The real 1.1.1 updater installed the new package into an unpacked 1.1.1 folder, and the restarted app came up as 1.1.2 and cleaned out the folder.
- The comments in `.gitignore` and the description in the project file were removed.

## [1.1.1] - 2026-09-27

### Added

- Automatic updates. On every start the app checks GitHub for a newer release, downloads it, verifies its SHA-256 checksum and restarts into the new version within a few seconds. If accounts are launching or Roblox instances are running at that moment, the update installs when you close the app. A notification shows the new version after the restart.

### Changed

- Texts across the app were rewritten to be clearer, starting with the Add account window, and no longer use dashes.
- Game fields and error messages consistently say "Place ID".
- The Data & privacy note in Settings now mentions that the app contacts GitHub to check for updates.
- The documentation was rewritten and cut down to the main features. Wrong values were corrected (the retry limit goes up to 30 plus unlimited, the launch pause up to 30 seconds), and a setting that doesn't exist was removed.

### Security

- Updates are only taken from this repository's GitHub releases. The download must match the SHA-256 digest GitHub publishes for it, redirects may only lead to GitHub hosts, and the package is unpacked only if every file name is safe. If replacing a file fails, every file already replaced is restored. [SECURITY.md](docs/SECURITY.md#updates) describes what the update check trusts.
- The dependency audit was repeated with no known vulnerable packages as of September 27, 2026.

### Internal

- The test suite grew from 368 to 383 tests, covering the update check, download, verification, installation and rollback.

## [1.1.0] - 2026-09-27

### Highlights

- Accounts launch strictly one after another, with live status per account, overall progress and a Cancel button.
- Expired logins are recognized and can be renewed with one click.
- New vector icons and a reworked Accounts page, menus and launch bar.
- Faster startup, lower CPU and memory use, and a smaller download.

### Added

- When Roblox no longer accepts an account's saved login, or the login can't be read on this PC, the row shows Login expired with the reason on hover and an Add again button.
- Each row shows its launch status (Queued, Starting, Opening Roblox, Joining game, Running, Login expired or Failed). The launch bar shows progress, and Cancel stops the batch after the account that is currently starting.
- Context menus on accounts (⋯ button or right-click) and on group headers.
- Keyboard shortcuts: Ctrl+1, Ctrl+2 and Ctrl+3 switch tabs, Ctrl+F searches, Ctrl+A selects all visible accounts, Ctrl+Enter launches, Esc clears the search and then the selection.
- A Discord button next to the notification bell that links to the community server.
- `docs/THIRD-PARTY-NOTICES.md` with the icon licenses.

### Changed

- After starting a client, the app waits until its window is open (up to 30 s) and its log reports the game join (up to 20 s) before starting the next account. The pause from Settings comes on top and is now called Pause between launches. Single launches and Auto Reconnect share the same queue.
- Accounts that are already running are skipped, and a client that closes while starting is retried once. The summary lists started, failed and already-running accounts.
- Icons are vector graphics based on Lucide instead of font glyphs.
- Clicking a row selects the account. The list header has a select-all checkbox, the selected and running counts, and Stop all. Running rows show a green dot and a stop button.
- The default Roblox version moved to Settings, Roblox version. Pinning a version and editing group membership moved into the account's ⋯ menu, and pinned accounts show their version as a tag.
- Once any group exists, ungrouped accounts get their own header; dropping an account there removes it from all groups.
- The launch modes are called Game and Server.
- Settings are split into Launching, Roblox version, Auto Reconnect, Appearance, Notifications, Confirmations and Data & privacy.
- The Games tab starts loading at startup, and avatars are cached on disk so they show immediately.

### Fixed

- The window no longer freezes while Roblox versions are detected.
- Launching an account whose client is already running no longer closes that client.
- A Roblox client exiting no longer blocks the UI thread.
- Launching with an empty Game or Server field opens the Roblox home screen instead of showing an error.
- Sliders jump to where you click on the track.

### Performance

Measured with the published build on the same PC, compared with 1.0.0:

- The main window appears about 27% sooner (842 ms down to about 620 ms).
- Startup uses about 25% less CPU time and about 16% less memory (149 MB down to about 125 MB private), even with the Games tab preloaded.
- The Roblox client's signature is no longer re-verified before every launch, which saves about half a second of CPU per launch.
- Game thumbnails are about 9 times smaller, and avatars are decoded off the UI thread.
- Log watching keeps each log file open instead of reopening it every second.
- Release builds precompile the app code (ReadyToRun).
- The download shrank from 943 KB to 899 KB zipped.

### Security

- A Roblox client that passed the signature check stays open with writes denied until the app exits, so it can't be changed between the check and a launch.
- The dependency audit was repeated with no known vulnerable packages as of September 27, 2026.

### Removed

- The version bar on the Accounts page, the stop drop-down and the inline pop-up menus, all replaced by the changes above.
- The dependency on the Segoe Fluent Icons and Segoe MDL2 Assets fonts.

## [1.0.0] - 2026-06-28

- First public release.

[1.1.1]: https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.1
[1.1.0]: https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.0
[1.0.0]: https://github.com/JeskoMts/Instance-Manager/releases/tag/1.0.0

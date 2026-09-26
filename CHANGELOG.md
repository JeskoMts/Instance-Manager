# Changelog

All notable changes to Instance Manager are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-09-27

### Highlights

- Accounts now launch strictly one after another, with live status per account, overall progress, and Cancel.
- Launch straight to the Roblox home screen when no game or server is entered.
- Expired logins are recognized and can be renewed with one click.
- A new set of sharp vector icons and reworked Accounts page, menus, and launch bar.
- Faster startup, less CPU and memory, and a smaller download.

### Added

- **Launch without a target.** Leave the Game or Server field empty to open every selected account signed in on the Roblox home screen. Anything typed into the field must still be a valid link or ID, so a typo never silently falls back to the home screen.
- **Expired-login detection.** When Roblox rejects an account's saved login (it expired, was signed out elsewhere, or the password changed), or the saved login cannot be read on this PC, the row shows **Login expired** with an explanation on hover and an **Add again** button that opens the sign-in window. Signing in to that account clears the mark.
- **Live launch status.** Each row shows Queued, Starting, Opening Roblox, Joining game, Running, Login expired, or Failed with the reason on hover. The launch bar shows a progress bar and "Launching 2 of 5…", and **Cancel** stops the batch after the account that is currently starting.
- **Context menus.** An account's ⋯ button, or a right-click on the row, opens Launch or Stop, Rename, Roblox version, Groups, and Remove account. Group headers offer Launch group, Edit group, and Delete group the same way.
- **Keyboard shortcuts.** Ctrl+1, Ctrl+2, and Ctrl+3 switch between Accounts, Games, and Settings; Ctrl+F searches; Ctrl+A selects all visible accounts; Ctrl+Enter launches the selection; Esc clears the search, then the selection.
- **Discord button** next to the notification bell, linking to the Instance Manager community server for ideas, questions, and bug reports.
- **Ungrouped header.** As soon as any group exists, ungrouped accounts get their own header, and dragging an account onto it removes the account from all groups.
- **Version tag.** Accounts pinned to a specific Roblox version show that version as a small tag on the row.
- **Slider dragging.** Sliders jump to the pointer when pressed anywhere on the track and follow it until release.
- **Avatar cache.** Avatar headshots are stored under `%LOCALAPPDATA%\Instance Manager\avatars`, so they appear immediately at startup. A fresh copy is still downloaded every session and replaces the cached one when it changed.
- `docs/THIRD-PARTY-NOTICES.md` with the licenses of the icon sets.

### Changed

- **Sequential launching.** After starting a client, the app waits until its window is open (up to 30 s) and until its log reports the game join (up to 20 s) before the next account starts. The pause from Settings is added on top and is now called *Pause between launches*. Single launches and Auto Reconnect share the same queue, so a reconnect never collides with a running batch. Launches to the home screen skip the join wait.
- **Launch results.** Accounts that are already running are skipped. A client that closes while it is still starting is retried once with a fresh ticket. The summary lists started, failed, and already-running accounts.
- **Icons.** All icons are now vector graphics based on Lucide instead of icon-font glyphs, so they stay crisp at every size and display scaling.
- **Accounts page.** Clicking a row selects the account. The list header has a checkbox that selects or clears everything visible, shows how many accounts are selected, and shows the running count with **Stop all**. The launch button names how many accounts it will start. Running rows show a green dot and a red stop button in place of the play button.
- **Roblox version.** The default version moved from the Accounts page to *Settings → Roblox version*. Pinning a version to one account moved into that account's ⋯ menu.
- **Groups.** Group membership is edited from the account's ⋯ menu (tick groups, or *Remove from all groups*).
- **Launch bar.** The modes are now called *Game* and *Server*, and both fields explain on hover that they may be left empty.
- **Settings.** Sections are now Launching, Roblox version, Auto Reconnect, Appearance, Notifications, Confirmations, and Data & privacy. The lists of individual notifications and confirmations are collapsible, and the app version moved to the page footer.
- **Games tab.** Loading starts immediately at startup instead of after version detection, and thumbnails are small JPEGs.

### Fixed

- The window no longer freezes while Roblox versions are detected at startup and whenever Settings is left.
- Launching an account whose client is already running no longer closes that running client.
- A Roblox client exiting no longer blocks on the UI thread.

### Performance

Measured with the published release build on the same PC, compared with 1.0.0:

- The main window appears about 27% sooner (842 ms to about 620 ms).
- Startup uses about 25% less CPU time and about 16% less memory (149 MB to about 125 MB private), even though the Games tab is now preloaded.
- The signature of the Roblox client (about half a second of CPU for each ~140 MB executable) is no longer re-verified on every launch and every time Settings is left. A batch of ten accounts saves about five seconds of CPU.
- Game thumbnails download about 9 times smaller (480×270 JPEG instead of 768×432 PNG). Avatars use 75×75 images and are decoded off the UI thread.
- Watching the logs of running clients keeps each log open instead of reopening it every second, lists log files without an extra system call per file, and no longer allocates a copy of every log line.
- Network responses are parsed and images decoded off the UI thread. An unchanged list of Roblox versions no longer rebuilds every row or rewrites the settings file.
- Release builds precompile the application code (ReadyToRun) and turn off dynamic PGO and unused property-changing notifications.
- The download is smaller: 943 KB to 899 KB zipped, 2.91 MB to 2.36 MB unpacked. API documentation files, debug symbols, and a duplicate WebView2 loader are no longer shipped.

### Security

- A Roblox client that passed the signature check is kept open with writes denied until the app exits, so its contents cannot change between the check and a launch. Later checks still apply every path rule and confirm the file identity (volume serial number and 128-bit file ID); a replaced, renamed, or deleted file is verified from scratch.
- The only non-Roblox address in the app is the Discord invite. It opens in the default browser only when the Discord button is clicked; the app itself sends no request there.
- The dependency audit was repeated: no known vulnerable direct or transitive packages as of September 27, 2026.

### Removed

- The version bar on the Accounts page (now *Settings → Roblox version*).
- The drop-down for stopping a single instance (replaced by the stop button on each running row and *Stop all*).
- The inline pop-up menus on account rows and group headers (replaced by context menus).
- The dependency on the Segoe Fluent Icons and Segoe MDL2 Assets fonts.

### Documentation

- README, FEATURES, TROUBLESHOOTING, ARCHITECTURE, DATA-STORAGE, and SECURITY describe the new behavior. Outdated passages about the tab layout, the version bar, and the launch pause range were corrected.
- The README documents the release publish command and links the Discord server.

### Internal

- Comments were removed from all source code.
- The test suite grew from 344 to 368 tests, covering sequential launching, expired logins, home-screen launches, executable pinning, log tailing, icons, and slider behavior.

## [1.0.0] - 2026-06-28

- First public release.

[1.1.0]: https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.0
[1.0.0]: https://github.com/JeskoMts/Instance-Manager/releases/tag/1.0.0

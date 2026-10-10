# Changelog

Every release of Instance Manager, newest first. Each entry starts with a short summary, followed by what is new, what changed and what was fixed. Downloads are on the [releases page](https://github.com/JeskoMts/Instance-Manager/releases).

## [1.1.4](https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.4) · 2026-10-10

Version 1.1.1 can update by itself again.

### Fixed

- Version 1.1.1 never updated to 1.1.2 or 1.1.3. Its updater only accepts a package that contains `InstanceManager.exe` and `InstanceManager.dll`, and the newer packages held a single `Instance Manager.exe`, so it dropped them without a message. The package now carries the app under both names plus a small file named `InstanceManager.dll`, which 1.1.1 installs at its next start. In such a folder the app keeps the name `InstanceManager.exe`, so shortcuts keep working, and the old DLL files are removed.

### Changed

- `InstanceManager-1.1.4.zip` holds three files instead of one. Run `Instance Manager.exe`; its first start deletes the other two, which are only there for the 1.1.1 updater.

## [1.1.3](https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.3) · 2026-10-10

Accounts can be selected by dragging a box over the list, the Automation page and the header of the account list are tidier, and updates are down to one switch. Stop all works right away, and the hints and tooltips across the app were rewritten.

### New

- Drag a box over the account list to select every account it touches, like on the Windows desktop. Start the drag on an empty part of a row or next to the list. The list scrolls when the pointer leaves it at the top or bottom, and holding Ctrl or Shift keeps the accounts that were already selected.

### Changed

- An account is now dragged by its name or avatar, to reorder it or to drop it on a group. Dragging from the empty part of a row draws the selection box instead.
- The Automation page is tidier. Stop only appears while accounts of the automation run, Duplicate and Delete moved into a ⋯ menu, the account search shows up from 9 accounts on, the Every day, Weekdays and Weekends shortcuts and most of the hint lines are gone.
- The header above the account list is plain text now: the selected count with Clear, the running count and Stop all. Stop all no longer asks first, also cancels a launch that is still going, and its confirmation setting is gone.
- An automation no longer shows "Unknown game" or "A specific server" under its destination. The line only appears once the game's name is known.
- Updates are simpler. The app asks GitHub for the latest release at startup and once an hour and installs it, and Check now installs a newer version right away when nothing is running. One switch, Update automatically, replaces Check at startup, the check interval, pre-releases, Skip this version and Keep the previous version. A copy of the old version is always kept, and Roll back turns Update automatically off.
- When a newer release is on GitHub but its package can't be verified, the Updates page says so instead of calling the app up to date.
- Hints and tooltips across the app were rewritten to say what a control does and what happens when it is off.

## [1.1.2](https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.2) · 2026-09-28

The window was redesigned around a sidebar, and the app gained Automation, graphics settings, a tray icon and Anti-AFK, where you choose any key. Private server links work in Server mode, Auto Reconnect no longer closes healthy clients, and the keyboard shortcuts are gone. Settings are sorted into three groups with a search box, the window opens faster, and the download is now one file.

### New

- Automation page. An automation is a saved set of accounts plus a game, a server or the Roblox home screen, and Run starts them one after another. An optional schedule starts them at a set time on the days you pick and closes them again after a number of minutes or at a set time, also past midnight. A new automation takes the accounts selected on the Accounts page and the game in the launch bar. Accounts can be found by name or added by group, and a favorite fills in the destination with one click. Schedules run while the app is open, also from the tray, and still run if the app comes back within 10 minutes of the planned time.
- The search box in Settings finds the page for a setting, for example "fps", "tray" or "afk".
- Check sessions on the Accounts page asks Roblox whether each saved login still works and marks the expired ones before a launch fails.
- Join another server, in an account's ⋯ menu, closes the account and starts it again in a different public server of the game in the launch bar.
- Graphics settings with an FPS cap and FastFlags for the graphics API, quality, anti-aliasing, textures, display scaling, grass, a gray sky, frozen lighting and exclusive fullscreen. Custom flags are accepted when they are on the allowlist of 18 rendering flags. Everything is written into Roblox's files right before each launch, and turning FastFlags off removes them again.
- Start with Windows, Start in the tray and Close to tray. The tray icon's menu opens the window, closes every Roblox window or quits.
- Anti-AFK presses a key of your choice in every running Roblox window every 1 to 60 minutes, then switches back to the window you were using. Click the Key button and press any key; Space is the default. Esc, the Windows keys and the media and browser keys can't be used.
- Avatars in the account list have a switch under Settings > General. When it's off, rows show initials and nothing is downloaded.
- Update settings: check at startup, check every 15 to 1440 minutes, include pre-releases, skip a version and roll back to the version you had before the last update.
- A color picker with a color field, a hue bar and a hex field, used for group colors and for the new Custom colors list of the current theme.
- A new logo in the title bar, on the taskbar and on the exe, so desktop shortcuts show it too.
- Saving a favorite fills in the game's real name from Roblox, without update tags like "[UPD]" or emoji at the ends.

### Changed

- The download is one file. `InstanceManager-1.1.2.zip` holds only `Instance Manager.exe`, which runs from any folder you can write to, the desktop included. The exe is called `Instance Manager.exe` now, like the app, instead of `InstanceManager.exe`. Accounts and settings stay in `%APPDATA%\Instance Manager`. Releases 1.0.0 to 1.1.1 were rebuilt the same way, so every release now has the same two downloads.
- A 1.1.1 that came as a folder with several DLL files can't install this update by itself, because its updater expects the old package. Download `InstanceManager-1.1.2.zip` once and put `Instance Manager.exe` into the old folder. Its first start deletes the files of the old version, including the old `InstanceManager.exe`, so shortcuts need to point to the new exe. Later updates install automatically again.
- A sidebar holds Accounts, Games, Automation and Settings, and the button in the title area opens and closes it. Every page has a title with a short description.
- Settings are sorted into three groups. App holds General, Appearance and Notifications; Roblox holds Launching, Graphics and Reconnect & AFK; System holds Updates and Data & privacy. Related options sit together: Anti-AFK sits next to Auto Reconnect, and "Switch to Accounts after picking a game" moved to General. Every page starts with a title and a one-line description.
- The message after Send now under Anti-AFK appears as a notification instead of a line of text under the settings. It can be muted as "Anti-AFK messages".
- Instance Manager opens faster and needs less memory. Games, Automation and Settings are built the first time you open them, each settings page as well, and icons are prepared only when they're shown. Measured by building the window without any data, building and drawing it takes about a quarter less time (404 ms down to 306 ms), and starting allocates 60% less memory (16.4 MB down to 6.6 MB). Opening Settings for the first time now takes about 20 ms longer, still less than two frames.
- Long account lists cost less. Avatars are looked up for up to 100 accounts per request and downloaded only when they changed, so with 100 accounts a later start sends 1 request instead of 151 and uses 16 ms of CPU instead of 390 ms. Reading 300 accounts allocates 57% less memory and keeps 1.3 MB less.
- The Games page loads its list two seconds after startup instead of during it. Going back to the popular games or to a search from the last five minutes shows it right away, without downloading and decoding the 30 pictures again.
- Every text in the app was rewritten.
- Instance Manager is licensed under the Apache License 2.0, in every release. The README used to call the source proprietary, which contradicted the `LICENSE` file.
- The Add account window has no bottom bar anymore. It saves the login and closes by itself once you have signed in.
- The theme editor window is gone. The colors of your own themes are changed in the Custom colors list, and Customize makes an editable copy of a built-in theme.
- The header above the account list says "1 account" instead of "1 accounts", becomes a "2 selected" chip with an × that clears the selection, and shows running instances as "3 running | Stop all". The Select all button is gone, since the checkbox does the same.
- On the Games page, the loading indicator sits next to the search box instead of covering the games.
- Saving a favorite in Server mode takes the game and the server from the server link. It used to read the hidden Game field, which often held something else.
- New installs start with a 2 second pause between launches (was 1.5), notifications that stay for 3 seconds (was 4.5), Auto Reconnect off (was on) and "Switch to Accounts after picking a game" on. Existing settings are kept.
- The updater replaces the running exe under its current name, so shortcuts keep working. It keeps a copy of the current version for Roll back, says so when GitHub can't be reached, and deletes a skipped download so it isn't installed on exit.
- Keyboard shortcuts are gone. Ctrl+1 to Ctrl+3, Ctrl+F, Ctrl+A, Ctrl+Enter and Esc no longer do anything in the main window, and every one of them has a button.
- Auto Reconnect covers accounts the app sent into a game or server. An account opened on the Roblox home screen is left alone, because there is no game to bring it back to.
- The "Only one window at a time" warning also appears when you start accounts one after another, and it mentions the Roblox tray icon.

### Fixed

- Server mode rejected every private server link with "The server link must contain both a Place ID and Job ID". Links of the form `roblox.com/share?code=...&type=Server`, the older `roblox.com/games/<id>?privateServerLinkCode=...` links and invite links now work. Roblox answers a share link only for a signed-in account and never puts the server into a redirect, so the app looks it up with the saved login of one of the accounts being launched. Favorites and automations keep the private server.
- Auto Reconnect closed and restarted healthy clients about 22 seconds after each launch, over and over, and other accounts' windows closed along with them. It read a harmless line of Roblox's network diagnostics, "Failed to connect to 10.110.101.222 port 5052", as a lost game. Diagnostic lines and whatever a game's own scripts print are ignored now, and a kick or error counts only once the client has started to join a game.
- When Roblox can't be reached while a server link is looked up, the launch stops with a clear message.
- Opening Instance Manager a second time started a second copy. The running copy now comes to the front.
- Game pictures on the Games page were cut off at the top and bottom in wide windows, because their height was fixed while the cards grew. Cards now keep the 16:9 shape of Roblox thumbnails, and the grid adds columns as the window gets wider, up to eight.
- Leaving a game through the Roblox menu closed the client, because Auto Reconnect read the leave as a lost connection. Roblox now goes back to its home screen and stays open. Kicks still reconnect.
- After an account had left a game through the Roblox menu, Auto Reconnect ignored a later crash or a client ended in Task Manager. Both reconnect again.

### Security

- The Roblox client must be signed by exactly "Roblox Corporation". A certificate whose name only contained it, such as "Roblox Corporation Fan Tools Ltd", passed before.
- Roblox API answers over 4 MB are dropped instead of buffered, and pictures are streamed, so the 5 MB limit now stops a download instead of checking it afterwards.
- An update's download address must be exactly the release asset on GitHub. A path with `../` could point to another repository before.
- The sign-in window denies requests for the camera, microphone, location and other permissions.
- Log lines drop line separators and bidi controls, which could fake a line or reverse text in an editor.
- The saved login goes to one more fixed Roblox address, the share link lookup, and only when you launch with a `roblox.com/share` link. A private server's link code is never written to the log.

## [1.1.1](https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.1) · 2026-09-27

Instance Manager updates itself. The texts in the app and the documentation were rewritten to be clearer.

The download was rebuilt on 2026-09-28 as a single `Instance Manager.exe`. Its updater also accepts the one-file packages of later releases; nothing else changed.

### New

- Automatic updates. On every start the app asks GitHub for a newer release, downloads it, checks its SHA-256 checksum and restarts into the new version within a few seconds. If accounts are launching or Roblox instances are running, the update waits until you close the app. A notification names the new version after the restart.

### Changed

- Texts across the app were rewritten, starting with the Add account window.
- Game fields and error messages say "Place ID" everywhere.
- The Data & privacy note in Settings mentions that the app contacts GitHub to check for updates.
- The documentation was rewritten and shortened. Wrong values were corrected (the retry limit goes up to 30 plus unlimited, the launch pause up to 30 seconds), and a setting that doesn't exist was removed.

### Security

- Updates come only from this repository's GitHub releases. The download must match the SHA-256 digest GitHub publishes for it, redirects may only lead to GitHub hosts, and the package is unpacked only when every file name is safe. If replacing a file fails, the files already replaced are restored.
- A dependency audit on September 27, 2026 found no known vulnerable packages.

## [1.1.0](https://github.com/JeskoMts/Instance-Manager/releases/tag/1.1.0) · 2026-09-27

Accounts launch strictly one after another with live status, expired logins are recognized, and the Accounts page was reworked with new icons. Startup is faster and uses less memory.

The download was rebuilt on 2026-09-28 as a single `Instance Manager.exe`; the app works the same.

### New

- When Roblox no longer accepts a saved login, or the login can't be read on this PC, the row shows Login expired with the reason on hover and an Add again button.
- Each row shows its launch status: Queued, Starting, Opening Roblox, Joining game, Running, Login expired or Failed. The launch bar shows the progress, and Cancel stops the batch after the account that is currently starting.
- Context menus on accounts, through the ⋯ button or a right-click, and on group headers.
- Keyboard shortcuts: Ctrl+1 to Ctrl+3 switch tabs, Ctrl+F searches, Ctrl+A selects all visible accounts, Ctrl+Enter launches and Esc clears the search, then the selection.
- A Discord button next to the notification bell that opens the community server.
- `docs/THIRD-PARTY-NOTICES.md` with the icon licenses.

### Changed

- After starting a client, the app waits until its window is open (up to 30 seconds) and its log reports the game join (up to 20 seconds) before it starts the next account. The pause from Settings comes on top and is now called Pause between launches. Single launches and Auto Reconnect use the same queue.
- Accounts that are already running are skipped, and a client that closes while starting gets one more try. The summary lists started, failed and already running accounts.
- Clicking a row selects the account. The list header has a select-all checkbox, the selected and running counts and Stop all. Running rows show a green dot and a stop button.
- The default Roblox version moved to Settings, and pinning a version or changing groups moved into the account's ⋯ menu. Pinned accounts show their version as a tag.
- Once a group exists, ungrouped accounts get their own header, and dropping an account there takes it out of every group.
- The launch modes are called Game and Server, and Settings are split into Launching, Roblox version, Auto Reconnect, Appearance, Notifications, Confirmations and Data & privacy.
- Icons are vector graphics based on Lucide, so the app no longer depends on the Segoe Fluent Icons and Segoe MDL2 Assets fonts.
- The Games tab starts loading at startup, and avatars are cached on disk so they show up right away.
- Startup is faster, measured with the published build on the same PC. The window appears about 27% sooner (842 ms down to about 620 ms), and startup takes about 25% less CPU time and 16% less memory (149 MB down to about 125 MB). Release builds are precompiled with ReadyToRun, and the download shrank from 943 KB to 899 KB.
- Launches got cheaper as well. The Roblox client's signature is no longer checked again before every launch, which saves about half a second of CPU, game thumbnails are about 9 times smaller, avatars are decoded off the UI thread, and log watching keeps each file open instead of reopening it every second.
- The version bar on the Accounts page, the stop drop-down and the inline pop-up menus were removed in favor of the changes above.

### Fixed

- The window froze while Roblox versions were being detected.
- Launching an account whose client was already running closed that client.
- A Roblox client that exited could block the UI thread.
- An empty Game or Server field showed an error. It now opens the Roblox home screen.
- Sliders jump to the spot you click on the track.

### Security

- A Roblox client that passed the signature check stays open with writes denied until the app exits, so it can't be swapped between the check and a launch.
- A dependency audit on September 27, 2026 found no known vulnerable packages.

## [1.0.0](https://github.com/JeskoMts/Instance-Manager/releases/tag/1.0.0) · 2026-06-28

The first public release: a local Windows app that runs several Roblox accounts at once, each in its own window.

The download was rebuilt on 2026-09-28 as a single `Instance Manager.exe`; the app works the same.

### New

- Any number of Roblox accounts, each with an optional nickname and notes. You sign in on the real Roblox login page in an embedded browser, the app never sees the password, and the session cookie is encrypted with Windows DPAPI before it is saved.
- Colored groups that collapse and launch as a whole. Accounts are dragged onto a group header, and one account can be in several groups.
- Launching many accounts in a row, each in its own Roblox window, with a pause you set. One failing account doesn't stop the others.
- Launch targets: a public game by link or Place ID, or a specific or private server by link or Job ID. Server links must stay on Roblox domains.
- Favorites with an optional private server. The primary favorite stays on top and is selected again at startup.
- A Games tab with a grid of popular games that fills in the launch target with one click.
- Several Roblox clients at once. The app holds the `ROBLOX_singletonEvent` and `ROBLOX_singletonMutex` objects that Roblox uses to allow only one client.
- Auto Reconnect after kicks, errors, lost connections and crashes, up to a retry limit, with each instance following its own Roblox log. An account you stop yourself is never restarted.
- A Roblox version for all accounts or per account, and a custom versions folder on a local drive. Only clients signed by Roblox are started.
- Color themes with an editor, shareable as a short code.
- Notifications with an adjustable display time and muting, confirmations that can be skipped, and Undo after removing an account, a group or a favorite.
- Everything stays on the PC. The app has no server of its own and no telemetry, and it runs without administrator rights.

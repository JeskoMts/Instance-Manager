# Troubleshooting

Most problems come down to one of three things: an expired login (add the account again), a Roblox window that was open before Instance Manager (close all Roblox windows), or a broken WebView2 Runtime (reinstall it). The sections below go through each problem in more detail.

Two folders come up often. You can paste either one into the File Explorer address bar:

- `%APPDATA%\Instance Manager` holds `settings.json`, `accounts.json` and `auto-reconnect.log`.
- `%LOCALAPPDATA%\Roblox` holds the Roblox clients (`Versions`) and their logs (`logs`), which the app reads but never changes.

## Quick reference

| Problem | Section |
|---|---|
| The app won't start or Windows asks for .NET | [Starting the app](#starting-the-app) |
| The app doesn't update, or an update failed | [Updates](#updates) |
| The login window is blank, or the account isn't added | [Adding accounts](#adding-accounts) |
| An account fails as soon as you launch it | [Launching](#launching) |
| Accounts worked on your old PC but fail on the new one | [Launching](#launching) |
| The launch bar rejects your link or ID | [Launching](#launching) |
| A second account closes the first one | [Several clients at once](#several-clients-at-once) |
| No Roblox version is found, or a custom folder is refused | [Roblox versions](#roblox-versions) |
| An instance dropped and didn't come back | [Auto Reconnect](#auto-reconnect) |
| A theme code is rejected | [Themes](#themes) |
| Settings don't stick, or you want to start fresh | [Data and resetting](#data-and-resetting) |

## Starting the app

### Windows says .NET is missing

The app needs the .NET 8 Desktop Runtime for x64. Windows shows a download link the first time you start the app without it. Install the Desktop Runtime (not the ASP.NET Core or plain .NET runtime) and start the app again.

### The window opens off-screen or at an odd size

The window size is saved in `settings.json`, and a saved size can land badly after a monitor was unplugged. Close the app, open `%APPDATA%\Instance Manager\settings.json` and delete the `WindowWidth` and `WindowHeight` lines, or delete the whole file to reset every setting.

## Updates

The app checks GitHub for a new version every time it starts. When it finds one, it restarts within a few seconds and shows "Instance Manager updated" with the new version number. If you're launching accounts or have Roblox instances running at that moment, you get "Update ready" instead, and the update installs when you close the app.

### The update failed

The app couldn't replace its own exe. This usually means it sits in a folder your Windows user can't write to, such as `C:\Program Files`, or that antivirus locked the file. Move `Instance Manager.exe` somewhere you own, like the desktop or `Documents`, and start it again. You can also download `Instance.Manager.<version>.zip` from the [releases page](https://github.com/JeskoMts/Instance-Manager/releases/latest) and replace the exe by hand; your accounts and settings aren't stored next to it, so nothing is lost.

### The app never updates

The check needs internet access to `api.github.com` and `github.com`. A firewall or proxy that blocks GitHub stops it silently. GitHub also limits how often one internet connection may ask for release information (60 times per hour), so on a shared network the check can be skipped now and then. It runs again at the next start. Builds you compile yourself never update.

### Leftover `.im-old` files or an `.im-update` folder

These are left over from an update and are deleted the next time the app starts. You can also delete them yourself while the app is closed.

### After updating from 1.1.1 the folder still has DLL files

The first start of 1.1.2 removes the files the old version needed. If the old process was still closing at that moment, some of them are renamed to `.im-old` instead and disappear on the next start. Only `InstanceManager.exe` is left; it keeps that name so your shortcuts still work.

## Adding accounts

### The login window is blank or never opens

The Add account window is a Microsoft Edge WebView2 browser. If the WebView2 Runtime is missing, the window says so and shows a download button; install the Evergreen Runtime and open Add account again. If the runtime is installed but the page stays blank, repair it from the same download, or restart Windows if a WebView2 update is half finished.

### You signed in but the account wasn't added

After you sign in, the app asks Roblox which account the login belongs to, and it only saves the account if that check works. It fails when there is no internet, when a firewall blocks `users.roblox.com`, or when Roblox rate-limits you after many logins in a row (wait a minute and try again). It also fails if you close the window before the Roblox home page has loaded.

### You added an account that's already in the list

That's fine. Accounts are matched by Roblox user ID, so the existing entry gets the new login and keeps its nickname, notes and groups. This is the normal way to fix an expired login.

### The login window remembers a previous account

It shouldn't, because the login profile is InPrivate and wiped every time the window closes. To be sure, close the app and delete `%LOCALAPPDATA%\Instance Manager\webview`; it is recreated on the next login.

### Memory use jumps while the login window is open

The login window is a full Edge browser and can use a few hundred megabytes. It is shut down completely when the window closes.

## Launching

### An account fails right away

Almost always its login expired. Roblox ends logins after some time, after a password change, or when you sign out of all sessions. The row then shows Login expired; click Add again and sign in to that account. The other accounts in the same launch still start.

### Accounts worked on your old PC but all fail on the new one

Logins are encrypted for one Windows user on one PC. A copied `accounts.json` keeps names, notes and groups, but the logins can't be decrypted on another PC or under another Windows user. Add each account again on the new PC.

### The launch bar rejects your game link or Place ID

In Game mode the field accepts a Place ID, a link like `roblox.com/games/<id>/...` or a link with `?placeId=<id>`. Shortened links from other sites don't work. Open the game on the Roblox website and copy the link from the address bar, or copy just the number from it.

### A server link or Job ID is rejected

Switch the launch bar to Server first. It takes a full server link, a private server link or a Job ID link. Only HTTPS links on Roblox domains are accepted, with at most five redirects, so links from URL shorteners and plain HTTP links are refused.

### Every account fails at once

Then the cause is shared. Check that a Roblox version is selected under Settings, Roblox version, that you're online (every launch asks Roblox for a fresh ticket), and that Roblox itself isn't down.

## Several clients at once

### A second account closes the first one

Roblox allows one client per PC and enforces it with two named system objects. Instance Manager holds both so extra windows can open. If a Roblox client was already running when the app tried to take them, the app can't, and you get one window at a time. To fix it:

1. Close every Roblox window, including any `RobloxPlayerBeta.exe` still listed in Task Manager.
2. Close any other account manager or multi-instance tool.
3. Make sure Multi-instance is on under Settings, Launching, then launch again.

## Roblox versions

### No Roblox version is found

The app only uses officially signed Roblox clients. It looks for `RobloxPlayerBeta.exe` under `%LOCALAPPDATA%\Roblox\Versions` and checks that Roblox Corporation signed it. Install Roblox or start it once so a client exists, or point the app at the right folder under Settings, Roblox version.

### A pinned version can't be found after a Roblox update

Roblox deletes old versions when it updates. Open the account's ⋯ menu, choose Roblox version and switch it back to the default version.

### A custom versions folder is refused

The app will run programs from that folder, so it is strict. The folder must be a full path on a drive inside your PC. Network paths like `\\server\share`, mapped network drives, USB and other removable drives, and relative paths are all refused. Inside the folder, each `RobloxPlayerBeta.exe` must have exactly that name, must not be a symbolic link or junction, and must carry a valid Roblox signature.

## Auto Reconnect

Every attempt is logged in `%APPDATA%\Instance Manager\auto-reconnect.log`, so start there:

```
2026-06-23 12:15:04  RECONNECT  'MainAlt' (userId 123)  trigger=Kick  attempt 1/3  -> placeId=920587237
2026-06-23 12:15:09  RESULT  'MainAlt' (userId 123)  attempt 1  started
2026-06-23 12:16:40  GIVEUP  'MainAlt' (userId 123)  trigger=Error  retry limit (3) reached
```

### An instance dropped and didn't come back

Check these in order:

1. Is the Auto Reconnect main switch on? It is off by default.
2. Did you stop it yourself or leave the game on purpose? Neither is restarted.
3. Is there a `GIVEUP` line? Then it hit the retry limit. Raise the limit in Settings, or launch the account again to reset the count.
4. Is the matching option on? Kicks and errors fall under Reconnect after Kick/Error, crashes under Reconnect after Instance Crash.

If the log has no line at all for the drop, the app didn't recognize it as a drop, which points to the log reading covered below.

### Only some of several accounts came back

Each instance follows its own Roblox log, and a fresh client can take a few seconds to create one. The app already waits for each client to open and join before starting the next. If you still see this, raise the pause between launches in Settings above the default of 2 seconds.

### An instance keeps reconnecting

The drop keeps happening, for example a game that kicks you on join or a full private server. Auto Reconnect stops at the retry limit. Stop the instance yourself or lower the limit while you sort out the target.

### Auto Reconnect never reacts

It reads the Roblox logs in `%LOCALAPPDATA%\Roblox\logs`. If a cleanup tool deletes that folder while you play, or another tool locks it, there is nothing to read.

## Themes

### A theme code is rejected

Theme codes are checked before they are applied. A code that was cut off while copying, edited by hand, or mixed with other text is refused. Copy the whole code again and paste it in one piece.

### A theme is hard to read

Switch to a built-in theme under Settings, Appearance, then fix your custom theme in the editor.

## Data and resetting

### Settings don't save

The app writes `settings.json` in `%APPDATA%\Instance Manager`. If antivirus or a sync tool like OneDrive locks the file, or it is read-only, the save is skipped instead of crashing the app. Make sure the file isn't read-only and exclude the folder from antivirus and sync tools. The same goes for the other JSON files there.

### Resetting one thing

Each kind of data is a separate file. Close the app and delete the one you want to reset:

- `settings.json` resets all settings
- `accounts.json` removes all accounts
- `groups.json` removes all groups (the accounts stay)
- `favorites.json` removes all favorites
- `themes.json` removes your own themes

### Starting completely fresh

Close the app and delete `%APPDATA%\Instance Manager` and `%LOCALAPPDATA%\Instance Manager`. The first holds your data and settings, the second the login browser data and the avatar cache.

## Reporting a bug

Bug reports go to the [Instance Manager Discord](https://discord.gg/8XyKcZdSGe). These help:

- `auto-reconnect.log` for anything about reconnecting
- the exact text of the notification or status line
- your Windows version and the app version, shown at the bottom of the Settings tab

Never post a `.ROBLOSECURITY` cookie, a launch ticket or someone else's account details.

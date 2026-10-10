# Troubleshooting

[README](../README.md) · [Features](FEATURES.md) · Troubleshooting · [Data storage](DATA-STORAGE.md) · [Security](SECURITY.md) · [Architecture](ARCHITECTURE.md) · [Changelog](../CHANGELOG.md)

Most problems have one of three causes: an expired login (add the account again), a Roblox window that was open before Instance Manager started (close every Roblox window), or a broken WebView2 Runtime (repair it). The sections below go through each problem in detail.

Two folders come up often. You can paste either one into the File Explorer address bar:

- `%APPDATA%\Instance Manager` holds `settings.json`, `accounts.json`, `automations.json` and `auto-reconnect.log`.
- `%LOCALAPPDATA%\Roblox` holds the Roblox clients (`Versions`) and their logs (`logs`). The app never changes the logs, and it writes into the rest only for the options under Settings > Graphics.

## Quick reference

| Problem | Section |
|---|---|
| Windows asks for .NET, or the app closes right after starting | [Starting the app](#starting-the-app) |
| Nothing happens when you open the app | [Starting the app](#starting-the-app) |
| The app doesn't update, or an update failed | [Updates](#updates) |
| The login window is blank, or the account isn't added | [Adding accounts](#adding-accounts) |
| An account fails as soon as you launch it | [Launching](#launching) |
| Roblox closes after minimizing or about 20 seconds after launch | [Launching](#launching) |
| Accounts worked on your old PC but fail on the new one | [Launching](#launching) |
| The launch bar rejects your link or ID | [Launching](#launching) |
| A second account closes the first one | [Several clients at once](#several-clients-at-once) |
| No Roblox version is found, or a custom folder is refused | [Roblox versions](#roblox-versions) |
| An instance dropped and didn't come back | [Auto Reconnect](#auto-reconnect) |
| An automation didn't start or close on time | [Automation](#automation) |
| Graphics options don't show up in Roblox | [Graphics](#graphics) |
| Anti-AFK doesn't reach a client or refuses a key | [Anti-AFK](#anti-afk) |
| A theme code is rejected | [Themes](#themes) |
| Settings don't stick, or you want to start fresh | [Data and resetting](#data-and-resetting) |

## Starting the app

### Windows says .NET is missing

The app needs the .NET 8 Desktop Runtime for x64. Windows shows a download link the first time you start the app without it. Install the Desktop Runtime (not the ASP.NET Core Runtime or the plain .NET Runtime) and start the app again.

### The app closes right after starting

Windows records the reason. Open Event Viewer, go to Windows Logs > Application and look for an Error from `.NET Runtime` or `Application Error` that names `Instance Manager.exe`. Post the text of the `.NET Runtime` entry in the [Discord](https://discord.gg/8XyKcZdSGe); it names the exact place that failed. Your accounts and settings aren't affected by the crash.

### Nothing happens when you open the app

Only one copy of Instance Manager runs at a time, and opening it again brings the running copy to the front. If the window still doesn't show, the app is probably in the tray, either because Close to tray is on or because it started with Windows while Start in the tray is on. Click its icon in the tray, which may be hidden behind the ^ arrow. If there is no icon, end `Instance Manager.exe` in Task Manager and start the app again.

### The app doesn't start with Windows

Start with Windows remembers where the exe was. If you moved it, start it once from the new place and the entry follows. Also look at Task Manager > Startup apps: if Instance Manager is disabled there, Windows skips it.

### The window opens off-screen or at an odd size

The window size is saved in `settings.json`, and it can end up in a bad place after a monitor was unplugged. Close the app, open `%APPDATA%\Instance Manager\settings.json` and delete the `WindowWidth` and `WindowHeight` lines, or delete the whole file to reset every setting.

## Updates

The app checks GitHub for a new version when it starts and then every 60 minutes. Settings > Updates changes both and shows when the last check ran. When the startup check finds a new version, the app restarts within a few seconds and shows "Instance Manager updated" with the new version number. If you're launching accounts or Roblox is running at that moment, or the update turns up later, you get "Update ready" instead, and the update installs when you close the app or click Restart to update.

### The update failed

The app couldn't replace its own exe. Usually it sits in a folder your Windows user can't write to, such as `C:\Program Files`, or antivirus locked the file. Move `Instance Manager.exe` somewhere you own, like the desktop or `Documents`, and start it again. You can also download `InstanceManager-<version>.zip` from the [releases page](https://github.com/JeskoMts/Instance-Manager/releases/latest) and replace the exe by hand. Your accounts and settings aren't stored next to it, so nothing is lost.

### Version 1.1.1 doesn't update to a newer version

The 1.1.1 download was a folder with several DLL files, and its updater only takes a package that still contains `InstanceManager.exe` and `InstanceManager.dll`. Versions 1.1.2 and 1.1.3 shipped a single exe, so 1.1.1 ignored them without a message. From 1.1.4 on the package carries both, and 1.1.1 updates by itself at its next start. The app keeps the name `InstanceManager.exe` in that folder, so your shortcuts still work, and the old DLL files are deleted.

If it still doesn't update, do it once by hand: download `InstanceManager-<version>.zip`, unzip it and put `Instance Manager.exe` into your old folder. Its first start deletes the files of the old version, including the old `InstanceManager.exe`, or renames them to `.im-old` if they are still in use, and those go away on the next start. Point your shortcuts to the new exe. After that, updates install by themselves again.

### The app never updates

Look at Settings > Updates first. If Update automatically is off, the app checks only when you click Check now. Rolling back turns it off, so turn it on again when you want updates back. The line under the version number says what the last check found.

The check needs access to `api.github.com` and `github.com`. When a firewall or proxy blocks GitHub, the Updates page says "Couldn't reach GitHub". GitHub also limits how often one internet connection may ask for release information (60 times per hour), so on a shared network a check can fail now and then; the next one usually works. Builds you compile yourself never update.

### A new version causes problems

Open Settings > Updates and click Roll back under Previous version. The app goes back to the version it had before the last update and turns Update automatically off, so the newer one isn't installed again. Turn the switch back on, or click Check now, when a fixed version is out. Any older release is also on the [releases page](https://github.com/JeskoMts/Instance-Manager/releases).

### Leftover `.im-old` files or an `.im-update` folder

They are left over from an update and are deleted the next time the app starts. You can also delete them yourself while the app is closed.

## Adding accounts

### The login window is blank or never opens

The Add account window is a Microsoft Edge WebView2 browser. If the WebView2 Runtime is missing, the window says so and shows a download button; install the Evergreen Runtime and open Add account again. If the runtime is installed but the page stays blank, repair it with the same download, or restart Windows if a WebView2 update is half finished.

### You signed in but the account wasn't added

After you sign in, the app asks Roblox which account the login belongs to and saves the account only if that works. It fails without internet, when a firewall blocks `users.roblox.com`, or when Roblox rate-limits you after many logins in a row; wait a minute and try again. It also fails if you close the window before the Roblox home page has loaded.

### You added an account that's already in the list

That's fine. Accounts are matched by Roblox user ID, so the existing entry gets the new login and keeps its nickname, notes and groups. This is the normal way to fix an expired login.

### The login window remembers a previous account

It shouldn't, because the login profile is InPrivate and wiped every time the window closes. To be sure, close the app and delete `%LOCALAPPDATA%\Instance Manager\webview`. It is created again at the next login.

### Memory use jumps while the login window is open

The login window is a full Edge browser and can use a few hundred megabytes. It shuts down completely when the window closes.

## Launching

### Roblox closes after minimizing or shortly after launch

In older versions, Auto Reconnect could close a healthy Roblox window about 22 seconds after launch. It mistook harmless network diagnostics for a lost game connection. Minimizing could make the timing look connected to the window state.

Version 1.1.3 includes the fix: network diagnostics are ignored, a kick or error cannot close a client before it starts joining a game, and clients opened on the home screen are left alone. Replace the older executable with the 1.1.3 download. If it still happens on 1.1.3, check `%APPDATA%\Instance Manager\auto-reconnect.log` at the time Roblox closed and Windows Logs > Application for a Roblox crash entry; those distinguish a reconnect action from Roblox exiting by itself.

### An account fails right away

Almost always its login expired. Roblox ends logins after some time, after a password change and when you sign out of all sessions. The row then shows Login expired; click Add again and sign in to that account. The other accounts in the same launch still start.

### Accounts worked on your old PC but all fail on the new one

Logins are encrypted for one Windows user on one PC. A copied `accounts.json` keeps names, notes and groups, but the logins can't be decrypted on another PC or under another Windows user. Add each account again on the new PC.

### The launch bar rejects your game link or Place ID

In Game mode the field takes a Place ID, a link like `roblox.com/games/<id>/...` or a link with `?placeId=<id>`. Shortened links from other sites don't work. Open the game on the Roblox website and copy the link from the address bar, or copy just the number.

### A server link or Job ID is rejected

Switch the launch bar to Server first. It takes a full server link, a private server link or a Job ID link. Only HTTPS links on Roblox domains are accepted, with at most five redirects, so links from URL shorteners and plain HTTP links are refused.

A private server link of the form `roblox.com/share?code=...` is looked up through Roblox, which answers only for a signed-in account. The app uses the saved login of one of the accounts you are launching. If it says that Roblox didn't accept the saved logins, sign in to an account again; Check sessions shows which logins expired. "This private server link has expired" means the owner made a new link, so ask for the current one.

### Every account fails at once

Then the cause is shared. Check that a Roblox version is selected under Settings > Launching, that you're online (every launch asks Roblox for a fresh ticket), and that Roblox itself isn't down.

## Several clients at once

### A second account closes the first one

Roblox allows one client per PC and enforces it with two named system objects. Instance Manager holds both so more windows can open. If a Roblox client was already running when the app tried to take them, it can't, and you get one window at a time. The app says so with "Only one window at a time" as soon as a launch would need a second window. To fix it:

1. Close every Roblox window, including any `RobloxPlayerBeta.exe` still listed in Task Manager. Roblox can also stay in the tray or start with Windows, so quit it from its tray icon too.
2. Close any other account manager or multi-instance tool.
3. Make sure Multi-instance is on under Settings > Launching, then launch again.

## Roblox versions

### No Roblox version is found

The app uses only officially signed Roblox clients. It looks for `RobloxPlayerBeta.exe` under `%LOCALAPPDATA%\Roblox\Versions` and checks that Roblox Corporation signed it. Install Roblox or start it once so a client exists, or point the app at the right folder under Settings > Launching.

### A pinned version can't be found after a Roblox update

Roblox deletes old versions when it updates. Open the account's ⋯ menu, choose Roblox version and switch it back to the default.

### A custom versions folder is refused

The app runs programs from that folder, so the rules are strict. The folder must be a full path on a drive inside your PC. Network paths like `\\server\share`, mapped network drives, USB and other removable drives and relative paths are refused. Inside the folder, each `RobloxPlayerBeta.exe` must have exactly that name, must not be a symbolic link or junction and must carry a valid Roblox signature.

## Auto Reconnect

Every attempt is logged in `%APPDATA%\Instance Manager\auto-reconnect.log`, so start there:

```
2026-06-23 12:15:04  RECONNECT  'MainAlt' (userId 123)  trigger=Kick  attempt 1/3  -> placeId=920587237
2026-06-23 12:15:09  RESULT  'MainAlt' (userId 123)  attempt 1  started
2026-06-23 12:16:40  GIVEUP  'MainAlt' (userId 123)  trigger=Error  retry limit (3) reached
```

### An instance dropped and didn't come back

Go through these in order:

1. Is the Auto Reconnect main switch on? It is off by default.
2. Did you stop the account from Instance Manager? Then it isn't restarted. Leaving a game through the Roblox menu isn't a drop either: Roblox stays open on its home screen, and the account comes back only if that window closes or crashes later.
3. Is there a `GIVEUP` line? Then it reached the retry limit. Raise the limit in Settings, or launch the account again to reset the count.
4. Is the matching option on? Kicks and errors fall under Reconnect after Kick/Error, crashes under Reconnect after Instance Crash.
5. Was the account opened on the Roblox home screen, with an empty launch bar? Auto Reconnect leaves those alone, because there is no game to return to.
6. Is there a `SKIP` line? The client closed within ten seconds of another account's reconnect and was left closed. Launch it again.

If the log has no line at all for the drop, the app didn't see it as one, which points to the log reading covered below.

### Only some of several accounts came back

Each instance follows its own Roblox log, and a fresh client can take a few seconds to create one. The app already waits for each client to open and join before it starts the next. If you still see this, raise Pause between launches above the default of 2 seconds.

### An instance keeps reconnecting

The drop keeps happening, for example in a game that kicks you on join or with a full private server. Auto Reconnect stops at the retry limit. Stop the instance yourself or lower the limit while you sort out the target.

### Clients restart although nothing happened

Versions before 1.1.2 read a line of Roblox's own network diagnostics as a lost connection and restarted healthy clients about 20 seconds after each launch. If `auto-reconnect.log` shows `RECONNECT ... trigger=Error` again and again about half a minute apart, update to the current version.

### Auto Reconnect never reacts

It reads the Roblox logs in `%LOCALAPPDATA%\Roblox\logs`. If a cleanup tool deletes that folder while you play, or another tool locks it, there is nothing to read.

## Automation

### An automation didn't start or close on time

Schedules run only while Instance Manager is open, in the window or in the tray. Check these:

1. Is the automation's Schedule switch on, and does the line under its name say what happens next? "Enter times like 18:30" means a time field has a typo.
2. Is today one of its days? No picked day means every day. A close time before the start time belongs to the next day.
3. Was the app closed or the PC asleep at that time? A start or close still happens if the app is back within 10 minutes. After that it waits for its next turn.
4. Did a notification say the automation "didn't run"? It names the reason, for example no accounts picked or a game link that couldn't be read.

Run on the automation tries it right away, which is the quickest way to test its accounts and destination.

### Accounts from an automation closed by themselves

Its schedule has Close set to After or At, so the accounts close that many minutes after the last start, or at that time. Choose Never to leave them running, or turn the schedule off. "Close after" also counts from a start with Run while the schedule is on; Close on the automation cancels it.

## Graphics

### The options don't show up in Roblox

- FastFlags are written only while Apply FastFlags is on.
- They are written right before a launch, so clients that were already running need to be started again.
- Custom flags that aren't on the allowlist are skipped, and the Custom flags box lists them.
- Roblox sometimes renames or drops FastFlags in an update. A flag Roblox no longer knows has no effect.

### The FPS cap changes back

The app writes the FPS cap into Roblox's settings before each launch, so a value chosen inside Roblox is overwritten at the next launch. Set FPS cap to Roblox default if you'd rather choose it inside Roblox.

## Anti-AFK

### Anti-AFK doesn't reach a client

Anti-AFK works only on clients the app started, and it has to bring each window to the front to press the key. When Windows refuses to switch windows, that client is skipped for the round. Send now under Settings > Reconnect & AFK says how many windows it reached. If your game reacts to the key, click the Key button and press a different one.

### The Key button refuses a key

The Windows keys and the media, browser and launch keys can't be used, because they would act outside Roblox, and you get a "Didn't work" notification. Esc cancels the choice and isn't picked as the key. Every other key on the keyboard works.

## Themes

### A theme code is rejected

Theme codes are checked before they are applied. A code that was cut off while copying, edited by hand or mixed with other text is refused. Copy the whole code again and paste it in one piece.

### A theme is hard to read

Switch to a built-in theme under Settings > Appearance, then fix the colors of your own theme under Custom colors.

## Data and resetting

### Settings don't save

The app writes `settings.json` in `%APPDATA%\Instance Manager`. If antivirus or a sync tool like OneDrive locks the file, or it is read-only, the save is skipped instead of crashing the app. Make sure the file isn't read-only and exclude the folder from antivirus and sync tools. The same goes for the other JSON files there.

### Resetting one thing

Each kind of data is a separate file. Close the app and delete the one you want to reset:

| File | What deleting it does |
|---|---|
| `settings.json` | Resets all settings |
| `accounts.json` | Removes all accounts |
| `groups.json` | Removes all groups, the accounts stay |
| `favorites.json` | Removes all favorites |
| `themes.json` | Removes your own themes |
| `automations.json` | Removes all automations |

### Starting completely fresh

Close the app and delete `%APPDATA%\Instance Manager` and `%LOCALAPPDATA%\Instance Manager`. The first holds your data and settings, the second the login browser data, the avatar cache and the copy kept for Roll back.

## Reporting a bug

Bug reports go to the [Instance Manager Discord](https://discord.gg/8XyKcZdSGe). These help:

- `auto-reconnect.log` for anything about reconnecting
- the exact text of the notification
- your Windows version and the app version, shown under Settings > Updates

Never post a Roblox login cookie, a launch ticket or someone else's account details.

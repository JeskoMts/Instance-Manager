# Troubleshooting

This guide covers the problems people actually run into with Instance Manager, what causes each one, and how to get past it. The entries are grouped by where you hit them: starting the app, adding accounts, launching, multi-instance, versions, Auto Reconnect, themes, and the data files underneath.

Most of the time the fix is one of three things: refresh an expired cookie by re-adding the account, free up the Roblox singleton so parallel instances work again, or repair the WebView2 runtime so the login window opens. The rest of this document is the long version.

Two things are worth knowing before you start, because half the entries below point at them:

- The app keeps its data in `%APPDATA%\Instance Manager`. That is where `settings.json`, `accounts.json`, and `auto-reconnect.log` live.
- Auto Reconnect reads (never writes) the Roblox client logs in `%LOCALAPPDATA%\Roblox\logs`, and the version picker reads the clients in `%LOCALAPPDATA%\Roblox\Versions`.

You can paste either path straight into the File Explorer address bar.

## Quick reference

| Symptom | Section |
|---|---|
| App will not start, or Windows asks for .NET | [Starting the app](#starting-the-app) |
| Login window is blank or never opens | [Adding accounts and login](#adding-accounts-and-login) |
| You sign in but the account is not added | [Adding accounts and login](#adding-accounts-and-login) |
| An account fails the moment you launch it | [Launching accounts](#launching-accounts) |
| Accounts worked on the old PC, fail on the new one | [Launching accounts](#launching-accounts) |
| The launch box rejects your link or id | [Launching accounts](#launching-accounts) |
| A second account closes the first instead of running alongside | [Multi-instance](#multi-instance) |
| Version not found, or a custom versions folder is refused | [Roblox versions](#roblox-versions) |
| An instance dropped and did not come back | [Auto Reconnect](#auto-reconnect) |
| A theme code is rejected on import | [Themes](#themes) |
| Settings do not stick, or you want a clean slate | [Data, settings, and a clean reset](#data-settings-and-a-clean-reset) |

## Starting the app

**The app does not start and Windows offers to download .NET.**
A published build needs the .NET 8 Desktop Runtime, x64. If it is missing, Windows shows a dialog with a download link the first time you run the exe. Install the Desktop Runtime (not the ASP.NET or the plain console runtime), then start the app again. If you are running from source instead, you need the .NET 8 SDK and you start it with `dotnet run --project src/InstanceManager/InstanceManager.csproj`.

**The app starts but the version bar is empty and launches fail with no usable version.**
Instance Manager only launches officially signed Roblox clients. It looks for `RobloxPlayerBeta.exe` under `%LOCALAPPDATA%\Roblox\Versions` by default, and it verifies the Authenticode signature and that the signer is Roblox Corporation. If Roblox is not installed, or it sits somewhere else, the bar comes up empty. Install or run Roblox once so a client exists, or point the app at the right folder with the versions override in Settings (see [Roblox versions](#roblox-versions) for the rules on what that folder may be).

**The window opens off-screen or at an odd size.**
Window width and height are remembered in `settings.json`. If a monitor was unplugged or the resolution changed, the saved size can land badly. Close the app, open `%APPDATA%\Instance Manager\settings.json`, and delete the `WindowWidth` and `WindowHeight` lines (or just delete the file to reset everything). The app rewrites sensible values on the next start.

## Adding accounts and login

**The login window is blank, white, or never opens.**
The Add Account window is a Microsoft Edge WebView2 browser pointed at the real Roblox login page. If the WebView2 runtime is missing or broken, the window cannot render. When the runtime is missing outright, the dialog says so and shows a Download button that takes you to `https://developer.microsoft.com/microsoft-edge/webview2/`. Install the Evergreen runtime, close the dialog, and open Add Account again. If the runtime is present but the page is still blank, the install is likely corrupted: repair it from the same download, or reboot if a half-finished WebView2 update is in the way.

**Sign-in works but the account is not added.**
After you log in, the app reads the `.ROBLOSECURITY` cookie, checks it against Roblox to confirm who you are, and only then stores the account. That check is a network call. If it fails, the account is not saved. Common causes:

- No internet, or a firewall is blocking the app from reaching `users.roblox.com`.
- Roblox is rate-limiting you (too many sign-ins in a short window). Wait a minute and try once more.
- You closed the window the instant the page finished, before the cookie was actually set. Sign in fully, wait for the Roblox home page to load, then let the capture run.

**You added an account that already exists.**
That is fine and it is the intended way to refresh a stale cookie. Accounts are matched by Roblox user id, so re-adding one updates the existing entry with the fresh cookie, username, and display name instead of making a duplicate. If a launch started failing, re-adding the account is usually the fix.

**The login window remembers a previous session.**
It should not. The login profile is InPrivate and the browsing data is wiped on every exit path: a successful capture, Cancel, the title-bar close, and errors all clear it. If you ever want to be certain, close the app and delete `%LOCALAPPDATA%\Instance Manager\webview`; it is rebuilt clean on the next Add Account.

**Memory use jumps while the Add Account window is open.**
That is expected. The login window is a full Edge WebView2 browser, and its host processes can use several hundred megabytes while sign-in is open. The dialog creates that browser on open and tears it down on close, so the memory is handed back as soon as the window goes away. If you want the app at its smallest footprint, just close the login dialog when you are done.

## Launching accounts

**An account fails the moment you launch it.**
The usual cause is an expired session cookie. Roblox invalidates cookies over time, after a password change, or when you sign out of that account somewhere else. Remove the account and add it again to capture a fresh cookie. A single failed account never stops the rest of the run: if you launch a group of ten and one cookie is dead, the other nine still start, and the status line reports how many started and how many failed.

**Accounts worked on the old PC but every one fails on the new PC.**
Cookies are encrypted with Windows DPAPI, scoped to your Windows user on that one machine. Copying `accounts.json` to a different PC, or to a different Windows user on the same PC, leaves the encrypted cookies unreadable, so each affected account fails at launch. There is no way around this by design; it is what keeps a copied file from being usable elsewhere. Add the accounts again on the new machine. Everything except the cookie (usernames, aliases, notes, groups) does carry over in the file, so only the sign-in has to be redone.

**The launch box rejects your game link or place id.**
The public-game box accepts plain digits, a `games/<id>` style link, and a `?placeId=<id>` style link, and it confirms the place id is real before launching. If it bounces your input, it is usually a shortened or redirected link from somewhere other than Roblox, or a typo in the id. Open the game on the Roblox site and copy the link straight from the address bar, or read the number out of the URL and paste just that.

**A private server or job id is rejected.**
Switch the box to Job ID mode first. It takes either a full private-server link or a raw job id (a GUID). Server links are validated hard on purpose: only HTTPS, only Roblox domains, and at most five redirects, each one re-checked. A link that points off Roblox, uses plain HTTP, or comes from a third-party URL shortener is refused. Use the full `https://www.roblox.com/...` private-server link, or paste the job id GUID on its own.

**Nothing launches at all and every account is counted as failed.**
If the whole run fails rather than one account, the problem is shared, not per-account. Check, in order: that a valid Roblox version is selected in the version bar (see [Roblox versions](#roblox-versions)), that you actually have internet (the launch fetches a fresh auth ticket from Roblox per account), and that Roblox is not down. The status line and a toast summarize the result; for a per-account reason, the failures are the same expired-cookie and missing-version cases listed above.

## Multi-instance

**Launching a second account closes the first one instead of running both.**
Roblox normally allows a single client per machine, enforced with two named singleton objects, `ROBLOX_singletonEvent` (current) and `ROBLOX_singletonMutex` (legacy). Instance Manager holds both open so extra launches stop replacing each other. If something else already owns one of those names, the app cannot take it over and the parallel behavior is simply not active. The usual culprit is a Roblox client that was already running before the app, or another multi-instance helper that grabbed the names first.

To recover:

1. Close every running Roblox client, including any stuck `RobloxPlayerBeta.exe` left in Task Manager.
2. Close any other account manager or multi-instance tool.
3. In Instance Manager, confirm `MultiInstanceEnabled` is on (Settings), then launch again. The app re-takes the grip on the next launch or when you toggle the setting.

This feature is best-effort by design. The app still launches when it cannot hold the singletons; you just get one client at a time until the names are free.

**Multi-instance is on, but how do I know the grip is actually held?**
Launch two accounts. If both windows stay open, the grip is held. If the second replaces the first, something else owns a name (see above). The setting being on only means the app will try; whether it succeeds depends on what else is running.

## Roblox versions

**A pinned version is "not found" after a Roblox update.**
If you pinned a specific client to an account and Roblox has since updated, that exact version folder may no longer exist on disk, because Roblox removes old versions when it patches. Clear the preferred version on the account row so it follows the global selection again, or pick the current version in the version bar. The same applies to the global selection if it points at a version that was cleaned up.

**A custom versions folder is refused.**
The override in Settings is deliberately strict, because the app is about to run executables out of that folder. A folder is rejected unless it is a fully qualified directory on a local fixed drive. These are all turned away before the app even looks inside:

- UNC and network paths (`\\server\share\...`). These are blocked so the app never triggers a Windows credential handshake against a remote server.
- Mapped network drives, removable drives, and non-fixed drives.
- Relative or device-style paths.

Even inside an accepted folder, each `RobloxPlayerBeta.exe` is checked again: the filename must be exactly that, the path may not use reparse points, the Authenticode signature must pass Windows trust, and the signer must be Roblox Corporation. Point the override at a normal local copy of `Roblox\Versions` on a fixed drive and these checks pass. If a specific client still fails, its signature is missing or it is not a genuine Roblox build.

## Auto Reconnect

Auto Reconnect watches each instance and brings it back when it drops out of a game. Every attempt, result, and give-up is written to `auto-reconnect.log` in `%APPDATA%\Instance Manager`, so when a reconnect does or does not happen, that file is the first place to look. A line looks like this:

```
2026-06-23 12:15:04  RECONNECT  'MainAlt' (userId 123)  trigger=Kick  attempt 1/3  -> placeId=920587237
2026-06-23 12:15:09  RESULT  'MainAlt' (userId 123)  attempt 1  started
2026-06-23 12:16:40  GIVEUP  'MainAlt' (userId 123)  trigger=Error  retry limit (3) reached
```

**An instance dropped but did not reconnect.**
Auto Reconnect has a few intentional limits. Walk this list:

- **You stopped it yourself.** A manual stop always wins; the app never reconnects an instance you deliberately stopped. Relaunch it by hand if you want it back.
- **The retry limit was reached.** Each instance gets a set number of tries per run (default 3, range 1 to 20). A `GIVEUP` line in the log means it hit that ceiling. Raise the limit on the Settings tab, or relaunch the account to reset its counter.
- **The matching trigger is off.** There are two toggles: one covers Kick and Error together, the other covers Crash. If the event that happened is behind a toggle you turned off, nothing fires. Turn the relevant toggle on.
- **The master switch is off.** It sits above both toggles and disables the whole feature in one click. Make sure it is on.

If the log shows no line at all for the drop, the event was not recognized as a drop. That points at the log-reading side rather than the settings, covered next.

**Several accounts dropped close together and only some came back.**
Each instance is bound to its own Roblox log file so a kick on one account reconnects only that account. Binding takes a moment because a fresh client can take a few seconds to create its log, and two clients started in the same instant briefly compete for the same files. The launch delay is what gives each client room to create its own log first. The default of 1500 ms is comfortable; if you have lowered it close to zero and see uneven reconnects, raise it back to around 1500 ms in Settings. Launching a large group with a tiny delay is the one situation where this still shows up.

**An instance keeps reconnecting in a loop.**
That means the drop keeps happening: a game that kicks you on join, a private server that is full or closed, or a place that crashes the client immediately. Auto Reconnect will retry up to the limit and then give up with a `GIVEUP` line, so the loop is bounded. If you do not want it retrying a known-bad target, stop the instance (a manual stop is never reconnected) or lower the retry limit while you sort out the target.

**Auto Reconnect never reacts to anything.**
The feature reads the Roblox client logs in `%LOCALAPPDATA%\Roblox\logs`. If that folder is redirected, locked by another tool, or cleared aggressively by a cleaner while the app runs, there are no lines to read and nothing reconnects. Make sure Roblox is logging there normally and that no disk-cleanup tool is wiping the folder mid-session.

## Themes

**A theme code will not import.**
Imported theme codes are validated before they are applied, so a code that was truncated when it was copied, came from a newer or incompatible build, or was edited by hand is rejected rather than applied half-broken. Ask for the full code again and paste it in one piece. Very large clipboard contents are also refused on purpose, so make sure you are pasting an actual theme code and not a wall of unrelated text.

**A theme looks wrong or unreadable.**
A custom theme is fifteen colors and nothing stops a combination that is hard to read. Switch back to a built-in theme on the Themes tab (the change is live, no restart), then edit your custom one from there.

## Data, settings, and a clean reset

**Settings do not save, or a change does not stick.**
Settings are written to `%APPDATA%\Instance Manager\settings.json`. Writes are atomic and best-effort: if an antivirus product or a cloud-sync client (OneDrive, Dropbox, and similar) has the file locked, or the file was made read-only, the save is skipped rather than crashing the app, and your change is lost on exit. Check that `settings.json` is not read-only, and add `%APPDATA%\Instance Manager` as an exclusion in your antivirus or sync tool. The same applies to `accounts.json`, `groups.json`, `favorites.json`, and `themes.json`.

**You want to reset one thing without losing everything.**
Each kind of data is its own file in `%APPDATA%\Instance Manager`. Close the app, delete the one you want to clear, and start again:

- `settings.json` resets all settings (delay, theme id, Auto Reconnect, window size) to defaults.
- `accounts.json` removes all accounts.
- `groups.json` removes all groups (accounts survive, just ungrouped).
- `favorites.json` removes saved games.
- `themes.json` removes your custom themes (built-in themes are in the app, not this file).

**You want a full, clean reinstall.**
Close the app, then delete both `%APPDATA%\Instance Manager` and `%LOCALAPPDATA%\Instance Manager`. The first holds your data and settings; the second holds the WebView2 login profile. With both gone, the next start is a fresh install and you add your accounts from scratch.

## Collecting information for a bug report

If you need to report something, the useful artifacts are:

- `auto-reconnect.log` in `%APPDATA%\Instance Manager`, for anything about reconnecting. It already strips control characters and rotates at one megabyte.
- The exact text on the status line and any toast when the problem happened.
- Your Windows version and whether you are on a published build or running from source.

Remove anything sensitive first. Never paste a `.ROBLOSECURITY` cookie, an authentication ticket, or another person's account details into a public report. The reporting process and what to redact are in the [security policy](../SECURITY.md).

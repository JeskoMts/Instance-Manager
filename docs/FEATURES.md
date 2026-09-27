# Features

This page covers the main parts of Instance Manager and how to use them. The app has three tabs: Accounts, Games and Settings. Most of the time you'll be on Accounts.

## Accounts

Each account stores its Roblox user ID, username and display name, an optional nickname and notes, and the encrypted session cookie that lets the app start Roblox as that account.

To add one, click Add account and sign in on the Roblox login page that opens. The app reads the login cookie, confirms with Roblox which account it belongs to, stores it encrypted and closes the window. It never sees your password. If you add an account that is already in the list, the existing entry gets the new login instead of a duplicate being created.

Roblox logins expire, for example after a password change or "Log out of all sessions". When that happens, the account's row shows Login expired and an Add again button. Sign in once more and the account keeps its nickname, notes and groups.

Open an account's ⋯ menu, or right-click the row, to rename it, pin a Roblox version, change its groups or remove it. Renaming only changes the name shown in the app, not the Roblox username. A removed account can be restored with Undo on the notification that follows.

## Groups

Groups keep related accounts together, for example all alts for one game. Each group has a name and a color, can be collapsed, and can be started as a whole with Launch group in its header.

Click New group to create one, then drag accounts onto its header or tick groups in an account's ⋯ menu. An account can be in more than one group. When you drag an account that already belongs to a group onto another one, the app asks whether to move it or add it to both. Dragging an account onto the Ungrouped header takes it out of every group. Deleting a group keeps its accounts.

## Launching

The launch bar at the bottom of the Accounts tab decides where the selected accounts go.

In Game mode, paste a Roblox game link or type a Place ID, and every account joins a public server of that game. In Server mode, paste a server link, a private server link or a Job ID link to put every account into that one server. Server links are checked before use: the app only accepts HTTPS links on Roblox domains and follows at most five redirects.

Select the accounts you want by clicking their rows, or use the checkbox in the list header, then click Launch or press Ctrl+Enter. Accounts start one after another. The app waits until each Roblox window is open and has joined the game before it starts the next one, and you can add an extra pause in Settings. Each row shows what its account is doing (queued, opening Roblox, joining, running or failed) and the launch bar shows the overall progress. Cancel stops the batch after the account that is currently starting.

If one account fails, for example because its login expired, the others still start. The summary at the end says how many started, failed or were already running.

Running accounts show a green dot. The red stop button on a row closes that account's Roblox window, and Stop all above the list closes every one.

## Favorites

Favorites are games you play often. Enter a game in the launch bar and click the star to save it under a name. A favorite can also carry a private server, so picking it fills in Server mode for you. Open the favorites list from the launch bar to pick, edit, reorder or delete them. Pinned favorites stay at the top, and the favorite you picked last is selected again the next time you start the app.

## Games tab

The Games tab shows a grid of popular Roblox games with a search box. Clicking a game makes it your launch target. If you turn on "Switch to Accounts after picking a game" in Settings, the app also jumps back to the Accounts tab.

## Running several clients at once

Roblox normally closes the first client when you open a second one. Instance Manager holds the two system objects Roblox uses to enforce this, so every account can have its own window. The switch for this is under Settings, Launching, and it is on by default.

This only works if no Roblox client was already open when the app took hold of those objects. If one was, close every Roblox window and launch again.

## Auto Reconnect

Auto Reconnect restarts an account when it drops out of its game. It has a main switch and two options:

- Reconnect after Kick/Error covers kicks, removals, moderation messages, error 267, lost connections, servers that shut down and being sent back to the Roblox menu.
- Reconnect after Instance Crash covers a Roblox client that closes unexpectedly while you're in a game.

The app reads each client's own Roblox log to find out what happened, so a kick on one account only restarts that account. The retry limit sets how many times one instance may reconnect before the app gives up. It goes from 1 to 30, the last step on the slider means no limit, and the default is 3. An instance you stop yourself is never restarted.

Every attempt, result and give-up is written to `auto-reconnect.log` in `%APPDATA%\Instance Manager`.

## Roblox versions

Under Settings, Roblox version, you choose which installed Roblox client the app uses. The refresh button looks for installed versions again, which is useful right after Roblox updated. An account can be pinned to a different version from its ⋯ menu; pinned accounts show the version as a small tag.

If your Roblox versions live somewhere other than `%LOCALAPPDATA%\Roblox\Versions`, set a custom folder. It has to be on a drive inside the PC. Network shares and removable drives are refused, and every `RobloxPlayerBeta.exe` must carry a valid Roblox signature.

## Themes

Settings, Appearance, holds the built-in color themes and your own. Click a theme to use it, drag it to change the order, or open the editor to create one. A custom theme can be exported as a short code and imported by someone else.

## Updates

On every start the app checks GitHub for a newer release. If it finds one, it downloads it, verifies the checksum, replaces its own files and restarts, which usually takes a second or two. If you are launching accounts or have Roblox instances running at that moment, the update is installed when you close the app instead. After an update you'll see a notification with the new version number.

## Other settings

- Notifications: how long pop-ups stay on screen, and which ones to mute. Muted notifications still appear in the bell.
- Confirmations: skip the "are you sure?" question for actions like removing an account or deleting a group.
- Data & privacy: opens the folder where the app keeps its data.

## Keyboard shortcuts

| Keys | Action |
|---|---|
| Ctrl+1, Ctrl+2, Ctrl+3 | Accounts, Games, Settings |
| Ctrl+F | Search accounts |
| Ctrl+A | Select all visible accounts |
| Ctrl+Enter | Launch the selected accounts |
| Esc | Clear the search, then the selection |

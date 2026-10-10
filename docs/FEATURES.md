# Features

[README](../README.md) · Features · [Troubleshooting](TROUBLESHOOTING.md) · [Data storage](DATA-STORAGE.md) · [Security](SECURITY.md) · [Architecture](ARCHITECTURE.md) · [Changelog](../CHANGELOG.md)

This page walks through Instance Manager in the order you meet it in the app: the four pages in the sidebar, then every section of Settings. The button in the title area opens and closes the sidebar.

## Accounts

Most of the time you'll be on the Accounts page. It lists your accounts, sorted into groups, with the launch bar at the bottom.

### Adding an account

Click Add account and sign in on the Roblox login page that opens. The app reads the login cookie, asks Roblox which account it belongs to, stores it encrypted and closes the window. It never sees your password. Adding an account that is already in the list gives the existing entry the new login, so its nickname, notes and groups stay.

Each account keeps its Roblox user ID, username and display name, an optional nickname and notes, and the encrypted session cookie that lets the app start Roblox as that account.

### Expired logins

Roblox logins end after some time, after a password change and after "Log out of all sessions". The row then shows Login expired, with the reason on hover, and an Add again button. Sign in once more and the account keeps everything else.

Check sessions, at the top of the page, asks Roblox about every saved login at once and marks the expired ones, so you find out before a launch fails. If Roblox can't be reached, the rows stay as they are.

### The account menu

Open an account's ⋯ menu, or right-click its row, to rename it, pin a Roblox version, change its groups, move it to another server or remove it. Renaming changes only the name shown in the app. A removed account can be brought back with Undo on the notification that follows.

### Groups

Groups keep related accounts together, for example all alts for one game. Each group has a name and a color, can be collapsed, and starts as a whole with Launch group in its header. Pick the color in the color field and hue bar, or type a hex code.

Click New group to create one, then drag accounts onto its header or tick groups in an account's ⋯ menu. An account can be in more than one group. If you drag an account that already has a group onto another one, the app asks whether to move it or keep it in both. Dropping an account on the Ungrouped header takes it out of every group, and deleting a group keeps its accounts.

### Launching

The launch bar decides where the selected accounts go.

- Game mode takes a game link or a Place ID. Every account joins a public server of that game.
- Server mode takes a server link, a private server link or a Job ID link and puts every account into that one server. Private server links work in both forms, `roblox.com/share?code=...&type=Server` and the older `roblox.com/games/<id>?privateServerLinkCode=...`, and so do invite links. Roblox opens a share link only for a signed-in account, so the app looks it up with the saved login of one of the accounts you are launching. The app accepts only HTTPS links on Roblox domains and follows at most five redirects.
- An empty field opens the Roblox home screen.

Select accounts by clicking their rows or with the checkbox in the list header, then click Launch. The accounts start one after another. The app waits until each Roblox window is open and has joined the game, then waits the pause from Settings (2 seconds by default) before the next one. Each row shows what its account is doing, and the launch bar shows the progress. Cancel stops the batch after the account that is starting at that moment.

If one account fails, for example because its login expired, the others still start. The summary at the end counts the started, failed and already running accounts.

Running accounts show a green dot, and the stop button on a row closes that account's Roblox window. While anything runs, the list header shows "3 running | Stop all". It also shows how many accounts are selected, and the × next to that count clears the selection.

### Moving to another server

Join another server, in the ⋯ menu for the selected accounts, closes those accounts' Roblox windows and starts them again in a different public server of the game in the launch bar. The app picks a server that isn't the current one and has enough free slots for all of them.

### Favorites

Favorites are games you play often. Enter a game or server link in the launch bar and click the star. The app fills in the game's name from Roblox without update tags like "[UPD]", and you can change it before saving. A favorite can carry a server or a private server, so picking it switches the launch bar to Server mode. The favorites list in the launch bar lets you pick, edit, reorder, pin or delete favorites. The one you picked last is selected again the next time the app starts.

## Games

The Games page shows a grid of popular Roblox games with a search box. Clicking a game makes it the launch target and takes you back to Accounts. Turn off "Switch to Accounts after picking a game" under Settings > General to stay on the Games page instead.

The grid adds columns as the window gets wider, and every card keeps the 16:9 shape of Roblox thumbnails, so the pictures are never cut off. The popular games and your last searches are kept for five minutes, so clearing the search or searching again shows them at once.

## Automation

An automation is a saved set of accounts plus where they go: a game, a server or the Roblox home screen. It can also start and close those accounts on its own at set times.

The list on the left shows every automation with its accounts, destination and schedule. The ▶ button next to one starts it right away. While any of its accounts run, it shows a green dot and a stop button instead.

### Creating one

New automation starts with the accounts selected on the Accounts page and the game or server in the launch bar. So the quickest way is to select the accounts, set the game, open Automation and click New automation. The name field is ready to type in.

- Accounts: tick accounts in the grid, find one by name, select or clear all accounts shown, or click a group to add or remove all of its accounts at once.
- Destination: Game, Server or Home screen, with the game link, Place ID or server link. The game's name appears under the field. A favorite fills it in with one click, and Use launch bar target copies the launch bar.

Run starts the accounts one after another, the same way the Launch button does. Close closes their Roblox windows. Duplicate makes a copy with its schedule turned off, and Delete can be undone from the notification that follows.

### Schedules

The schedule is off until you turn it on. It has:

- the days, where Every day, Weekdays and Weekends set them in one click and no picked day means every day
- a start time on the 24-hour clock, which you can turn off to only close accounts at a set time
- Close again: keep the accounts open, close them after a number of minutes (1 to 1440), or close them at a time

A close time that isn't after the start time belongs to the next day, so 22:00 to 02:00 works. "Close after" counts from the last start, including a start with Run while the schedule is on, and closing by hand cancels it. In the time fields, Up and Down change the time by 5 minutes, and by an hour with Shift.

The line under the name says what happens next, for example "Starts today at 18:00, closes today at 23:00." Schedules only run while Instance Manager is open, and that includes sitting in the tray. If the PC was asleep or the app was closed at the planned time, the start or close still happens when the app is back within 10 minutes; after that it waits for its next turn. A start that comes due during another launch waits for that launch to finish.

## Settings

The list on the left sorts the settings into three groups: App, Roblox and System. The search box above it finds the right page for a setting, for example "fps", "tray" or "afk", and opens it as soon as only one page matches. Changes are saved as soon as you make them.

### General

- Start with Windows opens Instance Manager when you sign in to Windows.
- Start in the tray keeps it in the tray instead of opening the window when it starts with Windows.
- Close to tray makes the close button hide the window, so automations and Auto Reconnect keep running. Quit from the tray icon's menu.
- Avatars shows each account's Roblox profile picture in the list. It is on by default. When it's off, rows show initials and nothing is downloaded.
- Switch to Accounts after picking a game controls what happens on the Games page.

Clicking the tray icon opens the window. Its menu can also close every Roblox window or quit the app.

Confirmations let you skip the "are you sure?" question, either for everything or only for some actions: removing an account, deleting a group, a favorite or a theme, stopping every running account and taking an account out of all groups.

### Appearance

Click a theme to use it, or drag it to change the order. New theme copies the current one so you can change it, and your own themes can be renamed, deleted, exported as a short code and imported from one.

Custom colors lists every color of the current theme with a swatch and its hex code. Click a swatch to open the color picker or type a hex code. Built-in themes stay as they are; Customize makes a copy you can change.

### Notifications

Pop-ups: Display time sets how long a notification stays on screen, from 0.5 to 5 seconds (3 by default). You can mute every notification or only some of them. Muted notifications still land in the bell.

### Launching

- Pause between launches is the extra wait after an account has opened and joined, from 0 to 30 seconds. Raise it if some accounts don't make it into the game.
- Multi-instance lets every account run in its own Roblox window. It is on by default.
- Default version is the Roblox client every account uses unless its ⋯ menu pins another one. The refresh button looks for installed versions again, which helps right after Roblox updated.
- Custom versions folder is for Roblox installs outside `%LOCALAPPDATA%\Roblox\Versions`. The folder has to be on a drive inside the PC, network and removable drives are refused, and every `RobloxPlayerBeta.exe` in it must carry a valid Roblox signature.

Roblox normally closes the first client when a second one opens. Instance Manager holds the two system objects Roblox uses for this, so every account keeps its window. That only works if no Roblox client was open when the app took them; otherwise close every Roblox window and launch again.

### Graphics

These options are written into Roblox's own files right before each launch, so they apply to every client the app starts.

- FPS cap sets Roblox's frame rate limit, the same value as the frame rate option inside Roblox. The page also shows what Roblox has saved right now. Roblox default leaves the value alone.
- Apply FastFlags turns the options below on or off as a whole: graphics API (Direct3D 11, Vulkan or OpenGL), graphics quality, anti-aliasing, texture quality, ignoring display scaling, hiding grass, a gray sky, frozen lighting and exclusive fullscreen with Alt+Enter.
- Custom flags takes a JSON object with more flags. Only flags on the allowlist of 18 rendering flags are used, and the others are listed as skipped.

FastFlags go into `ClientSettings\ClientAppSettings.json` inside each Roblox version folder. Turning Apply FastFlags off deletes that file again, unless something else has changed it since.

### Reconnect & AFK

Both keep accounts in their game while you're away.

Auto Reconnect starts an account again when it drops out of its game. It is off by default and has two options under its main switch:

- Reconnect after Kick/Error covers kicks, removals, moderation messages, error 267, lost connections and servers that shut down.
- Reconnect after Instance Crash covers a client that closes unexpectedly while in a game, including one ended in Task Manager.

Leaving a game yourself through the Roblox menu isn't a drop. Roblox goes back to its home screen and stays open, and if that window closes or crashes later, it counts as a crash. An account you stop from Instance Manager is never restarted.

Auto Reconnect covers accounts the app sent into a game or server. An account opened on the Roblox home screen is left alone, because there is no game to bring it back to.

The app reads each client's own Roblox log to find out what happened, so a kick on one account restarts only that account. It ignores Roblox's network diagnostics and whatever a game's own scripts print, and it counts a kick or error only once the client has started to join a game. The retry limit sets how often one instance may reconnect before the app gives up: 1 to 30, or no limit at the far right of the slider. The default is 3. Every attempt, result and give-up is written to `auto-reconnect.log` in `%APPDATA%\Instance Manager`.

Anti-AFK keeps idle Roblox clients from being kicked, which Roblox does after 20 idle minutes. Every few minutes (1 to 60, 15 by default) it brings each running Roblox window to the front and presses a key, Space unless you choose another. Click the Key button and press the key you want, any key on the keyboard; Esc cancels. The Windows keys and the media and browser keys can't be used, because they would act outside Roblox. Pick a key your game doesn't use for anything important. Switch back afterwards, on by default, returns to the window you were using after each round. Send now sends one round right away and says how many windows it reached. Its message is a normal notification and can be muted as "Anti-AFK messages" under Notifications.

Anti-AFK reaches only clients the app started, and it skips a window Windows won't bring to the front.

### Updates

The card at the top shows the version you're on, with What's new and Check now.

- Check at startup looks for a new version every time the app starts and installs it right away if nothing is running. The app restarts within a few seconds and names the new version in a notification.
- Check while running looks again in the background, every 60 minutes unless you change it (15 to 1440 minutes).
- Include pre-releases also offers test versions.
- An update found while you're busy installs when you close the app, or right away with Restart to update. Skip this version deletes its download, and Offer it again undoes the skip.
- Keep the previous version saves a copy of the version being replaced, and Roll back returns to it.

Both checks and Keep the previous version are on by default. Every download has to match the SHA-256 checksum GitHub publishes for it, and that check can't be turned off.

### Data & privacy

A short note on what the app stores and whom it contacts, and a button that opens the data folder. The app version is at the bottom.

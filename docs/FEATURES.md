# Features

This is the full tour of what Instance Manager can do, organized by area. Each section says what the feature is, how it behaves, and how to use it. For the short version, see the Features table in the [README](../README.md). For the design behind these features, see [ARCHITECTURE.md](ARCHITECTURE.md).

The app is organized into four tabs: Accounts, Games, Settings, and Themes. Most day-to-day work happens on the Accounts tab.

## Accounts

The Accounts tab is the home base. It holds every Roblox account you have added, grouped and searchable, with a running indicator on each one.

**What an account stores.** Each account keeps its Roblox identity (user id, username, display name), an optional alias and free-text notes, the encrypted session cookie, and a little launch metadata (sort order, a browser tracker id, and an optional preferred Roblox version). The avatar headshot is loaded from Roblox on demand and shown on the row.

**Adding an account.** Click Add Account. A window opens with the real Roblox login page inside an embedded browser. Sign in there exactly as you normally would. When the sign-in succeeds, the app reads the `.ROBLOSECURITY` cookie, checks it against Roblox to confirm the identity, stores the account in encrypted form, and clears the login browser session. The app never sees your username or password.

**Re-adding an account.** If you add an account that already exists (matched by Roblox user id), the app updates the existing entry with the fresh cookie and current username and display name instead of creating a duplicate. This is the normal way to refresh an account whose cookie has expired.

**Renaming.** Renaming sets an alias, which is what the row shows. It does not change the real Roblox username. Use it to label alts in a way that makes sense to you.

**Removing.** Removing an account deletes it and its encrypted cookie from disk. The action shows an Undo on its notification, so an accidental removal is one click away from being restored.

**How to use it.**
- Add: Accounts tab, Add Account, sign in.
- Rename: open a row's menu, choose rename, type an alias.
- Remove: open a row's menu, choose remove. Click Undo on the toast if it was a mistake.

## Groups

Groups keep related accounts together. They are colored, can be collapsed, and can be launched as a unit.

**Multi-group membership.** An account can belong to more than one group at once. When you drag an account that is already in some groups onto another group, the app asks whether to move it (drop its other memberships) or add it alongside them.

**Reordering and expansion.** Groups can be reordered, and accounts can be dragged into a new order. Whether a group is expanded or collapsed is remembered between sessions.

**Deleting.** Deleting a group keeps its accounts; they simply become ungrouped. The delete also has an Undo, which restores both the group and its memberships.

**Launching a group.** You can launch a whole group at once. An empty group reports that there is nothing to launch rather than doing nothing silently.

**How to use it.**
- Create: Create Group, pick a name and color.
- Add members: drag an account row onto a group header, or use the group's membership editor.
- Launch: use the group header's launch action.
- Collapse: click the group header's expander.

## Favorites

Favorites are saved games you launch often, so you do not have to paste a link every time.

**What a favorite holds.** A name, a place id, and an optional default private-server job id. A favorite can be pinned as primary, which floats it to the top of the list.

**Applying a favorite.** Selecting a favorite fills in the launch target for you. If the favorite has a default private server, it switches to private-server mode and fills that in too; otherwise it sets up a public launch by place id.

**Ordering.** Favorites sort with pinned ones first, then by your chosen order. You can move a favorite up or down, or drag to reorder within its pinned or unpinned partition.

**Editing and deleting.** Favorites can be edited (name, place, default server) and deleted. A delete has an Undo.

**Resume on startup.** The favorite you had selected when you closed the app is reselected and reapplied on the next start, so your launch target opens just as you left it.

**How to use it.**
- Save: enter a valid game target, then use the save/favorite action and give it a name.
- Apply: pick it from the favorites dropdown.
- Pin: toggle its primary star.
- Search: type in the favorites search box to filter the list.

## Launch targets

A target is where the selected accounts will go. There are two kinds.

**Public game.** Enter a Roblox game link or a place id. The app accepts plain digits, `games/<id>` style links, and `?placeId=<id>` style links, and validates that it is a real place id before launching.

**Private server.** Switch to Job ID mode and paste a full private-server link or a raw job id (a GUID). Server links are resolved and checked first: only HTTPS and only Roblox domains are accepted, redirects are followed at most five times, and each hop is rechecked, so a link that bounces off Roblox is rejected.

The last target you typed, the last join mode, and the last job id are all remembered between sessions.

**How to use it.**
- Public: type a link or id in the launch box and launch.
- Private: switch to Job ID mode, paste the server link or job id, and launch.

## Games tab

The Games tab shows a grid of popular Roblox games so you can pick a target without hunting for a link.

**Picking a game.** Clicking a game turns it into your current launch target as a public launch, exactly as if you had applied a favorite. If you have turned on "switch to Accounts after selecting a game" in Settings, the app also jumps to the Accounts tab so you can launch right away. The grid is warmed in the background at startup, so opening the tab is instant.

**How to use it.**
- Open the Games tab and click a game.
- Optionally enable the Settings switch so picking a game takes you straight to Accounts.

## Multi-instance launching

This is the headline capability: running several Roblox clients at once, each signed in as a different account.

**How parallel instances are possible.** Roblox normally allows one client per machine, enforced with two named singleton objects. Instance Manager holds both of them open while it runs, each on its own background thread, so additional launches no longer replace the first client. The feature is best-effort: if something else already owns one of those names, the app launches anyway, just without the parallel behavior until the name is free.

**Spacing launches out.** Launches happen one after another with a delay you set (the launch delay, from 0 to 60000 ms in 500 ms steps). This keeps the machine and the Roblox servers from being hit by a burst of simultaneous starts.

**Failure isolation.** If one account fails to launch (for example an expired cookie), it is counted as a failure and the run continues with the rest. One bad account never strands the others.

**How to use it.**
- Select the accounts you want, or launch a whole group.
- Click Launch. Watch the status line for progress and a summary at the end.
- Adjust the launch delay on the Settings tab.

## Running instances and stopping

Every instance the app starts is tracked, so you always know what is live.

**Indicators.** Running accounts are marked on the list, and the app shows a running count.

**Stopping.** You can stop a single account's instance from its row, or stop all running instances at once (with a confirmation). Stopping an instance yourself pauses Auto Reconnect for it, so a deliberate stop never bounces back.

**How to use it.**
- Stop one: use the stop action on a running row.
- Stop all: use Stop All and confirm.

## Auto Reconnect

Auto Reconnect watches your running instances and brings them back when they drop out of a game.

**What it reacts to.** Drops are sorted into three kinds:
- A kick or removal, including Roblox error 267 and moderation messages.
- A disconnect, a server that shut down or went offline, a generic error dialog, or being sent back to the Roblox menu.
- A crash, meaning the client process died while you were in a game with no clean exit.

**The switches.** There are two toggles plus a master switch, all on by default:
- Reconnect after Kick/Error covers the kick, removal, disconnect, error, and menu-drop cases.
- Reconnect after Instance Crash covers the process crash case.
- The master switch turns the whole feature off in one click.

**Retry limit.** Each instance gets a set number of automatic reconnects per run (1 to 20, default 3) before the app gives up on it.

**How it knows what happened.** The app tails the per-instance Roblox client log and reads its lines. Each running instance is bound to its own log file, so when you run several accounts at once, a kick on one of them reconnects only that one and leaves the others alone. Every attempt, result, and give-up is written to `auto-reconnect.log` in the data directory if you ever want to review it.

**Manual stop wins.** If you stop an instance yourself, Auto Reconnect does not bring it back.

**How to use it.**
- It works out of the box. To change it, open the Settings tab and find the Auto Reconnect section.
- Use the master switch to disable everything, or the two toggles for finer control.
- Set the retry limit with the slider.
- If an instance drops and does not come back, [TROUBLESHOOTING.md](TROUBLESHOOTING.md) lists the reasons (manual stop, retry limit, a disabled toggle) and where to read the log.

## Roblox versions

The app works with one or more installed Roblox clients.

**Global selection.** A version bar lets you pick which installed client to use for launches. The app can also check Roblox online for the latest version.

**Per-account version.** An account can pin a preferred version. When it launches, that version is used if it is installed; otherwise the launch falls back to the global selection.

**Custom path.** If your Roblox versions live somewhere other than the default `%LOCALAPPDATA%\Roblox\Versions`, set a versions path override in Settings.

**How to use it.**
- Pick the global version in the version bar.
- Set a per-account version from the version selector on its row.
- Change the versions folder in Settings if needed.

## Themes

The Themes tab controls the look of the app.

**Built-in and custom.** Several built-in color schemes ship with the app. A theme editor lets you build your own. A "more themes" toggle keeps the picker compact until you want to see everything.

**Sharing.** A theme can be exported as a short text code and imported the same way, so you can pass a look to someone else. Imports are validated, so a malformed or foreign code is simply rejected.

**Live application.** Switching a theme repaints the app immediately, with no restart.

**How to use it.**
- Switch: open the Themes tab and pick a theme.
- Edit or create: use the theme editor.
- Share: export a theme to a code, or paste a code to import one.

## Notifications and toasts

The app keeps you informed with brief on-screen messages.

**Duration.** How long a toast stays on screen is configurable (500 to 5000 ms).

**Muting.** Individual notification types can be muted so they no longer pop up, and a master mute silences all of them at once. Muted events still update the status line; they just do not float on screen.

**Undo.** The notifications for destructive actions (removing an account, deleting a group or favorite) carry an Undo button, so a mistake is quick to reverse.

**How to use it.**
- Set the display time and mutes on the Settings tab, Notifications section.
- Click Undo on a toast right after a destructive action.

## Confirmation prompts

Some actions ask for confirmation by default. If you do them often, you can skip the prompt.

**Per-action bypass.** You can turn off the confirmation for removing an account, deleting a group, or deleting a favorite individually. A master bypass turns off all of them at once.

**How to use it.**
- Settings tab, Confirmations section. Toggle the ones you want to skip, or the master switch.

## Search and selection

Working with a long list is meant to be fast.

**Search.** The account list filters as you type, matching on both the display label and the underlying username. Favorites have their own search box.

**Bulk selection.** You can select all visible accounts or clear the selection in one action, then launch the selected set.

**How to use it.**
- Type in the search box to filter, and clear it to see everything again.
- Use Select All and Clear to manage the selection, then Launch.

## Persistence and resume

The app tries to open the way you left it.

**What is remembered.** The window size, the active theme, the last launch target and join mode and job id, the last selected favorite, and group expansion states are all saved. Settings are written with a short debounce so rapid changes are coalesced, and any pending change is flushed when the app closes.

**Where it lives.** Everything is stored locally under `%APPDATA%\Instance Manager`. The full layout is in [DATA-STORAGE.md](DATA-STORAGE.md).

## Privacy and security

Everything stays on your machine. The session cookie is encrypted with Windows DPAPI, which ties it to your Windows user, and the plaintext cookie only exists briefly in memory while it is read or exchanged for a launch ticket. Outbound traffic goes only to official Roblox endpoints over HTTPS. The complete model, including its deliberate limits, is in [SECURITY.md](SECURITY.md).

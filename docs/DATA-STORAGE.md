# Data storage

[README](../README.md) · [Features](FEATURES.md) · [Troubleshooting](TROUBLESHOOTING.md) · Data storage · [Security](SECURITY.md) · [Architecture](ARCHITECTURE.md) · [Changelog](../CHANGELOG.md)

Instance Manager keeps everything on your PC in JSON files and a log. There is no database, no cloud service and no telemetry. This page lists what the app stores, where, in which format and how it is written.

## Where data lives

The main data folder is `%APPDATA%\Instance Manager`, set in [AppPaths.cs](../src/InstanceManager/Services/AppPaths.cs).

| File | Contents |
|---|---|
| `accounts.json` | Roblox accounts, including the encrypted session cookie |
| `groups.json` | Account groups |
| `favorites.json` | Favorite games |
| `settings.json` | All settings |
| `themes.json` | Your own themes; the built-in ones live in the code |
| `automations.json` | Automations: accounts, destination and schedule |
| `auto-reconnect.log` | One line per Auto Reconnect attempt, result and give-up |
| `binding-errors.log` | WPF binding warnings, Debug builds only |

`%LOCALAPPDATA%\Instance Manager` holds data that shouldn't roam with your Windows profile:

- `avatars\<userId>.png` caches avatar headshots so rows show them at startup, and `avatars\<userId>.url` holds the Roblox address each picture came from. Every session asks Roblox for the current addresses, up to 100 accounts per request, and downloads a picture again only when its address changed. The folder can be deleted at any time.
- `webview\` is the WebView2 runtime folder for the login window. The login itself uses an InPrivate profile that is created for each dialog and wiped when it closes.
- `previous-version\Instance Manager <version>.exe` is a copy of the version the last update replaced, kept for Roll back while Keep the previous version is on. There is never more than one.

The folder with the exe is touched only by the updater. During an update it holds an `.im-update` folder with the new exe and an `.im-old` copy of the replaced one, and both are deleted at the next start. The native WebView2 loader inside the exe is unpacked to `%TEMP%\.net\<exe name>\` when the app starts.

The app also works with these Roblox files:

- `%LOCALAPPDATA%\Roblox\Versions`, or the custom folder from Settings, to find installed clients. With Apply FastFlags on, the app writes `ClientSettings\ClientAppSettings.json` into each version folder before a launch and deletes it when you turn the option off, as long as the file is still the one the app wrote.
- `%LOCALAPPDATA%\Roblox\GlobalBasicSettings_13.xml`, Roblox's own settings file. When an FPS cap is set, the app changes only its `FramerateCap` value before each launch.
- `%LOCALAPPDATA%\Roblox\logs`, which Auto Reconnect reads, but never changes, to see when a client joined, was kicked or left.

Start with Windows adds the value `Instance Manager` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, holding the quoted path of the exe and `--startup`. Turning the option off removes it. The app writes nothing else to the registry.

## Personal data

For each account ([Account.cs](../src/InstanceManager/Models/Account.cs)) the app stores:

- the Roblox identity: `UserId`, `Username`, `DisplayName`
- the encrypted session cookie, `EncryptedCookie`
- what you type yourself: `Alias` (the nickname) and `Notes`
- organizing and launch data: `GroupIds`, `SortOrder`, `BrowserTrackerId`, `PreferredVersionGuid`, `CreatedAt`

The cookie is encrypted with Windows DPAPI for your Windows user (see [SECURITY.md](SECURITY.md)). Every other field is plain text. Avatars are public Roblox images. Groups, favorites, settings, themes and automations contain nothing personal beyond the names you give them and the account IDs they point to. A private server link in the launch bar, a favorite or an automation is stored as plain text, and whoever has it can join that server.

The app talks to Roblox for logins, launches, session checks, avatars, server lists, game names, the Games page and private server links, and to GitHub to check for updates. The update check sends nothing about you or your accounts. [ARCHITECTURE.md](ARCHITECTURE.md#external-services) lists every address.

## File formats

An account in `accounts.json`:

```jsonc
{
  "Id": "GUID",
  "UserId": 123456789,
  "Username": "playername",
  "DisplayName": "Display name",
  "Alias": "optional",
  "GroupId": null,              // old single-group field, null after migration
  "GroupIds": ["GUID"],
  "Notes": "optional",
  "EncryptedCookie": "base64(DPAPI blob)",
  "CreatedAt": "2026-01-01T00:00:00+00:00",
  "SortOrder": 0,
  "BrowserTrackerId": 123456789,
  "PreferredVersionGuid": null, // null means the default version
  "AutoReconnectEnabled": true  // old per-account switch, kept for old files
}
```

| File | Entries and fields |
|---|---|
| `groups.json` | `AccountGroup`: `Id`, `Name`, `ColorHex` (for example `#4C8DFF`), `SortOrder`, `IsExpanded` |
| `favorites.json` | `FavoriteGame`: `Id`, `Name`, `PlaceId`, `DefaultJobId` (optional), `PrivateServerLinkCode` (optional, the link code of a private server), `IsPrimary`, `SortOrder` |
| `themes.json` | `ThemeDefinition`: `Id`, `Name`, `IsBuiltIn` (always `false` after loading) and a `Palette` of fifteen hex colors ([ThemePalette.cs](../src/InstanceManager/Models/ThemePalette.cs)) |

`automations.json` holds one object with a list of automations ([Automation.cs](../src/InstanceManager/Models/Automation.cs)):

```jsonc
{
  "Automations": [
    {
      "Id": "GUID",
      "Name": "Evening farm",
      "AccountIds": ["GUID"],
      "Mode": 0,                  // 0 = game, 1 = server, 2 = Roblox home screen
      "Target": "2753915549",     // game link, Place ID or server link
      "TargetName": "Blox Fruits", // looked up once, shown in the list
      "ScheduleEnabled": true,
      "StartOnSchedule": true,
      "StartTime": "18:00",
      "Days": [1, 2, 3, 4, 5],    // 0 = Sunday, empty means every day
      "Stop": "AtTime",           // "Never", "AfterMinutes" or "AtTime"
      "StopAfterMinutes": 60,     // 1 to 1440
      "StopTime": "23:00",        // not after StartTime means the next day
      "LastStartLocal": "2026-09-28T18:00:04",
      "LastStopLocal": "2026-09-28T23:00:02"
    }
  ]
}
```

`Mode` uses the same values as the launch bar. `LastStartLocal` and `LastStopLocal` keep a start or close from happening twice and let "close after" count from the last start.

`settings.json` holds one `AppSettings` object ([AppSettings.cs](../src/InstanceManager/Models/AppSettings.cs)). You never need to edit it by hand. The fields, grouped by what they control:

| Area | Fields |
|---|---|
| Startup | `StartInTray`, `CloseToTray` (both off by default). Start with Windows lives in the registry, see above. |
| Launching | `MultiInstanceEnabled` (on), `LaunchDelayMs` (0 to 30000 in steps of 500, default 2000), `SwitchToAccountsOnGameSelect` (on) |
| Roblox version | `SelectedVersionGuid`, `VersionsPathOverride` |
| Launch bar | `LastTargetInput`, `LastJobIdInput`, `LastJoinMode`, `LastSelectedFavoriteId` |
| Auto Reconnect | `AutoReconnectMaster` (off), `AutoReconnectOnKickError` and `AutoReconnectOnCrash` (both on), `AutoReconnectMaxAttempts` (1 to 30, 31 means no limit, default 3) |
| Graphics | `FpsCap` (0 for the Roblox default, or 30, 60, 75, 120, 144, 165, 240), `ApplyFastFlags`, `GraphicsApi`, `GraphicsQuality` (0 for automatic, or 1 to 10), `AntiAliasing` (-1 for automatic, or 0, 1, 2, 4, 8), `TextureQuality` (-1 for automatic, or 0 to 3), `IgnoreDisplayScaling`, `HideGrass`, `GraySky`, `FreezeLighting`, `ExclusiveFullscreen`, `CustomFastFlags`, and `AppliedFastFlagsHash`, which remembers the flag file the app wrote |
| Appearance | `ThemeId` (default `dark`), `ThemeOrder`, `SidebarCollapsed`, `WindowWidth`, `WindowHeight` |
| Updates | `UpdateCheckOnStartup` and `UpdateCheckAutomatically` (both on), `UpdateCheckIntervalMinutes` (15 to 1440, default 60), `UpdateIncludePrereleases`, `SkippedUpdateVersion`, `KeepPreviousVersion` (on) |
| Notifications | `ToastDurationMs` (500 to 5000, default 3000), `NotifyMuteMaster`, `MutedNotifications` |
| Confirmations | `ConfirmBypassMaster` and one `ConfirmBypass...` switch per action |
| Anti-AFK | `AntiAfkEnabled` (off), `AntiAfkIntervalMinutes` (1 to 60, default 15), `AntiAfkKey` (the name of a key such as `Space`, `W`, `F13` or `LeftShift`, default `Space`), `AntiAfkReturnFocus` (on) |
| Account list | `LiveAvatars` (on) shows the avatars in the account rows |

Older files can contain `AutoRejoin...` fields, `PrimaryFavoriteId` and `CompactAccountList`. They are migrated when the file is loaded (see below).

## Logs

[AutoReconnectLog](../src/InstanceManager/Services/AutoReconnectLog.cs) writes one line per event to `auto-reconnect.log`:

```
2026-06-23 12:15:04  RECONNECT  'MainAlt' (userId 123)  trigger=Kick  attempt 1/3  -> placeId=920587237
2026-06-23 12:15:09  RESULT  'MainAlt' (userId 123)  attempt 1  started
2026-06-23 12:16:40  GIVEUP  'MainAlt' (userId 123)  trigger=Error  retry limit (3) reached
```

`RECONNECT` means an attempt is starting, `RESULT` says whether it started a client or failed and why, and `GIVEUP` means the instance reached its retry limit. `SKIP` means a client closed within ten seconds of another account's reconnect and was left closed. A reconnect into a private server reads `placeId=... private server`, without the link code.

The log strips control characters, line and paragraph separators and bidi controls from every field and moves to a `.1` file at one megabyte.

## How files are written

Every JSON file goes through [JsonFileStore](../src/InstanceManager/Services/JsonFileStore.cs):

- Writes are atomic. The store writes to a temporary file with a random name in the same folder and swaps it in with `File.Replace`, or `File.Move` for a new file, so an interrupted write can't damage the existing file.
- Files over four megabytes are refused before they are parsed.
- A save identical to the last one is skipped. The store remembers a SHA-256 hash of the last content for this, not the content itself.
- Files are read and written as UTF-8. Files that another editor saved as UTF-16 or with invalid UTF-8 are still read as before.
- Read and write errors, such as a lock from antivirus or a sync tool or a full disk, don't crash the app. A failed read falls back to defaults, and a failed write keeps the state in memory and tries again at the next save.

The repositories load their list once at startup, keep it in memory and write the whole list back after every change. [SettingsService](../src/InstanceManager/Storage/SettingsService.cs) waits 350 ms after the last change before saving and saves anything pending when the app closes.

## Migrations

There are no versioned migrations. Old formats are corrected when a file is loaded, and anything that changes is saved right away.

- `Account.NormalizeGroupMemberships` moves the old single `GroupId` into `GroupIds` and removes duplicates and empty IDs.
- Accounts without a `BrowserTrackerId` get a random one.
- The old `AutoRejoinOnError` and `AutoRejoinOnKick` switches become `AutoReconnectOnKickError`, which stays on if either of them was on. The other `AutoRejoin...` fields map to their `AutoReconnect...` counterparts.
- `PrimaryFavoriteId` becomes the `IsPrimary` flag on the favorite.
- `CompactAccountList` is switched off, since the compact row size no longer exists.
- `AppSettings.Normalize` clamps the numbers to their ranges, resets graphics values that aren't one of the offered choices, turns the Anti-AFK key of the first version, `Shift`, into `LeftShift`, resets a key that isn't a key name or can't be used, such as `Escape`, to `Space`, sets `ThemeId` when it is empty and creates missing lists.
- Settings files from before 1.1.2 get the new fields with their defaults. Values you already had, such as your launch pause or Auto Reconnect switch, are kept.
- A settings file written by a 1.1.2 preview can still hold the fields of the removed hotkeys, local API, Discord webhook, playtime, collectible value, online status and Robux balance. They are ignored and the next save drops them, the encrypted token and webhook URL included. The app deletes `playtime.json` and `diagnostics.log` from those builds when it starts.
- `automations.json` from a 1.1.2 preview, with separate `Templates` and `Schedules`, is converted once. Each template becomes an automation, a schedule is attached to its template's automation (a second schedule for the same template becomes a copy), and a schedule for a single account becomes an automation of its own. A Close schedule turns into a close time without a start.

## Consistency

Each file is consistent on its own, but there are no transactions across files. If the app is killed between writing `accounts.json` and `groups.json`, an account could point at a group that wasn't saved. The window for this is tiny, and the app ignores such group IDs.

## Deleting data

Data stays until you remove it.

- Removing an account deletes it and its encrypted cookie from `accounts.json`. Groups, favorites and themes are deleted the same way.
- Cached avatars stay in `%LOCALAPPDATA%\Instance Manager\avatars` after an account is removed. Delete the folder to clear them.
- The login window's cookies and browsing data are cleared every time it closes, whether the login worked, was cancelled or failed.
- Turning off Apply FastFlags removes the flag files the app wrote. The FPS cap stays in Roblox's settings; change it inside Roblox if you want a different one.
- To remove everything, turn off Start with Windows and Apply FastFlags first, then delete `%APPDATA%\Instance Manager` and `%LOCALAPPDATA%\Instance Manager`.

## Backups and moving to another PC

The app makes no backups. Because the data folder sits in the roaming `%APPDATA%`, profile backup and sync tools may copy it. The encrypted cookies work only for the same Windows user on the same PC, though. After restoring the files on another PC or under another user, every account needs to be added again; names, notes and groups carry over.

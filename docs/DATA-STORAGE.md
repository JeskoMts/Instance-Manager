# Data storage

Instance Manager keeps everything on your PC as JSON files and a log file. There is no database, no cloud service and no telemetry. This document lists what the app stores, where, in what shape, and how it is written.

## Where data lives

The main data folder is `%APPDATA%\Instance Manager`, defined in [AppPaths.cs](../src/InstanceManager/Services/AppPaths.cs).

| File | Contents |
|---|---|
| `accounts.json` | Roblox accounts, including the encrypted session cookie |
| `groups.json` | Account groups |
| `favorites.json` | Favorite games |
| `settings.json` | All settings |
| `themes.json` | Your own themes (the built-in ones live in the code) |
| `auto-reconnect.log` | One line per Auto Reconnect attempt, result and give-up |
| `binding-errors.log` | WPF binding warnings, Debug builds only |

`%LOCALAPPDATA%\Instance Manager` holds data that shouldn't roam with your Windows profile:

- `avatars\<userId>.png` caches avatar headshots so rows show them at startup. A fresh copy is still downloaded every session. The folder can be deleted at any time.
- `webview\` is the WebView2 runtime folder for the login window. The login itself uses an InPrivate profile that is created per dialog and wiped when it closes.

The folder with the app's exe is only touched by the updater. During an update it contains a `.im-update` folder with the new exe and an `.im-old` copy of the replaced one. Both are deleted on the next start. The native WebView2 loader inside the exe is extracted to `%TEMP%\.net\<exe name>\`.

The app also reads two Roblox folders without writing to them:

- `%LOCALAPPDATA%\Roblox\Versions`, or the custom folder from Settings, to find installed clients.
- `%LOCALAPPDATA%\Roblox\logs`, which Auto Reconnect reads to see when a client joined, was kicked or left.

## Personal data

For each account ([Account.cs](../src/InstanceManager/Models/Account.cs)) the app stores:

- the Roblox identity: `UserId`, `Username`, `DisplayName`
- the encrypted session cookie, `EncryptedCookie`
- what you type yourself: `Alias` (the nickname) and `Notes`
- organizing and launch data: `GroupIds`, `SortOrder`, `BrowserTrackerId`, `PreferredVersionGuid`, `CreatedAt`

Only the cookie is encrypted, with Windows DPAPI tied to your Windows user (see [SECURITY.md](SECURITY.md)). The other fields are plain text in `accounts.json`. Avatars are public Roblox images. Groups, favorites, settings and themes contain nothing personal beyond the names you give them.

Nothing is sent to third parties. The app contacts Roblox for logins, launches, avatars and the Games tab, and GitHub to check for updates. The update check sends no data about you or your accounts. [ARCHITECTURE.md](ARCHITECTURE.md#external-services) lists every endpoint.

## File formats

An account in `accounts.json`:

```jsonc
{
  "Id": "GUID",
  "UserId": 123456789,
  "Username": "playername",
  "DisplayName": "Display name",
  "Alias": "optional",
  "GroupId": null,             // old single-group field, null after migration
  "GroupIds": ["GUID", "..."],
  "Notes": "optional",
  "EncryptedCookie": "base64(DPAPI blob)",
  "CreatedAt": "2026-01-01T00:00:00+00:00",
  "SortOrder": 0,
  "BrowserTrackerId": 123456789,
  "PreferredVersionGuid": null, // null means use the global version
  "AutoReconnectEnabled": true  // old per-account flag, kept for old files
}
```

`groups.json` holds `AccountGroup` entries: `Id`, `Name`, `ColorHex` (for example `#4C8DFF`), `SortOrder`, `IsExpanded`.

`favorites.json` holds `FavoriteGame` entries: `Id`, `Name`, `PlaceId`, `DefaultJobId` (optional), `IsPrimary`, `SortOrder`.

`themes.json` holds `ThemeDefinition` entries: `Id`, `Name`, `IsBuiltIn` (always set to `false` on load) and a `Palette` of fifteen hex colors ([ThemePalette.cs](../src/InstanceManager/Models/ThemePalette.cs)).

`settings.json` holds one `AppSettings` object ([AppSettings.cs](../src/InstanceManager/Models/AppSettings.cs)). You never need to edit it by hand. The fields, grouped by what they control:

| Area | Fields |
|---|---|
| Launching | `MultiInstanceEnabled` (default on), `LaunchDelayMs` (0 to 30000 ms in 500 ms steps, default 2000), `SwitchToAccountsOnGameSelect` |
| Roblox version | `SelectedVersionGuid`, `VersionsPathOverride` |
| Launch bar | `LastTargetInput`, `LastJobIdInput`, `LastJoinMode`, `LastSelectedFavoriteId` |
| Auto Reconnect | `AutoReconnectMaster` (default off), `AutoReconnectOnKickError`, `AutoReconnectOnCrash` (both default on), `AutoReconnectMaxAttempts` (1 to 30, 31 means no limit, default 3) |
| Appearance | `ThemeId` (default `dark`), `ThemeOrder`, `WindowWidth`, `WindowHeight` |
| Notifications | `ToastDurationMs` (500 to 5000 ms, default 3000), `NotifyMuteMaster`, `MutedNotifications` |
| Confirmations | `ConfirmBypassMaster` and one `ConfirmBypass...` switch per action |

Older files can contain `AutoRejoin...` fields and `PrimaryFavoriteId`. They are migrated on load (see below) and written back as `null`.

## The Auto Reconnect log

[AutoReconnectLog](../src/InstanceManager/Services/AutoReconnectLog.cs) writes one line per event:

```
2026-06-23 12:15:04  RECONNECT  'MainAlt' (userId 123)  trigger=Kick  attempt 1/3  -> placeId=920587237
2026-06-23 12:15:09  RESULT  'MainAlt' (userId 123)  attempt 1  started
2026-06-23 12:16:40  GIVEUP  'MainAlt' (userId 123)  trigger=Error  retry limit (3) reached
```

`RECONNECT` means an attempt is starting, `RESULT` says whether it started a client or failed and why, and `GIVEUP` means the instance hit its retry limit. Control characters are stripped from every field, and the file rotates to `.1` at one megabyte.

## How files are written

Every JSON file goes through [JsonFileStore](../src/InstanceManager/Services/JsonFileStore.cs):

- Writes are atomic. The store writes to a temporary file with a random name in the same folder and swaps it in with `File.Replace`, or `File.Move` for a new file, so an interrupted write can't corrupt the existing file.
- Files over four megabytes are rejected before they are parsed.
- A save identical to the last one is skipped.
- Read and write errors, such as a lock from antivirus or a sync tool or a full disk, are swallowed. A failed read falls back to defaults; a failed write keeps the state in memory and tries again on the next save.

The repositories load their list once at startup, keep it in memory and write the whole list back on every change. [SettingsService](../src/InstanceManager/Storage/SettingsService.cs) waits 350 ms after the last change before saving, and saves anything pending when the app closes.

## Migrations

There are no versioned migrations. Old formats are corrected on load, and anything that changes is saved right away.

- `Account.NormalizeGroupMemberships` moves the old single `GroupId` into `GroupIds` and removes duplicates and empty IDs.
- Accounts without a `BrowserTrackerId` get a random one.
- The old `AutoRejoinOnError` and `AutoRejoinOnKick` switches are folded into `AutoReconnectOnKickError`, which stays on if either of them was on. The other `AutoRejoin...` fields map to their `AutoReconnect...` counterparts.
- `PrimaryFavoriteId` is turned into the `IsPrimary` flag on the favorite.
- `AppSettings.Normalize` clamps `LaunchDelayMs`, `ToastDurationMs` and `AutoReconnectMaxAttempts` to their ranges, sets `ThemeId` when it is empty and creates missing lists.

## Consistency

Each file is consistent on its own, but there are no transactions across files. If the app is killed between writing `accounts.json` and `groups.json`, an account could point at a group that wasn't saved. The window for this is tiny, and the UI ignores such dangling group IDs.

## Deleting data

Data stays until you remove it.

- Removing an account deletes it and its encrypted cookie from `accounts.json`. Groups, favorites and themes are deleted the same way.
- Cached avatars stay in `%LOCALAPPDATA%\Instance Manager\avatars` after an account is removed. Delete the folder to clear them.
- The login window's cookies and browsing data are cleared every time it closes, whether the login succeeded, was cancelled or failed.
- To remove everything, delete `%APPDATA%\Instance Manager` and `%LOCALAPPDATA%\Instance Manager`.

## Backups and moving to another PC

The app makes no backups. Because the data folder is in the roaming `%APPDATA%`, profile backup and sync tools may copy it. The encrypted cookies only work for the same Windows user on the same PC, though. After restoring the files on another PC or under another user, every account needs to be added again; names, notes and groups carry over.

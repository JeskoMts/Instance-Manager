# Data storage

Instance Manager keeps everything on the local machine as plain JSON files plus a couple of log files. There is no database, no cloud service, and no telemetry. This document lists every piece of data the app touches, where it lives, how it is shaped, how it is written, and what happens to it over time.

## Where data lives

The data directory is `%APPDATA%\Instance Manager` (the roaming profile, `Environment.SpecialFolder.ApplicationData`), defined in [AppPaths.cs](../src/InstanceManager/Services/AppPaths.cs).

| File or folder | Contents |
|---|---|
| `accounts.json` | Roblox accounts, including the encrypted session cookie |
| `groups.json` | Account groups |
| `favorites.json` | Favorite games |
| `settings.json` | Application settings |
| `themes.json` | User-defined themes (built-in themes live in code, not here) |
| `auto-reconnect.log` | A human-readable record of every Auto Reconnect attempt |
| `binding-errors.log` | WPF data-binding warnings, written only in Debug builds |

WebView2 runtime data is separate and non-roaming at `%LOCALAPPDATA%\Instance Manager\webview`. Login itself uses an InPrivate profile created per dialog and disposed on close, and session data is cleared on every close path.

### What the app reads outside its data directory

Two locations are read but never written:

- The Roblox `Versions` folder, by default `%LOCALAPPDATA%\Roblox\Versions`, overridable in Settings to another local fixed-drive folder. Network/UNC paths are rejected. This is how installed clients are discovered.
- The Roblox client logs, `%LOCALAPPDATA%\Roblox\logs`. Auto Reconnect tails these to learn when an instance joined, was kicked, disconnected, or left. It only reads them.

Avatar images are fetched over the network on demand and are not saved to disk.

## What data is collected

Per account ([Account.cs](../src/InstanceManager/Models/Account.cs)), the parts that are personal or identifying:

- The Roblox identity: `UserId`, `Username`, `DisplayName`.
- The encrypted session cookie, `EncryptedCookie`.
- Free-text fields you type: `Alias`, `Notes`.
- Organizational and launch metadata: `GroupIds`, `SortOrder`, `BrowserTrackerId`, `PreferredVersionGuid`, `CreatedAt`.

Groups, favorites, settings, and themes hold no personal data beyond names you choose to assign.

## Schemas of the main entities

**Account** (a list in `accounts.json`):

```jsonc
{
  "Id": "GUID",
  "UserId": 123456789,
  "Username": "playername",
  "DisplayName": "Display name",
  "Alias": "optional",
  "GroupId": null,             // legacy single-group field, null after migration
  "GroupIds": ["GUID", "..."],
  "Notes": "optional",
  "EncryptedCookie": "base64(DPAPI blob)",
  "CreatedAt": "2026-01-01T00:00:00+00:00",
  "SortOrder": 0,
  "BrowserTrackerId": 123456789,
  "PreferredVersionGuid": null, // null means follow the global version selection
  "AutoReconnectEnabled": true     // legacy per-account flag, kept for old files
}
```

**AccountGroup** (`groups.json`): `Id`, `Name`, `ColorHex` (for example `#4C8DFF`), `SortOrder`, `IsExpanded`.

**FavoriteGame** (`favorites.json`): `Id`, `Name`, `PlaceId`, `DefaultJobId` (optional), `IsPrimary`, `SortOrder`. The UI-only field `ShowDivider` is excluded from persistence with `[JsonIgnore]`.

**AppSettings** (`settings.json`), the launch and behavior settings ([AppSettings.cs](../src/InstanceManager/Models/AppSettings.cs)). Among them:

- Launch and version: `MultiInstanceEnabled`, `LaunchDelayMs`, `SelectedVersionGuid`, `VersionsPathOverride`, `CheckLatestVersionOnline`.
- Auto Reconnect: `AutoReconnectMaster`, `AutoReconnectOnKickError`, `AutoReconnectOnCrash`, `AutoReconnectMaxAttempts`.
- Appearance and window: `ThemeId`, `ThemeOrder`, `ToastDurationMs`, `WindowWidth`, `WindowHeight`.
- Confirmation and notification toggles: the `ConfirmBypass*` and `NotifyMute*` switches, plus `MutedNotifications`.

Two legacy Auto Reconnect fields, `AutoRejoinOnError` and `AutoRejoinOnKick`, may appear in older files. They are folded into `AutoReconnectOnKickError` on load and then written back as `null`. See Migrations below.

**ThemeDefinition** (`themes.json`): `Id`, `Name`, `IsBuiltIn` (forced to `false` on load, since built-in themes are never read from the file), and a `Palette` of fifteen hex colors ([ThemePalette.cs](../src/InstanceManager/Models/ThemePalette.cs)).

## The Auto Reconnect log

`auto-reconnect.log` is a plain text file, one line per event, written by [AutoReconnectLog](../src/InstanceManager/Services/AutoReconnectLog.cs). It is meant to be read by a human when you want to know why an instance did or did not come back. There are three kinds of line:

```
2026-06-23 12:15:04  RECONNECT  'MainAlt' (userId 123)  trigger=Kick  attempt 1/3  -> placeId=920587237
2026-06-23 12:15:09  RESULT  'MainAlt' (userId 123)  attempt 1  started
2026-06-23 12:16:40  GIVEUP  'MainAlt' (userId 123)  trigger=Error  retry limit (3) reached
```

`RECONNECT` records that an attempt is starting, with the trigger and the target. `RESULT` records whether that attempt started a process or failed (with a short reason). `GIVEUP` records that an instance hit its retry limit. Control characters are removed from fields, and the log rotates to `.1` at one megabyte. Writing remains best-effort.

## How files are written

Every JSON file goes through [JsonFileStore](../src/InstanceManager/Services/JsonFileStore.cs), which uses `System.Text.Json` with indented output and case-insensitive property names. Three properties are worth knowing:

- **Atomic writes.** The store writes to a uniquely named same-directory temporary file, then swaps it in with `File.Replace` (or `File.Move` when the target does not exist yet). A write that is interrupted halfway cannot corrupt the existing file, and another process cannot predict one fixed temp filename.
- **Size limit.** Files larger than four megabytes are rejected before deserialization.
- **Write deduplication.** The store remembers the last JSON it wrote. An identical save is skipped, so repeated no-op saves do not churn the disk.
- **Fault tolerance.** Read and write errors (`IOException`, `JsonException`, `UnauthorizedAccessException`, for example an antivirus or cloud-sync lock, or a full disk) are swallowed. On read it falls back to a default value; on write it keeps the last known state and a stray temp file is cleaned up, so the next save retries.

The repositories ([AccountRepository](../src/InstanceManager/Storage/AccountRepository.cs) and the others) load their list once at startup, keep it in memory as the runtime source of truth, and write the whole list back on every change. The [SettingsService](../src/InstanceManager/Storage/SettingsService.cs) adds a debounce: rapid settings changes are coalesced and flushed after 350 ms, and any pending change is also flushed when the service is disposed.

## Migrations

There is no versioned migration system. Schema changes are handled as small corrections applied on load, and anything that changes is written back immediately.

- **Group membership.** `Account.NormalizeGroupMemberships` moves the old single `GroupId` into the `GroupIds` list, drops duplicate and empty ids, and sets `GroupId` to `null`.
- **Browser tracker id backfill.** An account whose `BrowserTrackerId` is `0` (older files) is assigned a random id on load.
- **Auto Reconnect toggle merge.** Older files had two separate switches, `AutoRejoinOnError` and `AutoRejoinOnKick`. These are now one switch, `AutoReconnectOnKickError`. On load, `AppSettings.Normalize` folds the two legacy values into the merged one: reconnect stays on if either old switch wanted it, and is only off when both were off. The legacy fields are then set to `null` so they no longer drive behavior, and the change is saved. This mirrors how the legacy `GroupId` field is retired.
- **Settings clamping.** `AppSettings.Normalize` also clamps `LaunchDelayMs` (0 to 60000, in 500 ms steps), `ToastDurationMs` (500 to 5000), and `AutoReconnectMaxAttempts` (1 to 20), sets a default `ThemeId` when blank, and initializes empty lists. The deprecated `PrimaryFavoriteId` exists only to migrate to the per-favorite `IsPrimary` flag.

## Consistency notes

Each file is consistent on its own thanks to atomic writes, but there are no transactions across files. If the app is killed between writing `accounts.json` and `groups.json`, you could in theory end up with a group id referenced by an account whose group record did not get written. In practice the lists are small and written back together on a single user action, so the window is tiny, and dangling references are tolerated by the UI rather than treated as corruption.

## Personal data

The personal data is the Roblox identity (`UserId`, `Username`, `DisplayName`), the free-text `Alias` and `Notes`, and above all the session cookie. The cookie exists only in DPAPI-encrypted form, bound to your Windows user (see [SECURITY.md](SECURITY.md)). The other identity fields sit in plaintext in `accounts.json`. Avatar images are loaded from Roblox on demand, decoded into a small in-memory bitmap, and not stored.

Nothing is sent to third parties. Outbound traffic goes only to the official Roblox endpoints (auth, users, thumbnails, client version). The full list is in [ARCHITECTURE.md](ARCHITECTURE.md).

## Retention and deletion

Data stays until you remove it. There is no automatic expiry.

- Removing an account deletes it from `accounts.json`, including its encrypted cookie. Groups, favorites, and themes are deleted the same way.
- WebView2 login state is off-the-record. Cookies and browsing data are cleared after success, Cancel, title-bar close, and failures. Legacy roaming WebView state from older releases is deleted best-effort at startup.
- A full wipe deletes `%APPDATA%\Instance Manager` and `%LOCALAPPDATA%\Instance Manager`.

## Backups and portability

The app makes no backups and keeps no version history. The atomic temporary-file swap is the safeguard against a half-written file.

Because the data directory sits in roaming `%APPDATA%`, it may be picked up by existing profile, backup, or cloud-sync tooling. One caveat matters: the DPAPI-encrypted cookies are tied to your Windows user on this machine. If they are restored onto a different machine or a different user profile, they cannot be decrypted there, and the affected accounts will need to be added again on the new machine.

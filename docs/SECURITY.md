# Security

Instance Manager is a local, single-user Windows app. It has no server, no account system of its own, no telemetry and nothing listening for incoming connections. The most valuable thing it holds is the Roblox `.ROBLOSECURITY` session cookie of each account, because whoever has that cookie can act as that account.

## Trust model

- Trusted: the current Windows user, the installed app files, Windows' crypto and code-signing services, Roblox's HTTPS endpoints, and the GitHub releases of `JeskoMts/Instance-Manager`.
- Untrusted and validated: pasted links, theme codes, JSON files on disk, remote URLs and responses, custom Roblox folders, executables and update packages.
- Only partly defensible: other programs running as the same Windows user. The app keeps few reusable secrets and refuses untrusted executables, but DPAPI by design lets any code running as that user decrypt that user's data.
- Out of scope: attackers with administrator or kernel access, or full control of the Windows session.

The app runs as the current user (`asInvoker`) and never asks for administrator rights.

## Login and the session cookie

Login uses the real Roblox page in WebView2. Each login dialog creates its own WebView2 environment with an InPrivate profile and disposes it when the dialog closes, so no login state outlives the dialog. Top-level navigation is limited to HTTPS on `roblox.com` and its subdomains. Pop-ups, downloads, external URI schemes, DevTools, browser shortcuts, autofill, password saving, host objects and web messages are all turned off.

Whether the login succeeds, is cancelled, the window is closed or an error occurs, cookies and browsing data are deleted before the browser is disposed. The WebView2 runtime folder is `%LOCALAPPDATA%\Instance Manager\webview`, outside the roaming profile; an older roaming copy from earlier releases is deleted at startup.

The app reads the `.ROBLOSECURITY` cookie only after the Roblox login, confirms it with `users.roblox.com/v1/users/authenticated`, encrypts it right away and clears the browser session. Cookie values are checked for length and control characters before they go into an HTTP header.

## How secrets are stored

Cookies are encrypted with Windows DPAPI (`DataProtectionScope.CurrentUser`), so a copied `accounts.json` normally can't be decrypted under another Windows user or on another PC. A fixed entropy string keeps this app's DPAPI data separate from other apps; it isn't treated as a password.

Byte buffers holding plaintext during DPAPI calls are zeroed right after use. While a cookie is checked or traded for a launch ticket, it briefly exists as a .NET string, which can't be wiped reliably.

User IDs, usernames, display names, nicknames, notes and group data stay in plain text in `accounts.json`; only the cookie is encrypted. Avatar headshots are public Roblox images and are cached unencrypted as `%LOCALAPPDATA%\Instance Manager\avatars\<userId>.png`. The file name comes from the numeric user ID only, so it can't point outside that folder.

## Network

The shared `HttpClient` has cookies and automatic redirects turned off. Server links are checked against an HTTPS allowlist and every redirect is validated by hand. The cookie is only ever sent to fixed Roblox login and user endpoints. Windows validates TLS certificates. The app doesn't pin certificates, because there would be no way to ship an emergency pin update.

Avatar and game images must come over HTTPS from `rbxcdn.com` or its subdomains, and image downloads are cut off at five megabytes, including compressed responses and responses without a `Content-Length`.

The Discord invite behind the Discord button is the only address outside Roblox and GitHub. It opens in your browser when you click the button; the app itself never contacts Discord.

## Updates

On every start of a Release build the app asks `api.github.com` for the latest release of `JeskoMts/Instance-Manager`. The request carries only a user agent with the app version. If the release is newer than the running build, the update goes through these checks:

- The asset must be named `InstanceManager-<tag>.zip`, and its download URL must start with `https://github.com/JeskoMts/Instance-Manager/releases/download/`.
- Redirects are followed by hand, at most five, HTTPS only, and only to `github.com`, `objects.githubusercontent.com` and `release-assets.githubusercontent.com`.
- The download is capped at 64 MB, and its SHA-256 must match the `sha256:` digest GitHub reports for the asset. Without a digest there is no update.
- The zip may only contain plain file names (no folders, no `..`, no alternate data streams, no invalid characters, no duplicates), must include `InstanceManager.exe` and `InstanceManager.dll`, and its unpacked size is capped.
- Files are swapped by renaming, and a failed swap restores every file already replaced.

The digest proves the file is the one attached to the release; it doesn't prove who attached it. Release builds aren't code-signed, so the update is exactly as trustworthy as the GitHub account and repository that publish it. Whoever controls those can ship code that runs as your Windows user on the next start. This is the same trust you place in the repository when you download a release by hand.

## Input limits

- Server links have a length cap, must be correctly percent-encoded, must stay on Roblox HTTPS addresses and may redirect at most five times.
- Place IDs must be positive integers and Job IDs must be GUIDs.
- Theme imports are size-checked on the raw clipboard data before WPF turns it into text. The encoded and decoded payload, JSON depth, theme name, fields and every color are validated after that.
- JSON files over four megabytes are rejected before parsing.
- Auto Reconnect log fields are put on one line, stripped of control characters and length-capped, and the log rotates at one megabyte.

## Launching Roblox

A custom Roblox folder must be a full path on a fixed local drive. UNC paths, network drives, relative paths, device paths and removable drives are rejected before the app looks inside, so it never triggers a Windows login to a remote share.

Every `RobloxPlayerBeta.exe` is checked when it is found and again right before launch:

- Its real path is inside the configured folder.
- The file name is exactly `RobloxPlayerBeta.exe`.
- Neither the file nor its folder is a symbolic link or junction.
- Windows `WinVerifyTrust` accepts its Authenticode signature, and the signer is Roblox Corporation.

Hashing the roughly 140 MB client costs about half a second of CPU, so the signature isn't re-checked every time. After a successful check the app keeps the file open with writes denied until it exits; reads and deletion stay allowed so Roblox can still remove old versions. Later checks run all path rules again and confirm, by volume serial number and 128-bit file ID, that the path still points to that same pinned file. A replaced, renamed or deleted file is checked from scratch.

Roblox is started with `ProcessStartInfo.ArgumentList`, not through a shell, so the launch URI is passed as a single argument and never interpreted as a command.

## Logging

Cookies and launch tickets are never logged. `auto-reconnect.log` contains account names, user IDs, retry counts and Place and Job IDs, with untrusted text cleaned so it can't forge log lines. The Debug-only binding log contains no secrets on purpose.

## Dependencies and CI

Package versions are locked in `packages.lock.json`, and NuGet audit warnings `NU1901` to `NU1904` fail the build. CI restores in locked mode, scans the source and release archives for keys, secrets and runtime data, lists vulnerable direct and transitive packages, builds Release and runs the tests with read-only repository permissions.

On September 27, 2026, NuGet reported no known vulnerable direct or transitive packages for either project.

## Known limits

- Code already running as your Windows user can use your DPAPI keys. The app can't add a second boundary without a separate password or hardware-backed confirmation.
- The one-time Roblox launch ticket is briefly visible in the Roblox process command line, because the Roblox launch protocol requires it there. It expires quickly and works only once.
- Checking the executable right before `Process.Start` narrows but can't fully close a same-user race. Pinning rules out changing the file in place; swapping the path to another file between the last identity check and `Process.Start` remains possible in theory.
- The Roblox executable's signature is verified, but the other files in a Roblox version folder aren't.
- Automatic updates depend on the GitHub account behind the repository, as described under [Updates](#updates).
- Plain-text account fields in `accounts.json` are readable by anyone with access to your Windows profile.

## Reporting a vulnerability

Report security problems privately to the maintainer through the [Instance Manager Discord](https://discord.gg/8XyKcZdSGe) instead of opening a public issue. Never post Roblox cookies, launch tickets, personal data or a working exploit in public.

# Security

[README](../README.md) · [Features](FEATURES.md) · [Troubleshooting](TROUBLESHOOTING.md) · [Data storage](DATA-STORAGE.md) · Security · [Architecture](ARCHITECTURE.md) · [Changelog](../CHANGELOG.md)

Instance Manager is a local, single-user Windows app. It has no server, no account system of its own and no telemetry. The most sensitive thing it holds is the Roblox session cookie of each account, because whoever has that cookie can act as that account. This page describes how the app protects those cookies and the rest of your data, and where the protection ends.

## Trust model

- Trusted: the current Windows user, the app's exe, Windows' crypto and code-signing services, Roblox's HTTPS endpoints and the GitHub releases of `JeskoMts/Instance-Manager`.
- Untrusted and checked: pasted links, theme codes, custom FastFlags, JSON files on disk, remote responses, custom Roblox folders, executables and update packages.
- Only partly defensible: other programs running as the same Windows user. The app keeps few reusable secrets and refuses untrusted executables, but DPAPI by design lets any code running as that user decrypt that user's data.
- Out of scope: attackers with administrator or kernel access, or with full control of the Windows session.

The app runs as the current user (`asInvoker`) and never asks for administrator rights. It doesn't listen for network connections.

## Login and the session cookie

Login uses the real Roblox page in WebView2. Each login dialog creates its own WebView2 environment with an InPrivate profile and disposes it when the dialog closes, so no login state outlives the dialog. Top-level navigation is limited to HTTPS on `roblox.com` and its subdomains. Pop-ups, downloads, external URI schemes, DevTools, browser shortcuts, autofill, password saving, host objects and web messages are all turned off, and requests for the camera, microphone, location, notifications or any other permission are denied.

Whether the login works, is cancelled, the window is closed or an error occurs, cookies and browsing data are deleted before the browser is disposed. The WebView2 runtime folder is `%LOCALAPPDATA%\Instance Manager\webview`, outside the roaming profile, and an older roaming copy from earlier releases is deleted at startup.

The app reads the session cookie only after the Roblox login, confirms with Roblox which user it belongs to, encrypts it right away and clears the browser session. Cookie values are checked for length and control characters before they go into an HTTP header. Check sessions asks the same Roblox endpoint about each saved login, and a login that answers for a different user than the saved one counts as expired.

## How secrets are stored

Cookies are encrypted with Windows DPAPI (`DataProtectionScope.CurrentUser`), so a copied `accounts.json` or `settings.json` normally can't be decrypted under another Windows user or on another PC. A fixed entropy string keeps this app's DPAPI data apart from other apps; it isn't treated as a password.

Byte buffers that hold plaintext during DPAPI calls are zeroed right after use. While a cookie is checked, traded for a launch ticket or used to look up a share link, it briefly exists as a .NET string, which can't be wiped reliably.

User IDs, usernames, display names, nicknames, notes and group data stay in plain text in `accounts.json`. A private server link in the launch bar, a favorite or an automation is plain text as well. Avatar headshots are public Roblox images and are cached unencrypted as `%LOCALAPPDATA%\Instance Manager\avatars\<userId>.png`, next to `<userId>.url` with the Roblox address the picture came from. That address is only compared with the one Roblox reports and is never requested. The file names come from the numeric user ID only, so they can't point outside that folder.

## Network

The shared `HttpClient` has cookies and automatic redirects turned off, and it drops any Roblox API answer larger than four megabytes, including compressed answers, instead of buffering it. Server links are checked against an HTTPS allowlist, and every redirect is validated by hand. Windows validates TLS certificates. The app doesn't pin certificates, because there would be no way to ship an emergency pin update.

The cookie goes only to fixed Roblox endpoints: the authentication and user endpoints for logins, launches and Check sessions, and the share link endpoint when you launch with a `roblox.com/share` link. It is never sent to the address in a pasted link or to a redirect. Game details and server lists are requested without it. [ARCHITECTURE.md](ARCHITECTURE.md#external-services) lists every address.

Avatar and game images must come over HTTPS from `rbxcdn.com` or its subdomains, and image downloads are streamed and stop at five megabytes, including compressed responses and responses without a `Content-Length`.

The app contacts only Roblox and GitHub. The Discord button next to the bell opens the invite in your browser; the app itself doesn't load it.

## Updates

When the published app starts, and then once an hour, it asks `api.github.com` for the latest release of `JeskoMts/Instance-Manager`. Update automatically under Settings > Updates turns both off. The request carries only a user agent with the app version. If the release is newer than the running build, the update has to pass these checks, none of which can be turned off:

- The asset must be named `InstanceManager-<tag>.zip`, and its download URL, once `../` segments are resolved, must be exactly `https://github.com/JeskoMts/Instance-Manager/releases/download/<tag>/InstanceManager-<tag>.zip`.
- Redirects are followed by hand: at most five, HTTPS only, and only to `github.com`, `objects.githubusercontent.com` and `release-assets.githubusercontent.com`.
- The download is capped at 64 MB, and its SHA-256 must match the `sha256:` digest GitHub reports for the asset. Without a digest there is no update.
- The zip may contain only plain file names (no folders, no `..`, no alternate data streams, no invalid characters, no duplicates), its unpacked size is capped, and only `Instance Manager.exe` is extracted.
- The running exe is swapped by renaming, and a failed swap puts the old exe back.

Roll back uses only the copy the app made of the exe it ran before the last update, in `%LOCALAPPDATA%\Instance Manager\previous-version`. That folder is as writable by your Windows user as the exe itself, so it adds no new way in.

The single-file exe carries the native `WebView2Loader.dll`, which the .NET host unpacks to `%TEMP%\.net\<exe name>\` and loads from there. Like everything else in your profile, that folder is writable by other programs running as you; see the trust model above.

The digest proves the file is the one attached to the release. It doesn't prove who attached it. Release builds aren't code-signed, so an update is exactly as trustworthy as the GitHub account and repository that publish it. Whoever controls those can ship code that runs as your Windows user at the next start. You place the same trust in the repository when you download a release by hand.

## Changing Roblox files and windows

The graphics options write two kinds of files, both in your own profile:

- FastFlags go into `ClientSettings\ClientAppSettings.json` inside the Roblox version folders the app found and checked. Only flags on a fixed allowlist of 18 rendering flags are written, and custom flags with any other name are skipped and listed. Custom flags must be one JSON object of at most 64 KB with at most four levels of nesting. When the option is turned off, a flag file is deleted only if its SHA-256 still matches the one the app wrote, so a file you edited yourself stays.
- The FPS cap changes the `FramerateCap` value in `%LOCALAPPDATA%\Roblox\GlobalBasicSettings_13.xml` and leaves the rest of the file as it was.

Files that are locked or can't be read are skipped, and the launch goes on.

Anti-AFK brings the windows of clients the app started to the front and sends one key press with `keybd_event`. You choose the key. The Windows keys and the media, browser and launch keys are refused, because they would act on the whole PC instead of on the Roblox window. It reads no input and touches no other window, apart from giving focus back to the one you were using.

## Windows integration

- The app installs no keyboard hook and never sees keys you press. Anti-AFK only sends one.
- Start with Windows is one value under the current user's `Run` key. No administrator rights, service or scheduled task are involved.
- The single-instance check uses a mutex and an event in the `Local\` namespace, which are visible only inside your Windows session. The event carries no data; it only asks the running copy to show its window.
- The tray icon uses the normal notification area API and has no other access.

## Input limits

- Server links have a length cap, must be correctly percent-encoded, must stay on Roblox HTTPS addresses and may redirect at most five times.
- Place IDs must be positive integers, and Job IDs must be GUIDs. Private server link codes and share codes may hold only letters, digits, hyphens and underscores, at most 128 of them.
- Theme imports are size-checked on the raw clipboard data before WPF turns it into text. The encoded and decoded payload, the JSON depth, the theme name, the fields and every color are checked after that. Hex colors typed into the color picker must have three or six hex digits.
- Automation times must be valid 24-hour times, and an automation's game or server link goes through the same checks as the launch bar.
- JSON files over four megabytes are refused before parsing.
- Log fields are put on one line, stripped of control characters, line and paragraph separators and bidi controls, and capped in length, and the log rotates at one megabyte.

## Launching Roblox

A custom Roblox folder must be a full path on a fixed local drive. UNC paths, network drives, relative paths, device paths and removable drives are refused before the app looks inside, so it never triggers a Windows login to a remote share.

Every `RobloxPlayerBeta.exe` is checked when it is found and again right before launch:

- Its real path is inside the configured folder.
- The file name is exactly `RobloxPlayerBeta.exe`.
- Neither the file nor its folder is a symbolic link or junction.
- Windows `WinVerifyTrust` accepts its Authenticode signature, and the signing certificate's common name or organization is exactly Roblox Corporation. A name that only contains it, such as "Roblox Corporation Fan Tools Ltd", is refused.

Hashing the roughly 140 MB client costs about half a second of CPU, so the signature isn't checked again every time. After a successful check the app keeps the file open with writes denied until it exits; reads and deletion stay allowed so Roblox can still remove old versions. Later checks run all path rules again and confirm, by volume serial number and 128-bit file ID, that the path still points to that same pinned file. A replaced, renamed or deleted file is checked from scratch.

Roblox is started with `ProcessStartInfo.ArgumentList`, not through a shell, so the launch URI is passed as a single argument and never read as a command.

## Logging

Cookies and launch tickets are never logged. `auto-reconnect.log` contains account names, user IDs, retry counts and Place and Job IDs. A private server's link code is left out. Untrusted text in it is cleaned so it can't forge log lines. The Debug-only binding log contains no secrets.

## Dependencies and CI

Package versions are locked in `packages.lock.json`, and NuGet audit warnings `NU1901` to `NU1904` fail the build. CI restores in locked mode, scans the source and release archives for keys, secrets and runtime data, lists vulnerable direct and transitive packages, builds Release and runs the tests with read-only repository permissions.

On September 28, 2026, NuGet reported no known vulnerable direct or transitive packages for either project.

## Known limits

- Code already running as your Windows user can use your DPAPI keys. The app can't add a second boundary without a separate password or hardware-backed confirmation.
- The one-time Roblox launch ticket is briefly visible in the Roblox process command line, because the Roblox launch protocol requires it there. It expires quickly and works only once.
- Checking the executable right before `Process.Start` narrows but can't fully close a same-user race. Pinning rules out changing the file in place; swapping the path to another file between the last identity check and `Process.Start` remains possible in theory.
- The Roblox executable's signature is checked, but the other files in a Roblox version folder aren't.
- Automatic updates depend on the GitHub account behind the repository, as described under [Updates](#updates).
- Plain-text account fields in `accounts.json` are readable by anyone with access to your Windows profile.

## Reporting a vulnerability

Report security problems privately to the maintainer through the [Instance Manager Discord](https://discord.gg/8XyKcZdSGe) instead of opening a public issue. Never post Roblox cookies, launch tickets, personal data or a working exploit in public.

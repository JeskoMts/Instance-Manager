# Security

Instance Manager is a local, single-user Windows desktop app. It has no server, no account system of its own, no telemetry, and no inbound listener. Its most valuable asset is the Roblox `.ROBLOSECURITY` session cookie: possession of that cookie can give control of the corresponding Roblox account.

The detailed June 24, 2026 audit, including CVSS 3.1 vectors and remediation evidence, is in [SECURITY-AUDIT-2026-06-24.md](SECURITY-AUDIT-2026-06-24.md).

## Trust model

- **Trusted:** the current Windows user, the installed application files, Windows cryptographic and trust services, and official Roblox HTTPS endpoints.
- **Untrusted and validated:** typed or pasted links, theme codes, persisted JSON, remote URLs and response bodies, custom Roblox-version paths, and executable files.
- **Partially defensible:** another process running as the same Windows user. The app minimizes reusable secrets and refuses untrusted executables, but Windows DPAPI intentionally allows code running as that user to decrypt that user's data.
- **Out of scope:** administrator, kernel, or fully compromised Windows-session attackers.

The app runs as the current user with `asInvoker` and never requests administrator rights.

## Login and session-cookie handling

Account login uses the real Roblox login page in WebView2. Each login dialog builds its own WebView2 environment with an InPrivate/off-the-record profile and disposes it on close, so no shared or static login browser state outlives the dialog. Top-level navigation is limited to HTTPS on `roblox.com` and its subdomains; popups, downloads, external URI schemes, DevTools, browser accelerator keys, autofill, password saving, host objects, and web messages are disabled.

Successful capture, Cancel, title-bar close, and error cleanup all delete cookies and profile browsing data before disposal. WebView2 runtime data lives under `%LOCALAPPDATA%\Instance Manager\webview`, not roaming `%APPDATA%`. Old `%APPDATA%\Instance Manager\webview` state from earlier releases is removed best-effort at startup.

The app reads the `.ROBLOSECURITY` cookie only after Roblox login, validates it against `users/authenticated`, encrypts it immediately, and clears the browser session. Cookie values are length- and control-character checked before being added to an HTTP header.

## Secret storage

Cookies are encrypted with Windows DPAPI using `DataProtectionScope.CurrentUser`. A copied `accounts.json` cannot normally be decrypted under another Windows user or on another machine. The fixed entropy string separates this app's DPAPI blobs; it is not treated as a password.

Plaintext byte buffers used during DPAPI operations are zeroed immediately after use. A plaintext managed string still exists briefly while a cookie is validated or exchanged for a one-time Roblox authentication ticket; .NET strings cannot be reliably wiped in place.

User IDs, usernames, display names, aliases, notes, and organization metadata remain plaintext in `accounts.json`. Only the session cookie is encrypted.

## Network security

The shared `HttpClient` has cookies and automatic redirects disabled. Roblox server links use HTTPS allowlists and manually validate every redirect. Authentication cookies are sent only to fixed Roblox authentication/user endpoints. The operating system performs TLS certificate validation; certificate pinning is deliberately not used because there is no maintained emergency pin-update channel.

Avatar and game-image URLs must be HTTPS on `rbxcdn.com` or its subdomains. Image bodies are streamed through a strict five-megabyte limit, including responses without `Content-Length` and decompressed responses.

## Input and resource limits

- Roblox server links are capped in length, require valid percent-encoding, remain on Roblox HTTPS origins, and follow at most five redirects.
- Place IDs must be positive integers and Job IDs must be GUIDs.
- Theme imports are size-checked at the native clipboard handle before WPF materializes text. Encoded and decoded payloads, JSON depth, theme names, fields, and every color value are then validated.
- Persisted JSON files larger than four megabytes are rejected before deserialization.
- Auto Reconnect log fields are flattened to one line, control characters are removed, messages are capped, and the log rotates at one megabyte.

## Executable launch security

Custom Roblox-version roots are allowed only as fully qualified directories on a local fixed drive. UNC, network, relative, device, removable, and mapped-network roots are rejected before filesystem enumeration, preventing implicit SMB authentication.

Every `RobloxPlayerBeta.exe` is checked both during discovery and immediately before launch:

- Canonical path remains below the configured root.
- Filename is exactly `RobloxPlayerBeta.exe`.
- File and directory path do not use reparse points.
- Windows `WinVerifyTrust` accepts the embedded Authenticode signature.
- The signer identity is Roblox Corporation.

`ProcessStartInfo.ArgumentList` is used rather than a shell command, so launch data is one process argument rather than command text.

## Logging

Cookies and authentication tickets are never logged. `auto-reconnect.log` contains account labels, user IDs, retry state, and place/job IDs. Untrusted text is sanitized to prevent forged log lines. Debug-only WPF binding logs contain no intentional secret fields.

## Dependencies and CI

Package versions are locked with `packages.lock.json`. NuGet audit warnings `NU1901` through `NU1904` are build errors. CI restores in locked mode, scans source and release archives for secret/key patterns and forbidden runtime data, reports direct and transitive vulnerable packages, builds Release, and runs the test suite with read-only repository permissions.

As of June 24, 2026, the configured NuGet advisory sources report no known vulnerable direct or transitive packages.

## Residual risks

- Code already running as the same Windows user can use that user's DPAPI context. The app cannot create a trustworthy second security boundary without a separate credential or hardware-backed user-presence flow.
- The one-time Roblox authentication ticket is briefly present in the Roblox process command line because the Roblox launch protocol requires it. It is short-lived and single-use.
- Revalidating the executable immediately before `Process.Start` reduces but cannot mathematically eliminate a same-user time-of-check/time-of-use race.
- A valid signed Roblox directory could theoretically contain vulnerable or maliciously replaced sidecar content. The executable signature is verified, but the app does not maintain a signed manifest for every Roblox installation file.
- Plaintext identity and note fields remain visible to anyone who can read the Windows profile.

## Reporting a vulnerability

Follow the repository-level [security policy](../SECURITY.md). Do not place Roblox cookies, authentication tickets, personal data, or working exploit details in a public issue.

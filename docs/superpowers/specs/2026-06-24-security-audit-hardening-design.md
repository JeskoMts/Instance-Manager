# Security Audit and Hardening Design

## Goal

Perform a complete, evidence-based security review of Instance Manager, score every confirmed vulnerability with CVSS 3.1, fix exploitable weaknesses, add proportionate defense-in-depth, and leave a repeatable security verification process.

## Scope and threat model

The review covers all source, tests, project/build configuration, CI, release archives, dependencies, local persistence, WebView2 authentication, Roblox HTTPS integrations, process launching, logging, parsing, concurrency, and resource handling.

The primary assets are:

- Roblox `.ROBLOSECURITY` cookies.
- One-time Roblox authentication tickets.
- Account identity and notes.
- Integrity of the Roblox executable selected for launch.
- Availability and integrity of the local application and its data.

The attacker models are:

1. A remote party controlling user-supplied links or imported theme text.
2. A network service returning malformed or excessively large data through an otherwise valid HTTPS connection.
3. Another non-elevated process running under the same Windows user.
4. A party obtaining copied application data or release artifacts.

Administrator, kernel, and fully compromised Windows-session attackers are outside the achievable protection boundary. Same-user code execution is nevertheless included for hardening: the app will minimize reusable secrets, validate executable trust where practical, restrict file behavior, and avoid converting data tampering into execution.

## Audit methodology

The audit combines:

- Manual source review by trust boundary and data flow.
- Targeted searches for process execution, cryptography, serialization, networking, filesystem access, native interop, secrets, and dangerous APIs.
- Current NuGet advisory and outdated-package checks.
- Release archive inspection.
- Adversarial unit tests for malformed, oversized, redirected, and locally manipulated input.
- Release build and full test-suite verification.

Only reproducible, exploitable weaknesses are called vulnerabilities. Each confirmed vulnerability receives:

- A CVSS 3.1 Base score and complete vector.
- CWE classification.
- Preconditions and affected assets.
- Reproduction evidence.
- Root cause and exact remediation.
- Regression-test evidence and residual-risk statement.

Hardening measures without a distinct exploit path are reported separately and are not assigned inflated CVSS scores. CVSS scoring follows the official FIRST v3.1 specification and publishes the vector alongside the score.

## Security architecture changes

### Authentication browser lifecycle

The login browser must not preserve reusable Roblox authentication state after any exit path. Successful capture, Cancel, window-close, initialization failure, and unexpected exceptions all converge on one idempotent cleanup path. Login state is isolated from subsequent dialogs, cookies and browser data are cleared before disposal, and profile cleanup is retried when WebView2 still holds files.

The browser is navigation-restricted to Roblox HTTPS origins needed for authentication. New-window requests, downloads, external schemes, and unexpected origins are blocked or opened only through an explicit safe path. DevTools, default context menus, host objects, and unnecessary browser capabilities remain disabled.

### Executable launch integrity

`VersionsPathOverride` remains available, but executable discovery and launch share one validator. A candidate must:

- Be a regular existing file named `RobloxPlayerBeta.exe`.
- Resolve beneath the selected versions root without traversal.
- Not rely on a reparse-point escape from that root.
- Carry a valid trusted Authenticode signature whose signer identity matches Roblox Corporation when Windows exposes signer information.

Validation is repeated immediately before process creation to reduce time-of-check/time-of-use exposure. Failure is surfaced as a per-account launch failure rather than silently executing an untrusted file.

### Bounded untrusted data

All data crossing an untrusted boundary receives explicit limits:

- Theme-code characters and decoded JSON bytes.
- JSON persistence file bytes and collection sizes.
- HTTP response body bytes for avatars and game thumbnails.
- Text query lengths, redirect target lengths, and redirect count.
- Log line and log-file growth where app-controlled output is involved.

Malformed percent-encoding, invalid URI components, unsupported JSON shapes, duplicate identifiers, invalid color strings, and oversized payloads fail closed without terminating the UI thread.

### Filesystem protections

Sensitive data files are created under the current user profile with access limited to the current user and SYSTEM where Windows permits it. Atomic saves retain the existing replace/move strategy while rejecting reparse-point targets and avoiding writes through unexpected links. Temporary files are uniquely named in the destination directory and cleaned best-effort.

Account files continue to contain only DPAPI ciphertext for cookies. Plaintext cookie and ticket strings are kept in the narrowest possible scope and never included in exceptions, progress messages, logs, or test output.

### Network and HTTP behavior

All outbound destinations use explicit HTTPS allowlists. Redirects remain manual and every hop is revalidated. Authentication requests never follow redirects and never forward cookies outside their exact Roblox endpoint. Response reads are bounded and cancellation-aware. Security headers returned by Roblox are parsed defensively.

TLS certificate validation remains Windows-managed; certificate pinning is not introduced because it creates brittle outage and emergency-rotation risk without a maintained pin-delivery channel.

### Supply chain and CI

Direct packages are updated to compatible supported releases after regression testing; framework-major upgrades are not mixed into this audit unless required by an advisory. CI gains:

- `dotnet list package --vulnerable --include-transitive`.
- Locked restore metadata or an equivalent deterministic dependency check.
- Release build and test execution.
- A repository secret scan using a pinned action or deterministic local scanner.
- Minimal workflow permissions.

Release archives must exclude user data, WebView profiles, logs, test output, and credentials. Security documentation records the reporting channel, supported versions, disclosure expectations, and known residual risks.

## Error handling and compatibility

Security rejection messages explain the rejected category without echoing secrets. Existing account and settings files remain readable. Invalid records are quarantined or skipped rather than causing total data loss. No migration writes plaintext credentials.

Security controls that depend on Windows APIs degrade safely: an unverifiable executable is rejected, while inability to tighten an already user-private directory is logged as hardening status and does not destroy data.

## Testing and acceptance

Every production behavior change follows red-green-refactor:

1. Add a minimal failing regression or adversarial test.
2. Run it and confirm the expected security failure.
3. Implement the smallest root-cause fix.
4. Run the focused test and then the complete suite.

Acceptance requires:

- No known vulnerable NuGet packages from current configured feeds.
- No secrets in tracked source or release archives.
- Every confirmed vulnerability fixed or explicitly documented as accepted residual risk.
- Full Release build and test suite passing.
- Security tests covering every modified trust boundary.
- A final report containing findings, CVSS vectors, evidence, fixes, hardening, and residual risks.

## Initial evidence

Before implementation, the Release test baseline is 297 passing tests. Current NuGet advisory sources report no known vulnerable direct or transitive packages. Secret-pattern scanning found no embedded credentials. The repository's `.git` directory is empty, so this workspace cannot create the commits normally required by the workflow; documentation and code changes remain directly inspectable in the workspace.

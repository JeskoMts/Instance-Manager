# Security Audit and Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Find, score, fix, and verify security weaknesses across Instance Manager while preserving its supported local Windows workflow.

**Architecture:** Security checks are placed at the trust-boundary services that own authentication browsing, executable discovery/launch, parsing, HTTP body reads, and persistence. Shared validators prevent discovery-time and use-time disagreement, while regression tests prove each exploit path is closed.

**Tech Stack:** .NET 8, WPF, WebView2, xUnit, Windows DPAPI, Authenticode/WinTrust, GitHub Actions, CVSS 3.1.

---

### Task 1: Complete evidence-backed attack-surface audit

**Files:**
- Inspect: `src/InstanceManager/**/*.cs`
- Inspect: `src/InstanceManager/**/*.xaml`
- Inspect: `tests/InstanceManager.Tests/**/*.cs`
- Inspect: `.github/workflows/ci.yml`
- Inspect: `release-assets/*.zip`
- Create: `docs/SECURITY-AUDIT-2026-06-24.md`

- [ ] Trace cookie, ticket, URL, file path, JSON, image-byte, process, and log data from source to sink.
- [ ] Reproduce each suspected weakness with a focused test or deterministic command before classifying it as a finding.
- [ ] Record confirmed findings with CWE, CVSS 3.1 score/vector, evidence, affected files, and remediation status.
- [ ] Record non-exploitable observations separately as defense-in-depth.
- [ ] Re-run `dotnet list InstanceManager.sln package --vulnerable --include-transitive` and archive the result in the report.

### Task 2: Eliminate persistent WebView2 authentication residue

**Files:**
- Modify: `src/InstanceManager/Views/AddAccountWindow.xaml.cs`
- Modify: `src/InstanceManager/Services/AppPaths.cs`
- Create or modify: `tests/InstanceManager.Tests/WebViewSecurityTests.cs`

- [ ] Add a failing contract test proving every close path invokes one idempotent session-cleanup routine and no static shared login profile remains.
- [ ] Add failing tests for navigation policy: allow required Roblox HTTPS hosts; reject HTTP, non-Roblox hosts, downloads, external schemes, and popup navigation.
- [ ] Implement a per-dialog isolated WebView2 profile or supported in-private profile, with cookie/browser-data cleanup on success, Cancel, title-bar close, initialization failure, and exception.
- [ ] Wire `NavigationStarting`, `NewWindowRequested`, and `DownloadStarting` to the allowlist policy.
- [ ] Run the focused WebView tests and then the full suite.

### Task 3: Validate Roblox executable trust at discovery and launch

**Files:**
- Create: `src/InstanceManager/Services/RobloxExecutableValidator.cs`
- Modify: `src/InstanceManager/Services/VersionService.cs`
- Modify: `src/InstanceManager/Services/RobloxLauncher.cs`
- Modify: `src/InstanceManager/Composition/ServiceCollectionExtensions.cs`
- Create: `tests/InstanceManager.Tests/RobloxExecutableValidatorTests.cs`
- Modify: `tests/InstanceManager.Tests/RobloxLauncherTests.cs`
- Modify: `tests/InstanceManager.Tests/VersionServiceTests.cs`

- [ ] Add failing tests rejecting a wrong filename, a candidate outside the configured root, a reparse-point escape where supported, and an unsigned test executable.
- [ ] Add an injectable validator interface whose production implementation resolves canonical paths and verifies Windows trust plus Roblox signer identity.
- [ ] Make `VersionService` return only validated candidates and retain a clear validation reason for UI diagnostics.
- [ ] Revalidate the exact executable immediately before `Process.Start`.
- [ ] Run focused validator/discovery/launcher tests and the full suite.

### Task 4: Bound and validate imported and persisted data

**Files:**
- Modify: `src/InstanceManager/Services/ThemeCodec.cs`
- Modify: `src/InstanceManager/Services/JsonFileStore.cs`
- Modify: `src/InstanceManager/Models/ThemeDefinition.cs`
- Create or modify: `tests/InstanceManager.Tests/ThemeCodecTests.cs`
- Create or modify: `tests/InstanceManager.Tests/JsonFileStoreSecurityTests.cs`

- [ ] Add failing tests for oversized Base64 theme input, oversized decoded JSON, invalid colors, empty IDs, excessive names, and excessive JSON file size.
- [ ] Add explicit constants for accepted character/byte limits and reject before allocating large decoded objects.
- [ ] Validate every palette field using the existing color parser contract before accepting a theme.
- [ ] Read persistence files through a bounded stream and fall back safely when the limit is exceeded.
- [ ] Run focused codec/store tests and the full suite.

### Task 5: Harden URI parsing and HTTP response consumption

**Files:**
- Modify: `src/InstanceManager/Services/ServerLinkResolver.cs`
- Modify: `src/InstanceManager/Services/RobloxAvatarService.cs`
- Modify: `src/InstanceManager/Services/RobloxGamesService.cs`
- Modify: `src/InstanceManager/Composition/ServiceCollectionExtensions.cs`
- Modify: `tests/InstanceManager.Tests/ServerLinkResolverTests.cs`
- Modify: `tests/InstanceManager.Tests/RobloxAvatarServiceTests.cs`
- Create or modify: `tests/InstanceManager.Tests/RobloxGamesServiceSecurityTests.cs`

- [ ] Add failing tests showing malformed percent-encoding returns a normal validation failure.
- [ ] Add failing tests showing image bodies larger than the configured limit are rejected without reading unbounded memory.
- [ ] Add failing tests for unsupported content types and excessive query length.
- [ ] Implement exception-safe query decoding and a shared bounded HTTP-body reader.
- [ ] Configure transport limits and keep automatic redirects and cookies disabled.
- [ ] Run focused network/parser tests and the full suite.

### Task 6: Protect sensitive local files and logs

**Files:**
- Modify: `src/InstanceManager/Services/AppPaths.cs`
- Modify: `src/InstanceManager/Services/JsonFileStore.cs`
- Modify: `src/InstanceManager/Services/AutoReconnectLog.cs`
- Create: `tests/InstanceManager.Tests/FileSecurityTests.cs`

- [ ] Add failing Windows tests that verify data-directory and sensitive-file ACLs do not grant broad write/read access.
- [ ] Add failing tests rejecting writes through reparse-point targets and ensuring temporary filenames cannot be predicted and pre-planted.
- [ ] Apply current-user/SYSTEM ACLs when creating the data directory and sensitive files.
- [ ] Use unique same-directory temporary files, flush before replacement, and preserve retry behavior.
- [ ] Sanitize log fields to one line and cap app-controlled log growth with rotation.
- [ ] Run focused filesystem/log tests and the full suite.

### Task 7: Update compatible dependencies and automate supply-chain checks

**Files:**
- Modify: `src/InstanceManager/InstanceManager.csproj`
- Modify: `tests/InstanceManager.Tests/InstanceManager.Tests.csproj`
- Modify: `.github/workflows/ci.yml`
- Create: `Directory.Packages.props` or `packages.lock.json` files if compatible with the solution
- Create: `SECURITY.md`

- [ ] Update direct dependencies only within compatible target-framework lines unless a security advisory requires a major framework change.
- [ ] Restore, build, and test after each dependency group so regressions are attributable.
- [ ] Add CI commands for vulnerable-package detection and deterministic restore.
- [ ] Add a pinned secret-scanning step with read-only workflow permissions.
- [ ] Document supported versions and a private vulnerability-reporting route without inventing an email address.

### Task 8: Final verification and security report

**Files:**
- Finalize: `docs/SECURITY-AUDIT-2026-06-24.md`
- Modify: `docs/SECURITY.md`
- Modify: `README.md` only where behavior or requirements changed

- [ ] Run `dotnet restore InstanceManager.sln --locked-mode` when lock files are enabled.
- [ ] Run `dotnet build InstanceManager.sln --configuration Release --no-restore`.
- [ ] Run `dotnet test InstanceManager.sln --configuration Release --no-build`.
- [ ] Run the current vulnerable-package and outdated-package checks.
- [ ] Repeat secret and release-archive scans.
- [ ] Review every finding against its regression test and recompute the published CVSS 3.1 vector.
- [ ] Document accepted residual risks, especially the unavoidable same-user DPAPI boundary and brief ticket exposure required by Roblox process launch.

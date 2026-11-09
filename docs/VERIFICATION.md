# Verification

## Release 1.1.5 — 2026-11-09

Built with the .NET 8 SDK. `dotnet test` passed all tests, including new tests that close named Windows objects held by a helper process. Against the real Roblox client 0.746 it was checked that a tray process started with `--launch-to-tray` holds `ROBLOX_singletonEvent` and `ROBLOX_singletonMutex`, that a game launch is handed to that process (`WebPlay NoReload with Tray only`), that the 1.1.4 approach of holding both names makes a second client exit, and that closing the handles lets three Roblox processes run side by side without administrator rights. The full flow with two signed-in accounts was not run.

## Workspace maintenance — 2026-10-23

The existing Instance Manager 1.1.4 executable was retained. Only organization, packaging scripts and documentation were changed. The local machine has a .NET runtime but no .NET SDK, so no new WPF build or xUnit run was performed. No Roblox login or game launch was attempted.

All download folders include SHA-256 manifests. Documentation links and source/download separation were checked locally. Credentials, browser profiles and build caches are excluded.

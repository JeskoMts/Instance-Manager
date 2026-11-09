# Building and packaging

Requires the .NET 8 SDK on Windows.

```powershell
dotnet restore InstanceManager.sln --locked-mode
dotnet build InstanceManager.sln -c Release
dotnet test InstanceManager.sln -c Release
./scripts/build-release.ps1
```

The release script publishes in a temporary directory and emits the program/source GitHub ZIP assets into `release-assets/`. The program's historical updater compatibility filenames must be retained in that ZIP. To refresh the local sibling `InstanceManager-<version>` folder from a verified publish, copy the published `InstanceManager.exe` there as `Instance Manager.exe` and include the user docs. The source tree contains no publish output or installed packages.

Version 1.1.5 was built and tested with the .NET 8 SDK (see [VERIFICATION.md](VERIFICATION.md)).

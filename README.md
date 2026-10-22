# Driver Updater

A small native Windows app that scans a PC's drivers and installs newer versions from official sources only.

- **Sources:** Windows Update, the Microsoft Update Catalog, and the official ASUS, NVIDIA and Intel driver services.
- **Safe installs:** a System Restore point is created first; manufacturer installers must pass a Windows signature check;
  NVIDIA and Intel drivers install silently and driver-only (no NVIDIA app / Intel Graphics Software).
- **Single page UI:** all drivers listed; updates move to the top live during a scan; last scan remembered.
- **One file:** `DriverUpdaterSetup-<version>.exe` installs itself to Program Files, the installed copy is the app,
  and Settings > Apps uninstalls it.

Requires Windows 10 or 11 with .NET Framework 4.8 (built in). Administrator rights are needed to install drivers.

## Repository layout

| Folder | Contents |
|---|---|
| `src/` | C# source. `Core.cs`, `Vendors.cs` are pure logic (unit-tested); `Platform.cs`, `VendorNet.cs`, `Installer.cs`, `App.cs` use Windows APIs; `Xaml.cs` holds the window layouts; `Changelog.cs` feeds the About page. |
| `build/` | Build scripts for Linux/macOS. |
| `tests/` | Logic tests (PowerShell 7). |
| `assets/` | Icon and Windows manifest (requests administrator rights). |
| `third_party/` | NVIDIA GPU list (public domain), also embedded in `src/NvidiaData.cs`. |

## Building

**On Windows** (needs the .NET SDK):
```
dotnet build -c Release
```
Then rename `bin\Release\net48\DriverUpdater.exe` to `DriverUpdaterSetup-<version>.exe`. The file name starting with
`DriverUpdaterSetup` is what makes it run as the installer. Update the version in `src/Version.cs` first.

**On Linux/macOS** (needs PowerShell 7; it bundles the Roslyn C# compiler):
```
./build/build.sh 3.4.1
```
This fetches the .NET Framework 4.8 reference assemblies from `mono/reference-assemblies` on first run and writes
`out/DriverUpdaterSetup-3.4.1.exe` with the version, icon and manifest embedded.

## Tests
```
cd tests
pwsh ./run-all.ps1
```

## Releasing a new version
1. Add an entry at the top of `src/Changelog.cs`.
2. Build with the new version number.
3. Create a GitHub release (tag `v<version>`) and attach `DriverUpdaterSetup-<version>.exe`.

## Notes
- The app is not code-signed yet, so SmartScreen warns on first run and Smart App Control blocks it.
  Signing (e.g. Azure Artifact Signing) fixes both.
- ASUS, NVIDIA and Intel lookups use the same web services their own tools use; they are not documented public APIs
  and may change.
- Logs: `%TEMP%\DriverUpdater\log.txt`. Saved scan and caches: `%ProgramData%\DriverUpdater`.

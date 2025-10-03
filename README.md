# Teine64

Ultra-lightweight Windows tray utility ("caffeine" style) built on .NET 8 + direct Win32 API (no WinForms / WPF) to keep the system awake via `SetThreadExecutionState`.

Current default distribution: **NativeAOT self-contained exe** (no installed .NET runtime required). A secondary artifact provides the **tiny framework-dependent single-file (~160 KB)** build for those who already have the .NET 8 runtime and want minimal disk footprint.

---
## Features
* Starts directly in the system tray (hidden window)
* Double‑click tray icon: toggle Running / Paused
* Right‑click menu:
	* Pause / Resume
	* Pause 5 / 15 / 30 / 60 Minutes (auto-resume, countdown in tooltip)
	* Start with Windows (toggle, persisted)
	* About… dialog
	* Exit
* Timed pauses (5/15/30/60) auto‑resume with balloon notification
* Tooltip always shows: `Teine64 - Running` or `Teine64 - Paused XmYYs`
* Dynamic tiny tea‑mug icons (filled vs empty) generated at runtime (no assets on disk)
* Optional start paused: `--paused`
* Persists last active state + autostart flag in `%APPDATA%/Teine64/config.ini`
* Refreshes execution state every 25 seconds for reliability
* Single source file (`Program.cs`)

---
## How it Works
Creates a hidden window + message loop and registers a tray icon using `Shell_NotifyIcon`. While active it periodically calls:

```
SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED)
```

When paused (manual or timed), it reverts to:

```
SetThreadExecutionState(ES_CONTINUOUS)
```

Icons are 16×16 ARGB bitmaps created with a DIB section and converted via `CreateIconIndirect`.

---
## Build
Requires the .NET 8 SDK. (NativeAOT also needs the Visual C++ build tools on Windows — already assumed present.)

```powershell
dotnet build .\src\Teine64\Teine64.csproj -c Release
```

## Run (debug)
```powershell
dotnet run --project .\src\Teine64\Teine64.csproj -- --paused
```

publish\Teine64.exe
## Publish (NativeAOT default)
Produces a single self-contained native executable (no runtime dependency):

```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64
```

Result: `src/Teine64/bin/Release/net8.0/win-x64/publish/Teine64.exe`

### Alternate: framework-dependent ultra-small single file (~160 KB)
```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64 --self-contained false -p:PublishAot=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false -p:StripSymbols=true
```

### Alternate: self-contained managed single file (non-AOT)
```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### About Sizes (approximate)
* Framework-dependent single-file: ~160 KB (needs .NET 8 runtime installed)
* NativeAOT: larger (MBs) but zero dependencies & fastest startup
* Self-contained managed single-file: between the two extremes

---
## Usage
Launch `Teine64.exe` (it appears in tray only).

Arguments:
* `--paused` start in paused state.

State indicators:
* Filled mug = Paused (per latest icon inversion requirement)
* Empty mug = Running (preventing sleep)

Tooltip examples:
* `Teine64 - Running`
* `Teine64 - Paused 3m12s`

---
## Cleaning
Intermediate build artifacts live under `src/Teine64/bin` and `src/Teine64/obj`.
You can safely remove them:
```powershell
Remove-Item -Recurse -Force .\src\Teine64\bin, .\src\Teine64\obj
```
The distributable is only the file in `publish/`.

---
## Future Enhancements (Ideas)
* Custom user-defined pause durations
* Multi-monitor awareness / optional display-off prevention only
* Additional NativeAOT size reduction (profile-guided / analyzer trimming hints)
* Windows toast notifications (modern) instead of legacy balloon
* Optional configurable resume notification style

---
## Versioning
Semantic versioning (MAJOR.MINOR.PATCH). Current: see `CHANGELOG.md`.
Builds on CI embed file/product version info; InformationalVersion may append commit SHA for traceability.

---
## License
MIT

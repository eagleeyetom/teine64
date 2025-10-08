# Teine64

[![Build](https://github.com/eagleeyetom/teine64/actions/workflows/build.yml/badge.svg)](https://github.com/eagleeyetom/teine64/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/eagleeyetom/teine64?sort=semver)](https://github.com/eagleeyetom/teine64/releases)
[![Downloads](https://img.shields.io/github/downloads/eagleeyetom/teine64/total)](https://github.com/eagleeyetom/teine64/releases)
[![License](https://img.shields.io/github/license/eagleeyetom/teine64)](LICENSE)

Ultra‑light, zero‑window Windows tray utility ("caffeine" style) built on .NET 8 + raw Win32 to keep your system & display awake. Single source file core, instant startup, no dependencies (NativeAOT default).

Default artifact: NativeAOT self‑contained executable. Alternate artifact: tiny framework‑dependent single-file (~160 KB) if you already have the .NET 8 desktop runtime.

---
## Features
* Tray-only (no taskbar window) — double‑click toggles Running / Paused
* Timed pauses: 5 / 15 / 30 / 60 minutes (auto‑resume + balloon notice)
* Tooltip always shows live state & remaining pause countdown
* Runtime‑generated tea mug icons (no static assets)
* Persisted state + autostart toggle (registry) + optional `--paused` start flag
* Single source file core (`Program.cs`), minimal Win32 P/Invoke surface
* NativeAOT default build (fast cold start, no installed runtime required)

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
## Download
Grab the latest release from: https://github.com/eagleeyetom/teine64/releases

Artifacts:
* Teine64.exe (NativeAOT) — standalone
* Teine64.exe (fdd-singlefile) — framework-dependent ultra-small

## Build
Requires the .NET 8 SDK. (NativeAOT also needs the Visual C++ build tools on Windows.)

```powershell
dotnet build .\src\Teine64\Teine64.csproj -c Release
```
## Run (debug)
```powershell
dotnet run --project .\src\Teine64\Teine64.csproj -- --paused
```

## Publish (NativeAOT default)
Produces a single self-contained native executable (no runtime dependency):

```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64
```

Result: `src/Teine64/bin/Release/net8.0/win-x64/publish/Teine64.exe`

### Alternate: framework-dependent ultra-small single file (~160 KB)
Optional alternative mode (menu: "Simulate Shift+F15") sends a harmless key combination (Shift+F15) every ~59 seconds which some environments prefer when power policies ignore `SetThreadExecutionState`.
```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64 --self-contained false -p:PublishAot=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false -p:StripSymbols=true
```

* Tooltip shows "SimKey Mode" when key simulation mode is active
### Alternate: self-contained managed single file (non-AOT)
```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### Approximate Sizes
* Framework-dependent single-file: ~160 KB (needs .NET 8 runtime)
* NativeAOT: larger (MBs) — zero dependencies & fastest startup
* Self-contained managed single-file: between the two

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
## Versioning
Semantic versioning (MAJOR.MINOR.PATCH). Current: see `CHANGELOG.md`.
Builds on CI embed file/product version info; InformationalVersion may append commit SHA for traceability.

---
## License
MIT

# Teine64

Ultra-lightweight Windows tray utility ("caffeine" style) built on .NET 8 with direct Win32 API (no WinForms / WPF) to keep the system awake via `SetThreadExecutionState`.

Current distribution target: **framework-dependent single-file (~160 KB)**. (NativeAOT can be revisited later once a C++ build toolchain is installed.)

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
Requires only the .NET 8 SDK.

```powershell
dotnet build .\src\Teine64\Teine64.csproj -c Release
```

## Run (debug)
```powershell
dotnet run --project .\src\Teine64\Teine64.csproj -- --paused
```

## Publish (framework-dependent single file)
This produces the minimal ~160 KB executable (expects .NET 8 runtime installed):

```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false
```

The build script copies the result to the root `publish` directory:

```
publish\Teine64.exe
```

### (Optional) Self-contained
Larger (several MB) but no installed runtime needed:
```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### (Deferred) NativeAOT
Enable once the C++ toolchain is installed by setting in the project:
```xml
<PublishAot>true</PublishAot>
```
Then:
```powershell
dotnet publish .\src\Teine64\Teine64.csproj -c Release -r win-x64 -p:PublishAot=true
```

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
* NativeAOT size reduction pass
* Windows toast notifications (modern) instead of legacy balloon

---
## License
MIT

# Changelog

## v0.3.0 (2025-10-08)
### Added
- Optional SimKey mode: periodically sends Shift+F15 (~59s) as alternative keep-awake strategy.

### Changed
- Config persistence now always writes `active=1` so default startup is Running unless `--paused` is supplied.

### Fixed
- Cleaned and restored autostart logic after integration refactor.

### Notes
- SimKey state persisted via `simkey=1/0` in `%APPDATA%/Teine64/config.ini`.


## v0.2.0 (2025-10-03)
NativeAOT becomes the default build.

### Added
- NativeAOT self-contained win-x64 binary (single exe, no .NET runtime needed).
- Dual distribution: keeps an alternative ultra-small framework-dependent single-file (~160 KB) build artifact.
- Assembly/File versioning (0.2.0) embedded for About dialog and file properties.

### Changed
- Project file now enables `<PublishAot>true</PublishAot>` & size-focused settings (IlcOptimizationPreference=Size, InvariantGlobalization, stripped symbols) by default.
- GitHub Actions workflow publishes BOTH the NativeAOT binary and the tiny runtime-dependent single-file variant.

### Fixed
- Removed obsolete `PublishTrimmed` override that conflicted with NativeAOT (trimming is implied).

### Notes
- The NativeAOT executable is larger than the framework-dependent single-file build but starts instantly and needs no installed runtime.
- InformationalVersion now includes commit hash when built under CI for traceability.

## v0.1.0 (2025-10-03)
Initial public release.

### Features
- Prevents system & display sleep using SetThreadExecutionState.
- Tray-only UI (hidden window) with dynamic tea mug icons.
- Double-click toggle (Running/Paused).
- Timed pauses: 5 / 15 / 30 / 60 minutes with auto-resume + balloon notification.
- Tooltip shows current state and remaining pause countdown.
- About dialog with version.
- Autostart toggle (registry) + persisted state/config in %APPDATA%.
- Multi-size generated application icon (16–128px) via build-time generator.
- Framework-dependent single-file publish (~170 KB).

### Build / Dev
- Single source file core (`Program.cs`).
- Icon generator auxiliary project.
- Optional NativeAOT publish flag `/p:PublishNativeAot=true` (toolchain required).

### Removed
- Legacy WinForms implementation files (AwakeService, TrayApplicationContext).

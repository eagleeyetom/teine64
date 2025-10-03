# Changelog

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

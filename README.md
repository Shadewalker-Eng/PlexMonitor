# PlexMonitor

Native Windows desktop monitor for Plex Media Server with automatic recovery, Now Playing, Plex bandwidth telemetry, a dark responsive interface, and a Tron-inspired icon.

Current version: **1.0.4**. Download `PlexMonitor-1.0.4-Windows.zip` from this repository or run the included `PlexMonitor-1.0.4.exe` on 64-bit Windows 10/11 with .NET Framework 4.5 or later.

## Build

Run `./Build.ps1` in Windows PowerShell to build and package the next patch version automatically. Use `./Build.ps1 -Version 1.1.0` to select a version. Versioned outputs are not overwritten. The icon is embedded in the EXE; run `./BuildIcon.ps1` to regenerate its multi-resolution ICO from the included PNG master.

## Tests

Run `./RunTests.ps1` on Windows. It compiles and runs the included 217 playback, telemetry, layout, icon-integration, and pixel-rendering checks. Some tests briefly create a sample app window. Live Plex recovery and telemetry still require verification on a Plex host.

## Setup and behavior

See [APP-GUIDE.md](APP-GUIDE.md) for configuration, authentication, recovery behavior, telemetry units, window controls, and build history.

Settings and an encrypted Plex token are stored locally under `%LOCALAPPDATA%\PlexMonitor`; no user settings, credentials, or activity logs are included in this repository.
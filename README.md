# PlexMonitor

Native Windows desktop monitor for Plex Media Server. Current version: **1.1.2**.

[Download PlexMonitor-1.1.2.exe](https://github.com/Shadewalker-Eng/PlexMonitor/releases/download/v1.1.2/PlexMonitor-1.1.2.exe) · [Release notes](https://github.com/Shadewalker-Eng/PlexMonitor/releases/tag/v1.1.2)

Run the single executable on 64-bit Windows 10 build 19041 or later, including Windows 11. No separate .NET or Windows App SDK installation is needed. Bundled components extract automatically into the runtime cache on first launch. Exit an older monitor from its tray menu before starting the update.

## Features

- WinUI 3 interface with native Fluent controls, dark scrollbars/title bar, gold accents, and responsive wide/compact layouts.
- Plex server name and status, with optional automatic recovery and a 120-second retry cooldown.
- Now Playing titles, users, devices, playback state/mode, and progress.
- Plex local/remote bandwidth averages and a one-minute graph.
- Tray controls and the Tron-inspired Plex icon.
- Existing settings and encrypted Plex tokens preserved under `%LOCALAPPDATA%\PlexMonitor`.

## Build and tests

Install the .NET 10 SDK, then run `./Build.ps1`. The next patch version is selected automatically and the single executable is written to `artifacts/`. Select a version with `./Build.ps1 -Version 1.2.0`.

Run `./RunTests.ps1` for the 26 current core checks. UI verification is available through the executable's `--verify-ui` option; it uses isolated sample data, disables recovery, and captures native wide/compact layouts inside the extracted runtime cache. The 1.1.2 single EXE passed that verification from an otherwise empty folder. Live Plex recovery and telemetry require validation on a Plex host.

Current source: [src/PlexMonitor](src/PlexMonitor). [Full app guide](src/PlexMonitor/README.md) · [Change log](CHANGELOG.md).

The original 1.0.4 Windows Forms implementation and its tests are preserved under `legacy/1.0.4`. The current executable is attached to the release rather than stored in Git because it exceeds GitHub's 100 MiB Git file limit. No credentials, settings, logs, or runtime caches are included in the source tree.

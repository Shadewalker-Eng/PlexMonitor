# PlexMonitor 1.1.2

PlexMonitor now uses native Microsoft WinUI 3 controls, XAML application resources, Fluent inputs and switches, native dark scrolling, and a Windows title bar with the existing Tron-inspired Plex icon.

## Run

Download and run **PlexMonitor-1.1.2.exe**. This is a single distributable executable; there are no companion DLLs or asset folders to keep beside it. This is an unpackaged, self-contained x64 app for Windows 10 version 2004 (build 19041) or later, including Windows 11. No separate .NET or Windows App SDK runtime installation is required. The build is unsigned. The executable automatically extracts its bundled runtimes and resources into the .NET runtime cache on first launch; no installer is required.

Exit the previous monitor from its tray menu before launching this version. Existing settings and the encrypted Plex token under `%LOCALAPPDATA%\PlexMonitor` are preserved. No settings or tokens are included in the download.

## Interface and monitoring

- Wide windows show Server & Settings beside Live Activity. Compact windows provide a selector for those two sections, preventing horizontal overflow.
- Status includes Plex's reported server name and checks every 15 seconds. Optional automatic recovery starts a stopped Plex application or a named Windows service, with a 120-second cooldown. A running but unresponsive server is reported without launching another copy.
- Now Playing refreshes every five seconds, with title, user/device, state, playback mode and progress.
- Bandwidth shows Plex's local and remote transfer statistics as a trailing 60-second average, with a gold total / blue local graph. Missing or denied data is shown explicitly, not as zero.
- Connection opens a native WinUI dialog for a Plex owner token. It is stored using Windows Data Protection and sent only to localhost:32400. Access to bandwidth statistics may depend on Plex permissions or Plex Pass.
- Browse selects the server executable with a native Windows file picker. Leave the service name blank for a normal Plex installation; do not use Plex Update Service.
- Closing the window keeps monitoring in the tray. Tray actions show the window or exit the monitor. Service starts may require administrator rights.

## Build and checks

Build with the .NET 10 SDK: `./Build.ps1`. The initial WinUI release is 1.1.0 and subsequent builds automatically increment the patch number. `-Version 1.2.0` selects an explicit version. Windows App SDK 2.6.5 and service-control dependencies are pinned in the project. Windows Forms is used only for the tray icon and duplicate-instance notice; the application GUI is WinUI 3.

Core tests: `dotnet run --project Tests/CoreTests.csproj`. The 26 checks cover session parsing, bandwidth calculations, XML safety, legacy settings, token encryption and request authentication.

UI verification: run the EXE with `--verify-ui`. This isolated mode uses sample responses, disables recovery, avoids reading real user settings/tokens, and writes its result plus native WinUI render captures under a `verification` folder in the extracted runtime cache. It exits automatically. The wide and compact layouts were captured and visually inspected in this mode. A standard launch was also confirmed. Live Plex recovery and telemetry remain unverified on a real Plex host.

Sources: https://learn.microsoft.com/windows/apps/windows-app-sdk/ and https://developer.plex.tv/pms/

Version 1.1.1 removes the framework name from the window title, delivery folder, and archive names. Future builds retain versioned PlexMonitor filenames.


Version 1.1.2 bundles the .NET runtime, Windows App SDK, compiled XAML resources, icon, assets, and license notices into one compressed executable. Future Build.ps1 runs produce versioned single EXEs. Validation: an EXE-only copy in an isolated folder launched successfully and passed the native wide/compact UI verification without external dependencies.


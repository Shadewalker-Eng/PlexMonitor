# PlexMonitor — Now Playing

Extract the ZIP and run **PlexMonitor-<version>.exe**. Requires 64-bit Windows 10/11 and .NET Framework 4.5 or later. This compiled Windows Forms app no longer requires WebView2 or a browser runtime. Exit any older PlexMonitor instance from its tray menu before running the update.

## Now Playing view

The right pane displays only current Plex playback sessions, with:

- Movie, episode, or music title
- User and playback device
- Playing, paused, or buffering state
- Direct play, direct stream, or transcoding mode when Plex reports it
- Playback progress and elapsed/total time

Sessions refresh every five seconds. **Refresh** and **Refresh playing** trigger an immediate update. An empty successful response displays “Nothing is playing right now.” Connection and authentication failures display a separate message and clear previous sessions. Requests continue automatically after an outage. Playback progress comes from Plex; it is not estimated between updates.

The app first uses a saved connection token or the local current-user Plex installation's account token, when available. If access is denied, select **Connection…** and enter a Plex server owner token. See [Plex's token instructions](https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/). Saved tokens are encrypted for your Windows account using Windows Data Protection and sent in a header only to localhost:32400. They are not written to the activity log. Clearing the Connection field and saving removes the saved token. On a later launch the app may again detect the local Plex token. The old embedded browser sign-in is not used by this native view.

## Server monitor

Select **Plex Media Server.exe** if not detected and click **Save settings**. For an installation using a Windows service wrapper, enter its exact service name instead; otherwise leave that field blank. Do not select Plex Update Service. Starting a service may require running PlexMonitor as administrator.

Server status is checked every 15 seconds. Automatic recovery starts Plex when stopped; disable it and save for monitoring only. Start attempts are at least 120 seconds apart, including manual attempts. A running but unresponsive server is reported without killing it or launching duplicates. Paused or transitioning services are reported without attempting to start them.

Close the window to continue monitoring in the tray; use **Exit monitor** to quit. Monitoring runs while the app is open and you are signed in. For optional sign-in startup, create a shortcut to PlexMonitor-<version>.exe in the folder opened with `Win+R`, `shell:startup`. Add `--tray` to the shortcut target to start in the tray.

Settings, encrypted token, and rotating activity logs are in `%LOCALAPPDATA%\PlexMonitor`. Existing service/recovery settings are preserved. The app monitors the local server on port 32400.

## Build and validation

Source: PlexMonitor.cs, NowPlaying.cs, PlexTheme.cs, Telemetry.cs, and WindowUI.cs. Build.ps1 uses the Windows .NET Framework C# compiler. No external libraries are required.

Compilation and 16 fixture checks passed, covering mixed video/music sessions, paused playback, missing metadata, live duration, direct play/transcode decisions, safe XML parsing, progress clamping, card rendering, and clearing empty/offline sessions. Active-stream and empty-state layouts were visually checked using rendered Windows Forms previews, including a smaller window.  Live Plex integration remains unverified in this restricted environment.

API reference: https://developer.plex.tv/pms/ — GET /status/sessions.


## Versioned builds

Run Build.ps1 to compile and package the next build. The first release is 1.0.0; each subsequent build increments the patch version (1.0.1, 1.0.2, and so on). To choose a release explicitly, run `./Build.ps1 -Version 1.1.0`. Existing versioned outputs are never overwritten.

Executable: `PlexMonitor-<version>.exe`. ZIP: `PlexMonitor-<version>-Windows.zip`. The window title and Windows file properties show the version too. `Launch PlexMonitor.cmd` points to the newest build. version.json records the latest compiled version and is included in the ZIP for future rebuilds.

On first launch, existing settings and the encrypted connection token are copied from the previous PlexWatch data folder if no PlexMonitor settings exist. Exit the older app before running the renamed version.

## Server name and bandwidth (1.0.1)

System Status shows the server friendly name reported by Plex, refreshed every 15 seconds. If the server becomes unavailable, its last known name is marked “last seen.” A name that has not been retrieved is shown as unavailable, rather than substituting the Windows machine name.

Live Activity includes a Plex bandwidth monitor refreshed every five seconds. It uses `/statistics/bandwidth?timespan=6`, showing a trailing 60-second average in decimal Mbps, split into local and remote traffic. The graph groups the last minute into five-second averages: gold is total traffic and blue is local traffic. The scale adjusts automatically. This is Plex-reported transfer usage, not a sum of media bitrates or the entire computer's network traffic. It can lag as Plex publishes statistics.

The bandwidth monitor uses the same local Plex owner token as Now Playing. Access can depend on server version, permissions, and Plex Pass availability. Access failures and missing samples are shown explicitly; unavailable data is not displayed as zero. Server identity, sessions, and bandwidth are fetched independently so a failed bandwidth request does not hide Now Playing.

Validation for 1.0.1: 16 existing Now Playing checks plus 13 telemetry checks passed. Telemetry checks cover server names, XML escaping, missing names, sample windows, invalid data, multiple-device aggregation, Mbps conversion, graph buckets, and unavailable states. An offscreen Windows Forms preview was visually inspected with sample data; live Plex telemetry has not been verified in this environment.

References: https://developer.plex.tv/pms/ and https://python-plexapi.readthedocs.io/en/latest/modules/server.html#plexapi.server.PlexServer.bandwidth

## Themed windows and responsive layout (1.0.2)

The main window and connection dialog now use a matching charcoal title bar. The main window supports dragging by its title, resizing at its edges, double-clicking the title to maximize/restore, and the minimize/maximize/close buttons. Closing the main window still keeps monitoring in the tray.

Server controls scale with the available window width and height. Live Activity adapts its header and card widths. Long labels are constrained to their panels, and excess content scrolls vertically within its section. Sidebar, Now Playing, and activity-log scrollbars use dark tracks with gray thumbs and a gold hover/drag highlight. Use the mouse wheel, drag the thumb, or focus a scrollbar and use arrow/Page Up/Page Down/Home/End keys. Page Up/Page Down also scroll the focused content section. There are no horizontal scrollbars. Minimum main-window size is 920 by 620 pixels; smaller layouts retain readable text and scroll their overflow.

Validation: 151 resize, containment, scroll-clamping, log-scroll, resize-hit-target, and maximize/restore checks passed at 920x620, 1040x760, 1280x820, and 1600x1000, plus 29 existing playback/telemetry checks. Offscreen rendered layouts were visually checked at normal and compact sizes. Live Plex telemetry remains unverified in this environment.

## Tron-inspired Plex icon (1.0.3)

A gold Plex chevron with cyan neon edging is now embedded into the executable and displayed in the window title bar, taskbar, and system tray. The icon includes nine sizes: 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixels. It is embedded in the EXE, so the application can run without external icon files.

PlexMonitor-icon.png is the generated master; PlexMonitor.ico is the Windows multi-resolution icon. BuildIcon.ps1 regenerates the ICO from the master. Build.ps1 retains the icon on subsequent builds. IconPrompt.txt records the exact generation prompt and tool used.

Validation: 18 icon resource, resolution, Windows extraction, form, caption, and tray checks passed, along with 180 existing layout/playback/telemetry checks. Small icon sizes were visually inspected. Live Plex telemetry remains unverified in this environment.

## Title-bar icon rendering fix (1.0.4)

Fixed the title-bar icon rendering as colored noise. The custom caption now draws directly from an embedded PNG, rendered at the displayed size, rather than converting a PNG-compressed Windows icon through Icon.ToBitmap. Small ICO frames (16–128px) now use compatible 32-bit DIB encoding; the 256px frame remains PNG-compressed. Caption bitmap resizing occurs after the PictureBox completes its own resize processing.

Validation: the colored-noise issue was reproduced in an offscreen Windows Forms preview and the corrected preview was visually checked. Nineteen pixel/scale regression checks compare caption artwork at 22, 28, 33, and 44 pixels and Windows icon conversions against the master artwork. The 18 icon integration and 151 layout checks also pass. Live Plex telemetry remains unverified in this environment.

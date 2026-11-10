# Change log

## 1.1.2

- Bundle the .NET runtime, Windows App SDK, compiled XAML resources, icon, assets, and license notices into one compressed executable.
- Make subsequent builds produce versioned standalone EXEs.
- Verify the EXE from an isolated directory with no companion files.

## 1.1.1

- Remove the framework name from the app title and delivery names.
- Use PlexMonitor-<version> naming for executables and packages.

## 1.1.0

- Migrate the GUI from Windows Forms to native WinUI 3 with Fluent controls, dark scrolling, native title bar, and adaptive wide/compact views.
- Preserve server recovery, Now Playing, bandwidth telemetry, settings, encrypted tokens, and tray behavior.
- Bundle compiled XAML/PRI resources and the app runtimes.
- Add 26 current core checks and native UI verification using isolated fixtures.

The previous 1.0.4 source/build/tests remain under legacy/1.0.4.

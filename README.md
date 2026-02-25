# ScreenFlip

A small Windows tray app that swaps your primary monitor with one click.

## Prerequisites
- Windows 10/11
- .NET 10 SDK

## Build

```
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `bin\Release\net10.0-windows7.0\win-x64\publish\ScreenFlip.exe`

## Usage

Launch `ScreenFlip.exe` - it appears in the system tray and opens a small control window.

- **Enable** swaps the primary monitor to your other screen. Optionally dims the old primary with a dark overlay so you know it's inactive.
- **Disable** swaps back to the original primary and removes the overlay.
- The window can be closed (it minimises to tray). Double-click the tray icon or use **Open** from the tray menu to bring it back.
- Use **Exit** from the tray menu to quit fully.

## Settings

The control window has a **"Show overlay on inactive monitor"** checkbox (on by default). When enabled, a 90% black overlay is drawn over the old primary screen while ScreenFlip is active, clearly marking it as inactive. The checkbox state is saved between runs.

## How It Works

- Uses Win32 `ChangeDisplaySettingsEx` to reassign the primary monitor
- Stores the original primary device name in `%LOCALAPPDATA%\ScreenFlip\state.txt` so it can be restored on disable
- Saves the overlay preference to `%LOCALAPPDATA%\ScreenFlip\settings.txt`
- The overlay is a click-through (`WS_EX_TRANSPARENT`) WinForms window so it doesn't interfere with input on that monitor

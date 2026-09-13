# ETS2LA AR Window Align

**English** &nbsp;|&nbsp; [简体中文](README.zh-CN.md)

A third-party plugin for [ETS2LA](https://github.com/ETS2LA/ETS2LA) (Euro Truck Simulator 2 autopilot): makes AR rendering follow the **game window** instead of the primary monitor, and keeps AR inside the window.

Tested with ETS2LA [v2026.9.5012] / Euro Truck Simulator 2 [1.6.0]

## What it does

- AR position and scale match the game window in windowed mode
- AR is clipped at the window edges, so nothing spills onto the desktop
- When the game fills the primary monitor (fullscreen / borderless), it does nothing and ETS2LA behaves as usual

## Install

1. Grab `ARWindowAlign-v1.0.0.zip` from [Releases](../../releases).
2. Extract it and run the install script (copies the folder and registers the plugin):

   ```powershell
   powershell -ExecutionPolicy Bypass -File install.ps1
   ```

   Or manually: copy the whole `mldjy.arwindowalign` folder into
   `%LOCALAPPDATA%\ETS2LA\current\Plugins\` and add an entry to
   `%APPDATA%\ETS2LA\InstalledPluginManifest.json`:

   ```json
   { "Id": "mldjy.arwindowalign", "Version": "1.0.0",
     "DllPath": "Plugins\\mldjy.arwindowalign\\ARWindowAlign.dll",
     "Dependencies": [], "Type": 0 }
   ```

   > ETS2LA only auto-scans DLLs directly inside `Plugins\`; a plugin in a subfolder
   > must be listed in the manifest.
3. Restart ETS2LA.

Start an assist in-game: AR elements should follow the lane and converge on the vanishing point.

## Disable / uninstall

- **Temporarily** — disable `mldjy.arwindowalign` in ETS2LA's plugin manager.
- **Permanently** — run `uninstall.ps1`, or delete the `mldjy.arwindowalign` folder and its manifest entry.

## Build from source

Needs the .NET SDK 10+ and an ETS2LA install (for `ETS2LA.Shared.dll` / `ETS2LA.Logging.dll`, which are compile-time references only and are **not** redistributed here).

```powershell
cd <repository folder>
powershell -ExecutionPolicy Bypass -File scripts\fetch-harmony.ps1
dotnet build ARWindowAlign.Core\ARWindowAlign.Core.csproj -c Release
dotnet build ARWindowAlign\ARWindowAlign.csproj -c Release
```

Output: `ARWindowAlign.dll`, `ARWindowAlign.Core.dll` (both in `bin\Release`), plus `lib\0Harmony.dll` — put all three into `Plugins\mldjy.arwindowalign\`.

## Limitations

- **Windows only.**
- **Primary monitor only.** If the game window sits on a secondary monitor, the overlay doesn't cover that area anyway.
- **No letterbox detection.** When the window's aspect ratio differs from the game's render resolution, AR is mapped to the whole client rect.
- AR elements are drawn only while AR rendering is enabled in ETS2LA.

## Disclaimer

- Compatibility after **ETS2LA or game updates** is not guaranteed.
- This project is **not guaranteed to receive ongoing updates or maintenance**.

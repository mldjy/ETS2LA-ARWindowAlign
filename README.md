# ARWindowAlign

**English** | [简体中文](README.zh-CN.md)

**Makes ETS2LA's AR rendering follow the game window instead of the primary monitor** — so AR path lines line up with the road when you drive in **windowed mode** (Euro Truck Simulator 2 / American Truck Simulator).

**The geometry, nested:**

```
primary monitor  3840x2160                    <- ETS2LA's AR basis by default
└── game window  (1907,524) 1920x1080         <- where the game actually renders
    └── AR must be scaled to this rect and offset by its screen origin
```

## The problem

ETS2LA's AR pipeline uses the **primary monitor's resolution** as its rendering basis and never reads the game window's position or size:

```csharp
// ETS2LA.Overlay/Overlay.cs
public float OverlayWidth  => GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).Width;
public float OverlayHeight => GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).Height;

// ETS2LA.Overlay/AR/AR.cs
thisFrameWidth  = (int)OverlayHandler.Current.OverlayWidth;
thisFrameHeight = (int)OverlayHandler.Current.OverlayHeight;
```

If the game runs in a window (e.g. 1920×1080 on a 3840×2160 desktop), every AR element is scaled and offset for the **full monitor**, so it no longer matches the road/vehicle.

## How it works

A runtime patch (Harmony) applied by a plugin — **ETS2LA itself is not modified**. Four things are patched: the coordinate mapping and the clipping, for **each** of the two AR render channels.

### 1 · Coordinate mapping — ImGui channel

`ARRenderer.WorldToScreen(Vector3, int, int)` — every element (path lines, dots, labels, 3D windows) goes through it when the overlay's *Simplified Graphics* is on.

- `prefix` replaces the destination size (monitor resolution) with the **game client rect** → fixes the scale
- `postfix` adds the client rect's screen origin → fixes the offset

### 2 · Coordinate mapping — shader channel

`ARRenderer.WorldToNDC(Vector3)` — the gradient path bands are handed to the `LineWithGradient` shader as NDC. The shader still draws into the monitor-sized overlay viewport, so monitor-mapped NDC is remapped to window-mapped NDC:

```
ndc'x = (2·winX + (ndc_x + 1)·winW) / monW − 1
ndc'y = 1 − (2·winY + (1 − ndc_y)·winH) / monH
```

### 3 · Clipping — ImGui channel

The overlay keeps covering the whole monitor, so an AR element at a window edge would be drawn onto the desktop. Every `Draw3D*` / `EndWindow` call therefore pushes a clip rect intersected with the game window rect (and pops it afterwards), cutting AR at the window edge exactly like fullscreen cuts it at the screen edge.

### 4 · Clipping — shader channel

The gradient bands are drawn by GL, where ImGui clipping has no effect, so `LineWithGradient.RenderPass()` is wrapped in a GL scissor rectangle (GL's origin is bottom-left, so Y is flipped).

When the game fills the primary monitor (fullscreen / borderless), all four become no-ops — stock behaviour, unchanged.


## Install

### Option A — prebuilt (nothing to compile)

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
   > must be listed in the manifest — the same layout the original author's plugins use.
3. Restart ETS2LA.

### Option B — build from source

Needs the .NET SDK 10+ and an ETS2LA install (for `ETS2LA.Shared.dll` / `ETS2LA.Logging.dll`, which are compile-time references only and are **not** redistributed here).

```powershell
# fetch Lib.Harmony into lib/0Harmony.dll
powershell -ExecutionPolicy Bypass -File scripts/fetch-harmony.ps1

dotnet build ARWindowAlign.Core/ARWindowAlign.Core.csproj -c Release
dotnet build ARWindowAlign/ARWindowAlign.csproj          -c Release

# ETS2LA not in the default location? override it:
# dotnet build ARWindowAlign/ARWindowAlign.csproj -c Release -p:Ets2laDir="D:\path\to\ETS2LA\current"
```

Then copy `ARWindowAlign.dll`, `ARWindowAlign.Core.dll` (both from `bin\Release`) and `0Harmony.dll` into the `Plugins` folder.

## Verify

On startup, `%LOCALAPPDATA%\ETS2LA\current\ets2la.log` should show:

```
INF  WorldToScreen, WorldToNDC, clip 7 draw methods, shader scissor
INF  monitor 3840x2160, game client rect = (1907,524) 1920x1080 | patches: WorldToScreen, WorldToNDC, clip 7 draw methods, shader scissor
INF  ... imgui clip <pushed>/0 failed, gl scissor <set>/0 failed
INF 已启用插件：mldjy.arwindowalign
```

The trailing counters are reported a few seconds after startup so clipping can be verified from the log alone: `imgui clip` counts the AR draws that were clipped, `gl scissor` the shader passes that were scissored. `0 failed` on both means the patches are executing.

Then enable the assist in-game: AR elements should follow the lane and converge on the vanishing point. If your AR already lines up, you're in fullscreen and the plugin is simply idle.

## Disable / uninstall

- **Temporarily** — disable `mldjy.arwindowalign` in ETS2LA's plugin manager (patches are removed immediately, stock behaviour returns).
- **Permanently** — delete the three DLLs from the `Plugins` folder.

## Limitations

- **Windows only.** The tracker uses Win32 (`GetClientRect` / `ClientToScreen` / `GetSystemMetrics`).
- **Bound to ETS2LA internals.** If upstream renames `ARRenderer` / `WorldToScreen` / `WorldToNDC`, the plugin skips patching and logs `skipped: ...` rather than crashing.
- **Primary monitor only.** If the game window sits on a secondary monitor, the overlay doesn't cover that area anyway.
- **No letterbox detection.** When the window's aspect ratio differs from the game's render resolution, AR is mapped to the whole client rect (game-drawn black bars aren't detected).

## Disclaimer

- Compatibility after **ETS2LA or game updates** is not guaranteed; upstream internal changes may make the patches skip or stop working.
- This project is **not guaranteed to receive ongoing updates or maintenance**.

## Licence

- **MIT** — see [LICENSE](LICENSE).
- Bundles [Lib.Harmony](https://github.com/pardeike/Harmony) 2.4.2 (MIT) to apply the runtime patch — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- ETS2LA is a separate project by Tumppi066; no ETS2LA binaries are redistributed here.
- 中文说明见 [README.zh-CN.md](README.zh-CN.md).

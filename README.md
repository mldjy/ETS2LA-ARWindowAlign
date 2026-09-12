# ARWindowAlign

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

A runtime patch (Harmony) applied by a plugin — **ETS2LA itself is not modified**. AR has two render channels and *both* are patched:

| Channel | When | Patched method |
|---|---|---|
| ImGui 2D | Overlay setting *Simplified Graphics* = **on** | `ARRenderer.WorldToScreen(Vector3, int, int)` |
| Shader / full quality | *Simplified Graphics* = **off** | `ARRenderer.WorldToNDC(Vector3)` |

- **`WorldToScreen`** — `prefix` replaces the destination size (monitor resolution) with the **game client rect**; `postfix` adds the client rect's screen origin.
- **`WorldToNDC`** — the shader still draws into the monitor-sized overlay viewport, so monitor-mapped NDC is remapped to window-mapped NDC:

  ```
  ndc'x = (2·winX + (ndc_x + 1)·winW) / monW − 1
  ndc'y = 1 − (2·winY + (1 − ndc_y)·winH) / monH
  ```

When the game fills the primary monitor (fullscreen / borderless), both are identity transforms and the plugin does nothing — stock behaviour, no risk.

Geometry is measured in **DPI-aware physical pixels** (a background thread sets `PER_MONITOR_AWARE_V2` before measuring), so 150% display scaling doesn't skew the numbers. The window rect is re-read every 150 ms; if the process isn't running, the plugin is idle.

### Why two assemblies

ETS2LA loads plugins into a **collectible** `AssemblyLoadContext`. Harmony's generated patch wrappers cannot reference patch methods that live in a collectible assembly — attempting it fails with:

```
Could not load file or assembly '0Harmony' ... Operation is not supported
```

So the patching code lives in `ARWindowAlign.Core.dll`, which the plugin explicitly loads into the **default (non-collectible)** context at runtime (loading Harmony first, so the dependency resolves).

## Install

### Option A — prebuilt (nothing to compile)

1. Grab `ARWindowAlign-v1.0.0.zip` from [Releases](../../releases).
2. Copy the three DLLs into ETS2LA's `Plugins` folder — usually `%LOCALAPPDATA%\ETS2LA\current\Plugins\` (create it if it doesn't exist).
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
INF  WorldToScreen patched, WorldToNDC patched
INF  monitor 3840x2160, game client rect = (1907,524) 1920x1080
INF 已启用插件：mldjy.arwindowalign
```

Then enable the assist in-game: AR elements should follow the lane and converge on the vanishing point. If your AR already lines up, you're in fullscreen and the plugin is simply idle.

## Disable / uninstall

- **Temporarily** — disable `mldjy.arwindowalign` in ETS2LA's plugin manager (patches are removed immediately, stock behaviour returns).
- **Permanently** — delete the three DLLs from the `Plugins` folder.

## Limitations

- **Windows only.** The tracker uses Win32 (`GetClientRect` / `ClientToScreen` / `GetSystemMetrics`). Upstream wants a *cross-platform* fix, which this is not — see below.
- **Bound to ETS2LA internals.** If upstream renames `ARRenderer` / `WorldToScreen` / `WorldToNDC`, the plugin skips patching and logs `skipped: ...` rather than crashing.
- **Primary monitor only.** If the game window sits on a secondary monitor, the overlay doesn't cover that area anyway.
- **No letterbox detection.** When the window's aspect ratio differs from the game's render resolution, AR is mapped to the whole client rect (game-drawn black bars aren't detected).

## Upstream status

Feature request filed: **[ETS2LA#631 — AR rendering should follow the game window rect](https://github.com/ETS2LA/ETS2LA/issues/631)**

Response from the maintainer, [@Tumppi066](https://github.com/Tumppi066):

> This is a known issue that we haven't yet fixed. Once we implement this we want it to be cross platform, and I haven't investigated how this is done on Windows yet. For now we'll sadly have to ask you to either disable AR rendering or use fullscreen mode 👍

So this plugin is a **community-side workaround for the Windows case** until a cross-platform fix lands upstream. It is not affiliated with, endorsed by, or shipped with ETS2LA.

## Credits & licence

- Plugin code: **MIT** — see [LICENSE](LICENSE).
- Bundles [Lib.Harmony](https://github.com/pardeike/Harmony) 2.4.2 (MIT) to apply the runtime patch — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- ETS2LA is a separate project by Tumppi066; no ETS2LA binaries are redistributed here.
- 中文说明见 [README.zh-CN.md](README.zh-CN.md).

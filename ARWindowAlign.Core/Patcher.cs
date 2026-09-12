// ARWindowAlign.Core — 实际执行运行时补丁的部分（必须加载进"默认/不可回收"的程序集上下文）。
//
// ETS2LA 的 AR 有两条渲染通道，本程序集两处都补：
//
//  ① 简化图形（ImGui 2D 绘制通道）
//     所有元素（路径线/圆点/文字/3D 窗口）都经
//     ETS2LA.Overlay.AR.ARRenderer.WorldToScreen(Vector3, int, int)
//     把 NDC 换算成屏幕像素，换算基准是传入的 destination 尺寸（= 主显示器分辨率）。
//     → prefix  把 destination 换成游戏窗口客户区尺寸（修正缩放）
//       postfix 给结果加上客户区在屏幕上的原点（修正偏移）
//
//  ② 全画质（着色器通道）
//     Draw3DLineWithGradient() 用 ARRenderer.WorldToNDC(Vector3) 算出四个角的 NDC，
//     交给 LineWithGradient 着色器按叠加层视口（仍然是整块主显示器）绘制。
//     着色器收到的是 NDC，所以要把"按主显示器映射"换算成"按游戏窗口映射"：
//         ndc'x = (2*winX + (ndc_x + 1) * winW) / monW - 1
//         ndc'y = 1 - (2*winY + (1 - ndc_y) * winH) / monH
//     （游戏铺满主显示器时该式恒等于原值，即不干预）
//
// WorldToNDC 在整个 ETS2LA 仓库里仅此一处调用，因此补它不会影响其它功能。

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace ARWindowAlign.Core;

public static class Patcher
{
    private const string HarmonyId = "mldjy.ets2la.arwindowalign";
    private static Harmony? _harmony;

    /// <summary>Applies both patches. Returns a human readable status string.</summary>
    public static string Apply()
    {
        if (_harmony != null) return "already patched";

        var results = new List<string>();
        try
        {
            var type = AccessTools.TypeByName("ETS2LA.Overlay.AR.ARRenderer");
            if (type == null) return "skipped: ARRenderer not found (ETS2LA internals changed?)";

            GameWindowTracker.Start();
            _harmony = new Harmony(HarmonyId);

            // ① ImGui path (simplified graphics)
            var wts = AccessTools.Method(type, "WorldToScreen", new[] { typeof(Vector3), typeof(int), typeof(int) });
            if (wts != null)
            {
                _harmony.Patch(wts,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.WorldToScreenPrefix)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.WorldToScreenPostfix)));
                results.Add("WorldToScreen patched");
            }
            else results.Add("WorldToScreen NOT found");

            // ② Shader path (full graphics)
            var wtn = AccessTools.Method(type, "WorldToNDC", new[] { typeof(Vector3) });
            if (wtn != null)
            {
                _harmony.Patch(wtn,
                    postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.WorldToNDCPostfix)));
                results.Add("WorldToNDC patched");
            }
            else results.Add("WorldToNDC NOT found");

            return string.Join(", ", results);
        }
        catch (Exception ex)
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            return "error: " + ex;
        }
    }

    /// <summary>Removes all patches (stock behaviour restored).</summary>
    public static string Remove()
    {
        try
        {
            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
            return "unpatched";
        }
        catch (Exception ex)
        {
            return "error: " + ex;
        }
    }

    /// <summary>Current measured geometry, for logging/diagnostics.</summary>
    public static string Status()
    {
        var r = GameWindowTracker.Current;
        return r.Valid
            ? $"monitor {r.MonW}x{r.MonH}, game client rect = ({r.X},{r.Y}) {r.Width}x{r.Height}"
            : "no override (game fullscreen / not running) - stock behaviour";
    }
}

public static class Patches
{
    // ---- ① ImGui / screen-space channel ----

    public static void WorldToScreenPrefix(ref int destinationWidth, ref int destinationHeight, out Vector2 __state)
    {
        __state = default;

        var r = GameWindowTracker.Current;
        if (!r.Valid) return;

        __state = new Vector2(r.X, r.Y);
        destinationWidth = r.Width;
        destinationHeight = r.Height;
    }

    public static void WorldToScreenPostfix(ref Vector2? __result, Vector2 __state)
    {
        if (!__result.HasValue) return;
        if (__state.X == 0f && __state.Y == 0f) return;

        __result = new Vector2(__result.Value.X + __state.X, __result.Value.Y + __state.Y);
    }

    // ---- ② Shader / NDC channel ----

    public static void WorldToNDCPostfix(ref Vector2? __result)
    {
        if (!__result.HasValue) return;

        var r = GameWindowTracker.Current;
        if (!r.Valid) return;
        if (r.MonW <= 0 || r.MonH <= 0) return;

        float ndcX = __result.Value.X;
        float ndcY = __result.Value.Y;

        // Remap: monitor-mapped NDC -> game-window-mapped NDC, in the overlay's (monitor) viewport.
        float x = (2f * r.X + (ndcX + 1f) * r.Width) / r.MonW - 1f;
        float y = 1f - (2f * r.Y + (1f - ndcY) * r.Height) / r.MonH;

        __result = new Vector2(x, y);
    }
}

internal sealed class WindowRect
{
    public static readonly WindowRect Invalid = new(0, 0, 0, 0, 0, 0);

    public readonly int X;
    public readonly int Y;
    public readonly int Width;
    public readonly int Height;
    public readonly int MonW;
    public readonly int MonH;

    public WindowRect(int x, int y, int w, int h, int monW, int monH)
    {
        X = x; Y = y; Width = w; Height = h; MonW = monW; MonH = monH;
    }

    public bool Valid => Width > 0 && Height > 0 && MonW > 0 && MonH > 0;
}

internal static class GameWindowTracker
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    private static readonly string[] GameProcessNames = { "eurotrucks2", "amtrucks" };

    private static volatile WindowRect _current = WindowRect.Invalid;
    private static volatile bool _started;

    public static WindowRect Current => _current;

    public static void Start()
    {
        if (_started) return;
        _started = true;

        var t = new Thread(Loop)
        {
            IsBackground = true,
            Name = "ARWindowAlign.WindowTracker",
            Priority = ThreadPriority.BelowNormal
        };
        t.Start();
    }

    private static void Loop()
    {
        try { SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { /* older OS */ }

        while (true)
        {
            try { _current = Measure(); }
            catch { _current = WindowRect.Invalid; }
            Thread.Sleep(150);
        }
    }

    private static WindowRect Measure()
    {
        int monW = GetSystemMetrics(SM_CXSCREEN);
        int monH = GetSystemMetrics(SM_CYSCREEN);

        foreach (var name in GameProcessNames)
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(name); }
            catch { continue; }

            foreach (var p in processes)
            {
                try
                {
                    var hwnd = p.MainWindowHandle;
                    if (hwnd == IntPtr.Zero) continue;
                    if (IsIconic(hwnd) || !IsWindowVisible(hwnd)) continue;
                    if (!GetClientRect(hwnd, out var rc)) continue;

                    var origin = new POINT { X = 0, Y = 0 };
                    if (!ClientToScreen(hwnd, ref origin)) continue;

                    int w = rc.Right - rc.Left;
                    int h = rc.Bottom - rc.Top;
                    if (w <= 0 || h <= 0) continue;

                    // Already filling the whole primary monitor => stock behaviour is correct.
                    if (origin.X == 0 && origin.Y == 0 && w >= monW && h >= monH)
                        return WindowRect.Invalid;

                    return new WindowRect(origin.X, origin.Y, w, h, monW, monH);
                }
                finally { p.Dispose(); }
            }
        }

        return WindowRect.Invalid;
    }
}

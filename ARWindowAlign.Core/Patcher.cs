// ARWindowAlign.Core — 运行时补丁（必须加载进默认/不可回收程序集上下文）。
//
// ETS2LA 的 AR 有两条渲染通道，本程序集四组补丁全部覆盖：
//
//  ① 坐标换算 · ImGui 通道
//     所有元素（路径线/圆点/文字/3D 窗口）经 WorldToScreen(Vector3, int, int)
//     把 NDC 换算成屏幕像素，基准是传入的 destination 尺寸（= 主显示器分辨率）。
//       prefix  把 destination 换成游戏窗口客户区尺寸（修正缩放）
//       postfix 给结果加上客户区在屏幕上的原点（修正偏移）
//
//  ② 坐标换算 · 着色器通道
//     Draw3DLineWithGradient() 用 WorldToNDC(Vector3) 算四角 NDC，交给
//     LineWithGradient 着色器按叠加层视口（整块主显示器）绘制。
//       postfix ndc'x = (2*winX + (ndc_x+1)*winW)/monW - 1
//               ndc'y = 1 - (2*winY + (1-ndc_y)*winH)/monH
//     （游戏铺满主显示器时恒等于原值）
//
//  ③ 绘制裁剪 · ImGui 通道
//     窗口模式下叠加层仍铺满整块显示器，AR 元素在窗口边缘会画到桌面上。
//     给 ARRenderer 的每个绘制方法 push/pop 一个与窗口矩形相交的裁剪矩形。
//     （全屏时该矩形等于整屏，等于不干预）
//
//  ④ 绘制裁剪 · 着色器通道
//     渐变带由 GL 直接绘制，ImGui 裁剪管不到，因此对 LineWithGradient.RenderPass()
//     加 GL scissor（GL 原点在左下角，需翻转 y）。
//
// 所有这些方法都在 ETS2LA 内部，若上游重命名，Apply() 会逐项报告 MISSING 而不会崩溃。

using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Hexa.NET.ImGui;
using Hexa.NET.OpenGL;

namespace ARWindowAlign.Core;

public static class Patcher
{
    private const string HarmonyId = "mldjy.ets2la.arwindowalign";
    private static Harmony? _harmony;
    private static string _status = "not applied";

    /// <summary>Applies the patches. Returns a human readable status string.</summary>
    public static string Apply()
    {
        if (_harmony != null) return "already patched";

        var notes = new List<string>();
        try
        {
            var arType = AccessTools.TypeByName("ETS2LA.Overlay.AR.ARRenderer");
            if (arType == null) return "skipped: ARRenderer not found (ETS2LA internals changed?)";

            GameWindowTracker.Start();
            _harmony = new Harmony(HarmonyId);

            // ① ImGui 通道坐标换算
            var wts = AccessTools.Method(arType, "WorldToScreen", new[] { typeof(Vector3), typeof(int), typeof(int) });
            if (wts != null)
            {
                _harmony.Patch(wts,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.WorldToScreenPrefix)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.WorldToScreenPostfix)));
                notes.Add("WorldToScreen");
            }
            else notes.Add("WorldToScreen MISSING");

            // ② 着色器通道坐标换算
            var wtn = AccessTools.Method(arType, "WorldToNDC", new[] { typeof(Vector3) });
            if (wtn != null)
            {
                _harmony.Patch(wtn, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.WorldToNDCPostfix)));
                notes.Add("WorldToNDC");
            }
            else notes.Add("WorldToNDC MISSING");

            // ③ AR 绘制裁剪到窗口（ImGui 通道）
            int clipped = 0;
            foreach (var m in AccessTools.GetDeclaredMethods(arType))
            {
                if (Array.IndexOf(DrawMethodNames, m.Name) < 0) continue;
                if (m.IsAbstract || m.ContainsGenericParameters) continue;
                try
                {
                    _harmony.Patch(m,
                        prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.DrawClipPrefix)),
                        postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.DrawClipPostfix)));
                    clipped++;
                }
                catch { /* skip individual overloads we cannot patch */ }
            }
            notes.Add($"clip {clipped} draw methods");

            // ④ 着色器绘制裁剪到窗口（GL scissor）
            var shaderType = AccessTools.TypeByName("ETS2LA.Overlay.Shaders.LineWithGradient");
            var renderPass = shaderType != null ? AccessTools.Method(shaderType, "RenderPass", Type.EmptyTypes) : null;
            if (renderPass != null)
            {
                _harmony.Patch(renderPass,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.ScissorPrefix)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.ScissorPostfix)));
                notes.Add("shader scissor");
            }
            else notes.Add("shader RenderPass MISSING");

            _status = string.Join(", ", notes);
            return _status;
        }
        catch (Exception ex)
        {
            try { _harmony?.UnpatchAll(HarmonyId); } catch { }
            _harmony = null;
            _status = "error";
            return "error: " + ex.GetBaseException().Message;
        }
    }

    /// <summary>Removes all patches (stock behaviour restored).</summary>
    public static string Remove()
    {
        try
        {
            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
            _status = "not applied";
            return "unpatched";
        }
        catch (Exception ex)
        {
            return "error: " + ex.GetBaseException().Message;
        }
    }

    /// <summary>Current measured geometry and patch state, for logging/diagnostics.</summary>
    public static string Status()
    {
        var r = GameWindowTracker.Current;
        string geom = r.Valid
            ? $"monitor {r.MonW}x{r.MonH}, game client rect = ({r.X},{r.Y}) {r.Width}x{r.Height}"
            : "no override (game fullscreen / not running) - stock behaviour";
        return $"{geom} | patches: {_status} | "
             + $"imgui clip {ClipPushed} pushed/{ClipPushFailed} failed, gl scissor {ScissorEnabled} set/{ScissorFailed} failed";
    }

    /// <summary>Method names of ARRenderer that draw through ImGui and must be clipped.</summary>
    private static readonly string[] DrawMethodNames =
    {
        "Draw3DLine", "Draw3DCircle", "Draw3DPolygon", "Draw3DQuad", "Draw3DTriangle", "Draw3DText", "EndWindow"
    };

    // Clip/scissor counters, for verifying from the log that clipping actually runs.
    internal static int ClipPushed;
    internal static int ClipPushFailed;
    internal static int ScissorEnabled;
    internal static int ScissorFailed;

    // ---- GL instance access (Hexa.NET.OpenGL, cached per shader type) ----

    private static readonly Dictionary<Type, FieldInfo?> GlFields = new();

    internal static GL? GetGl(object instance)
    {
        var t = instance.GetType();
        if (!GlFields.TryGetValue(t, out var field))
        {
            field = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .FirstOrDefault(f => f.FieldType == typeof(GL) || f.FieldType.Name == "GL");
            GlFields[t] = field;
        }
        return field?.GetValue(instance) as GL;
    }
}

public static class Patches
{
    [ThreadStatic] private static bool _clipPushed;
    [ThreadStatic] private static bool _scissorSet;

    // ---------- ① ImGui 通道坐标换算 ----------

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

    // ---------- ② 着色器通道坐标换算 ----------

    public static void WorldToNDCPostfix(ref Vector2? __result)
    {
        if (!__result.HasValue) return;
        if (!Clip.TryGetWindowRect(out int x, out int y, out int w, out int h, out int monH)) return;
        if (monH <= 0) return;

        var r = GameWindowTracker.Current;
        float ndcX = __result.Value.X;
        float ndcY = __result.Value.Y;

        float nx = (2f * x + (ndcX + 1f) * w) / r.MonW - 1f;
        float ny = 1f - (2f * y + (1f - ndcY) * h) / r.MonH;

        __result = new Vector2(nx, ny);
    }

    // ---------- ③ AR 绘制裁剪到窗口（ImGui 通道） ----------

    public static void DrawClipPrefix()
    {
        _clipPushed = false;
        try
        {
            var (min, max) = Clip.TargetRect();
            ImGui.GetBackgroundDrawList().PushClipRect(min, max, true);
            _clipPushed = true;
            Patcher.ClipPushed++;
        }
        catch
        {
            _clipPushed = false;
            Patcher.ClipPushFailed++;
        }
    }

    public static void DrawClipPostfix()
    {
        if (!_clipPushed) return;
        _clipPushed = false;
        try { ImGui.GetBackgroundDrawList().PopClipRect(); } catch { }
    }

    // ---------- ④ 着色器绘制裁剪到窗口（GL scissor） ----------

    public static void ScissorPrefix(object __instance)
    {
        _scissorSet = false;
        try
        {
            if (!Clip.TryGetWindowRect(out int x, out int y, out int w, out int h, out int monH)) return;
            if (monH <= 0) return;

            var gl = Patcher.GetGl(__instance);
            if (gl == null) return;

            int glY = monH - (y + h);      // GL 原点在左下角
            gl.Enable(GLEnableCap.ScissorTest);
            gl.Scissor(x, glY, w, h);
            _scissorSet = true;
            Patcher.ScissorEnabled++;
        }
        catch
        {
            _scissorSet = false;
            Patcher.ScissorFailed++;
        }
    }

    public static void ScissorPostfix(object __instance)
    {
        if (!_scissorSet) return;
        _scissorSet = false;
        try { Patcher.GetGl(__instance)?.Disable(GLEnableCap.ScissorTest); } catch { }
    }
}

/// <summary>Window-rect helpers shared by the clipping patches.</summary>
internal static class Clip
{
    /// <summary>
    /// The clip rectangle for AR drawing: the game client rect, or a rectangle large enough
    /// to be a no-op when intersected (game fills the monitor / not running).
    /// </summary>
    public static (Vector2 Min, Vector2 Max) TargetRect()
    {
        if (TryGetWindowRect(out int x, out int y, out int w, out int h, out _))
            return (new Vector2(x, y), new Vector2(x + w, y + h));

        const float big = 1e6f;
        return (new Vector2(-big, -big), new Vector2(big, big));
    }

    /// <summary>
    /// True when the game runs in a window that does not already fill the primary monitor,
    /// i.e. when clipping/remapping is actually needed.
    /// </summary>
    public static bool TryGetWindowRect(out int x, out int y, out int w, out int h, out int monitorHeight)
    {
        x = y = w = h = monitorHeight = 0;

        var r = GameWindowTracker.Current;
        if (!r.Valid || r.Width <= 0 || r.Height <= 0 || r.MonH <= 0) return false;
        if (r.X <= 0 && r.Y <= 0 && r.Width >= r.MonW && r.Height >= r.MonH) return false;

        x = r.X; y = r.Y; w = r.Width; h = r.Height; monitorHeight = r.MonH;
        return true;
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

                    // Filling the whole primary monitor => stock behaviour is correct.
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

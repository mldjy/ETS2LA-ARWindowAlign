// ARWindowAlign — ETS2LA 插件：让 AR 渲染跟随游戏窗口（窗口模式），而不是主显示器全屏。
//
// 背景：ETS2LA 的 AR 换算基准写死在主显示器分辨率上
//   OverlayHandler.OverlayWidth  => GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).Width
//   ARRenderer.thisFrameWidth/Height 取自上面这个值，
//   WorldToScreen() 再把 NDC 直接映射到 0..thisFrameWidth / 0..thisFrameHeight。
// 游戏跑在窗口里时，AR 会被放大且偏移，对不上路面。
//
// 插件本身只做"装载与开关"，真正的运行时补丁在 ARWindowAlign.Core 里，
// 由本插件显式加载进默认(不可回收) AssemblyLoadContext —— 这是 Harmony 能正常
// 生成补丁包装方法的前提（ETS2LA 用可回收上下文加载插件）。

using System.Reflection;
using System.Runtime.Loader;
using ETS2LA.Logging;
using ETS2LA.Shared;

namespace ARWindowAlign;

public class ARWindowAlignPlugin : Plugin
{
    private static readonly object Gate = new();
    private static Type? _patcher;

    public override PluginInformation Info => new PluginInformation
    {
        Id = "mldjy.arwindowalign",
        Version = "1.0.0",
        Name = "AR Window Align",
        Description = "Games AR rendering to the game window instead of the primary monitor, so windowed play lines up.",
        AuthorName = "迷路的鲸鱼",
    };

    public override void Init()
    {
        base.Init();
        Logger.Info("[ARWindowAlign] " + InvokeCore("Apply"));
        Logger.Info("[ARWindowAlign] " + InvokeCore("Status"));
        StartStatusReports();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        Logger.Info("[ARWindowAlign] " + InvokeCore("Apply"));
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Logger.Info("[ARWindowAlign] " + InvokeCore("Remove"));
    }

    public override void Shutdown()
    {
        Logger.Info("[ARWindowAlign] " + InvokeCore("Remove"));
        base.Shutdown();
    }

    /// <summary>
    /// Logs the measured geometry and patch counters a few seconds after startup,
    /// so clipping can be verified from ets2la.log without watching the screen.
    /// </summary>
    private static void StartStatusReports()
    {
        var t = new Thread(() =>
        {
            foreach (var delayMs in new[] { 6000, 12000, 25000 })
            {
                Thread.Sleep(delayMs);
                try { Logger.Info("[ARWindowAlign] " + InvokeCore("Status")); } catch { }
            }
        })
        { IsBackground = true, Name = "ARWindowAlign.Status" };
        t.Start();
    }

    /// <summary>Calls a static method on ARWindowAlign.Core.Patcher (loaded into the default context).</summary>
    private static string InvokeCore(string methodName)
    {
        try
        {
            lock (Gate)
            {
                if (_patcher == null)
                {
                    var dir = ResolvePluginDirectory();
                    var core = LoadIntoDefaultContext(Path.Combine(dir, "ARWindowAlign.Core.dll"), "ARWindowAlign.Core");
                    _patcher = core.GetType("ARWindowAlign.Core.Patcher", throwOnError: true)!;
                }
            }

            var method = _patcher!.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null) return $"{methodName}: method not found";

            return method.Invoke(null, null) as string ?? $"{methodName}: no result";
        }
        catch (Exception ex)
        {
            return $"{methodName} failed: {ex.GetBaseException().Message}";
        }
    }

    private static string ResolvePluginDirectory()
    {
        var candidates = new List<string>();

        // Preferred: the live install's Plugins folder.
        try { candidates.Add(Path.Combine(AppContext.BaseDirectory, "Plugins")); } catch { }

        // Fallback: alongside this assembly (works when the plugin is not shadow copied).
        try
        {
            var here = Path.GetDirectoryName(typeof(ARWindowAlignPlugin).Assembly.Location);
            if (!string.IsNullOrEmpty(here)) candidates.Add(here);
        }
        catch { }

        foreach (var c in candidates)
        {
            try
            {
                if (File.Exists(Path.Combine(c, "ARWindowAlign.Core.dll")) &&
                    File.Exists(Path.Combine(c, "0Harmony.dll")))
                    return c;
            }
            catch { }
        }

        return candidates.FirstOrDefault() ?? AppContext.BaseDirectory;
    }

    /// <summary>
    /// Loads an assembly (and, first, Harmony) into the DEFAULT, non-collectible context.
    /// Plugins are loaded into a collectible context by ETS2LA, and Harmony patches cannot
    /// reference patch methods that live in a collectible assembly.
    /// </summary>
    private static Assembly LoadIntoDefaultContext(string path, string simpleName)
    {
        var alc = AssemblyLoadContext.Default;

        var existing = alc.Assemblies.FirstOrDefault(a =>
            string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        // Harmony first: the Core assembly depends on it and the default context
        // does not know about the Plugins folder.
        var harmonyPath = Path.Combine(Path.GetDirectoryName(path)!, "0Harmony.dll");
        if (File.Exists(harmonyPath))
        {
            var hasHarmony = alc.Assemblies.Any(a =>
                string.Equals(a.GetName().Name, "0Harmony", StringComparison.OrdinalIgnoreCase));
            if (!hasHarmony) alc.LoadFromAssemblyPath(harmonyPath);
        }

        return alc.LoadFromAssemblyPath(path);
    }
}

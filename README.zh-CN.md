# ARWindowAlign（中文说明）

[English](README.md) | **简体中文**

让 **ETS2LA 的 AR 渲染跟随游戏窗口**，而不是假定游戏铺满主显示器 —— 解决「窗口模式玩欧卡2/美卡时，AR 路径线被按全屏放大、偏移，对不上路面」的问题。

**几何关系（嵌套一层）：**

```
主显示器  3840x2160                            <- ETS2LA 默认的 AR 基准
└── 游戏窗口  (1907,524) 1920x1080             <- 游戏实际渲染的位置
    └── AR 必须按这个矩形缩放，并加上它的屏幕原点
```

## 问题成因

ETS2LA 的 AR 换算基准写死在**主显示器分辨率**上，全流程不读取游戏窗口的位置与尺寸：

```csharp
// ETS2LA.Overlay/Overlay.cs
public float OverlayWidth  => GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).Width;
public float OverlayHeight => GLFW.GetVideoMode(GLFW.GetPrimaryMonitor()).Height;

// ETS2LA.Overlay/AR/AR.cs
thisFrameWidth  = (int)OverlayHandler.Current.OverlayWidth;
thisFrameHeight = (int)OverlayHandler.Current.OverlayHeight;
```

游戏以窗口模式运行时，AR 元素被按**整块显示器**缩放并偏移，于是与路面完全对不上。

## 工作原理

由插件施加运行时补丁（Harmony），**不修改 ETS2LA 本体**。AR 有两条渲染通道，两处都挂钩：

| 通道 | 触发条件 | 挂钩目标 |
|---|---|---|
| ImGui 2D 绘制 | 叠加层设置「简化图形」= **开** | `ARRenderer.WorldToScreen(Vector3, int, int)` |
| 着色器 / 全画质 | 「简化图形」= **关** | `ARRenderer.WorldToNDC(Vector3)` |

- **`WorldToScreen`**：`prefix` 把目标尺寸（原为主显示器分辨率）换成**游戏窗口客户区尺寸**；`postfix` 给结果加上客户区在屏幕上的原点坐标。
- **`WorldToNDC`**：着色器仍按整块显示器的视口绘制，因此把「按显示器映射」的 NDC 换算成「按游戏窗口映射」：

  ```
  ndc'x = (2·winX + (ndc_x + 1)·winW) / monW − 1
  ndc'y = 1 − (2·winY + (1 − ndc_y)·winH) / monH
  ```

游戏铺满主显示器（全屏 / 无边框全屏）时，两式恒等于原值 —— 插件**完全不干预**，行为与原版一致。

坐标一律取 **DPI 感知的物理像素**（后台线程先设 `PER_MONITOR_AWARE_V2` 再测量），150% 缩放下不会取到虚拟化尺寸；窗口矩形每 150ms 刷新，游戏未运行时插件处于空闲。

### 为什么拆成两个程序集

ETS2LA 用**可回收 (collectible) `AssemblyLoadContext`** 加载插件。Harmony 生成的补丁包装方法无法引用可回收程序集里的补丁方法，会报：

```
Could not load file or assembly '0Harmony' ... Operation is not supported
```

所以补丁代码放在 `ARWindowAlign.Core.dll`，由插件运行时显式加载进**默认（不可回收）上下文**（先装载 Harmony，其依赖才能解析）。

## 安装

### 方式 A — 用现成包（无需编译）

1. 从 [Releases](../../releases) 下载 `ARWindowAlign-v1.0.0.zip`
2. 把三个 DLL 复制到 ETS2LA 的 `Plugins` 目录：通常是 `%LOCALAPPDATA%\ETS2LA\current\Plugins\`（不存在就新建）
3. 重启 ETS2LA

### 方式 B — 自行编译

需要 .NET SDK 10+ 以及一份 ETS2LA 安装（用于编译期引用 `ETS2LA.Shared.dll` / `ETS2LA.Logging.dll`，本仓库**不**分发这些文件）。

```powershell
# 拉取 Lib.Harmony 到 lib/0Harmony.dll
powershell -ExecutionPolicy Bypass -File scripts/fetch-harmony.ps1

dotnet build ARWindowAlign.Core/ARWindowAlign.Core.csproj -c Release
dotnet build ARWindowAlign/ARWindowAlign.csproj          -c Release

# ETS2LA 不在默认位置时覆盖：
# dotnet build ARWindowAlign/ARWindowAlign.csproj -c Release -p:Ets2laDir="D:\path\to\ETS2LA\current"
```

然后把 `bin\Release` 下的 `ARWindowAlign.dll`、`ARWindowAlign.Core.dll` 与 `0Harmony.dll` 复制进 `Plugins`。

## 验证

启动后 `%LOCALAPPDATA%\ETS2LA\current\ets2la.log` 应出现：

```
INF  WorldToScreen patched, WorldToNDC patched
INF  monitor 3840x2160, game client rect = (1907,524) 1920x1080
INF 已启用插件：mldjy.arwindowalign
```

游戏内启动辅助后，AR 元素应贴合车道并向消失点收束。若本来就对齐，说明你在全屏模式，插件处于空闲、不干预。

## 停用 / 卸载

- **临时**：在 ETS2LA 插件管理器里禁用 `mldjy.arwindowalign`（立即撤掉补丁，恢复原版行为）
- **彻底**：删除 `Plugins` 目录下的三个 DLL

## 已知限制

- **仅 Windows**：几何跟踪使用 Win32（`GetClientRect` / `ClientToScreen` / `GetSystemMetrics`）。上游想要的是一套**跨平台**方案，本插件不是。
- **与 ETS2LA 内部实现绑定**：上游若重命名 `ARRenderer` / `WorldToScreen` / `WorldToNDC`，插件会跳过补丁并在日志打印 `skipped: ...`，不会崩溃。
- **仅主显示器**：游戏窗口若在副屏，叠加层本身也不覆盖那块区域。
- **不识别黑边**：窗口宽高比与游戏渲染分辨率不一致时，AR 按整个客户区映射（未检测游戏自己画的上下黑边）。

## 上游状态

已提交功能请求：**[ETS2LA#631 — AR rendering should follow the game window rect](https://github.com/ETS2LA/ETS2LA/issues/631)**

项目负责人 [@Tumppi066](https://github.com/Tumppi066) 的回复：

> This is a known issue that we haven't yet fixed. Once we implement this we want it to be cross platform, and I haven't investigated how this is done on Windows yet. For now we'll sadly have to ask you to either disable AR rendering or use fullscreen mode 👍

（已知问题、尚未修复；官方实现时要跨平台，而 Windows 侧尚未研究；目前的建议是关掉 AR 渲染或改用全屏。）

因此本插件是**社区侧针对 Windows 的临时解法**，与 ETS2LA 项目无隶属或背书关系。

## 许可与致谢

- 插件代码：**MIT** —— 见 [LICENSE](LICENSE)
- 附带 [Lib.Harmony](https://github.com/pardeike/Harmony) 2.4.2（MIT）用于施加运行时补丁 —— 见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
- ETS2LA 为 Tumppi066 的独立项目，本仓库不分发其任何二进制文件
- English: [README.md](README.md)

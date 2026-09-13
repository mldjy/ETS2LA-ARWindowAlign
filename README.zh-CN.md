# ETS2LA AR 窗口对齐（ARWindowAlign）

[ETS2LA](https://github.com/ETS2LA/ETS2LA)（欧洲卡车模拟2 / 美国卡车模拟自动驾驶辅助）的第三方插件：让 AR 渲染跟随**游戏窗口**而不是主显示器，并保证 AR 只画在窗口内。

基于 ETS2LA **v2026.9.5012**、欧洲卡车模拟2 **1.6.0** 测试

## 功能

- 窗口模式下游玩时，AR 的位置与缩放与游戏窗口一致
- AR 在窗口边缘被切断，不会溢出到桌面
- 游戏铺满主显示器（全屏 / 无边框全屏）时不作任何改动，行为与原版一致

## 安装

1. 从 [Releases](../../releases) 下载 `ARWindowAlign-v1.0.0.zip`
2. 解压后运行安装脚本（自动复制文件夹并登记到 ETS2LA 的插件清单）：

   ```powershell
   powershell -ExecutionPolicy Bypass -File install.ps1
   ```

   也可以手动安装：把 `mldjy.arwindowalign` 整个文件夹复制到
   `%LOCALAPPDATA%\ETS2LA\current\Plugins\`，再在
   `%APPDATA%\ETS2LA\InstalledPluginManifest.json` 里加一条：

   ```json
   { "Id": "mldjy.arwindowalign", "Version": "1.0.0",
     "DllPath": "Plugins\\mldjy.arwindowalign\\ARWindowAlign.dll",
     "Dependencies": [], "Type": 0 }
   ```

   > ETS2LA 只自动扫描 `Plugins\` 根目录下的 DLL，子文件夹里的插件必须登记清单才会被加载。
3. 重启 ETS2LA

游戏内启动辅助后，AR 元素应贴合车道并向消失点收束。

## 停用 / 卸载

- **临时**：在 ETS2LA 插件管理器里禁用 `mldjy.arwindowalign`
- **彻底**：运行 `uninstall.ps1`，或手动删除 `mldjy.arwindowalign` 文件夹与清单条目

## 自行编译

需要 .NET SDK 10+ 以及一份 ETS2LA 安装（用于编译期引用 `ETS2LA.Shared.dll` / `ETS2LA.Logging.dll`，本仓库**不**分发这些文件）。

```powershell
cd <仓库所在目录>
powershell -ExecutionPolicy Bypass -File scripts\fetch-harmony.ps1
dotnet build ARWindowAlign.Core\ARWindowAlign.Core.csproj -c Release
dotnet build ARWindowAlign\ARWindowAlign.csproj -c Release
```

产物为 `bin\Release` 下的 `ARWindowAlign.dll`、`ARWindowAlign.Core.dll`，连同 `lib\0Harmony.dll` 一起放进 `Plugins\mldjy.arwindowalign\`。

## 已知限制

- **仅 Windows**
- **仅主显示器**：游戏窗口若在副屏，叠加层本身也不覆盖那块区域
- **不识别黑边**：窗口宽高比与游戏渲染分辨率不一致时，AR 按整个客户区映射
- 仅在 ETS2LA 开启 AR 渲染时绘制 AR 元素

## 免责声明

- 不保证在 **ETS2LA 或游戏版本更新后仍然兼容**。
- 本项目**不保证后续持续更新与维护**。

## 许可

采用自定义许可（v1.1，见 [LICENSE](LICENSE)）：

- **允许**：免费使用、修改，以及免费分发原版或修改版
- **不允许**：在未做实质性独立开发之前，以任何形式直接或间接销售本插件；
  仅做非关键修改（例如换成其它语言的翻译文本、调整执行顺序等细微改动）不构成可销售的理由
- **例外**：若本插件因 ETS2LA 或游戏更新而失效，且作者已停止更新一段时间，
  允许他人在仅做恢复可用性的简单修复后销售该版本；作者恢复更新后须立即停止此类销售，
  此前已完成的销售不受影响
- 附带 [Lib.Harmony](https://github.com/pardeike/Harmony) 2.4.2（MIT）—— 见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
- ETS2LA 为 Tumppi066 的独立项目，本仓库不分发其任何二进制文件
- English: [README.md](README.md)

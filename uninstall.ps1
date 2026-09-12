<#
  卸载 / uninstall: 删除插件文件夹并移除安装清单中的条目。
  Plugin id: mldjy.arwindowalign
#>
param(
    [string]$Root = ""
)
$ErrorActionPreference = "Stop"
$PluginId = "mldjy.arwindowalign"

if (-not $Root) {
    $cands = @()
    if ($env:LOCALAPPDATA) { $cands += (Join-Path $env:LOCALAPPDATA "ETS2LA\current") }
    $p = Get-Process ETS2LA -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p -and $p.Path) { $cands += (Split-Path -Parent $p.Path) }
    $Root = $cands | Where-Object { $_ -and (Test-Path (Join-Path $_ "Plugins")) } | Select-Object -First 1
}
if (-not $Root) { Write-Host "ERROR: ETS2LA folder not found. Use -Root <...\ETS2LA\current>"; exit 1 }

$target = Join-Path (Join-Path $Root "Plugins") $PluginId
if (Test-Path $target) { Remove-Item $target -Recurse -Force; Write-Host "Removed: $target" }

$manifest = Join-Path $env:APPDATA "ETS2LA\InstalledPluginManifest.json"
if (Test-Path $manifest) {
    Copy-Item $manifest "$manifest.bak" -Force
    $json = Get-Content $manifest -Raw -Encoding UTF8 | ConvertFrom-Json
    $kept = @($json.InstalledPlugins | Where-Object { $_.Id -ne $PluginId })
    $json.InstalledPlugins = $kept
    $json | ConvertTo-Json -Depth 8 | Set-Content -Path $manifest -Encoding UTF8
    Write-Host "Manifest entry removed."
}
Write-Host "Done."

<#
  安装 / install: 把插件放进 Plugins\<插件id>\ 文件夹，并登记到安装清单。
  Plugin id: mldjy.arwindowalign
  Dll      : ARWindowAlign.dll
#>
param(
    [string]$Root = ""     # 可选：手动指定 ...\ETS2LA\current
)
$ErrorActionPreference = "Stop"
$PluginId = "mldjy.arwindowalign"
$DllName  = "ARWindowAlign.dll"
$CoreName = "ARWindowAlign.Core.dll"
$Version  = "1.0.0"

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $here $PluginId
if (-not (Test-Path $src)) { Write-Host "ERROR: folder not found: $src"; exit 1 }

if (-not $Root) {
    $cands = @()
    if ($env:LOCALAPPDATA) { $cands += (Join-Path $env:LOCALAPPDATA "ETS2LA\current") }
    $p = Get-Process ETS2LA -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p -and $p.Path) { $cands += (Split-Path -Parent $p.Path) }
    $Root = $cands | Where-Object { $_ -and (Test-Path (Join-Path $_ "Plugins")) } | Select-Object -First 1
}
if (-not $Root) { Write-Host "ERROR: ETS2LA folder not found. Use: install.ps1 -Root <...\ETS2LA\current>"; exit 1 }
Write-Host "ETS2LA root: $Root"

$plugins = Join-Path $Root "Plugins"
$target  = Join-Path $plugins $PluginId
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item (Join-Path $src "*") $target -Recurse -Force
Write-Host "Installed folder: $target"

foreach ($f in @($DllName, $CoreName)) {
    $legacy = Join-Path $plugins $f
    if (Test-Path $legacy) { Remove-Item $legacy -Force; Write-Host "Removed legacy file: $f" }
}

$manifest = Join-Path $env:APPDATA "ETS2LA\InstalledPluginManifest.json"
$manDir = Split-Path -Parent $manifest
if (-not (Test-Path $manDir)) { New-Item -ItemType Directory -Force -Path $manDir | Out-Null }
if (-not (Test-Path $manifest)) {
    if (Get-Process ETS2LA -ErrorAction SilentlyContinue) {
        Write-Host "ERROR: manifest missing and ETS2LA is running. Start ETS2LA once, then rerun."
        exit 1
    }
    '{ "InstalledPlugins": [] }' | Set-Content -Path $manifest -Encoding UTF8
}
Copy-Item $manifest "$manifest.bak" -Force

$json  = Get-Content $manifest -Raw -Encoding UTF8 | ConvertFrom-Json
$rel   = "Plugins\$PluginId\$DllName"
$entry = $json.InstalledPlugins | Where-Object { $_.Id -eq $PluginId }
if ($entry) {
    $entry.Version = $Version
    $entry.DllPath = $rel
    $entry.Type = 0
    if (-not $entry.Dependencies) { $entry.Dependencies = @() }
    Write-Host "Manifest entry updated."
} else {
    $json.InstalledPlugins += [pscustomobject]@{
        Id = $PluginId; Version = $Version; DllPath = $rel; Dependencies = @(); Type = 0
    }
    Write-Host "Manifest entry added."
}
$json | ConvertTo-Json -Depth 8 | Set-Content -Path $manifest -Encoding UTF8
Write-Host "Registered: $rel"
Write-Host ""
Write-Host "Done. Restart ETS2LA, then enable the plugin in the plugin manager."

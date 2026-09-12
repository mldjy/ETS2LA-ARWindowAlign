# Fetches Lib.Harmony from NuGet and extracts 0Harmony.dll into <repo>\lib\0Harmony.dll
# so ARWindowAlign.Core can be built. The binary is intentionally not committed.
#
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\fetch-harmony.ps1

$ErrorActionPreference = 'Stop'
$version = '2.4.2'

$root = Split-Path -Parent $PSScriptRoot
$lib  = Join-Path $root 'lib'
New-Item -ItemType Directory -Force -Path $lib | Out-Null

$nupkg = Join-Path $env:TEMP "lib.harmony.$version.nupkg"
Write-Host "Downloading Lib.Harmony $version ..."
Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Lib.Harmony/$version" -OutFile $nupkg -UseBasicParsing

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg)
try {
    $entry = $zip.Entries |
        Where-Object { $_.FullName -match '^lib/net(8|9|10)\.0/0Harmony\.dll$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $entry) {
        $names = ($zip.Entries | Where-Object { $_.FullName -like '*0Harmony*' } | Select-Object -ExpandProperty FullName) -join ', '
        throw "0Harmony.dll not found in the package. Candidates: $names"
    }

    $out = Join-Path $lib '0Harmony.dll'
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $out, $true)
    Write-Host "Extracted $($entry.FullName) -> $out"
} finally {
    $zip.Dispose()
}

Remove-Item $nupkg -Force -ErrorAction SilentlyContinue
Write-Host "Done. You can now build ARWindowAlign.Core."

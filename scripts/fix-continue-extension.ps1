# Fix Continue "Error activating" on Windows — must use win32-x64 VSIX, not universal package.
$ErrorActionPreference = "Stop"
$cache = Join-Path $PSScriptRoot ".cache"
$vsix = Join-Path $cache "continue-win32-x64.vsix"
$unpack = Join-Path $cache "continue-x64-unpack"
$extRoot = Join-Path $env:USERPROFILE ".cursor\extensions"
$dest = Join-Path $extRoot "Continue.continue-2.1.0-win32-x64"
$cursor = Join-Path $env:LOCALAPPDATA "Programs\cursor\Cursor.exe"

New-Item -ItemType Directory -Force -Path $cache | Out-Null

$arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
if ($arch -eq 'Arm64') {
    Write-Host "Windows ARM64 detected — use Cursor marketplace install (Pre-Release) if this script fails." -ForegroundColor Yellow
    $platform = "win32-arm64"
} else {
    $platform = "win32-x64"
}

Write-Host "Downloading Continue $platform VSIX..."
$url = "https://marketplace.visualstudio.com/_apis/public/gallery/publishers/Continue/vsextensions/continue/2.1.0/vspackage?targetPlatform=$platform"
Invoke-WebRequest -Uri $url -OutFile $vsix -UseBasicParsing

if (Test-Path $unpack) { Remove-Item -Recurse -Force $unpack }
Expand-Archive -Path $vsix -DestinationPath $unpack -Force

$bin = Join-Path $unpack "extension\bin\napi-v3\win32\x64\onnxruntime_binding.node"
if (-not (Test-Path $bin)) {
    Write-Host "VSIX missing Windows binary at $bin" -ForegroundColor Red
    exit 1
}

Get-ChildItem $extRoot -Directory | Where-Object { $_.Name -like "Continue.continue*" } | ForEach-Object {
    Remove-Item -Recurse -Force $_.FullName
}

if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
Copy-Item -Recurse (Join-Path $unpack "extension") $dest

if (Test-Path $cursor) {
    & $cursor --install-extension $vsix --force 2>$null
}

Write-Host "OK: $dest" -ForegroundColor Green
Write-Host "Reload Cursor: Ctrl+Shift+P -> Developer: Reload Window"

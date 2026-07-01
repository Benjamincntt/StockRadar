# Don cache Chrome an toan (dong Chrome truoc)
#   powershell -ExecutionPolicy Bypass -File scripts\clean-chrome-cache.ps1
$ErrorActionPreference = "SilentlyContinue"
$chrome = "$env:LOCALAPPDATA\Google\Chrome\User Data"

if (-not (Test-Path $chrome)) {
    Write-Host "Khong tim thay Chrome User Data."
    exit 0
}

$before = (Get-ChildItem $chrome -Recurse | Measure-Object Length -Sum).Sum

Write-Host "Dong Chrome..." -ForegroundColor Yellow
Get-Process chrome -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

$targets = @(
    "$chrome\OptGuideOnDeviceModel",
    "$chrome\OptGuideOnDeviceClassifierModel",
    "$chrome\optimization_guide_model_store",
    "$chrome\OnDeviceHeadSuggestModel",
    "$chrome\Default\Cache",
    "$chrome\Default\Code Cache",
    "$chrome\Default\GPUCache",
    "$chrome\Default\Service Worker\CacheStorage",
    "$chrome\Default\Service Worker\ScriptCache",
    "$chrome\ShaderCache",
    "$chrome\GrShaderCache",
    "$chrome\GPUPersistentCache"
)

foreach ($t in $targets) {
    if (Test-Path $t) {
        Remove-Item $t -Recurse -Force
        Write-Host "  Xoa: $(Split-Path $t -Leaf)"
    }
}

$after = (Get-ChildItem $chrome -Recurse | Measure-Object Length -Sum).Sum
$freed = [math]::Round(($before - $after) / 1GB, 2)
$disk = (Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace

Write-Host ""
Write-Host "Giai phong: ~${freed} GB" -ForegroundColor Green
Write-Host "Chrome User Data: $([math]::Round($after/1GB,2)) GB"
Write-Host "C: trong: $([math]::Round($disk/1GB,1)) GB"
Write-Host "Mo lai Chrome - model AI co the tai lai khi can." -ForegroundColor Yellow

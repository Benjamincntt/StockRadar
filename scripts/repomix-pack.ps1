# Pack codebase cho AI — CHỈ chạy khi cần phân tích toàn project (tốn token khi attach).
# Output: repomix-output.xml (đã ignore trong git)
param(
    [switch]$Copy
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

if (-not (Get-Command repomix -ErrorAction SilentlyContinue)) {
    Write-Host "Cài repomix: npm install -g repomix" -ForegroundColor Yellow
    npm install -g repomix@latest
}

Write-Host "==> repomix (source only, no build artifacts)" -ForegroundColor Cyan
repomix --config repomix.config.json

$out = Join-Path $root "repomix-output.xml"
if (-not (Test-Path $out)) {
    Write-Host "Không tạo được $out" -ForegroundColor Red
    exit 1
}

$sizeMb = [math]::Round((Get-Item $out).Length / 1MB, 2)
Write-Host "Xong: $out ($sizeMb MB)" -ForegroundColor Green
Write-Host "Gắn file này vào chat CHỈ khi cần review kiến trúc toàn repo." -ForegroundColor Yellow

if ($Copy) {
    Set-Clipboard -Path $out
    Write-Host "Đã copy đường dẫn vào clipboard." -ForegroundColor Green
}

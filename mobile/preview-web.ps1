# Preview JUICE mobile UI trong trình duyệt (hot reload — không cần build APK)
param(
    [int]$Port = 5199,
    [string]$ApiBase = "http://localhost:5280/api/v1",
    [switch]$Production
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

if ($Production) {
    $ApiBase = "http://103.226.248.6/api/v1"
}

if (-not $env:PUB_CACHE) { $env:PUB_CACHE = "D:\pub-cache" }

$flutter = "flutter"
if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) {
    $fallback = "D:\flutter\bin\flutter.bat"
    if (Test-Path $fallback) { $flutter = $fallback }
    else {
        Write-Host "Chua tim thay flutter trong PATH." -ForegroundColor Red
        exit 1
    }
}

Write-Host "==> JUICE mobile preview (Flutter Web)" -ForegroundColor Cyan
Write-Host "    URL:  http://localhost:$Port" -ForegroundColor Green
Write-Host "    API:  $ApiBase" -ForegroundColor Yellow
Write-Host ""
Write-Host "Hot reload:  nhan 'r' trong terminal nay sau khi sua UI" -ForegroundColor DarkGray
Write-Host "Hot restart: nhan 'R' (hoac Ctrl+Shift+F5) de thay doi lon" -ForegroundColor DarkGray
Write-Host "Thoat:      nhan 'q'" -ForegroundColor DarkGray
Write-Host ""

# Giai phong port neu preview cu con chay
$listeners = netstat -ano | Select-String ":$Port\s" | ForEach-Object { ($_ -split '\s+')[-1] } | Sort-Object -Unique
foreach ($procId in $listeners) {
    if ($procId -match '^\d+$' -and $procId -ne '0') {
        Write-Host "Dang tat process cu tren port $Port (PID $procId)..." -ForegroundColor Yellow
        taskkill /PID $procId /F 2>$null | Out-Null
    }
}
Start-Sleep -Milliseconds 500

& $flutter pub get
& $flutter run -d web-server `
    --web-port=$Port `
    --web-hostname=localhost `
    --dart-define=API_BASE=$ApiBase

# vnstock KBS worker -> POST /api/v1/market/sync (goi tu start-all.ps1)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$venvPython = Join-Path $here ".venv\Scripts\python.exe"
$configDev = Join-Path $here "config.dev.json"

. (Join-Path $here "find-python.ps1")

$pythonExe = Find-StockRadarPython
if (-not $pythonExe) {
    Write-Host ""
    Write-Host "LOI: Chua co Python 3.10+." -ForegroundColor Red
    Write-Host ""
    Write-Host "Cach 1 - Cai Python (de chay vnstock worker):" -ForegroundColor Yellow
    Write-Host "  https://www.python.org/downloads/" -ForegroundColor White
    Write-Host "  Bat 'Add python.exe to PATH'" -ForegroundColor DarkGray
    Write-Host "  Tat stub Store: Settings > Apps > Advanced app settings > App execution aliases" -ForegroundColor DarkGray
    Write-Host "       -> tat python.exe va python3.exe" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Cach 2 - Khong can Python:" -ForegroundColor Yellow
    Write-Host "  Dong cua so nay. API da co KBS sync san (start-all tu bat neu khong co Python)." -ForegroundColor White
    Write-Host ""
    Read-Host "Nhan Enter de dong"
    exit 1
}

Set-Location $here

if (-not (Test-Path $venvPython)) {
    Write-Host "==> Tao venv..." -ForegroundColor Cyan
    & $pythonExe -m venv .venv
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "==> pip install..." -ForegroundColor Cyan
& $venvPython -m pip install -q -r requirements.txt
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$env:STOCKRADAR_SYNC_CONFIG = $configDev

Write-Host ""
Write-Host "StockRadar data-sync (vnstock KBS)" -ForegroundColor Green
Write-Host "  Python:   $pythonExe" -ForegroundColor DarkGray
Write-Host "  API:      http://localhost:5280/api/v1/market/sync" -ForegroundColor DarkGray
Write-Host "  Config:   config.dev.json (force_sync=true, 60s)" -ForegroundColor DarkGray
Write-Host "  Ctrl+C de dung." -ForegroundColor DarkGray
Write-Host ""

& $venvPython (Join-Path $here "sync.py")

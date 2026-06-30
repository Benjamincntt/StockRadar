# Build + restart API nen (tach khoi terminal Cursor)
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProj = Join-Path $root "StockRadar.Api\StockRadar.Api.csproj"
$binDir = Join-Path $root "StockRadar.Api\bin\Debug\net10.0"
$dll = Join-Path $binDir "StockRadar.Api.dll"
$logDir = Join-Path $root "logs"
$logFile = Join-Path $logDir "api-dev.log"
$pidFile = Join-Path $logDir "api-dev.pid"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

& (Join-Path $root "stop-api.ps1") | Out-Host

if (-not $SkipBuild) {
    Write-Host "==> Build API (Debug)..." -ForegroundColor Cyan
    dotnet build $apiProj
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not (Test-Path $dll)) {
    Write-Error "Khong tim thay $dll"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "==> Khoi dong API nen -> http://localhost:5280" -ForegroundColor Green
Write-Host "    Log: $logFile" -ForegroundColor DarkGray

$logErr = Join-Path $logDir "api-dev.err.log"

$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList "`"$dll`"" `
    -WorkingDirectory $binDir `
    -WindowStyle Hidden `
    -PassThru `
    -RedirectStandardOutput $logFile `
    -RedirectStandardError $logErr

$proc.Id | Out-File -FilePath $pidFile -Encoding ascii -Force

Start-Sleep -Seconds 3

try {
    $resp = Invoke-WebRequest -Uri "http://localhost:5280/swagger/index.html" -UseBasicParsing -TimeoutSec 15
    if ($resp.StatusCode -eq 200) {
        Write-Host "API san sang (PID $($proc.Id))" -ForegroundColor Green
        exit 0
    }
} catch {
    Write-Host "API chua phan hoi - xem log: $logFile" -ForegroundColor Yellow
    Get-Content $logFile -Tail 20 -ErrorAction SilentlyContinue
    exit 1
}

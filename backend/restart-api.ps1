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
# Listen moi interface — dien thoai cung WiFi goi duoc (APK -Local).
$env:ASPNETCORE_URLS = "http://0.0.0.0:5280"

$lanIp = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -match '^(192\.168\.|10\.)' -and $_.PrefixOrigin -ne 'WellKnown' } |
    Select-Object -First 1 -ExpandProperty IPAddress
if (-not $lanIp) {
    $lanIp = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -match '^(192\.168\.|10\.)' } |
        Select-Object -First 1 -ExpandProperty IPAddress
}

Write-Host "==> Khoi dong API nen -> http://0.0.0.0:5280 (localhost + LAN)" -ForegroundColor Green
if ($lanIp) {
    Write-Host "    LAN: http://${lanIp}:5280/swagger" -ForegroundColor Yellow
}
Write-Host "    Log: $logFile" -ForegroundColor DarkGray

# Mo firewall inbound 5280 neu co quyen (bo qua neu fail).
try {
    $rule = Get-NetFirewallRule -DisplayName "StockRadar API 5280" -ErrorAction SilentlyContinue
    if (-not $rule) {
        New-NetFirewallRule -DisplayName "StockRadar API 5280" -Direction Inbound -Protocol TCP -LocalPort 5280 -Action Allow -ErrorAction Stop | Out-Null
        Write-Host "    Firewall: da mo TCP 5280" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "    Firewall: chua mo TCP 5280 (can Admin). Neu phone khong vao duoc swagger LAN, mo thu cong." -ForegroundColor Yellow
}

$logErr = Join-Path $logDir "api-dev.err.log"

$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList "`"$dll`"", "--urls", "http://0.0.0.0:5280" `
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

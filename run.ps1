# StockRadar - 1 file: publish + API + frontend + pipeline (bo qua Job 1, khong clear DB)
param(
    [switch]$SkipPublish,
    [switch]$SkipPipeline,
    [int]$MonitorRounds = 2,
    [int]$MonitorWaitSec = 8,
    [switch]$OpenBrowser
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$backend = Join-Path $root "backend"
$frontend = Join-Path $root "frontend"
$dataSync = Join-Path $root "data-sync"

. (Join-Path $dataSync "api-helper.ps1")

$cfg = Get-Content (Join-Path $dataSync "config.json") -Raw | ConvertFrom-Json
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key
$headers = @{ "X-Sync-Key" = $key }
$swagger = "http://localhost:5280/swagger"
$uiUrl = "http://localhost:5173"
$uiAlerts = "$uiUrl/alerts"

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Wait-Frontend {
    param([int]$TimeoutSec = 90)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $null = Invoke-WebRequest -Uri $uiUrl -TimeoutSec 3 -UseBasicParsing
            return $true
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    return $false
}

function Invoke-Api([string]$Label, [scriptblock]$Action) {
    Write-Step $Label
    try {
        $result = & $Action
        if ($null -ne $result) {
            $result | ConvertTo-Json -Depth 6
        }
        return $result
    } catch {
        Write-Host "LOI: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

function Start-FrontendWindow {
    $runDev = Join-Path $frontend "run-dev.ps1"
    if (-not (Test-Path $runDev)) {
        Write-Error "Khong tim thay frontend\run-dev.ps1"
    }
    Start-Process -FilePath "powershell.exe" -ArgumentList @(
        "-NoExit", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $runDev
    ) | Out-Null
}

function Start-ApiWindow {
    # uu tien dotnet run tu source (khong bi Windows Application Control chan .exe publish)
    $devStart = Join-Path $backend "start-api.ps1"
    if (Test-Path $devStart) {
        Start-Process -FilePath "powershell.exe" -ArgumentList @(
            "-NoExit", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $devStart
        ) | Out-Null
        return
    }

    $publishedStart = Join-Path $env:LOCALAPPDATA "StockRadarApi\start.ps1"
    if (-not (Test-Path $publishedStart)) {
        $publishedStart = Join-Path $env:USERPROFILE "StockRadarApi\start.ps1"
    }
    if (-not (Test-Path $publishedStart)) {
        Write-Error "Khong tim thay start-api.ps1 hoac StockRadarApi\start.ps1"
    }
    Start-Process -FilePath "powershell.exe" -ArgumentList @(
        "-NoExit", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $publishedStart
    ) | Out-Null
}

Write-Host "========================================" -ForegroundColor Green
Write-Host " StockRadar - run all" -ForegroundColor Green
Write-Host " API + Frontend + Pipeline" -ForegroundColor Green
Write-Host " Pipeline: Job 2 + Phan tich + Job 3" -ForegroundColor Green
Write-Host " KHONG chay Job 1 (backfill), KHONG clear DB" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Green

if (-not $SkipPublish) {
    Write-Step "Publish API (ban deploy %USERPROFILE%\\StockRadarApi)"
    & (Join-Path $backend "stop-api.ps1") | Out-Null
    $null = & (Join-Path $backend "publish-api.ps1")
} else {
    Write-Host "Bo qua publish (-SkipPublish)" -ForegroundColor DarkGray
}

Write-Step "Khoi dong API (dotnet run tu source)"
& (Join-Path $backend "stop-api.ps1") | Out-Null
Start-ApiWindow

if (-not (Wait-StockRadarApi -BaseUrl $base -TimeoutSec 180)) {
    Write-Error "API khong san sang. Kiem tra cua so API."
}

Write-Step "Khoi dong Frontend"
Start-FrontendWindow

# Cho API on dinh truoc khi UI ket noi SignalR
Start-Sleep -Seconds 3

if (Wait-Frontend -TimeoutSec 90) {
    Write-Host "Frontend OK: $uiUrl" -ForegroundColor Green
} else {
    Write-Host "Frontend chua san sang trong 90s (co the npm install dang chay)." -ForegroundColor Yellow
}

if ($OpenBrowser) {
    Start-Process $uiUrl | Out-Null
}

$summary = [ordered]@{
    apiReady = $true
    frontendReady = $true
    session = $null
    analysis = $null
    opportunities = 0
    monitorRounds = @()
    alerts = 0
    signals = 0
}

if (-not $SkipPipeline) {
    Write-Host ""
    Write-Host "Pipeline (khong co Job 1):" -ForegroundColor DarkGray
    Write-Host "  Job 2  -> sync phien ngay T (co the mat 3-5 phut)" -ForegroundColor DarkGray
    Write-Host "  Phan tich -> watchlist + canh bao tin hieu" -ForegroundColor DarkGray
    Write-Host "  Job 3  -> monitor order flow (2 vong)" -ForegroundColor DarkGray

    Start-Sleep -Seconds 3
    if (-not (Wait-StockRadarApi -BaseUrl $base -TimeoutSec 30)) {
        Write-Error "API khong phan hoi truoc pipeline. Kiem tra cua so start-api.ps1"
    }

    $summary.session = Invoke-Api "Job 2 - sync phien ngay T" {
        Invoke-LongJobPost -Uri "$base/market/jobs/session" -Headers $headers -TimeoutSec 3600 -BaseUrl $base
    }

    $summary.analysis = Invoke-Api "Phan tich SmartMoney -> watchlist ngay mai" {
        Invoke-LongJobPost -Uri "$base/market/jobs/analysis" -Headers $headers -TimeoutSec 600 -BaseUrl $base
    }

    $opps = Invoke-Api "Kiem tra top co hoi" {
        Invoke-RestMethod -Uri "$base/opportunities?page=1&pageSize=10" -TimeoutSec 60
    }
    $summary.opportunities = @($opps.items).Count
    if ($summary.opportunities -gt 0) {
        Write-Host "Top $($summary.opportunities) ma:" -ForegroundColor Green
        @($opps.items) | ForEach-Object {
            Write-Host ("  {0} score={1} {2}%" -f $_.symbol, $_.score, $_.changePercent)
        }
    } else {
        Write-Host "Chua co co hoi (can Job 1 history hoac dieu kien loc)." -ForegroundColor Yellow
    }

    for ($i = 1; $i -le $MonitorRounds; $i++) {
        if ($i -gt 1 -and $MonitorWaitSec -gt 0) {
            Write-Host "Cho ${MonitorWaitSec}s (Job 3 can 2 vong de co delta)..." -ForegroundColor DarkGray
            Start-Sleep -Seconds $MonitorWaitSec
        }

    $round = Invoke-Api "Job 3 - monitor lan $i/$MonitorRounds" {
        Invoke-LongJobPost -Uri "$base/market/jobs/opportunity-monitor" -Headers $headers -TimeoutSec 300 -BaseUrl $base
    }
        $summary.monitorRounds += $round
    }

    $alerts = Invoke-Api "Kiem tra canh bao" {
        Invoke-RestMethod -Uri "$base/alerts?page=1&pageSize=20" -TimeoutSec 60
    }
    $summary.alerts = @($alerts.items).Count
    if ($summary.alerts -gt 0) {
        Write-Host "Co $($summary.alerts) canh bao gan day:" -ForegroundColor Green
        @($alerts.items) | Select-Object -First 5 | ForEach-Object {
            Write-Host ("  [{0}] {1}" -f $_.createdAt, $_.title)
        }
    } else {
        Write-Host "Chua co canh bao (can 2 vong Job 3 hoac chua co tin hieu moi)." -ForegroundColor Yellow
    }

    $signals = Invoke-RestMethod -Uri "$base/signals?page=1&pageSize=5" -TimeoutSec 60
    $summary.signals = @($signals.items).Count
} else {
    Write-Host "Bo qua pipeline (-SkipPipeline)" -ForegroundColor DarkGray
}

$market = Invoke-RestMethod -Uri "$base/market" -TimeoutSec 30

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " TOM TAT" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ("  VNINDEX: {0} ({1}%)" -f $market.indexPrice, $market.indexChangePercent)

if (-not $SkipPipeline) {
    Write-Host ("  Job 2 sync: {0} ma" -f $summary.session.symbolsSynced)
    Write-Host ("  Phan tich: {0} co hoi, {1} canh bao tin hieu" -f `
        $summary.analysis.opportunitiesSaved, $summary.analysis.patternAlertsPublished)
    Write-Host ("  Job 3 tong alert gui: {0}" -f (($summary.monitorRounds | ForEach-Object { $_.alertsSent }) -join " + "))
    Write-Host ("  Alerts trong DB: {0}" -f $summary.alerts)
}

Write-Host ""
Write-Host " URL (dang chay):" -ForegroundColor White
Write-Host "   UI:       $uiUrl"
Write-Host "   Canh bao: $uiAlerts"
Write-Host "   Swagger:  $swagger"
Write-Host ""
Write-Host " Dong: .\stop-all.ps1" -ForegroundColor DarkGray
Write-Host " Chi stack (khong pipeline): .\run.ps1 -SkipPipeline" -ForegroundColor DarkGray
Write-Host "========================================" -ForegroundColor Green

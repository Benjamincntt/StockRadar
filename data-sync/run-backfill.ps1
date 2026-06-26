# Job 1: backfill HOSE history (2000-01-01 -> T-1).
# Starts API automatically if not running.

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "api-helper.ps1")

$cfgPath = Join-Path $here "config.json"
$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key
$start = if ($cfg.backfill_start_date) { $cfg.backfill_start_date } else { "2000-01-01" }

Write-Host ""
Write-Host "Job 1 - Backfill universe (FAST mode)" -ForegroundColor Cyan
Write-Host "  startDate: $start" -ForegroundColor DarkGray
Write-Host ""

Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 180

$headers = @{
    "X-Sync-Key"   = $key
    "Content-Type" = "application/json"
}

$body = @{ startDate = $start } | ConvertTo-Json -Compress

Write-Host "Running backfill (may take several minutes)..." -ForegroundColor Cyan
Write-Host "Tip: run .\watch-job1-status.ps1 in another terminal to see progress." -ForegroundColor DarkGray
Write-Host ""

$result = Invoke-RestMethod `
    -Uri "$base/market/jobs/history" `
    -Method POST `
    -Headers $headers `
    -Body $body `
    -TimeoutSec 72000

$result | ConvertTo-Json -Depth 4

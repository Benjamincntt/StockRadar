# Job 2: append session day T + analysis for next session.

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "api-helper.ps1")

$cfg = Get-Content (Join-Path $here "config.json") -Raw | ConvertFrom-Json
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key
$headers = @{ "X-Sync-Key" = $key }

Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 120

Write-Host "Job 2 - Sync session (day T), co the mat vai phut..." -ForegroundColor Cyan
$session = Invoke-LongJobPost `
    -Uri "$base/market/jobs/session" `
    -Headers $headers `
    -TimeoutSec 3600 `
    -BaseUrl $base

Write-Host "Analysis - watchlist for next session" -ForegroundColor Cyan
$analysis = Invoke-LongJobPost `
    -Uri "$base/market/jobs/analysis" `
    -Headers $headers `
    -TimeoutSec 600 `
    -BaseUrl $base

@{ session = $session; analysis = $analysis } | ConvertTo-Json -Depth 5

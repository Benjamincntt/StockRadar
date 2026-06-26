# Job 3: one round of watchlist monitor (in-app alerts). Requires API on port 5280.
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "api-helper.ps1")

$cfg = Get-Content (Join-Path $here "config.json") -Raw | ConvertFrom-Json
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key

Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 180

Write-Host "Job 3 - opportunity monitor (1 round)..." -ForegroundColor Cyan
$result = Invoke-LongJobPost `
    -Uri "$base/market/jobs/opportunity-monitor" `
    -Headers @{ "X-Sync-Key" = $key } `
    -TimeoutSec 300 `
    -BaseUrl $base

$result | Format-List

if ($result.alertsSent -gt 0) {
    Write-Host "Check alerts: http://localhost:5280/api/v1/alerts" -ForegroundColor Green
    Write-Host "Or UI: http://localhost:5173/alerts" -ForegroundColor DarkGray
} else {
    Write-Host "No alerts this round (cooldown 30min/symbol, or no spike)." -ForegroundColor Yellow
}

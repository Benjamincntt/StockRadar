# Re-run SmartMoney analysis only (no Job 2 sync). Updates DailyOpportunities.

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "api-helper.ps1")

$cfg = Get-Content (Join-Path $here "config.json") -Raw | ConvertFrom-Json
$base = $cfg.api_base_url.TrimEnd("/")
$headers = @{ "X-Sync-Key" = $cfg.sync_api_key }

Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 120

Write-Host "Phan tich lai watchlist (SmartMoney)..." -ForegroundColor Cyan
$result = Invoke-LongJobPost `
    -Uri "$base/market/jobs/analysis" `
    -Headers $headers `
    -TimeoutSec 600 `
    -BaseUrl $base

$result | ConvertTo-Json -Depth 5

Write-Host ""
Write-Host "Ngay watchlist: $($result.forTradingDate)" -ForegroundColor DarkGray
Write-Host "Top co hoi (ngay $($result.forTradingDate)):" -ForegroundColor Cyan
$opps = Invoke-RestMethod -Uri "$base/opportunities?page=1&pageSize=15" -TimeoutSec 60
@($opps.items) | ForEach-Object {
    Write-Host ("  {0} score={1} price={2}" -f $_.symbol, $_.score, $_.price)
}
$lpb = @($opps.items) | Where-Object { $_.symbol -eq "LPB" }
if ($lpb) {
    Write-Host "LPB van trong top - kiem tra API da build lai chua." -ForegroundColor Yellow
} else {
    Write-Host "LPB da bi loai khoi top." -ForegroundColor Green
}

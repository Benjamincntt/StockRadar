# Job 1 night mode — delay lớn hơn, chạy khi ít tải.

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "api-helper.ps1")

$cfg = Get-PipelineConfig
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key
$start = if ($cfg.backfill_start_date) { $cfg.backfill_start_date } else { "2000-01-01" }

Write-Host ""
Write-Host "Job 1 - Backfill universe (NIGHT mode)" -ForegroundColor Cyan
Write-Host "  startDate: $start" -ForegroundColor DarkGray
Write-Host ""

Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 180

$headers = @{
    "X-Sync-Key"   = $key
    "Content-Type" = "application/json"
}

$body = @{ startDate = $start; mode = "night" } | ConvertTo-Json -Compress

$result = Invoke-RestMethod `
    -Uri "$base/market/jobs/history/night" `
    -Method POST `
    -Headers $headers `
    -Body $body `
    -TimeoutSec 72000

$result | ConvertTo-Json -Depth 4

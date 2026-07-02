# Job 3 — một vòng quét lệnh đột biến (OpportunityMonitor).

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "api-helper.ps1")

$cfg = Get-PipelineConfig
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key

Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 120

$result = Invoke-LongJobPost `
    -Uri "$base/market/jobs/opportunity-monitor" `
    -Headers @{ "X-Sync-Key" = $key } `
    -TimeoutSec 300 `
    -BaseUrl $base

$result | ConvertTo-Json -Depth 4

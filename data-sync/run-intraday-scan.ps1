# Quét đột biến HOSE thủ công (test ngoài giờ: ForceScanOutsideHours=true)

$ErrorActionPreference = "Stop"
$cfg = Get-Content (Join-Path $PSScriptRoot "config.json") -Raw | ConvertFrom-Json
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key

Invoke-RestMethod `
    -Uri "$base/market/jobs/intraday-scan" `
    -Method POST `
    -Headers @{ "X-Sync-Key" = $key } `
    -TimeoutSec 600

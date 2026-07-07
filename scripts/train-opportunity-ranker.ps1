# Train OpportunityRanker (logistic regression T+2.5) tren API
param(
    [string]$ApiBase = "http://localhost:5280/api/v1",
    [int]$Days = 180,
    [string]$SyncKey = ""
)

$ErrorActionPreference = "Stop"
$ApiBase = $ApiBase.TrimEnd("/")

if ([string]::IsNullOrWhiteSpace($SyncKey)) {
    $cfgPath = Join-Path (Split-Path -Parent $PSScriptRoot) "backend\StockRadar.Api\appsettings.json"
    if (Test-Path $cfgPath) {
        $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
        $SyncKey = $cfg.MarketData.SyncApiKey
    }
}

if ([string]::IsNullOrWhiteSpace($SyncKey)) {
    throw "Thieu SyncKey — truyen -SyncKey hoac cau hinh MarketData:SyncApiKey"
}

$headers = @{ "X-Sync-Key" = $SyncKey }
$uri = "$ApiBase/ml/train/t25-ranking?days=$Days"

Write-Host "==> POST $uri" -ForegroundColor Cyan
$result = Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -TimeoutSec 600
$result | ConvertTo-Json -Depth 4

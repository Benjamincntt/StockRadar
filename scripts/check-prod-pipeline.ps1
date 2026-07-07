# Kiem tra nhanh API prod: ranker, north-star, opportunities, (tuy chon) daily job
param(
    [string]$ApiBase = "http://103.226.248.6/api/v1",
    [string]$SyncKey = "",
    [switch]$RunDaily
)

$ErrorActionPreference = "Stop"
$ApiBase = $ApiBase.TrimEnd("/")

if ([string]::IsNullOrWhiteSpace($SyncKey)) {
    $cfgPath = Join-Path $PSScriptRoot "pipeline-config.json"
    if (Test-Path $cfgPath) {
        $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
        $SyncKey = $cfg.sync_api_key
    }
}

function Invoke-Check {
    param([string]$Label, [string]$Uri, [hashtable]$Headers = @{}, [string]$Method = "GET")
    Write-Host "`n==> $Label" -ForegroundColor Cyan
    Write-Host "    $Method $Uri" -ForegroundColor DarkGray
    try {
        $resp = Invoke-WebRequest -Uri $Uri -Method $Method -Headers $Headers -UseBasicParsing -TimeoutSec 300
        Write-Host "    HTTP $($resp.StatusCode)" -ForegroundColor Green
        $resp.Content | ConvertFrom-Json | ConvertTo-Json -Depth 6 -Compress:$false
    }
    catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        Write-Host "    HTTP $code" -ForegroundColor Red
        Write-Host "    $($_.Exception.Message)"
    }
}

Invoke-Check "ML ranker status" "$ApiBase/ml/ranker/status"

$ns = Invoke-RestMethod -Uri "$ApiBase/performance/north-star?days=90" -TimeoutSec 60
Write-Host "`n==> North Star (90d)" -ForegroundColor Cyan
Write-Host "    measuredSetups: $($ns.measuredSetups)"
$top5 = $ns.rankBuckets | Where-Object { $_.bucketId -eq "Top5" }
if ($top5) { Write-Host "    Top5 hit%: $($top5.hitRatePercent)" }

$opp = Invoke-RestMethod -Uri "$ApiBase/opportunities?PageSize=10" -TimeoutSec 60
Write-Host "`n==> Opportunities" -ForegroundColor Cyan
Write-Host "    forTradingDate: $($opp.forTradingDate)  total: $($opp.totalCount)  items: $($opp.items.Count)"
$opp.items | ForEach-Object { Write-Host "    - $($_.symbol) score=$($_.score) hit=$($_.predictedHitPercent)" }

if ($RunDaily) {
    if ([string]::IsNullOrWhiteSpace($SyncKey)) {
        throw "Can SyncKey — -SyncKey hoac pipeline-config.json"
    }
    $h = @{ "X-Sync-Key" = $SyncKey }
    Invoke-Check "Daily pipeline (Job2+analysis)" "$ApiBase/market/jobs/daily" $h "POST"
}
else {
    Write-Host "`n(Bo qua POST daily — them -RunDaily de chay job)" -ForegroundColor DarkGray
}

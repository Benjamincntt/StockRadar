# Snapshot North Star + ML ranker — chạy mỗi tuần (Phase 3 monitoring)
param(
    [string]$ApiBase = "http://103.226.248.6/api/v1",
    [string]$SyncKey = "",
    [int]$NorthStarDays = 90,
    [int]$DatasetDays = 180,
    [string]$OutDir = ""
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

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $PSScriptRoot "snapshots"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$stamp = Get-Date -Format "yyyy-MM-dd_HHmm"
$outFile = Join-Path $OutDir "ranker-weekly-$stamp.json"

Write-Host "==> North Star + ranker snapshot" -ForegroundColor Cyan

$ranker = Invoke-RestMethod -Uri "$ApiBase/ml/ranker/status" -TimeoutSec 60
$north = Invoke-RestMethod -Uri "$ApiBase/performance/north-star?days=$NorthStarDays" -TimeoutSec 60

$dataset = $null
if (-not [string]::IsNullOrWhiteSpace($SyncKey)) {
  $h = @{ "X-Sync-Key" = $SyncKey }
  $dataset = Invoke-RestMethod -Uri "$ApiBase/ml/dataset/t25-ranking?days=$DatasetDays" -Headers $h -TimeoutSec 120
}

$top5 = $north.rankBuckets | Where-Object { $_.bucketId -eq "Top5" } | Select-Object -First 1

$featureImportance = @()
if ($ranker.weights -and $ranker.featureNames) {
    for ($i = 0; $i -lt [Math]::Min($ranker.weights.Count, $ranker.featureNames.Count); $i++) {
        $featureImportance += [PSCustomObject]@{
            feature = $ranker.featureNames[$i]
            weight  = $ranker.weights[$i]
        }
    }
    $featureImportance = $featureImportance | Sort-Object { [Math]::Abs([double]$_.weight) } -Descending
}

$snapshot = [PSCustomObject]@{
    capturedAtUtc     = (Get-Date).ToUniversalTime().ToString("o")
    apiBase           = $ApiBase
    ranker            = $ranker
    northStar         = @{
        measuredSetups = $north.measuredSetups
        top5HitPercent = $top5.hitRatePercent
        top5Count      = $top5.measuredCount
        rankBuckets    = $north.rankBuckets
    }
    dataset           = if ($dataset) {
        @{
            rowCount            = $dataset.rowCount
            positiveLabels      = $dataset.positiveLabels
            positiveRatePercent = $dataset.positiveRatePercent
        }
    } else { $null }
    featureImportance = $featureImportance
}

$snapshot | ConvertTo-Json -Depth 8 | Set-Content -Path $outFile -Encoding UTF8

Write-Host "    measuredSetups: $($north.measuredSetups)" -ForegroundColor Green
Write-Host "    Top5 hit%:      $($top5.hitRatePercent) ($($top5.measuredCount) mẫu)" -ForegroundColor Green
Write-Host "    modelActive:    $($ranker.modelActive)  samples: $($ranker.trainingSamples)" -ForegroundColor Green
if ($dataset) {
    Write-Host "    dataset rows:   $($dataset.rowCount)  positives: $($dataset.positiveLabels)" -ForegroundColor Green
}
if ($featureImportance.Count -gt 0) {
    Write-Host "    Top weights:" -ForegroundColor DarkGray
    $featureImportance | Select-Object -First 3 | ForEach-Object {
        Write-Host "      $($_.feature): $($_.weight)"
    }
}
Write-Host "==> Saved: $outFile" -ForegroundColor Cyan

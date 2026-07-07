# So sanh backtest SmartMoney: MaxResults 10 vs 15 vs 30 (hold T+2, strict, no relaxed)
#
# Vi du:
#   .\scripts\compare-backtest-max-results.ps1
#   .\scripts\compare-backtest-max-results.ps1 -ApiBase http://localhost:5280/api/v1 -Days 60

param(
    [string]$ApiBase = "http://103.226.248.6/api/v1",
    [int]$Days = 90,
    [int]$HoldSessions = 2,
    [int]$MinScore = 60
)

$ErrorActionPreference = "Stop"
$ApiBase = $ApiBase.TrimEnd("/")

function Invoke-Backtest {
    param([int]$MaxPicks)
    $uri = "$ApiBase/backtest/smartmoney?days=$Days&maxPicksPerDay=$MaxPicks&holdSessions=$HoldSessions&relaxedFallback=false&mode=strict&minScore=$MinScore"
    Write-Host "==> GET $uri" -ForegroundColor Cyan
    return Invoke-RestMethod -Uri $uri -TimeoutSec 600
}

Write-Host "========================================" -ForegroundColor Green
Write-Host " Backtest MaxResults comparison" -ForegroundColor Green
Write-Host " days=$Days hold=$HoldSessions strict no-relaxed" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

$results = @()
foreach ($max in @(10, 15, 30)) {
    $r = Invoke-Backtest -MaxPicks $max
    $s = $r.summary
    $results += [PSCustomObject]@{
        MaxPicksPerDay = $max
        TradingDays    = $s.tradingDaysScanned
        DaysWithPicks  = $s.daysWithPicks
        TotalTrades    = $s.totalTrades
        WinRatePct     = $s.winRatePercent
        AvgReturnPct   = $s.avgReturnPercent
        MedianReturnPct = $s.medianReturnPercent
        MaxDrawdownPct = $s.maxDrawdownPercent
        SuccessThreshold = $s.successThresholdPercent
    }
}

Write-Host ""
$results | Format-Table -AutoSize
Write-Host ""
Write-Host "Goi y: chon MaxPicks co WinRate cao + AvgReturn on dinh (thuong 10 tot hon 30 neu strict)." -ForegroundColor DarkGray

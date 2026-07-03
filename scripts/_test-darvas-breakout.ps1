# Kiem tra Darvas breakout tren ma (mac dinh ORS)
param(
    [string]$Symbol = "ORS",
    [string]$ApiBase = "http://127.0.0.1:5280/api/v1"
)

$stock = Invoke-RestMethod -Uri "$ApiBase/stocks/$Symbol" -TimeoutSec 120
$signals = ($stock.activeSignals -join " | ")
$base = $stock.basePrice

Write-Host "=== $Symbol ===" -ForegroundColor Cyan
Write-Host "Gia: $($stock.price) | Doi: $($stock.changePercent)%"
Write-Host "Active signals: $signals"
if ($base) {
    Write-Host "Nen gia: $($base.baseLow) - $($base.baseHigh) | +$($base.gainFromBasePercent)% tu dinh nen"
} else {
    Write-Host "Nen gia: null"
}

$darvas = $stock.activeSignals | Where-Object { $_ -match "Darvas" }
if ($darvas) {
    Write-Host "DARVAS BREAKOUT: CO" -ForegroundColor Green
} else {
    Write-Host "DarvasBreakout signal: khong" -ForegroundColor Yellow
}

$entry = $stock.buyDecision.entry
if ($entry) {
    Write-Host "Diem vao: $($entry.status) | $($entry.type) | cat lo $($entry.stopLoss)"
}

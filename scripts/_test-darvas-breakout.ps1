# Kiem tra hoi tich luy phang + breakout tren ma (mac dinh ORS)
param(
    [string]$Symbol = "ORS",
    [string]$ApiBase = "http://127.0.0.1:5280/api/v1"
)

$stock = Invoke-RestMethod -Uri "$ApiBase/stocks/$Symbol" -TimeoutSec 120
$signals = ($stock.activeSignals -join " | ")
$box = $stock.flatBox

Write-Host "=== $Symbol ===" -ForegroundColor Cyan
Write-Host "Gia: $($stock.price) | Doi: $($stock.changePercent)%"
Write-Host "Active signals: $signals"
if ($box) {
    $status = if ($box.isBreakoutConfirmed) { "BREAKOUT" } else { "tich luy" }
    Write-Host "Hoi phang: $($box.boxLow) - $($box.boxHigh) | $($box.sessionDays) phien | $status"
    Write-Host "  FOMO +$($box.filterGainFromBoxTopPercent)% so dinh hoi $($box.filterBoxTop)"
} else {
    Write-Host "Hoi phang: null"
}

$darvas = $box -and $box.isBreakoutConfirmed
if ($darvas) {
    Write-Host "DARVAS BREAKOUT: CO" -ForegroundColor Green
} else {
    Write-Host "DarvasBreakout signal: khong" -ForegroundColor Yellow
}

$entry = $stock.buyDecision.entry
if ($entry) {
    Write-Host "Diem vao: $($entry.status) | $($entry.type) | cat lo $($entry.stopLoss)"
}

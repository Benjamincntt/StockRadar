# Watch Job 1 backfill progress (poll every 3s). Ctrl+C to stop.

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "api-helper.ps1")

$cfg = Get-PipelineConfig
$base = $cfg.api_base_url.TrimEnd("/")
$uri = "$base/market/jobs/history/status"

Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 120

Write-Host ""
Write-Host "Watching Job 1 backfill (Ctrl+C to stop)" -ForegroundColor Cyan
Write-Host "  If IDLE 0/0, run: .\run-backfill.ps1 in another terminal" -ForegroundColor DarkGray
Write-Host ""

$hintShown = $false

while ($true) {
    try {
        $s = Invoke-RestMethod -Uri $uri -TimeoutSec 10
        if ($s.isRunning) {
            $hintShown = $false
            $pct = if ($null -ne $s.percentComplete) { $s.percentComplete } else { 0 }
            Write-Host ("[{0:HH:mm:ss}] RUNNING {1}/{2} ({3}%) - {4}" -f (Get-Date), $s.processed, $s.total, $pct, $s.currentSymbol) -ForegroundColor Green
        } elseif ($s.total -gt 0 -and $s.processed -ge $s.total) {
            Write-Host ("[{0:HH:mm:ss}] DONE {1}/{2}" -f (Get-Date), $s.processed, $s.total) -ForegroundColor Cyan
        } else {
            if (-not $hintShown) {
                Write-Host ("[{0:HH:mm:ss}] IDLE - run .\run-backfill.ps1 to start Job 1" -f (Get-Date)) -ForegroundColor Yellow
                $hintShown = $true
            } else {
                Write-Host ("[{0:HH:mm:ss}] IDLE - waiting..." -f (Get-Date)) -ForegroundColor DarkGray
            }
        }
    } catch {
        Write-Host ("[{0:HH:mm:ss}] API unreachable - retrying..." -f (Get-Date)) -ForegroundColor Yellow
    }
    Start-Sleep -Seconds 3
}

# Clear market data (keep Users). Stop API first if running.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$settingsPath = Join-Path $root "StockRadar.Api\appsettings.Development.json"

if (-not (Test-Path $settingsPath)) {
    Write-Error "Missing $settingsPath"
}

$json = Get-Content $settingsPath -Raw | ConvertFrom-Json
$cs = $json.ConnectionStrings.DefaultConnection

$parts = @{}
foreach ($pair in $cs.Split(';')) {
    if ($pair -match '^\s*([^=]+)\s*=\s*(.*)$') {
        $parts[$Matches[1].Trim()] = $Matches[2].Trim()
    }
}

$server = $parts['Server']
$database = $parts['Database']
$user = $parts['User Id']
$password = $parts['Password']
$useWindowsAuth = -not $user -and (
    $parts['Integrated Security'] -eq 'True' -or $parts['Trusted_Connection'] -eq 'True'
)

if (-not $server -or -not $database) {
    Write-Error "Cannot parse Server/Database from appsettings.Development.json"
}
if (-not $useWindowsAuth -and (-not $user -or -not $password)) {
    Write-Error "SQL auth requires User Id and Password in connection string"
}

Write-Host "==> Stop API if running..." -ForegroundColor Yellow
& (Join-Path $root "stop-api.ps1") | Out-Null

$sql = @"
DELETE FROM [SessionRadarHits];
DELETE FROM [DailyOpportunities];
DELETE FROM [WatchlistItems];
DELETE FROM [Alerts];
DELETE FROM [Stocks];
DELETE FROM [MarketIndices];
SELECT COUNT(*) AS StocksRemaining FROM [Stocks];
"@

$sqlFile = Join-Path $env:TEMP "stockradar-clear.sql"
Set-Content -Path $sqlFile -Value $sql -Encoding UTF8

Write-Host "==> Clear data in $database on $server ..." -ForegroundColor Cyan

function Invoke-ClearSql {
    if (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
        if ($useWindowsAuth) {
            & sqlcmd -S $server -d $database -C -E -i $sqlFile
        } else {
            & sqlcmd -S $server -d $database -C -U $user -P $password -i $sqlFile
        }
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE" }
        return
    }

    if (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
        $params = @{
            ServerInstance = $server
            Database       = $database
            InputFile      = $sqlFile
        }
        if (-not $useWindowsAuth) {
            $params.Username = $user
            $params.Password = $password
        }
        Invoke-Sqlcmd @params
        return
    }

    throw "Neither sqlcmd nor Invoke-Sqlcmd found. Install SQL Server tools."
}

Invoke-ClearSql

Write-Host ""
Write-Host "Cleared: Stocks, MarketIndices, DailyOpportunities, SessionRadarHits, Alerts, WatchlistItems" -ForegroundColor Green
Write-Host "Kept: Users" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Next:" -ForegroundColor Yellow
Write-Host "  .\publish-api.ps1" -ForegroundColor White
Write-Host "  .\start-api-published.ps1" -ForegroundColor White
Write-Host "  cd ..\scripts" -ForegroundColor White
Write-Host "  .\run-backfill.ps1" -ForegroundColor White
Write-Host "  .\run-daily-jobs.ps1" -ForegroundColor White

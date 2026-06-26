# Run published StockRadar API (%LOCALAPPDATA%\StockRadarApi)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here "StockRadar.Api.exe"
$dll = Join-Path $here "StockRadar.Api.dll"

if (-not (Test-Path $exe) -and -not (Test-Path $dll)) {
    Write-Error "StockRadar.Api.exe not found. Run: D:\Source\StockRadar\backend\start-api.ps1"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

Get-ChildItem $here -Recurse -Include *.dll, *.exe -ErrorAction SilentlyContinue |
    ForEach-Object {
        Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath "$($_.FullName):Zone.Identifier" -ErrorAction SilentlyContinue
    }

Write-Host ""
Write-Host "StockRadar API - http://localhost:5280 (Ctrl+C to stop)" -ForegroundColor Green
Write-Host "Swagger: http://localhost:5280/swagger" -ForegroundColor DarkGray
Write-Host ""

Set-Location $here
if (Test-Path $exe) {
    & $exe
} else {
    dotnet $dll
}

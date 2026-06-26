# Publish + chay API (single-file exe)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $env:LOCALAPPDATA "StockRadarApi"

& (Join-Path $root "publish-api.ps1") | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $outDir "StockRadar.Api.exe"
$dll = Join-Path $outDir "StockRadar.Api.dll"

if (-not (Test-Path $exe) -and -not (Test-Path $dll)) {
    Write-Error "Publish failed: khong tim thay exe/dll trong $outDir"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host ""
Write-Host "==> API http://localhost:5280 (Ctrl+C de dung)" -ForegroundColor Green
Write-Host "    Swagger: http://localhost:5280/swagger" -ForegroundColor DarkGray
Write-Host ""

Set-Location $outDir
if (Test-Path $exe) {
    & $exe
} else {
    dotnet $dll
}

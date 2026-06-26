# Chi chay API da publish (single-file exe)
$ErrorActionPreference = "Stop"
$outDir = Join-Path $env:LOCALAPPDATA "StockRadarApi"
$exe = Join-Path $outDir "StockRadar.Api.exe"
$dll = Join-Path $outDir "StockRadar.Api.dll"

if (-not (Test-Path $exe) -and -not (Test-Path $dll)) {
    Write-Error "Chua co API publish. Chay backend\publish-api.ps1 hoac .\start-api.ps1 truoc."
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

Set-Location $outDir
Get-ChildItem $outDir -Recurse -Include *.dll, *.exe -ErrorAction SilentlyContinue |
    ForEach-Object {
        Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath "$($_.FullName):Zone.Identifier" -ErrorAction SilentlyContinue
    }

Write-Host "StockRadar API - http://localhost:5280" -ForegroundColor Green
Write-Host "Ctrl+C de dung." -ForegroundColor DarkGray

if (Test-Path $exe) {
    & $exe
} else {
    dotnet $dll
}

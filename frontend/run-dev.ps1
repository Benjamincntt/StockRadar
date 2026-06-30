# Chi chay Vite dev server — goi tu start-all.ps1
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path "node_modules")) {
    Write-Host "npm install..." -ForegroundColor Cyan
    npm install
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "JUICE UI - http://localhost:5173" -ForegroundColor Green
Write-Host "Ctrl+C de dung." -ForegroundColor DarkGray
npm run dev

# Dung API + frontend (Vite tren port 5173)
$ErrorActionPreference = "SilentlyContinue"
$backend = Join-Path $PSScriptRoot "backend"

& (Join-Path $backend "stop-api.ps1")

Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { $_.OwningProcess } |
    Select-Object -Unique |
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

Write-Host "Da dung API, frontend (neu co)." -ForegroundColor Green

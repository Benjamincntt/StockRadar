# Dung API + data-sync worker + frontend (Vite tren port 5173)
$ErrorActionPreference = "SilentlyContinue"
$backend = Join-Path $PSScriptRoot "backend"
$dataSync = Join-Path $PSScriptRoot "data-sync"

& (Join-Path $backend "stop-api.ps1")

Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object {
        $cmd = $_.CommandLine
        $null -ne $cmd -and $cmd -like "*data-sync*sync.py*"
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { $_.OwningProcess } |
    Select-Object -Unique |
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

Write-Host "Da dung API, data-sync worker, frontend (neu co)." -ForegroundColor Green

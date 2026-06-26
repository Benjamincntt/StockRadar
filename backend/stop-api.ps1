# Dừng StockRadar.Api đang chạy và chờ giải phóng DLL (tránh MSB3027)
$ErrorActionPreference = "SilentlyContinue"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiBin = Join-Path $root "StockRadar.Api\bin\Debug\net10.0"
$probeDll = Join-Path $apiBin "StockRadar.Application.dll"
$killed = [System.Collections.Generic.List[int]]::new()

function Stop-IfRunning([int]$processId) {
    if ($processId -le 0) { return }
    if ($killed.Contains($processId)) { return }
    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    [void]$killed.Add($processId)
}

# Process host trực tiếp (StockRadar.Api.exe)
Get-Process -Name "StockRadar.Api" -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-IfRunning $_.Id
}

# dotnet run / dotnet exec — KHÔNG kill dotnet build/restore/publish
Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object {
        $cmd = $_.CommandLine
        if ($null -eq $cmd) { return $false }
        if ($cmd -match '\b(build|msbuild|restore|publish|test)\b') { return $false }
        return ($cmd -like '*StockRadar.Api.dll*' -or ($cmd -like '*run*' -and $cmd -like '*StockRadar.Api*'))
    } |
    ForEach-Object { Stop-IfRunning $_.ProcessId }

# Process đang listen port 5280
Get-NetTCPConnection -LocalPort 5280 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { $_.OwningProcess } |
    Select-Object -Unique |
    ForEach-Object { Stop-IfRunning $_ }

if ($killed.Count -gt 0) {
    Write-Host "Da dung process: $($killed -join ', ')"
} else {
    Write-Host "Khong co API nao dang chay."
}

# Cho Windows giai phong handle (toi da ~5 giay)
if (Test-Path $probeDll) {
    for ($i = 0; $i -lt 20; $i++) {
        try {
            $stream = [System.IO.File]::Open(
                $probeDll,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
            $stream.Close()
            $stream.Dispose()
            break
        } catch {
            Start-Sleep -Milliseconds 250
        }
    }
}

exit 0

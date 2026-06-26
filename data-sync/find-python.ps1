# Tim Python that (bo qua Microsoft Store stub). Tra ve duong dan hoac lenh, hoac $null.
function Find-StockRadarPython {
    $dataSync = $PSScriptRoot
    $venvPython = Join-Path $dataSync ".venv\Scripts\python.exe"
    if (Test-Path $venvPython) { return $venvPython }

    function Test-PyExe([string]$exe) {
        if (-not (Test-Path $exe)) { return $false }
        $out = & $exe -c "import sys; print(sys.version_info[0])" 2>&1
        if ($LASTEXITCODE -ne 0) { return $false }
        $text = ($out | Out-String).Trim()
        if ($text -match 'Python was not found|Microsoft Store|App execution aliases') { return $false }
        return $text -eq '3'
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Python\Python313\python.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Python\Python312\python.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Python\Python311\python.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Python\Python310\python.exe")
    )
    foreach ($exe in $candidates) {
        if (Test-PyExe $exe) { return $exe }
    }

    $glob = Get-ChildItem (Join-Path $env:LOCALAPPDATA "Programs\Python") -Filter "python.exe" -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($glob -and (Test-PyExe $glob.FullName)) { return $glob.FullName }

    if (Get-Command py -ErrorAction SilentlyContinue) {
        $out = & py -3 -c "import sys; print(sys.executable)" 2>&1
        $text = ($out | Out-String).Trim()
        if ($LASTEXITCODE -eq 0 -and $text -and (Test-Path $text)) { return $text }
    }

    foreach ($name in @("python3", "python")) {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue
        if (-not $cmd) { continue }
        if ($cmd.Source -like "*WindowsApps*") { continue }
        if (Test-PyExe $cmd.Source) { return $cmd.Source }
    }

    return $null
}

if ($MyInvocation.InvocationName -ne '.') {
    $found = Find-StockRadarPython
    if ($found) { Write-Output $found } else { exit 1 }
}

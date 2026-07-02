# Shared helpers for StockRadar pipeline scripts (dot-source).

function Wait-StockRadarApi {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [int]$TimeoutSec = 180
    )

    $healthUri = "$($BaseUrl.TrimEnd('/'))/market"
    $deadline = (Get-Date).AddSeconds($TimeoutSec)

    while ((Get-Date) -lt $deadline) {
        try {
            $null = Invoke-RestMethod -Uri $healthUri -TimeoutSec 3
            return $true
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    return $false
}

function Ensure-StockRadarApi {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [int]$TimeoutSec = 180
    )

    $healthUri = "$($BaseUrl.TrimEnd('/'))/market"

    try {
        $null = Invoke-RestMethod -Uri $healthUri -TimeoutSec 3
        Write-Host "API OK: $healthUri" -ForegroundColor Green
        return
    } catch {
        Write-Host "API not reachable at $healthUri" -ForegroundColor Yellow
    }

    $devStart = Join-Path (Split-Path -Parent $PSScriptRoot) "backend\start-api.ps1"
    $publishedStart = Join-Path $env:LOCALAPPDATA "StockRadarApi\start.ps1"
    if (-not (Test-Path $publishedStart)) {
        $publishedStart = Join-Path $env:USERPROFILE "StockRadarApi\start.ps1"
    }

    if (Test-Path $devStart) {
        Write-Host "Starting API from $devStart (dotnet run)..." -ForegroundColor Cyan
        Start-Process -FilePath "powershell.exe" -ArgumentList @(
            "-NoExit", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $devStart
        ) | Out-Null
    } elseif (Test-Path $publishedStart) {
        Write-Host "Starting API from $publishedStart..." -ForegroundColor Cyan
        Start-Process -FilePath "powershell.exe" -ArgumentList @(
            "-NoExit", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $publishedStart
        ) | Out-Null
    } else {
        Write-Error "Cannot find backend\start-api.ps1 or StockRadarApi\start.ps1"
    }

    if (-not (Wait-StockRadarApi -BaseUrl $BaseUrl -TimeoutSec $TimeoutSec)) {
        Write-Error @"
API still not reachable after ${TimeoutSec}s.
1. Check the new API window for errors (SQL connection, port in use).
2. Open http://localhost:5280/swagger in browser.
3. Then run this script again.
"@
    }

    Write-Host "API is ready." -ForegroundColor Green
}

function Invoke-LongJobPost {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [hashtable]$Headers = @{},
        [int]$TimeoutSec = 3600,
        [int]$Retries = 3,
        [string]$BaseUrl = ""
    )

    if (-not ("System.Net.Http.HttpClient" -as [type])) {
        Add-Type -AssemblyName System.Net.Http
    }

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        $client = $null
        $handler = $null
        try {
            $handler = [System.Net.Http.HttpClientHandler]::new()
            $client = [System.Net.Http.HttpClient]::new($handler)
            $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSec)

            $request = [System.Net.Http.HttpRequestMessage]::new(
                [System.Net.Http.HttpMethod]::Post,
                $Uri)
            $request.Headers.ConnectionClose = $true

            foreach ($key in $Headers.Keys) {
                [void]$request.Headers.TryAddWithoutValidation($key, [string]$Headers[$key])
            }

            $response = $client.SendAsync($request).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

            if (-not $response.IsSuccessStatusCode) {
                throw "HTTP $([int]$response.StatusCode): $body"
            }

            if ([string]::IsNullOrWhiteSpace($body)) {
                return $null
            }

            return $body | ConvertFrom-Json
        } catch {
            $msg = $_.Exception.Message
            if ($attempt -ge $Retries) {
                throw @"
Job API that bai sau $Retries lan: $msg
Kiem tra cua so API (co the crash giua Job 2 sync).
Thu: cd backend; .\start-api.ps1 roi chay lai script
"@
            }

            Write-Host "Loi ket noi (lan $attempt/$Retries): $msg" -ForegroundColor Yellow
            if ($BaseUrl) {
                Write-Host "Cho API on dinh lai..." -ForegroundColor DarkGray
                Start-Sleep -Seconds 12
                Ensure-StockRadarApi -BaseUrl $BaseUrl -TimeoutSec 120
            } else {
                Start-Sleep -Seconds 12
            }
        } finally {
            if ($null -ne $client) { $client.Dispose() }
            if ($null -ne $handler) { $handler.Dispose() }
        }
    }
}

function Get-PipelineConfig {
    $cfgPath = Join-Path $PSScriptRoot "pipeline-config.json"
    if (-not (Test-Path $cfgPath)) {
        Write-Error "Thieu scripts/pipeline-config.json"
    }
    return Get-Content $cfgPath -Raw | ConvertFrom-Json
}

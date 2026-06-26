# Chay StockRadar API - http://localhost:5280
# Mac dinh: publish multi-file -> %LOCALAPPDATA%\StockRadarApi
# Dev:     .\start-api.ps1 -Dev  (dotnet build + bin\Debug)
param(
    [switch]$SkipBuild,
    [switch]$Dev
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProj = Join-Path $root "StockRadar.Api\StockRadar.Api.csproj"
$publishedDir = Join-Path $env:LOCALAPPDATA "StockRadarApi"
$env:ASPNETCORE_ENVIRONMENT = "Development"

function Unblock-Tree {
    param([string]$Dir)
    if (-not (Test-Path $Dir)) { return }
    Get-ChildItem -LiteralPath $Dir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.PSIsContainer) { return }
        Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue
        $zone = "$($_.FullName):Zone.Identifier"
        if (Test-Path -LiteralPath $zone) {
            Remove-Item -LiteralPath $zone -Force -ErrorAction SilentlyContinue
        }
    }
}

function Show-WacHelp {
    Write-Host ""
    Write-Host "Neu gap loi SqlClient SNI / Dll was not found:" -ForegroundColor Yellow
    Write-Host '  .\start-api.ps1 -Dev          (dotnet run tu source - khuyen nghi dev)' -ForegroundColor DarkGray
    Write-Host '  .\publish-api.ps1             (multi-file, SNI DLL canh exe)' -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Windows Application Control (Smart App Control) co the chan file local." -ForegroundColor Yellow
    Write-Host '  Settings -> Privacy and security -> Smart App Control -> Off' -ForegroundColor DarkGray
    Write-Host '  hoac Settings -> System -> For developers -> Developer Mode' -ForegroundColor DarkGray
    Write-Host ""
}

function Start-DevApi {
    $binDir = Join-Path $root "StockRadar.Api\bin\Debug\net10.0"
    $dll = Join-Path $binDir "StockRadar.Api.dll"

    if (-not $SkipBuild) {
        Write-Host "==> Build API (Debug)..." -ForegroundColor Cyan
        dotnet build $apiProj
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Host "Bo qua build (-SkipBuild)" -ForegroundColor DarkGray
    }

    if (-not (Test-Path $dll)) {
        Write-Error "Khong tim thay $dll sau khi build."
    }

    Unblock-Tree $binDir

    Write-Host ""
    Write-Host "==> Khoi dong API (Debug bin)" -ForegroundColor Green
    Write-Host "    URL:     http://localhost:5280" -ForegroundColor DarkGray
    Write-Host "    Swagger: http://localhost:5280/swagger" -ForegroundColor DarkGray
    Write-Host ""

    Set-Location $binDir
    dotnet $dll
    if ($LASTEXITCODE -ne 0) {
        Show-WacHelp
        exit $LASTEXITCODE
    }
}

function Start-PublishedApi {
    $exe = Join-Path $publishedDir "StockRadar.Api.exe"
    $dll = Join-Path $publishedDir "StockRadar.Api.dll"

    if (-not $SkipBuild -or (-not (Test-Path $exe) -and -not (Test-Path $dll))) {
        Write-Host "    (multi-file publish - SqlClient SNI.dll canh exe)" -ForegroundColor DarkGray
        & (Join-Path $root "publish-api.ps1") | Out-Host
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Host "Bo qua publish (-SkipBuild)" -ForegroundColor DarkGray
    }

    Unblock-Tree $publishedDir

    if (-not (Test-Path $exe) -and -not (Test-Path $dll)) {
        Write-Error "Khong tim thay API da publish trong $publishedDir"
    }

    Write-Host ""
    Write-Host "==> Khoi dong API (published)" -ForegroundColor Green
    Write-Host "    URL:     http://localhost:5280" -ForegroundColor DarkGray
    Write-Host "    Swagger: http://localhost:5280/swagger" -ForegroundColor DarkGray
    Write-Host "    Ctrl+C de dung API." -ForegroundColor DarkGray
    Write-Host ""

    Set-Location $publishedDir
    if (Test-Path $exe) {
        & $exe
    } else {
        dotnet $dll
    }
    if ($LASTEXITCODE -ne 0) {
        Show-WacHelp
        exit $LASTEXITCODE
    }
}

if ($Dev) {
    Start-DevApi
} else {
    Start-PublishedApi
}

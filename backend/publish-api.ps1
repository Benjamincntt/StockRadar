# Publish API ra %LOCALAPPDATA%\StockRadarApi
param(
    [switch]$DisableBuiltinKbs,
    [switch]$FrameworkDependent,
    [switch]$SingleFile
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProj = Join-Path $root "StockRadar.Api\StockRadar.Api.csproj"
$outDir = Join-Path $env:LOCALAPPDATA "StockRadarApi"

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

Write-Host "==> Publish API -> $outDir" -ForegroundColor Cyan
if ($FrameworkDependent) {
    Write-Host "    Framework-dependent (can .NET 10 runtime)" -ForegroundColor DarkGray
    dotnet publish $apiProj -c Release -o $outDir
} elseif ($SingleFile) {
    Write-Host "    Single-file self-contained (win-x64)" -ForegroundColor DarkGray
    dotnet publish $apiProj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -o $outDir
} else {
    Write-Host "    Multi-file self-contained (win-x64) - SqlClient SNI DLL canh exe" -ForegroundColor DarkGray
    dotnet publish $apiProj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -o $outDir
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$devSettings = Join-Path $root "StockRadar.Api\appsettings.Development.json"
if (Test-Path $devSettings) {
    Copy-Item $devSettings (Join-Path $outDir "appsettings.Development.json") -Force
}

$startScript = Join-Path $root "templates\StockRadarApi-start.ps1"
if (Test-Path $startScript) {
    Copy-Item $startScript (Join-Path $outDir "start.ps1") -Force
}

Unblock-Tree $outDir

if ($DisableBuiltinKbs) {
    $appSettings = Join-Path $outDir "appsettings.json"
    if (Test-Path $appSettings) {
        $json = Get-Content $appSettings -Raw | ConvertFrom-Json
        if (-not $json.MarketData) {
            $json | Add-Member -NotePropertyName MarketData -NotePropertyValue ([pscustomobject]@{})
        }
        $json.MarketData | Add-Member -NotePropertyName AutoSyncEnabled -NotePropertyValue $false -Force
        $json | ConvertTo-Json -Depth 10 | Set-Content $appSettings -Encoding UTF8
        Write-Host "    AutoSyncEnabled=false (chi data-sync Python)" -ForegroundColor DarkGray
    }
}

Write-Output $outDir

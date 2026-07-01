# Build APK release — chạy trên Windows sau khi cài Flutter + Android SDK
param(
    [string]$OutDir = "D:\JUICE-build",
    [string]$ApiBase = "",
    [switch]$Local
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

# Pub cache + Gradle cache cung o D: — tranh loi khi project o D:
if (-not $env:PUB_CACHE) {
    $env:PUB_CACHE = "D:\pub-cache"
    [Environment]::SetEnvironmentVariable("PUB_CACHE", $env:PUB_CACHE, "User")
}
$env:GRADLE_USER_HOME = "D:\gradle-home"
$env:ANDROID_HOME = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { "D:\Android\Sdk" }

if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) {
    $fallback = "D:\flutter\bin\flutter.bat"
    if (Test-Path $fallback) { $flutter = $fallback }
    else {
        Write-Host "Chua tim thay flutter trong PATH." -ForegroundColor Red
        Write-Host "1. Cai Flutter: https://docs.flutter.dev/get-started/install/windows"
        Write-Host "2. Cai Android Studio (Android SDK)"
        Write-Host "3. Chay: flutter doctor --android-licenses"
        exit 1
    }
} else {
    $flutter = "flutter"
}

if (-not $env:JAVA_HOME) {
    $jdkCandidates = @(
        "C:\Program Files\Microsoft\jdk-17.0.19.10-hotspot",
        "C:\Program Files\Android\Android Studio\jbr",
        "D:\Android\Android Studio\jbr"
    )
    foreach ($jdk in $jdkCandidates) {
        if (Test-Path "$jdk\bin\java.exe") {
            $env:JAVA_HOME = $jdk
            $env:Path = "$jdk\bin;" + $env:Path
            break
        }
    }
}

if ($Local -and -not $ApiBase) {
    $lanIp = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -match '^(192\.168\.|10\.)' -and $_.PrefixOrigin -ne 'WellKnown' } |
        Select-Object -First 1 -ExpandProperty IPAddress
    if (-not $lanIp) {
        $lanIp = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -match '^(192\.168\.|10\.)' } |
            Select-Object -First 1 -ExpandProperty IPAddress
    }
    if ($lanIp) {
        $ApiBase = "http://${lanIp}:5280/api/v1"
        Write-Host "Local API: $ApiBase (dien thoai + PC cung WiFi, API listen 0.0.0.0:5280)" -ForegroundColor Yellow
    } else {
        Write-Host "Khong tim thay IP LAN - truyen -ApiBase thu cong." -ForegroundColor Red
        exit 1
    }
}

Write-Host "==> flutter pub get" -ForegroundColor Cyan
& $flutter pub get

$buildArgs = @("build", "apk", "--release")
if ($ApiBase) {
    $buildArgs += "--dart-define=API_BASE=$ApiBase"
}
$syncKey = if ($env:SYNC_API_KEY) { $env:SYNC_API_KEY } else { "dev-sync-key-change-me" }
$buildArgs += "--dart-define=SYNC_API_KEY=$syncKey"

Write-Host "==> flutter build apk --release" -ForegroundColor Cyan
& $flutter @buildArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$apk = Join-Path $root "build\app\outputs\flutter-apk\app-release.apk"
if (-not (Test-Path $apk)) {
    Write-Host "Khong tim thay APK tai $apk" -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$dest = Join-Path $OutDir "juice-app.apk"
Copy-Item $apk $dest -Force

Write-Host ""
Write-Host "Xong!" -ForegroundColor Green
Write-Host "APK: $dest"
Write-Host ""
Write-Host "Cai len dien thoai:" -ForegroundColor Yellow
Write-Host "  - Copy file APK sang may, mo va cai (bat 'Nguon khong xac dinh')"
Write-Host "  - Hoac USB: adb install '$dest'"

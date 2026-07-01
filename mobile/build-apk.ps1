# Build APK release — chạy trên Windows sau khi cài Flutter + Android SDK
param(
    [string]$OutDir = "D:\JUICE-build",
    [string]$ApiBase = ""
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
    Write-Host "Chua tim thay flutter trong PATH." -ForegroundColor Red
    Write-Host "1. Cai Flutter: https://docs.flutter.dev/get-started/install/windows"
    Write-Host "2. Cai Android Studio (Android SDK)"
    Write-Host "3. Chay: flutter doctor --android-licenses"
    exit 1
}

Write-Host "==> flutter pub get" -ForegroundColor Cyan
flutter pub get

$buildArgs = @("build", "apk", "--release")
if ($ApiBase) {
    $buildArgs += "--dart-define=API_BASE=$ApiBase"
}

Write-Host "==> flutter build apk --release" -ForegroundColor Cyan
& flutter @buildArgs
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
Write-Host "  - Hoac USB: adb install `"$dest`""

# Cài Android SDK command-line vào D:\Android\Sdk (không cần Android Studio)
$ErrorActionPreference = "Stop"

$SdkRoot = "D:\Android\Sdk"
$CmdToolsZip = "$env:TEMP\android-cmdline-tools.zip"
$CmdToolsUrl = "https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip"

Write-Host "==> Kiem tra Java JDK 17" -ForegroundColor Cyan
if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    Write-Host "Cai OpenJDK 17 qua winget..."
    winget install --id Microsoft.OpenJDK.17 -e --accept-package-agreements --accept-source-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
}

if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    throw "Chua co java. Khoi dong lai PowerShell roi chay lai script."
}

$env:ANDROID_HOME = $SdkRoot
$env:ANDROID_SDK_ROOT = $SdkRoot

if (-not (Test-Path "$SdkRoot\cmdline-tools\latest\bin\sdkmanager.bat")) {
    Write-Host "==> Tai Android command-line tools" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path "$SdkRoot\cmdline-tools" | Out-Null
    Invoke-WebRequest -Uri $CmdToolsUrl -OutFile $CmdToolsZip -UseBasicParsing
    $tmp = Join-Path $env:TEMP "android-cmdtools-extract"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -Path $CmdToolsZip -DestinationPath $tmp -Force
    $latest = "$SdkRoot\cmdline-tools\latest"
    if (Test-Path $latest) { Remove-Item $latest -Recurse -Force }
    Move-Item "$tmp\cmdline-tools" $latest
}

$sdkmanager = "$SdkRoot\cmdline-tools\latest\bin\sdkmanager.bat"

if (-not (Test-Path "$SdkRoot\platform-tools\adb.exe")) {
    Write-Host "==> Cai SDK packages (co the mat vai phut)" -ForegroundColor Cyan
    & $sdkmanager --sdk_root=$SdkRoot "platform-tools" "platforms;android-35" "platforms;android-36" "build-tools;35.0.0" "build-tools;36.0.0"
}

Write-Host "==> Chap nhan licenses (flutter)" -ForegroundColor Cyan
flutter config --android-sdk $SdkRoot
1..30 | ForEach-Object { "y" } | flutter doctor --android-licenses

[Environment]::SetEnvironmentVariable("ANDROID_HOME", $SdkRoot, "User")
[Environment]::SetEnvironmentVariable("ANDROID_SDK_ROOT", $SdkRoot, "User")
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$extra = "$SdkRoot\platform-tools;$SdkRoot\cmdline-tools\latest\bin"
if ($userPath -notlike "*$SdkRoot\platform-tools*") {
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$extra", "User")
}

Write-Host ""
flutter doctor
Write-Host ""
Write-Host "Xong! Build APK:" -ForegroundColor Green
Write-Host "  cd D:\Source\StockRadar\mobile"
Write-Host "  .\build-apk.ps1"

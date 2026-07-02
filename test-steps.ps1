# StockRadar - test tung buoc
# Dung: .\test-steps.ps1
#       .\test-steps.ps1 -Step 1
#       .\test-steps.ps1 -Step 2 -StartApi
#       .\test-steps.ps1 -From 1 -To 4 -StartApi

param(
    [int]$Step = 0,
    [int]$From = 0,
    [int]$To = 0,
    [switch]$StartApi,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$backend = Join-Path $root "backend"
$scripts = Join-Path $root "scripts"
. (Join-Path $scripts "api-helper.ps1")

$cfg = Get-Content (Join-Path $scripts "pipeline-config.json") -Raw | ConvertFrom-Json
$base = $cfg.api_base_url.TrimEnd("/")
$key = $cfg.sync_api_key
$headers = @{ "X-Sync-Key" = $key }

function Write-Banner([string]$Title) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-Pass([string]$Msg) { Write-Host "[PASS] $Msg" -ForegroundColor Green }
function Write-Fail([string]$Msg) { Write-Host "[FAIL] $Msg" -ForegroundColor Red }
function Write-Warn([string]$Msg) { Write-Host "[WARN] $Msg" -ForegroundColor Yellow }
function Write-Info([string]$Msg) { Write-Host "       $Msg" -ForegroundColor DarkGray }

function Test-ApiUp {
    try {
        $null = Invoke-WebRequest -Uri "$base/market" -TimeoutSec 5 -UseBasicParsing
        return $true
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in 200, 503) { return $true }
        return $false
    }
}

function Ensure-Api {
    if (Test-ApiUp) {
        Write-Pass "API dang chay: $base"
        return
    }
    if ($StartApi) {
        Write-Warn "API chua chay - dang khoi dong..."
        $devStart = Join-Path $backend "start-api.ps1"
        Start-Process powershell -ArgumentList "-NoExit", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $devStart | Out-Null
        if (-not (Wait-StockRadarApi -BaseUrl $base -TimeoutSec 180)) {
            throw "API khong san sang sau 180s. Mo cua so API xem loi SQL/port."
        }
        Write-Pass "API da khoi dong"
        return
    }
    throw "API chua chay. Chay: cd backend; .\start-api.ps1  HOAC  .\test-steps.ps1 -Step N -StartApi"
}

function Step-0-Help {
    Write-Banner "HUONG DAN TEST TUNG BUOC"
    @"
  Buoc 1  Build backend + frontend
  Buoc 2  API + DB (migrate, Quartz log)
  Buoc 3  Kiem tra du lieu DB (so ma, VNINDEX)
  Buoc 4  Smoke API (sectors, opportunities)
  Buoc 5  Job 1 backfill lich su (LAU - chi khi DB trong/thieu)
  Buoc 6  Job 2 sync phien hom nay (3-5 phut)
  Buoc 7  Phan tich SmartMoney + cham diem tieu chi
  Buoc 8  Criteria summary + chi tiet 1 ma top
  Buoc 9  Frontend UI

  Vi du:
    .\test-steps.ps1 -Step 1
    .\test-steps.ps1 -Step 2 -StartApi
    .\test-steps.ps1 -From 1 -To 4 -StartApi
    .\test-steps.ps1 -Step 6 -StartApi

  Pipeline nhanh (da co Job 1):
    cd scripts
    .\run-daily-jobs.ps1

  Full stack:
    .\run.ps1 -SkipPublish -SkipPipeline
    .\run.ps1 -SkipPublish
"@ | Write-Host
}

function Step-1-Build {
    Write-Banner "BUOC 1 - Build"
    if (-not $SkipBuild) {
        Write-Info "dotnet build API..."
        dotnet build (Join-Path $backend "StockRadar.Api\StockRadar.Api.csproj")
        if ($LASTEXITCODE -ne 0) { throw "Build API that bai" }
        Write-Pass "Backend build OK"

        Push-Location (Join-Path $root "frontend")
        Write-Info "npm run build..."
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        npm run build 2>&1 | ForEach-Object {
            $line = if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { "$_" }
            if ($line -match "INVALID_ANNOTATION|rolldown\.rs/in-depth") { return }
            if ($_ -is [System.Management.Automation.ErrorRecord]) {
                Write-Host $line -ForegroundColor DarkYellow
            } else {
                Write-Host $line
            }
        }
        $frontendOk = $LASTEXITCODE -eq 0
        $ErrorActionPreference = $prevEap
        Pop-Location
        if (-not $frontendOk) { throw "Frontend build that bai" }
        Write-Pass "Frontend build OK"
    } else {
        Write-Warn "Bo qua build (-SkipBuild)"
    }
}

function Step-2-ApiDb {
    Write-Banner "BUOC 2 - API + Database"
    Ensure-Api
    Write-Info "Kiem tra log API: Database ready, Quartz job ... next"
    Write-Info "Swagger: http://localhost:5280/swagger"
    try {
        $r = Invoke-WebRequest -Uri "$base/market" -TimeoutSec 10 -UseBasicParsing
        Write-Pass "GET /market -> HTTP $($r.StatusCode)"
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        if ($code -eq 503) {
            Write-Warn "GET /market -> 503 (chua co VNINDEX - binh thuong neu chua Job 2)"
        } else {
            Write-Fail "GET /market -> $code"
        }
    }
}

function Step-3-DbData {
    Write-Banner "BUOC 3 - Du lieu trong DB"
    Ensure-Api
    $quotes = Invoke-RestMethod -Uri "$base/market/quotes" -TimeoutSec 60
    $count = @($quotes).Count
    if ($count -eq 0) {
        Write-Fail "DB trong - chua co ma. Chay Buoc 5 (Job 1 backfill)."
        Write-Info "POST $base/market/jobs/history  (header X-Sync-Key)"
        Write-Info "Hoac: cd scripts; .\run-backfill.ps1"
    } else {
        Write-Pass "Co $count ma trong DB"
        $sample = @($quotes | Select-Object -First 8 | ForEach-Object { $_.symbol })
        Write-Info ("Mau: " + ($sample -join ", "))
    }

    $status = Invoke-RestMethod -Uri "$base/market/jobs/history/status" -TimeoutSec 15
    if ($status.isRunning) {
        Write-Warn "Job 1 dang chay: $($status.processed)/$($status.total)"
    } else {
        Write-Info "Job 1 khong chay (OK neu da backfill xong)"
    }
}

function Step-4-SmokeApi {
    Write-Banner "BUOC 4 - Smoke API"
    Ensure-Api

    try {
        $sectors = Invoke-RestMethod -Uri "$base/sectors?page=1&pageSize=5" -TimeoutSec 60
        Write-Pass "GET /sectors -> $($sectors.items.Count) nganh (trang 1)"
    } catch {
        Write-Fail "GET /sectors - $($_.Exception.Message)"
    }

    $opps = Invoke-RestMethod -Uri "$base/opportunities?page=1&pageSize=5" -TimeoutSec 60
    if ($opps.hasFreshData) {
        Write-Pass "GET /opportunities -> $($opps.items.Count) ma (hasFreshData=true)"
    } else {
        Write-Warn "Chua co danh sach co hoi: $($opps.statusMessage)"
        Write-Info "Chay Buoc 6+7 hoac doi Quartz 15:02 VN"
    }
}

function Step-5-Job1 {
    Write-Banner "BUOC 5 - Job 1 Backfill (LAU)"
    Ensure-Api
    $status = Invoke-RestMethod -Uri "$base/market/jobs/history/status" -TimeoutSec 15
    if ($status.isRunning) {
        Write-Warn "Job 1 da chay - theo doi: cd scripts; .\watch-job1-status.ps1"
        return
    }
    Write-Warn "Bat dau backfill fast mode..."
    $result = Invoke-LongJobPost -Uri "$base/market/jobs/history" -Headers $headers -TimeoutSec 7200 -BaseUrl $base
    $result | ConvertTo-Json -Depth 4
    Write-Pass "Job 1 xong"
}

function Step-6-Job2 {
    Write-Banner "BUOC 6 - Job 2 Sync phien"
    Ensure-Api
    Write-Warn "Dang sync phiên hom nay - thuong 3-10 phut, cho den khi co ket qua..."
    $result = Invoke-LongJobPost -Uri "$base/market/jobs/session" -Headers $headers -TimeoutSec 3600 -BaseUrl $base
    $result | ConvertTo-Json -Depth 4
    Write-Pass "Job 2 xong - synced: $($result.symbolsSynced) ma"
}

function Step-7-Analysis {
    Write-Banner "BUOC 7 - Phan tich SmartMoney"
    Ensure-Api
    Write-Warn "Dang quet ~300-500 ma + cham diem tieu chi - co the 5-15 phut, KHONG Ctrl+C..."
    $result = Invoke-LongJobPost -Uri "$base/market/jobs/analysis" -Headers $headers -TimeoutSec 1200 -BaseUrl $base
    $result | ConvertTo-Json -Depth 4
    Write-Pass "Phan tich xong - $($result.opportunitiesSaved) co hoi / $($result.stocksScored) ma quet"
}

function Step-8-Criteria {
    Write-Banner "BUOC 8 - Criteria + chi tiet CP"
    Ensure-Api

    $criteria = Invoke-RestMethod -Uri "$base/criteria/summary" -TimeoutSec 60
    Write-Pass "GET /criteria/summary -> $($criteria.groups.Count) nhom tieu chi"

    $opps = Invoke-RestMethod -Uri "$base/opportunities?page=1&pageSize=1" -TimeoutSec 60
    if (@($opps.items).Count -gt 0) {
        $sym = $opps.items[0].symbol
        $detail = Invoke-RestMethod -Uri "$base/stocks/$sym" -TimeoutSec 60
        Write-Pass "GET /stocks/$sym -> score=$($detail.score)"
    } else {
        Write-Warn "Khong co ma top de test chi tiet - chay Buoc 7 truoc"
    }
}

function Step-9-Frontend {
    Write-Banner "BUOC 9 - Frontend"
    Write-Info "Terminal moi: cd frontend; .\run-dev.ps1"
    Write-Info "Mo: http://localhost:5173"
    try {
        $null = Invoke-WebRequest -Uri "http://localhost:5173" -TimeoutSec 3 -UseBasicParsing
        Write-Pass "Frontend dang chay tai http://localhost:5173"
    } catch {
        Write-Warn "Frontend chua chay - cd frontend; .\run-dev.ps1"
    }
}

$steps = @{
    0 = { Step-0-Help }
    1 = { Step-1-Build }
    2 = { Step-2-ApiDb }
    3 = { Step-3-DbData }
    4 = { Step-4-SmokeApi }
    5 = { Step-5-Job1 }
    6 = { Step-6-Job2 }
    7 = { Step-7-Analysis }
    8 = { Step-8-Criteria }
    9 = { Step-9-Frontend }
}

if ($Step -gt 0) {
    & $steps[$Step]
} elseif ($From -gt 0 -and $To -ge $From) {
    foreach ($i in $From..$To) {
        & $steps[$i]
    }
} else {
    Step-0-Help
}

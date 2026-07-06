# Commit (tuy chon) -> push -> deploy production -> chay pipeline job (tru Job 1)
#
# Vi du:
#   .\scripts\ship-all.ps1 -Message "Noi long Base Price Engine cho VN market"
#   .\scripts\ship-all.ps1 -SkipCommit -Message ""          # chi push neu da commit
#   .\scripts\ship-all.ps1 -LocalOnly -SkipDeploy           # local API + job, khong deploy
#   .\scripts\ship-all.ps1 -SkipJobs                        # chi commit + deploy
#
param(
    [string]$Message = "",
    [switch]$SkipCommit,
    [switch]$SkipDeploy,
    [switch]$SkipJobs,
    [switch]$LocalOnly,
    [string]$Branch = "master",
    [string]$Server = "root@103.226.248.6",
    [string]$SshKey = "D:\ssh\id_rsa",
    [string]$Domain = "stock.baobiantea.com",
    [ValidateSet("all", "fe", "be")]
    [string]$DeployAction = "all",
    [int]$CriteriaDays = 30,
    [int]$MonitorRounds = 2,
    [int]$MonitorWaitSec = 8
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$scripts = Join-Path $root "scripts"
$sshArgs = @("-i", $SshKey, "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=accept-new")

Set-Location $root

. (Join-Path $scripts "api-helper.ps1")

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host "==> $Text" -ForegroundColor Cyan
}

Write-Host "========================================" -ForegroundColor Green
Write-Host " StockRadar ship-all" -ForegroundColor Green
Write-Host " commit -> push -> deploy -> pipeline jobs" -ForegroundColor Green
Write-Host " (KHONG Job 1 history backfill)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Green

# --- Git commit ---
if (-not $SkipCommit) {
    $porcelain = git status --porcelain
    if ($porcelain) {
        if ([string]::IsNullOrWhiteSpace($Message)) {
            Write-Error "Co thay doi chua commit. Truyen -Message 'mo ta' hoac -SkipCommit."
        }
        Write-Step "Git commit"
        @("backend", "frontend", "scripts", "docs", "mobile/lib", "CLAUDE.md", ".cursor") | ForEach-Object {
            if (Test-Path $_) { git add $_ }
        }
        git commit -m $Message
        Write-Host "Committed." -ForegroundColor Green
    } else {
        Write-Host 'Khong co thay doi - bo qua commit.' -ForegroundColor DarkGray
    }
} else {
    Write-Host 'Bo qua commit (-SkipCommit)' -ForegroundColor DarkGray
}

# --- Git push ---
if (-not $LocalOnly) {
    Write-Step "Git push origin $Branch"
    git push -u origin $Branch
}

# --- Deploy production ---
if (-not $SkipDeploy -and -not $LocalOnly) {
    Write-Step "Deploy ($DeployAction) tren $Server"
    & (Join-Path $scripts "deploy-remote.ps1") `
        -Server $Server `
        -SshKey $SshKey `
        -Domain $Domain `
        -Action $DeployAction `
        -SkipGitPush
} elseif ($LocalOnly) {
    Write-Host 'Bo qua deploy (-LocalOnly)' -ForegroundColor DarkGray
} else {
    Write-Host 'Bo qua deploy (-SkipDeploy)' -ForegroundColor DarkGray
}

# --- Pipeline jobs ---
if (-not $SkipJobs) {
    if ($LocalOnly) {
        Write-Step "Khoi dong / kiem tra API local"
        $cfg = Get-PipelineConfig
        $base = $cfg.api_base_url.TrimEnd("/")
        $key = $cfg.sync_api_key
        $headers = @{ "X-Sync-Key" = $key }

        & (Join-Path $root "backend\restart-api.ps1") | Out-Host
        Ensure-StockRadarApi -BaseUrl $base -TimeoutSec 120

        $jobs = @(
            @{ Label = "Job 2 - session sync"; Uri = "$base/market/jobs/session" }
            @{ Label = "Phan tich SmartMoney"; Uri = "$base/market/jobs/analysis" }
            @{ Label = "Criteria backfill ${CriteriaDays}d"; Uri = "$base/market/jobs/criteria-backfill?days=$CriteriaDays" }
        )

        foreach ($j in $jobs) {
            Write-Step $j.Label
            Invoke-LongJobPost -Uri $j.Uri -Headers $headers -TimeoutSec 3600 -BaseUrl $base | ConvertTo-Json -Depth 6
        }

        for ($i = 1; $i -le $MonitorRounds; $i++) {
            if ($i -gt 1 -and $MonitorWaitSec -gt 0) {
                Write-Host "Cho ${MonitorWaitSec}s..." -ForegroundColor DarkGray
                Start-Sleep -Seconds $MonitorWaitSec
            }
            Write-Step "Job 3 - opportunity monitor ($i/$MonitorRounds)"
            Invoke-LongJobPost -Uri "$base/market/jobs/opportunity-monitor" -Headers $headers -TimeoutSec 300 -BaseUrl $base |
                ConvertTo-Json -Depth 6
        }
    } else {
        Write-Step "Pipeline jobs tren server (session -> analysis -> criteria -> monitor)"
        $remoteOneLine = "cd /var/www/StockRadar && sed -i 's/\r$//' scripts/run-pipeline-jobs.sh && chmod +x scripts/run-pipeline-jobs.sh && CRITERIA_DAYS=$CriteriaDays MONITOR_ROUNDS=$MonitorRounds MONITOR_WAIT_SEC=$MonitorWaitSec bash scripts/run-pipeline-jobs.sh"
        ssh @sshArgs $Server $remoteOneLine
    }
} else {
    Write-Host 'Bo qua pipeline jobs (-SkipJobs)' -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " SHIP-ALL XONG" -ForegroundColor Green
if ($LocalOnly) {
    Write-Host " Local API: http://localhost:5280" -ForegroundColor White
} else {
    Write-Host " Site: https://$Domain/" -ForegroundColor White
}
Write-Host "========================================" -ForegroundColor Green

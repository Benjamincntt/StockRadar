# Commit (tuy chon) -> push -> deploy production (backend only)
#
# Khong build frontend: dung -DeployAction all/fe khi thuc su can deploy web.
# Khong tu trigger lai Job 2 / criteria backfill / opportunity monitor VIP - ca 3
# da co lich Quartz tu dong rieng (Job2 ~5 phut/lan, monitor ~60s trong phien),
# cho ket qua moi trong vai phut ma khong can chay lai ngay sau deploy.
#
# Vi du:
#   .\scripts\ship-all.ps1 -Message "Noi long Base Price Engine cho VN market"
#   .\scripts\ship-all.ps1 -SkipCommit -Message ""          # chi push neu da commit
#   .\scripts\ship-all.ps1 -DeployAction all                # can deploy ca frontend
#
param(
    [string]$Message = "",
    [switch]$SkipCommit,
    [switch]$SkipDeploy,
    [string]$Branch = "master",
    [string]$Server = "root@103.226.248.6",
    [string]$SshKey = "D:\ssh\id_rsa",
    [string]$Domain = "stock.baobiantea.com",
    [ValidateSet("all", "fe", "be")]
    [string]$DeployAction = "be"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$scripts = Join-Path $root "scripts"

Set-Location $root

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host "==> $Text" -ForegroundColor Cyan
}

Write-Host "========================================" -ForegroundColor Green
Write-Host " StockRadar ship-all" -ForegroundColor Green
Write-Host " commit -> push -> deploy" -ForegroundColor Green
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
Write-Step "Git push origin $Branch"
git push -u origin $Branch

# --- Deploy production ---
if (-not $SkipDeploy) {
    Write-Step "Deploy ($DeployAction) tren $Server"
    & (Join-Path $scripts "deploy-remote.ps1") `
        -Server $Server `
        -SshKey $SshKey `
        -Domain $Domain `
        -Action $DeployAction `
        -SkipGitPush
} else {
    Write-Host 'Bo qua deploy (-SkipDeploy)' -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " SHIP-ALL XONG" -ForegroundColor Green
Write-Host " Site: https://$Domain/" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Green

# One-time: GitHub Actions secrets cho auto deploy.
# Chay: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-github-deploy-secrets.ps1
param(
    [string]$SshKey = "D:\ssh\id_rsa",
    [string]$Server = "root@103.226.248.6",
    [string]$DeployKeyOnServer = "/root/.ssh/gh_actions_deploy",
    [string]$Host_ = "103.226.248.6",
    [string]$User = "root"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
            [System.Environment]::GetEnvironmentVariable("Path", "User")

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "Chua co gh CLI. Cai: winget install GitHub.cli" -ForegroundColor Red
    exit 1
}

gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Can dang nhap GitHub (mo trinh duyet, nhap ma device)..." -ForegroundColor Yellow
    gh auth login --hostname github.com --git-protocol https --web --scopes "repo,workflow"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "==> Kiem tra SSH key deploy tren server" -ForegroundColor Cyan
$sshArgs = @("-i", $SshKey, "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=accept-new")
ssh @sshArgs $Server "test -f $DeployKeyOnServer || (ssh-keygen -t ed25519 -f $DeployKeyOnServer -N '' -C github-actions-stockradar && grep -qF \"`$(cat ${DeployKeyOnServer}.pub)\" ~/.ssh/authorized_keys || cat ${DeployKeyOnServer}.pub >> ~/.ssh/authorized_keys && chmod 600 $DeployKeyOnServer ~/.ssh/authorized_keys)"

Write-Host "==> Dat secrets (Benjamincntt/StockRadar)" -ForegroundColor Cyan
gh secret set SSH_HOST -b $Host_
gh secret set SSH_USER -b $User
ssh @sshArgs $Server "cat $DeployKeyOnServer" | gh secret set SSH_PRIVATE_KEY

Write-Host ""
Write-Host "Xong! GitHub Actions da san sang auto deploy." -ForegroundColor Green
Write-Host "Thu: gh workflow run deploy.yml -f action=all" -ForegroundColor Yellow

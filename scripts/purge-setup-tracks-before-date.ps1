# Xoa SetupTracks (Mua diem 1/2) truoc ngay chi dinh tren server production.
# Vi du:
#   .\scripts\purge-setup-tracks-before-date.ps1 -BeforeDate 2026-07-10 -WhatIf
#   .\scripts\purge-setup-tracks-before-date.ps1 -BeforeDate 2026-07-10

param(
    [Parameter(Mandatory = $true)][DateTime]$BeforeDate,
    [string]$Server = "root@103.226.248.6",
    [string]$SshKey = "D:\ssh\id_rsa",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$dateSql = $BeforeDate.ToString("yyyy-MM-dd")
$root = Split-Path $PSScriptRoot -Parent
$scriptPath = Join-Path $PSScriptRoot "purge-setup-tracks-before-date.sh"
$sshArgs = @("-i", $SshKey, "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=accept-new")

Write-Host "==> SetupTracks MuaDiem1/MuaDiem2 truoc $dateSql tren $Server" -ForegroundColor Cyan

scp @sshArgs $scriptPath "${Server}:/tmp/purge-setup-tracks-before-date.sh" | Out-Null

$mode = if ($WhatIf) { "count" } else { "--delete" }
ssh @sshArgs $Server "sed -i 's/\r$//' /tmp/purge-setup-tracks-before-date.sh && bash /tmp/purge-setup-tracks-before-date.sh $dateSql $mode"

if ($WhatIf) {
    Write-Host "WhatIf - khong xoa." -ForegroundColor Yellow
}

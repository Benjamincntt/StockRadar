# Export local DB + upload + restore tren server Gdata
#   powershell -ExecutionPolicy Bypass -File scripts\upload-db-to-server.ps1
param(
    [string]$Server = "root@103.226.248.6",
    [string]$SshKey = "D:\ssh\id_rsa",
    [string]$RemoteBak = "/var/opt/mssql/backup/StockRadarDb.bak"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\export-db.ps1"

$backupPath = Get-DbBackupPath
$sizeMb = [math]::Round((Get-Item $backupPath).Length / 1MB, 1)

Write-Host "`n=== Upload $sizeMb MB -> $Server ===" -ForegroundColor Cyan
ssh -i $SshKey -o StrictHostKeyChecking=accept-new $Server "mkdir -p /var/opt/mssql/backup && chown mssql:mssql /var/opt/mssql/backup 2>/dev/null || true"
scp -i $SshKey $backupPath "${Server}:${RemoteBak}"

Write-Host "`n=== Restore tren server (can mat khau sa) ===" -ForegroundColor Cyan
$remoteScript = (Get-Content "$PSScriptRoot\restore-db-server.sh" -Raw) -replace "`r`n", "`n"
$remoteScript | ssh -i $SshKey $Server "cat > /tmp/restore-stockradar-db.sh && chmod +x /tmp/restore-stockradar-db.sh && bash /tmp/restore-stockradar-db.sh"

Write-Host "`nXong. Kiem tra: http://103.226.248.6/" -ForegroundColor Green

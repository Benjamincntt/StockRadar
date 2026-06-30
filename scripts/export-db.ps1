# Export StockRadarDb -> scripts/db/StockRadarDb.bak
#   powershell -ExecutionPolicy Bypass -File scripts\export-db.ps1

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\db-config.ps1"

Write-Host "=== Export StockRadarDb ===" -ForegroundColor Cyan

$db = Get-DbSettings
$backupPath = Get-DbBackupPath
$backupSqlPath = $backupPath.Replace("'", "''")

Write-Host "Server:   $($db.Server)"
Write-Host "Database: $($db.Database)"
Write-Host "Backup:   $backupPath"

$query = @"
BACKUP DATABASE [$($db.Database)]
TO DISK = N'$backupSqlPath'
WITH FORMAT, INIT, COMPRESSION, STATS = 10;
"@

Invoke-DbSql -Query $query -DbSettings $db

$sizeMb = [math]::Round((Get-Item $backupPath).Length / 1MB, 1)
Write-Host "`nExport thanh cong ($sizeMb MB)." -ForegroundColor Green
Write-Host "Tiep theo: powershell -File scripts\upload-db-to-server.ps1" -ForegroundColor Yellow

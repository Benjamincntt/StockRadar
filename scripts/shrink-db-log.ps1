# StockRadarDb: RECOVERY SIMPLE + shrink log (local & server — tiet kiem disk)
#   powershell -ExecutionPolicy Bypass -File scripts\shrink-db-log.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\shrink-db-log.ps1 -TargetMb 512
#
# -KeepFull: chi dung neu can point-in-time restore (khong khuyen nghi StockRadar)

param(
    [int]$TargetMb = 512,
    [switch]$KeepFull
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\db-config.ps1"

$db = Get-DbSettings
$dbName = $db.Database

Write-Host "=== Shrink log: $dbName ===" -ForegroundColor Cyan
Write-Host "Server: $($db.Server) | Target log: ${TargetMb} MB"

$before = @"
SELECT CAST(SUM(CASE WHEN type_desc='LOG' THEN size END)*8./1024 AS DECIMAL(10,1)) AS log_mb,
       recovery_model_desc
FROM sys.databases d
JOIN sys.master_files mf ON d.database_id=mf.database_id
WHERE d.name='$dbName'
GROUP BY recovery_model_desc
"@
Write-Host "`nTruoc:"
Invoke-DbSql -Query $before -DbSettings $db

if ($KeepFull) {
    $logBackup = Join-Path $PSScriptRoot "db\StockRadarDb_log.trn"
    $dir = Split-Path $logBackup -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $escaped = $logBackup.Replace("'", "''")
    Write-Host "`n==> BACKUP LOG (FULL recovery)" -ForegroundColor Yellow
    Invoke-DbSql -Query "BACKUP LOG [$dbName] TO DISK = N'$escaped' WITH INIT, COMPRESSION, STATS=10;" -DbSettings $db
}
else {
    Write-Host "`n==> Chuyen RECOVERY SIMPLE" -ForegroundColor Yellow
    Invoke-DbSql -Query "ALTER DATABASE [$dbName] SET RECOVERY SIMPLE;" -DbSettings $db
}

Write-Host "==> DBCC SHRINKFILE" -ForegroundColor Yellow
Invoke-DbSql -Query @"
USE [$dbName];
DBCC SHRINKFILE (N'${dbName}_log', $TargetMb);
"@ -DbSettings $db

Write-Host "`nSau:"
Invoke-DbSql -Query $before -DbSettings $db
Write-Host "`nXong. Kiem tra dung luong o C:." -ForegroundColor Green

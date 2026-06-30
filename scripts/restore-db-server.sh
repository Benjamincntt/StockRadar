#!/usr/bin/env bash
# Restore StockRadarDb.bak on Gdata server (SQL Server Linux)
#   sudo bash scripts/restore-db-server.sh

set -euo pipefail

BAK="${BAK_PATH:-/var/opt/mssql/backup/StockRadarDb.bak}"
SQLCMD="${SQLCMD:-/opt/mssql-tools18/bin/sqlcmd}"
DB=StockRadarDb
DATA_DIR=/var/opt/mssql/data

if [ ! -f "$BAK" ]; then
  echo "Khong tim thay: $BAK"
  exit 1
fi

if [ -z "${SA_PASSWORD:-}" ]; then
  echo "Nhap mat khau SQL sa:"
  read -rs SA_PASSWORD
  echo
fi

echo "==> Dung StockRadar API"
systemctl stop stockradar || true

echo "==> Doc logical names tu backup"
mapfile -t LOGICAL < <($SQLCMD -S localhost -U sa -P "$SA_PASSWORD" -C -h -1 -W -Q "RESTORE FILELISTONLY FROM DISK = N'$BAK';" | awk 'NF {print $1}' | head -2)
DATA_LOGICAL="${LOGICAL[0]:?}"
LOG_LOGICAL="${LOGICAL[1]:?}"
echo "    Data: $DATA_LOGICAL"
echo "    Log:  $LOG_LOGICAL"

echo "==> Restore database"
$SQLCMD -S localhost -U sa -P "$SA_PASSWORD" -C -Q "
IF DB_ID(N'$DB') IS NOT NULL
BEGIN
  ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END;
RESTORE DATABASE [$DB]
FROM DISK = N'$BAK'
WITH REPLACE,
  MOVE N'$DATA_LOGICAL' TO N'$DATA_DIR/${DB}.mdf',
  MOVE N'$LOG_LOGICAL' TO N'$DATA_DIR/${DB}_log.ldf',
  STATS = 10;
USE [$DB];
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'hieunl')
  CREATE USER [hieunl] FOR LOGIN [hieunl];
ALTER ROLE db_owner ADD MEMBER [hieunl];
"

echo "==> Khoi dong API"
systemctl start stockradar
sleep 5
systemctl is-active stockradar
curl -s http://127.0.0.1:5281/api/v1/market | head -c 200
echo
echo "==> Restore xong"

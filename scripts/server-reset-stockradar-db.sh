#!/usr/bin/env bash
set -euo pipefail
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
USER="${SQL_USER:-hieunl}"
PASS="${SQL_PASSWORD:?Set SQL_PASSWORD}"
systemctl stop stockradar || true

$SQLCMD -S localhost -U "$USER" -P "$PASS" -C -Q "
ALTER DATABASE StockRadarDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE StockRadarDb;
CREATE DATABASE StockRadarDb;
ALTER DATABASE StockRadarDb SET RECOVERY SIMPLE;
USE StockRadarDb;
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'$USER')
BEGIN
  CREATE USER [$USER] FOR LOGIN [$USER];
  ALTER ROLE db_owner ADD MEMBER [$USER];
END
"

systemctl start stockradar
sleep 5
systemctl is-active stockradar
curl -s http://127.0.0.1:5281/api/v1/market | head -c 300
echo

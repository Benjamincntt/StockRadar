#!/usr/bin/env bash
# Dat StockRadarDb RECOVERY SIMPLE + shrink log (tiet kiem disk).
# Local Windows:  powershell -File scripts\shrink-db-log.ps1
# Server:         cd /var/www/StockRadar && bash scripts/set-db-recovery-simple.sh
#
# Env: SQL_USER, SQL_PASSWORD, DB_NAME, LOG_TARGET_MB, SQL_SERVER

set -euo pipefail

DB_NAME="${DB_NAME:-StockRadarDb}"
LOG_TARGET_MB="${LOG_TARGET_MB:-512}"
SQL_SERVER="${SQL_SERVER:-localhost}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
PROD_JSON="$PROJECT_DIR/backend/StockRadar.Api/appsettings.Production.json"

SQLCMD="${SQLCMD:-sqlcmd}"
if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then
  SQLCMD=/opt/mssql-tools18/bin/sqlcmd
fi

if [ -z "${SQL_PASSWORD:-}" ] && [ -f "$PROD_JSON" ]; then
  eval "$(python3 - <<PY
import json, shlex
with open("$PROD_JSON") as f:
    cs = json.load(f)["ConnectionStrings"]["DefaultConnection"]
parts = {}
for p in cs.split(";"):
    if "=" in p:
        k, v = p.split("=", 1)
        parts[k.strip()] = v.strip()
u = parts.get("User Id") or parts.get("User ID") or ""
pw = parts.get("Password") or ""
print(f"export SQL_USER={shlex.quote(u)}")
print(f"export SQL_PASSWORD={shlex.quote(pw)}")
PY
)"
fi

if [ -z "${SQL_PASSWORD:-}" ]; then
  echo "Thieu SQL_PASSWORD (hoac $PROD_JSON)"
  exit 1
fi

SQL_USER="${SQL_USER:-sa}"

status_sql() {
  $SQLCMD -S "$SQL_SERVER" -U "$SQL_USER" -P "$SQL_PASSWORD" -C -b -Q "
SET NOCOUNT ON;
SELECT d.name, d.recovery_model_desc,
  CAST(SUM(CASE WHEN mf.type_desc='LOG' THEN mf.size END)*8./1024 AS DECIMAL(10,1)) AS log_mb,
  CAST(SUM(CASE WHEN mf.type_desc='ROWS' THEN mf.size END)*8./1024 AS DECIMAL(10,1)) AS data_mb
FROM sys.databases d
JOIN sys.master_files mf ON d.database_id = mf.database_id
WHERE d.name = N'$DB_NAME'
GROUP BY d.name, d.recovery_model_desc;
"
}

echo "==> Truoc:"
status_sql

echo "==> RECOVERY SIMPLE + shrink log -> ${LOG_TARGET_MB} MB"
$SQLCMD -S "$SQL_SERVER" -U "$SQL_USER" -P "$SQL_PASSWORD" -C -b -Q "
ALTER DATABASE [$DB_NAME] SET RECOVERY SIMPLE;
USE [$DB_NAME];
DBCC SHRINKFILE (N'${DB_NAME}_log', $LOG_TARGET_MB);
"

echo "==> Sau:"
status_sql
echo "==> Xong."

#!/usr/bin/env bash
# Chay tren server: xoa SetupTracks Mua diem 1/2 truoc ngay.
# Usage: bash purge-setup-tracks-before-date.sh 2026-07-10 [--delete]

set -e
BEFORE_DATE="${1:?Can ngay YYYY-MM-DD}"
MODE="${2:-count}"
DATABASE="${DATABASE:-StockRadarDb}"

for PROD_JSON in \
  /var/www/publish/stockradar-api/appsettings.Production.json \
  /var/www/StockRadar/backend/StockRadar.Api/appsettings.Production.json \
  /var/www/StockRadar/backend/StockRadar.Api/appsettings.json
do
  if [ -f "$PROD_JSON" ]; then
    break
  fi
done

if [ ! -f "$PROD_JSON" ]; then
  echo "Khong tim thay appsettings tren server."
  exit 1
fi

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

if [ -z "${SQL_PASSWORD:-}" ]; then
  echo "Connection string khong co User Id/Password trong $PROD_JSON"
  exit 1
fi

SQL_USER="${SQL_USER:-sa}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
if [ ! -x "$SQLCMD" ]; then SQLCMD=sqlcmd; fi

echo "==> Dem SetupTracks MuaDiem1/MuaDiem2 truoc $BEFORE_DATE"
$SQLCMD -S localhost -U "$SQL_USER" -P "$SQL_PASSWORD" -C -d "$DATABASE" -Q "
SELECT SourceType, COUNT(*) AS Cnt
FROM SetupTracks
WHERE EntryDate < '$BEFORE_DATE'
  AND SourceType IN (N'MuaDiem1', N'MuaDiem2')
GROUP BY SourceType
ORDER BY SourceType;

SELECT COUNT(*) AS TotalToDelete
FROM SetupTracks
WHERE EntryDate < '$BEFORE_DATE'
  AND SourceType IN (N'MuaDiem1', N'MuaDiem2');
"

if [ "$MODE" = "--delete" ]; then
  echo "==> DELETE..."
  $SQLCMD -S localhost -U "$SQL_USER" -P "$SQL_PASSWORD" -C -d "$DATABASE" -Q "
SET QUOTED_IDENTIFIER ON;
DELETE FROM SetupTracks
WHERE EntryDate < '$BEFORE_DATE'
  AND SourceType IN (N'MuaDiem1', N'MuaDiem2');

SELECT @@ROWCOUNT AS DeletedRows;
"
  echo "==> Xong."
fi

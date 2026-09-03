#!/usr/bin/env bash
# Xóa nến giả ngày lễ (31/8, 1/9/2026) khỏi HistoryJson — chạy trên server production.
# Dùng: bash scripts/cleanup-holiday-bars.sh          (dry run — chỉ đếm)
#       bash scripts/cleanup-holiday-bars.sh --apply   (sửa dữ liệu thật)
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
if [ ! -x "$SQLCMD" ]; then SQLCMD=sqlcmd; fi

# Đọc credentials từ appsettings.Production.json
SETTINGS=/var/www/StockRadar/backend/StockRadar.Api/appsettings.Production.json
if [ -f "$SETTINGS" ]; then
  CONN=$(python3 -c "import json; print(json.load(open('$SETTINGS'))['ConnectionStrings']['DefaultConnection'])")
  SQL_USER=$(echo "$CONN" | sed -n 's/.*User Id=\([^;]*\).*/\1/p')
  SQL_PASSWORD=$(echo "$CONN" | sed -n 's/.*Password=\([^;]*\).*/\1/p')
fi

SQL_USER="${SQL_USER:-sa}"
SQL_PASSWORD="${SQL_PASSWORD:?Khong tim thay password — set SQL_PASSWORD hoac kiem tra appsettings.Production.json}"
DB=StockRadarDb

echo "=== Kiem tra nen ngay le 31/8 + 1/9/2026 ==="

$SQLCMD -S localhost -U "$SQL_USER" -P "$SQL_PASSWORD" -d "$DB" -b -C -Q "
SELECT 'Stocks' AS [Table], COUNT(*) AS Affected
FROM Stocks
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%'
UNION ALL
SELECT 'MarketIndices', COUNT(*)
FROM MarketIndices
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%';
"

if [ "${1:-}" != "--apply" ]; then
  echo ""
  echo "[DRY RUN] Khong sua du lieu. Chay lai voi --apply de cap nhat."
  exit 0
fi

echo ""
echo "=== Cap nhat HistoryJson — loai bar 31/8 va 1/9 ==="

$SQLCMD -S localhost -U "$SQL_USER" -P "$SQL_PASSWORD" -d "$DB" -b -C -Q "
UPDATE Stocks
SET HistoryJson = (
    SELECT N'[' + STRING_AGG(CAST(j.[value] AS nvarchar(max)), N',')
           WITHIN GROUP (ORDER BY CAST(JSON_VALUE(j.[value], '\$.date') AS date)) + N']'
    FROM OPENJSON(Stocks.HistoryJson) j
    WHERE JSON_VALUE(j.[value], '\$.date') NOT IN ('2026-08-31','2026-09-01')
)
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%';
PRINT 'Stocks updated: ' + CAST(@@ROWCOUNT AS varchar);

UPDATE MarketIndices
SET HistoryJson = (
    SELECT N'[' + STRING_AGG(CAST(j.[value] AS nvarchar(max)), N',')
           WITHIN GROUP (ORDER BY CAST(JSON_VALUE(j.[value], '\$.date') AS date)) + N']'
    FROM OPENJSON(MarketIndices.HistoryJson) j
    WHERE JSON_VALUE(j.[value], '\$.date') NOT IN ('2026-08-31','2026-09-01')
)
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%';
PRINT 'MarketIndices updated: ' + CAST(@@ROWCOUNT AS varchar);
"

echo ""
echo "Xong. Nen 31/8 va 1/9 da xoa khoi DB."

<#
.SYNOPSIS
  Xóa nến giả ngày lễ (31/8, 1/9/2026) khỏi HistoryJson của Stocks và MarketIndices.
.DESCRIPTION
  KBS trả dữ liệu cho ngày lễ Quốc Khánh 2026 mà danh sách VietnamHolidays thiếu 31/8 và 1/9.
  Script này parse JSON, loại bar có date = 2026-08-31 hoặc 2026-09-01, ghi lại.
  Chạy 1 lần, không cần chạy lại.
.PARAMETER DryRun
  Chỉ đếm và báo cáo, không sửa dữ liệu. Mặc định: $true (an toàn).
#>
param(
    [switch]$DryRun = $true
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\db-config.ps1"
$db = Get-DbSettings

$badDates = "'2026-08-31','2026-09-01'"

$countQuery = @"
USE [$($db.Database)];

SELECT 'Stocks' AS [Table],
       COUNT(*) AS Affected
FROM Stocks
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%'

UNION ALL

SELECT 'MarketIndices',
       COUNT(*)
FROM MarketIndices
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%';
"@

Write-Host "`n=== Kiem tra nen ngay le ===" -ForegroundColor Cyan
Invoke-DbSql -Query $countQuery -DbSettings $db

if ($DryRun) {
    Write-Host "`n[DRY RUN] Khong sua du lieu. Chay lai voi -DryRun:`$false de cap nhat." -ForegroundColor Yellow
    return
}

$updateQuery = @"
USE [$($db.Database)];

-- Stocks: loai bar ngay le
UPDATE Stocks
SET HistoryJson = (
    SELECT N'[' + STRING_AGG(CAST(j.[value] AS nvarchar(max)), N',')
           WITHIN GROUP (ORDER BY CAST(JSON_VALUE(j.[value], '$."date"') AS date)) + N']'
    FROM OPENJSON(Stocks.HistoryJson) j
    WHERE JSON_VALUE(j.[value], '$."date"') NOT IN ($badDates)
)
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%';

PRINT 'Stocks updated: ' + CAST(@@ROWCOUNT AS varchar);

-- MarketIndices: loai bar ngay le
UPDATE MarketIndices
SET HistoryJson = (
    SELECT N'[' + STRING_AGG(CAST(j.[value] AS nvarchar(max)), N',')
           WITHIN GROUP (ORDER BY CAST(JSON_VALUE(j.[value], '$."date"') AS date)) + N']'
    FROM OPENJSON(MarketIndices.HistoryJson) j
    WHERE JSON_VALUE(j.[value], '$."date"') NOT IN ($badDates)
)
WHERE HistoryJson LIKE '%2026-08-31%' OR HistoryJson LIKE '%2026-09-01%';

PRINT 'MarketIndices updated: ' + CAST(@@ROWCOUNT AS varchar);
"@

Write-Host "`n=== Cap nhat HistoryJson ===" -ForegroundColor Cyan
Invoke-DbSql -Query $updateQuery -DbSettings $db
Write-Host "`nXong. Nen 31/8 va 1/9 da xoa khoi DB." -ForegroundColor Green

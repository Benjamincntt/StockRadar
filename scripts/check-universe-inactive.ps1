# Chẩn đoán vì sao một mã bị "ngắt theo dõi" (rớt khỏi universe active).
#
# Kiểm tra: IsActive, TradingRestricted, lý do loại (TradingStatus), thời điểm loại
# (UniverseUpdatedAt), ngày nến cuối + số nến trong HistoryJson, và số ngày kể từ nến cuối.
#
# Mặc định đọc connection string từ backend\StockRadar.Api\appsettings.Development.json.
# Để trỏ vào DB PRODUCTION, set biến môi trường trước khi chạy:
#   $env:SR_SQL_SERVER  = "<host>"
#   $env:SR_SQL_DATABASE= "StockRadarDb"
#   $env:SR_SQL_USER    = "<user>"
#   $env:SR_SQL_PASSWORD= "<password>"
#
# Ví dụ:
#   .\scripts\check-universe-inactive.ps1                 # kiểm tra FRT
#   .\scripts\check-universe-inactive.ps1 -Symbol DXG     # kiểm tra mã khác
#   .\scripts\check-universe-inactive.ps1 -All            # liệt kê TẤT CẢ mã inactive (không hạn chế GD)

param(
    [string]$Symbol = "FRT",
    [switch]$All,
    [int]$Top = 500
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here "db-config.ps1")

$db = Get-DbSettings
Write-Host ""
Write-Host "Universe diagnosis" -ForegroundColor Cyan
Write-Host "  server : $($db.Server)" -DarkGray
Write-Host "  database: $($db.Database)" -DarkGray
Write-Host ""

# OPENJSON cần HistoryJson hợp lệ; ISJSON guard tránh lỗi với row rỗng/'null'.
$barExpr = @"
CROSS APPLY (
    SELECT MAX(JSON_VALUE(b.value, '`$.date')) AS LastBarDate,
           MIN(JSON_VALUE(b.value, '`$.date')) AS FirstBarDate,
           COUNT(*) AS BarCount
    FROM OPENJSON(CASE WHEN ISJSON(s.HistoryJson) = 1 THEN s.HistoryJson ELSE '[]' END) b
) bar
"@

if ($All) {
    $query = @"
USE [$($db.Database)];
SET NOCOUNT ON;
SELECT TOP ($Top)
    s.Symbol,
    s.TradingRestricted                                   AS Restricted,
    CONVERT(varchar(19), s.UniverseUpdatedAt, 120)        AS InactiveSinceUtc,
    s.LastClose,
    s.AvgVolume30d,
    bar.LastBarDate,
    DATEDIFF(day, CAST(bar.LastBarDate AS date), CAST(GETDATE() AS date)) AS DaysSinceLastBar,
    LEFT(ISNULL(s.TradingStatus, ''), 48)                 AS Reason
FROM Stocks s
$barExpr
WHERE s.IsActive = 0
ORDER BY s.TradingRestricted, s.UniverseUpdatedAt;
"@
    Write-Host "Tất cả mã inactive (kèm mã bị hạn chế GD) — DaysSinceLastBar lớn = history đóng băng:" -ForegroundColor Yellow
    Invoke-DbSql -Query $query -DbSettings $db
    return
}

$sym = $Symbol.Trim().ToUpperInvariant().Replace("'", "''")
$query = @"
USE [$($db.Database)];
SET NOCOUNT ON;
SELECT
    s.Symbol,
    s.IsActive,
    s.TradingRestricted                                   AS Restricted,
    CONVERT(varchar(19), s.UniverseUpdatedAt, 120)        AS UniverseUpdatedAtUtc,
    s.LastClose,
    s.LastVolume,
    s.AvgVolume30d,
    bar.BarCount,
    bar.FirstBarDate,
    bar.LastBarDate,
    DATEDIFF(day, CAST(bar.LastBarDate AS date), CAST(GETDATE() AS date)) AS DaysSinceLastBar,
    s.TradingStatus                                       AS Reason
FROM Stocks s
$barExpr
WHERE s.Symbol = '$sym';
"@

Write-Host "Trạng thái universe của $sym :" -ForegroundColor Yellow
Invoke-DbSql -Query $query -DbSettings $db

Write-Host ""
Write-Host "Đọc kết quả:" -ForegroundColor Cyan
Write-Host "  IsActive=0 + DaysSinceLastBar lớn (>>15)  -> history đóng băng: Job 2 bỏ qua mã inactive nên không có giá mới."
Write-Host "  Reason                                    -> lý do rớt universe lúc bị loại (giá/thanh khoản/hạn chế GD)."
Write-Host "  Restricted=1                              -> bị hạn chế GD; Job 1 cũng KHÔNG khôi phục cho tới khi listing hết restricted."
Write-Host ""
Write-Host "Khắc phục ngay (re-fetch fresh + rescreen): chạy Job 1 một lần" -ForegroundColor Green
Write-Host "  .\scripts\run-backfill-night.ps1     # hoặc POST /market/jobs/history (X-Sync-Key)"

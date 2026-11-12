# Xuất dữ liệu 26 bảng V1 rồi ship migration xoá bảng (spec 016, đợt 4)

> **Thứ tự bắt buộc:** xuất xong → tải file về → kiểm `sha256` → xoá file trên server → **mới** ship.
> API tự chạy `MigrateAsync` lúc khởi động, nên ship là 26 bảng bị xoá ngay; không có bước xuất nào chen vào được nữa.
>
> Chạy **trên server production**. Không chạy bước xuất trên DB dev.
> Lệnh dưới đây viết cho **Windows PowerShell 5.1**: lệnh từ xa bọc trong nháy đơn, không dùng `\"`, không dùng `$()`, không dùng `&&`.
> Thay `<SERVER>` bằng địa chỉ server. Không ghi mật khẩu hay khoá vào file này.

## 0. Điều kiện trước khi chạy

- `sqlcmd` và `bcp` có ở `/opt/mssql-tools18/bin/` trên server.
- `/var/www/publish/stockradar-api/appsettings.Production.json` có `ConnectionStrings:DefaultConnection` (script đọc `Server`, `Database`, `User Id`, `Password` từ đây).
- Đủ chỗ trong `/tmp`: `StockCriterionDetails` ~129 MB + `StockCriterionScores` ~81 MB, cộng phần còn lại và file nén — để dư **1 GB**.

## 1. Copy script lên server

```powershell
scp specs\016-remove-v1-top\xuat-bang-v1.sh <SERVER>:/tmp/xuat-bang-v1.sh
ssh <SERVER> 'chmod 700 /tmp/xuat-bang-v1.sh'
```

## 2. Chạy script

```powershell
ssh <SERVER> 'bash /tmp/xuat-bang-v1.sh'
```

Script chỉ đọc (SELECT + `bcp out`), không có `DROP`/`DELETE`/`TRUNCATE`. Mỗi bảng in một dòng `KHỚP`/`LỆCH`, cuối cùng in kích thước + `sha256` + tổng số bảng KHỚP/LỆCH.

- Mã thoát `0`: cả 26 bảng KHỚP → sang bước 3.
- Mã thoát khác `0`: có bảng LỆCH hoặc thiếu → **dừng**, báo lại trước khi làm gì tiếp.

Kết quả nằm ở `/tmp/v1-export-<YYYYMMDD>/`:

| File | Nội dung |
|---|---|
| `counts.txt` | `COUNT(*)` của từng bảng (số đối chiếu) |
| `<Bang>.columns.txt` | tên cột theo thứ tự, để đọc lại `<Bang>.dat` |
| `<Bang>.dat` | dữ liệu thô: trường tách bằng `0x1F`, dòng tách bằng `0x1E`, UTF-8 |
| `so-sanh.txt` | kết quả đối chiếu `COUNT(*)` với số dòng trong file |

Ghi lại số `sha256` script in ra để so ở bước 4.

## 3. Tải file nén về máy Windows

```powershell
scp <SERVER>:/tmp/v1-export-<YYYYMMDD>.tar.gz "$env:USERPROFILE\Downloads\"
```

## 4. Kiểm `sha256` hai bên

```powershell
Get-FileHash "$env:USERPROFILE\Downloads\v1-export-<YYYYMMDD>.tar.gz" -Algorithm SHA256 | Format-List
ssh <SERVER> 'sha256sum /tmp/v1-export-<YYYYMMDD>.tar.gz'
```

Hai giá trị phải giống nhau. Khác → tải lại; vẫn khác → dừng, chưa xoá gì trên server.

Mở thử file nén ở máy (7-Zip/WinRAR) để chắc chắn có đủ 26 cặp `<Bang>.dat` + `<Bang>.columns.txt` và file `counts.txt`.

## 5. Xoá file tạm trên server

Chỉ làm sau khi bước 4 đạt.

```powershell
ssh <SERVER> 'rm -rf /tmp/v1-export-<YYYYMMDD> /tmp/v1-export-<YYYYMMDD>.tar.gz /tmp/xuat-bang-v1.sh'
ssh <SERVER> 'ls -d /tmp/v1-export-* 2>/dev/null || echo DA-XOA-HET'
```

## 6. Ship migration

Ship như thường lệ (owner tự chạy `scripts\ship-all.ps1 -Message "..."`). Migration `RemoveV1Tables` đi kèm bản build; API khởi động là apply.

## 7. Kiểm sau khi ship (chỉ đọc)

Mở một phiên ssh tương tác rồi dán hai khối dưới đây (làm vậy để không phải lồng nháy qua PowerShell):

```powershell
ssh <SERVER>
```

Khối 1 — lấy thông tin kết nối từ file cấu hình, không gõ mật khẩu:

```bash
CFG=/var/www/publish/stockradar-api/appsettings.Production.json
export SQLCMDPASSWORD="$(grep -oP '(?:^|;)\s*Password\s*=\s*\K[^;]+' "$CFG" | head -n1)"
SQLCMD_USER="$(grep -oP '(?:^|;)\s*User Id\s*=\s*\K[^;]+' "$CFG" | head -n1)"
/opt/mssql-tools18/bin/sqlcmd -S localhost -d StockRadarDb -U "$SQLCMD_USER" -C -W -b -Q "SET NOCOUNT ON; SELECT @@VERSION;"
```

Khối 2 — bốn câu kiểm:

```bash
/opt/mssql-tools18/bin/sqlcmd -S localhost -d StockRadarDb -U "$SQLCMD_USER" -C -W -b -Q "SET NOCOUNT ON;
SELECT name FROM sys.tables WHERE name IN (
 'Alerts','CriterionGroupDailyAccuracies','CriterionGroupWeeklyReviews','CriterionWeights',
 'DailyAnalysisRuns','DailyCriterionAccuracies','DailyOpportunities','EarlyRecoveryRadar',
 'EntryTimingStates','FalsePositiveMiningStates','HitCalibrationBuckets','HitCalibrationStates',
 'MasterAlertPositions','PersonalCalibrationStates','PositionSellLegs','SectorWaveRegimes',
 'SessionRadarHits','SetupTracks','ShadowWeightPicks','ShadowWeightSummaries',
 'StockCriterionDetails','StockCriterionScores','TradeJournalEntries','VipAlertFires',
 'WeeklyCriterionReviews','WeeklyOpportunityReviews');"
```

- Kỳ vọng: **0 dòng** (26 bảng đã mất).

```bash
/opt/mssql-tools18/bin/sqlcmd -S localhost -d StockRadarDb -U "$SQLCMD_USER" -C -W -b -Q "SET NOCOUNT ON;
SELECT name FROM sys.tables WHERE name IN (
 'ChiSoNgayV3','JobRunStatuses','MarketIndices','NhatKyGiaoDich','NhatKyTinHieuBoQua',
 'SectorDefinitions','Stocks','ThietBiNhanThongBao','TinHieuV3','Users','ViTheV3',
 'WatchlistItems','Watchlists') ORDER BY name;
SELECT 'Stocks' AS Bang, COUNT(*) AS SoDong FROM dbo.Stocks
UNION ALL SELECT 'TinHieuV3', COUNT(*) FROM dbo.TinHieuV3
UNION ALL SELECT 'ViTheV3', COUNT(*) FROM dbo.ViTheV3
UNION ALL SELECT 'ChiSoNgayV3', COUNT(*) FROM dbo.ChiSoNgayV3;"
```

- Kỳ vọng: đủ **13 dòng** bảng giữ lại, kèm 4 dòng `COUNT(*)` của `Stocks`, `TinHieuV3`, `ViTheV3`, `ChiSoNgayV3` (so với lần đếm trước khi ship).

```bash
/opt/mssql-tools18/bin/sqlcmd -S localhost -d StockRadarDb -U "$SQLCMD_USER" -C -W -b -Q "SET NOCOUNT ON;
SELECT name FROM sys.tables WHERE name = 'KetQuaKichBan';"
```

- Kỳ vọng: **1 dòng**. Bảng này không thuộc phạm vi đợt 4, còn nguyên.

```bash
/opt/mssql-tools18/bin/sqlcmd -S localhost -d StockRadarDb -U "$SQLCMD_USER" -C -W -b -Q "SET NOCOUNT ON;
SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '%RemoveV1Tables';"
```

- Kỳ vọng: **1 dòng** có `RemoveV1Tables`.

Gõ `exit` để đóng phiên ssh.

## Nếu có sự cố

- Script báo `LỆCH` ở bảng nào: **không ship**. Chạy lại script (script đọc lại từ đầu, không ghi vào DB) và báo lại số liệu của bảng đó.
- Quên xuất mà đã ship: bảng đã bị xoá; chỉ còn cách khôi phục từ backup DB (`scripts\export-db.ps1` / bản `.bak` gần nhất) hoặc từ thư mục `/tmp` nếu chưa xoá.
- Cần rollback migration: `dotnet ef database update 20261102045424_AddViTheV3DoiChieu` (tạo lại 26 bảng **rỗng**) — dữ liệu chỉ lấy lại được từ file xuất.

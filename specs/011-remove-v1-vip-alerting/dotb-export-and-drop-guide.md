# ĐỢT B — Hướng dẫn xuất dữ liệu 3 bảng VIP rồi xoá bảng (Spec 011)

> Chỉ chạy **ĐỢT B sau khi Đợt A đã merge** và **chủ sản phẩm đã báo xuất xong dữ liệu production**.
> 3 bảng: `MasterAlertPositions`, `PositionSellLegs`, `VipAlertFires`.
> Chạy trên **server production** (SQL Server). KHÔNG chạy trên DB dev localhost làm bước xuất production.

## Bước 0 — Ghi lại số dòng trước khi xuất (để đối chiếu)

```bash
sqlcmd -S <SERVER> -d StockRadarDb -U <USER> -P '<PASSWORD>' -C -Q \
"SELECT 'MasterAlertPositions' AS t, COUNT(*) AS n FROM dbo.MasterAlertPositions
UNION ALL SELECT 'PositionSellLegs', COUNT(*) FROM dbo.PositionSellLegs
UNION ALL SELECT 'VipAlertFires', COUNT(*) FROM dbo.VipAlertFires;"
```

## Bước 1 — Xuất 3 bảng ra file (chọn 1 trong 2 cách)

### Cách A — `bcp` từng bảng ra CSV (khuyên dùng, nhẹ, dễ lưu trữ)
```bash
# Tạo thư mục lưu có dấu ngày
mkdir -p ~/vip-export-$(date +%Y%m%d)

# Xuất từng bảng (định dạng ký tự, phân tách dấu phẩy, có header qua -t, utf8)
bcp StockRadarDb.dbo.MasterAlertPositions out ~/vip-export-$(date +%Y%m%d)/MasterAlertPositions.csv -S <SERVER> -U <USER> -P '<PASSWORD>' -T -c -t, -S
bcp StockRadarDb.dbo.PositionSellLegs   out ~/vip-export-$(date +%Y%m%d)/PositionSellLegs.csv   -S <SERVER> -U <USER> -P '<PASSWORD>' -T -c -t,
bcp StockRadarDb.dbo.VipAlertFires      out ~/vip-export-$(date +%Y%m%d)/VipAlertFires.csv      -S <SERVER> -U <USER> -P '<PASSWORD>' -T -c -t,
```
- `-T` = xác thực Windows (trusted). Nếu dùng SQL Auth thì thay bằng `-U <USER> -P '<PASSWORD>'` và bỏ `-T`.
- Nếu `bcp` báo thiếu `-S` trùng: `-S <SERVER>` là máy chủ, đã có ở đầu; dòng `MasterAlertPositions` phía trên có `-S` dư, xoá `-S` cuối dòng đó.
- Đối chiếu số dòng file = số dòng ở Bước 0:
  ```bash
  for f in ~/vip-export-*/*.csv; do echo "$f: $(($(wc -l < "$f")-1)) dòng"; done
  ```
  (trừ 1 dòng header nếu có; `bcp -c` mặc định không ghi header nên số dòng = số bản ghi).

### Cách B — Backup nguyên DB (dùng script có sẵn), rồi trích 3 bảng khi cần
```powershell
# Từ máy Windows trỏ tới prod (sửa SR_SQL_* env, xem scripts/db-config.ps1)
$env:SR_SQL_SERVER='<SERVER>'; $env:SR_SQL_DATABASE='StockRadarDb'
$env:SR_SQL_USER='<USER>'; $env:SR_SQL_PASSWORD='<PASSWORD>'
powershell -ExecutionPolicy Bypass -File scripts\export-db.ps1
# -> scripts\db\StockRadarDb.bak (BACKUP DATABASE đầy đủ, nén)
```
Cách B an toàn nhất (lưu trọn DB), nhưng file to. Chủ sản phẩm muốn "xuất 3 bảng" thì dùng **Cách A**.

> **CHỈ SAU KHI** các file export tồn tại + số dòng khớp Bước 0, mới sang Bước 2 (xoá).

## Bước 2 — Gỡ Entity + DbSet (code, Đợt B)

Trong `backend/StockRadar.Infrastructure/Persistence/`:
1. `ApplicationDbContext.cs`: bỏ 3 `DbSet` (`MasterAlertPositions`, `PositionSellLegs`, `VipAlertFires`) và 3 khối `modelBuilder.Entity<...>()` tương ứng.
2. `Entities/PerformanceEntities.cs`: bỏ `MasterAlertPositionEntity`, `PositionSellLegEntity`, `VipAlertFireEntity`.
3. Dọn mọi tham chiếu còn lại (nếu có) — ví dụ `GetAlertHistoryAsync` trong `EfPerformanceRepositories.cs` hiện LEFT JOIN các entity này; đổi thành không join, bỏ cột realized lấy từ `MasterAlertPositions`.
4. `dotnet build StockRadar.slnx` cho tới khi 0 lỗi (khi đó model snapshot thật sự không còn 3 entity).

## Bước 3 — Sinh migration Đợt B (không xài tay file SQL, để snapshot chuẩn)

```bash
cd backend
dotnet ef migrations add DropVipAlertTables `
  --project StockRadar.Infrastructure --startup-project StockRadar.Api
```
- **ĐỌC LẠI BODY** migration vừa sinh (repo hay sinh sai thứ tự/thiếu FK): thứ tự `DropTable` phải là
  `PositionSellLegs` → `MasterAlertPositions` → `VipAlertFires`. `Down()` phải recreate được 3 bảng (để rollback).
- Nếu EF sinh sai/thiếu, sửa tay body migration cho đúng thứ tự trên; `specs/011-remove-v1-vip-alerting/dotb-drop-vip-tables.sql` **chỉ để tham chiếu thứ tự FK** (KHÔNG chạy file đó).

## Bước 4 — Test trên DB DEV (KHÔNG phải 103.226.248.6): apply / rollback / apply lại

```bash
cd backend
# apply
dotnet ef database update --project StockRadar.Infrastructure --startup-project StockRadar.Api
# rollback về migration liền trước
dotnet ef database update <TenMigrationLienTruoc> --project StockRadar.Infrastructure --startup-project StockRadar.Api
# apply lại
dotnet ef database update --project StockRadar.Infrastructure --startup-project StockRadar.Api
```
Sau mỗi bước: build + `dotnet test` phải xanh, API dev khởi động không lỗi, và `/api/v1/market/jobs` không còn "VIP".

## Bước 5 — Áp production
Sau khi Đợt B được duyệt + DB dev test đạt, chạy **migration EF** (`dotnet ef database update`) trên prod — ĐÂY LÀ CƠ CHẾ DUY NHẤT để xoá bảng. KHÔNG chạy tay `dotb-drop-vip-tables.sql` trên production. Xác nhận 3 bảng biến mất, nghiệp vụ V2 + Top V1 vẫn chạy.

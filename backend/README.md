# StockRadar Backend

## Layers

```
Domain → Application → Infrastructure → Api
```

## REST API v1

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| GET | `/api/v1/market` | Tổng quan thị trường |
| GET | `/api/v1/market/quotes` | Snapshot giá tất cả mã (fallback polling) |
| GET | `/api/v1/sectors?page&pageSize` | Top ngành (phân trang) |
| GET | `/api/v1/opportunities?page&pageSize` | Top cơ hội |
| GET | `/api/v1/signals?page&pageSize` | Tín hiệu mới |
| GET | `/api/v1/radar-items?page&pageSize&filters` | Radar scanner |
| GET | `/api/v1/stocks/{symbol}` | Chi tiết cổ phiếu |
| GET | `/api/v1/alerts?page&pageSize&category&type` | Cảnh báo |
| GET | `/api/v1/watchlist-items` | Watchlist |
| PUT | `/api/v1/watchlist-items/{symbol}` | Thêm mã (idempotent) |
| POST | `/api/v1/watchlist-items` | Thêm mã (body `{symbol}`) |
| DELETE | `/api/v1/watchlist-items/{symbol}` | Xóa mã → 404 nếu không có |
| POST | `/api/v1/users` | Đăng ký → **201 Created** |
| POST | `/api/v1/auth/tokens` | Đăng nhập → JWT |

Lỗi trả `application/problem+json` (RFC 7807).

Swagger: http://localhost:5280/swagger

## Chạy full stack (khuyến nghị test)

```powershell
cd D:\Source\StockRadar
.\start-all.ps1
```

Xem README gốc project. Chỉ API: `.\start-api-published.ps1`

**Lỗi MSB3027 (file locked)?** Project tự dừng API trước khi build. Hoặc:

```powershell
cd D:\Source\StockRadar\backend
.\stop-api.ps1
```

## Database

**SQL Server** — cùng server với PhuKienTuiLoc (`localhost`), database riêng **`StockRadarDb`**.

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=StockRadarDb;User Id=...;Password=...;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

Cấu hình dev: `StockRadar.Api/appsettings.Development.json` (user/password giống PhuKienTuiLoc).

`dotnet run` chỉ migrate + guest user. **Không seed dữ liệu mẫu** — dữ liệu từ KBS / Job pipeline.

## Lên lịch job (Quartz.NET)

Các job chạy tự động trong API qua **Quartz.NET** (`Infrastructure/Scheduling/`). Cấu hình trong `appsettings.json`:

| Job | Config | Lịch mặc định |
|-----|--------|----------------|
| **Job 1** Backfill | `MarketJobs:History:Enabled` + `RunOnStartup` | Một lần sau khởi động (nếu bật) |
| **Job 2** Sync phiên | `MarketJobs:DailySession:Enabled` | Cron `15:00` T2–T6 (giờ VN) |
| **Phân tích** | `MarketJobs:DailyAnalysis:Enabled` | Cron `15:02` VN (+`DelayAfterSessionMinutes`) |
| **KBS realtime** | `MarketData:AutoSyncEnabled` | Mỗi `SyncIntervalSeconds` trong phiên |
| **Intraday scanner** | `IntradayScanner:Enabled` | Mỗi `IntervalSeconds` trong phiên |
| **Order flow monitor** | `OpportunityMonitor:Enabled` | Mỗi `IntervalSeconds` trong phiên |

Khi API khởi động, log hiển thị job đã lên lịch và thời điểm chạy tiếp theo. Chạy thủ công: `POST /api/v1/market/jobs/history`, `POST /api/v1/market/jobs/daily`.

Xóa dữ liệu cũ để test KBS:

```powershell
cd D:\Source\StockRadar\backend
.\clear-sample-data.ps1
.\start-api-published.ps1
```

## Dữ liệu thị trường (KBS)

**Một nguồn duy nhất:** API .NET gọi trực tiếp KB Buddy (`kbbuddywts.kbsec.com.vn`) qua `KbsPriceBoardClient`, `KbsHistoryClient`, …

- **Realtime trong phiên:** `MarketData:AutoSyncEnabled=true` → Quartz `KbsMarketSyncJob` mỗi `SyncIntervalSeconds` (mặc định 60s)
- **Job thủ công:** `POST /api/v1/market/jobs/*` (header `X-Sync-Key` = `MarketData:SyncApiKey`)
- **Script tiện ích:** `scripts/run-backfill.ps1`, `scripts/run-daily-jobs.ps1` (cấu hình `scripts/pipeline-config.json`)

## Realtime (SignalR)

Luồng:

1. **Snapshot** — frontend load REST lần đầu (`GET /market`, `/stocks/...`)
2. **Push** — Quartz sync KBS → `MarketSyncService` → broadcast **SignalR** (`/hubs/market`)
3. **Fallback** — mất WebSocket thì poll `GET /market/quotes` mỗi 45s

Hub events: `QuotesUpdated`, `IndexUpdated`. Badge **Realtime** trên TopBar khi kết nối live.

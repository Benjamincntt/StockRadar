# StockRadar

Ứng dụng theo dõi cổ phiếu Việt Nam — **Opportunity Score**, Signal Engine, Sector Rotation.

## Cấu trúc

```
D:\Source\StockRadar\
├── backend/
│   ├── StockRadar.sln
│   ├── StockRadar.Domain/
│   ├── StockRadar.Application/
│   ├── StockRadar.Infrastructure/
│   └── StockRadar.Api/
└── frontend/
```

## Tính năng MVP

- **Dashboard**: VNINDEX, Market Trend, Top ngành, Top Opportunities, tín hiệu mới
- **Radar**: Scanner lọc Breakout / Accumulation / RS / Volume / Shakeout / Distribution
- **Chi tiết cổ phiếu**: Opportunity Score breakdown, summary, mức giá
- **Alerts**: Danh sách cảnh báo mua/bán
- **Watchlist**: Theo dõi mã yêu thích (guest hoặc đăng nhập JWT)

## Auth

- `POST /api/v1/users` — tạo tài khoản (201 Created)
- `POST /api/v1/auth/tokens` — nhận JWT
- Frontend: `/login`, token lưu `localStorage`, gửi `Authorization: Bearer`

## Hạ tầng scale

- **SQL Server** — database riêng `StockRadarDb`
- **KBS trong API** — Quartz auto-sync giá trong phiên (không worker Python)
- **SignalR realtime** — giá live khi API sync KBS
- **IMemoryCache** cho đọc stock/market
- **REST v1** + Problem Details + pagination

## Chạy nhanh (full stack)

```powershell
cd D:\Source\StockRadar
.\start-all.ps1
```

Mở **2 cửa sổ**:
1. **API** — http://localhost:5280 (SignalR `/hubs/market`, KBS sync tự chạy trong phiên)
2. **Frontend** — http://localhost:5173

Dừng: `.\stop-all.ps1`

Pipeline thủ công (Job 1/2): xem `scripts/run-backfill.ps1`, `scripts/run-daily-jobs.ps1`.

## Chạy backend

```powershell
cd D:\Source\StockRadar\backend\StockRadar.Api
dotnet run
```

API: http://localhost:5280/swagger (hoặc `/` tự redirect)

## Pipeline job (thủ công)

```powershell
cd D:\Source\StockRadar\scripts
.\run-backfill.ps1      # Job 1 — lần đầu
.\run-daily-jobs.ps1    # Job 2 + phân tích
```

`pipeline-config.json`: `sync_api_key` khớp `MarketData:SyncApiKey` trong `appsettings.json`.

## Chạy frontend

```powershell
cd D:\Source\StockRadar\frontend
npm install
npm run dev
```

UI: http://localhost:5173

## Deploy lên server (Gdata / PhuKienTuiLoc)

Cùng máy với AnTea (`baobiantea.com`), server **`103.226.248.6`** (LAN `192.168.200.20`), subdomain **`stock.baobiantea.com`**, API port **5281**.

Xem chi tiết: [`docs/build-and-deploy.md`](docs/build-and-deploy.md)

```bash
# Trên server (lần đầu)
git clone https://github.com/Benjamincntt/StockRadar.git /var/www/StockRadar
cd /var/www/StockRadar
DOMAIN=stock.baobiantea.com SQL_USER=hieunl SQL_PASSWORD='...' bash scripts/gdata-stockradar-setup.sh

# Cập nhật
cd /var/www/StockRadar && git pull && bash deploy.sh all
```

## Công thức Opportunity Score (100 điểm)

| Tiêu chí | Điểm |
|----------|------|
| Market Trend | 20 |
| Sector Strength | 20 |
| Relative Strength | 20 |
| Accumulation | 15 |
| Breakout | 15 |
| Volume Expansion | 10 |

## Frontend — AI Stock Flow Monitor

Giao diện mobile-first theo thiết kế Stitch:

- Top Bar: menu, tiêu đề, Sign In
- Bottom Bar: Trang chủ, Radar, Heatmap, Cảnh báo, Watchlist
- Theme sáng, card bo tròn, xanh/đỏ cho dữ liệu tài chính
- Sparkline, line chart, volume bar, heatmap treemap

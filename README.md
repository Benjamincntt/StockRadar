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
- **vnstock KBS worker** — `data-sync/` đồng bộ giá (giờ giao dịch VN)
- **SignalR realtime** — giá nhảy live trên UI khi worker sync
- **IMemoryCache** cho đọc stock/market
- **REST v1** + Problem Details + pagination
- **External market sync** — xem `backend/README.md`

## Chạy nhanh (full stack — test luồng dữ liệu)

```powershell
cd D:\Source\StockRadar
.\start-all.ps1
```

Mở **3 cửa sổ**:
1. **API** — http://localhost:5280 (SignalR `/hubs/market`)
2. **data-sync** — vnstock KBS → `POST /market/sync` mỗi 60s
3. **Frontend** — http://localhost:5173

Cần **Python 3.10+** (cài từ python.org). Lần đầu tự tạo `.venv` và `pip install`.

Dừng: `.\stop-all.ps1`

## Chạy backend

```powershell
cd D:\Source\StockRadar\backend\StockRadar.Api
dotnet run
```

API: http://localhost:5280/swagger (hoặc `/` tự redirect)

## Đồng bộ giá KBS (vnstock)

Cần **Python 3.10+** (cài từ [python.org](https://www.python.org/downloads/), bật "Add to PATH").

```powershell
cd D:\Source\StockRadar\data-sync
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
python sync.py
```

`config.json`: `sync_api_key` phải khớp `MarketData:SyncApiKey` trong `appsettings.json`.  
Test ngoài giờ giao dịch: `"force_sync": true`, `"run_once": true`.

Quản lý/test API qua Swagger UI — xem `backend/README.md` và `StockRadar.Api.http`.

## Chạy frontend

```powershell
cd D:\Source\StockRadar\frontend
npm install
npm run dev
```

UI: http://localhost:5173

## Deploy lên server (Gdata / PhuKienTuiLoc)

Cùng máy với AnTea (`baobiantea.com`), subdomain **`stock.baobiantea.com`**, API port **5281**.

Xem chi tiết: [`docs/DEPLOY-GDATA.md`](docs/DEPLOY-GDATA.md)

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

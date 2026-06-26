# StockRadar — vnstock KBS sync worker

Đồng bộ bảng giá **KBS** (cùng nguồn KB Buddy/KBSV) vào `StockRadarDb` qua API.

## Cài đặt

Yêu cầu **Python 3.10+** (không dùng stub Microsoft Store — cài từ [python.org](https://www.python.org/downloads/)).

```powershell
cd D:\Source\StockRadar\data-sync
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
```

## Cấu hình

Sửa `config.json`:

| Key | Mô tả |
|-----|--------|
| `api_base_url` | `http://localhost:5280/api/v1` |
| `sync_api_key` | Khớp `MarketData:SyncApiKey` trong `appsettings.json` |
| `interval_seconds` | Chu kỳ sync (mặc định 120s) |
| `use_api_symbols` | `true` = lấy mã từ `GET /market/sync/symbols` (bắt buộc sau Job 1) |
| `symbols` | Chỉ dùng khi `use_api_symbols: false` |
| `force_sync` | `true` = sync cả ngoài giờ giao dịch (test) |

## Chạy

```powershell
# Terminal 1 — API
cd D:\Source\StockRadar\backend\StockRadar.Api
dotnet run

# Terminal 2 — sync worker
cd D:\Source\StockRadar\data-sync
.\.venv\Scripts\activate
python sync.py
```

Chạy một lần (test):

```json
"run_once": true
```

## Giờ giao dịch

Worker chỉ gọi vnstock trong phiên VN (9:00–11:30, 13:00–14:45). Ngoài giờ vẫn chạy vòng lặp nhưng skip sync.

## API

- `POST /api/v1/market/sync` — body `{ index, quotes }`, header `X-Sync-Key`
- `GET /api/v1/market/sync/symbols` — danh sách mã trong DB

## Ba job thị trường (mục tiêu chính)

| Job | Khi chạy | Việc làm |
|-----|----------|----------|
| **1. Lịch sử** | Một lần (thủ công) | Toàn bộ CP niêm yết → `Stocks.HistoryJson` |
| **2. Phiên 15h** | Hàng ngày 15:00 VN | OHLCV phiên hôm nay + VNINDEX |
| **3. Phân tích** | Ngay sau Job 2 (+2 phút) | Chấm điểm → `DailyOpportunities` cho **ngày mai** |

### Job 1 — Backfill lịch sử

```powershell
cd D:\Source\StockRadar\data-sync
.\run-backfill.ps1
```

API: `POST /api/v1/market/jobs/history` (header `X-Sync-Key`)

Cấu hình: `MarketJobs:History` — `Universe: AllListed` (~1500+ mã HOSE/HNX/UPCOM).

### Job 2+3 — Hàng ngày

Tự chạy trong API (`MarketJobs:DailySession` + `DailyAnalysis`). Test thủ công:

```powershell
.\run-daily-jobs.ps1
```

API: `POST /api/v1/market/jobs/daily`

**Realtime sync** (`AutoSyncEnabled`) mặc định **tắt** — pipeline chính qua Quartz Job 2 lúc 15h. Bật lại nếu cần giá live trong phiên.

Các job trong API dùng **Quartz.NET** — xem `backend/README.md` mục Lên lịch job.

`GET /api/v1/opportunities` đọc từ `DailyOpportunities` (sau Job 3). Nếu chưa phân tích, API trả danh sách rỗng kèm hướng dẫn chạy Job 3.

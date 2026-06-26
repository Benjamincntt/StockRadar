# Pipeline dữ liệu StockRadar

## Định nghĩa job (đúng ý thiết kế)

| Job | Khi chạy | Việc làm | DB |
|-----|----------|----------|-----|
| **Job 1** | Một lần (thủ công) | Backfill OHLCV **2000-01-01 → T-1** | `Stocks.HistoryJson` (full) |
| **Job 2** | Sau đóng cửa ngày **T** (15h VN) | **Append** nến phiên ngày **T** — không cắt Job 1 | Ghép thêm 1 ngày vào `HistoryJson` |
| **Phân tích** | Ngay sau Job 2 (+2 phút) | SmartMoney → watchlist phiên **T+1** | `DailyOpportunities` |
| **Job 3** | Trong phiên ngày **T+1** (60s) | Monitor watchlist, cảnh báo biến động | Zalo + quote live |

### Ví dụ timeline

```
23/06 (T-1)  Job 1 backfill kết thúc tại đây
24/06 (T)    15h → Job 2 append nến 24/06 → phân tích → list cho 25/06
25/06 (T+1)  9h–14h45 → Job 3 monitor 60s
25/06        15h → Job 2 append nến 25/06 → ...
```

## API

| Endpoint | Job |
|----------|-----|
| `POST /market/jobs/history` | Job 1 |
| `POST /market/jobs/session` | Job 2 |
| `POST /market/jobs/analysis` | Phân tích sau phiên |
| `POST /market/jobs/daily` | Job 2 + phân tích (tiện test) |
| `POST /market/jobs/opportunity-monitor` | Job 3 (1 vòng) |

Header: `X-Sync-Key` = `MarketData:SyncApiKey`

## Script

```powershell
# Job 1 — vài giờ, ~400 mã HOSE
.\data-sync\run-backfill.ps1

# Job 2 (+ phân tích)
.\data-sync\run-daily-jobs.ps1

# Job 3 — API tự chạy nếu OpportunityMonitor.Enabled=true
```

## Lưu ý kỹ thuật

- Job 1 mặc định `endDate` = **phiên giao dịch trước** (T-1), không gồm ngày hôm nay.
- Job 2 merge theo `Date` — **không giới hạn số nến** (đã bỏ cap 90 cũ).
- Job 3 chỉ theo dõi mã trong `DailyOpportunities` của phiên đang active.

# Pipeline dữ liệu StockRadar

## Định nghĩa job (đúng ý thiết kế)

| Job | Khi chạy | Việc làm | DB |
|-----|----------|----------|-----|
| **Job 1** | Một lần (thủ công) | KBS listing + backfill OHLCV **2000-01-01 → T-1** + lọc universe | `Stocks.HistoryJson` (full), `IsActive` |
| **Job 2** | Mỗi **5 phút** trong giờ GD (9h–11h30, 13h–14h45 VN) + cron dự phòng | **Append** nến phiên **T** cho mã active Job 1; KBS chỉ bảng giá | Ghép/cập nhật nến ngày T; alert `DarvasBreakout` |
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
.\scripts\run-backfill.ps1

# Job 2 (+ phân tích)
.\scripts\run-daily-jobs.ps1

# Job 3 — API tự chạy nếu OpportunityMonitor.Enabled=true
```

## Lưu ý kỹ thuật

- Job 1 mặc định `endDate` = **phiên giao dịch trước** (T-1), không gồm ngày hôm nay.
- Job 1 cuối run: `UniverseRescreenRunner` (DB only) — loại/khôi phục mã theo giá + thanh khoản.
- Job 2 merge theo `Date` — chỉ mã `IsActive && !TradingRestricted` từ Job 1; **không** gọi KBS listing/rescreen.
- Cuối Job 2: `DarvasBreakoutAlertPublisher` (`DailySessionSyncRunner`) — quét breakout hộp Darvas; trả `DarvasBreakoutAlerts` trong DTO.
- Job 3 chỉ theo dõi mã trong `DailyOpportunities` của phiên đang active.

## Phase 1 — North Star (baseline Top cơ hội)

- Config prod: `MaxResults=10`, `RelaxedFallbackEnabled=false`, `SmartMoney.MinPassScore=62`
- Báo cáo: `GET /api/v1/performance/north-star?days=90` — Hit@T+2.5 theo Top3/5/10 + TradeState + MFE/MAE
- Backtest so sánh: `.\scripts\compare-backtest-max-results.ps1`

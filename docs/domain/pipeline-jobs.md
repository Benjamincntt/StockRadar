# Pipeline jobs (dữ liệu & phân tích)

## Mục đích

Thứ tự và trách nhiệm Job 1 / Job 2 / phân tích daily / monitor VIP / ML·HPO — **as-is** vận hành.

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | Market job controllers / `POST /api/v1/market/jobs/*` | Trigger thủ công |
| 2 | `DailySessionSyncRunner` | Job 2 + Darvas alerts |
| 3 | `DailyAnalysisRunner` | Top + criterion + **cuối**: breadth → ReversalBounce |
| 4 | `OpportunityIntradayMonitorRunner` | Job 3 / VIP ~60s |
| 5 | `docs/architecture.md` | Tổng quan lịch Quartz |

> Khi docs lệch code → **tin code trên disk**.

## Luật as-is

| Job | Khi | Việc | DB / output |
|-----|-----|------|-------------|
| **Job 1** | Thủ công | Listing + backfill OHLCV + universe | `Stocks`, `IsActive` |
| **Job 2** | ~5 phút trong giờ GD (+ cron) | Append nến T; Darvas alert | History ngày T |
| **Phân tích** | ~11:30 + ~15:05 VN | SmartMoney Top; criterion T+2.5; **MarketBreadth + Regime**; **ReversalBounce scan** | `DailyOpportunities`, breadth snapshots, `ReversalCandidateSnapshots` |
| **Monitor** | ~60s trong phiên T+1 | VIP trên Top | Telegram / SignalR / positions |

API tiện: `POST .../jobs/daily` = Job 2 + phân tích. Header `X-Sync-Key`.

**Thứ tự cuối analysis (quan trọng):** breadth/regime (cho sóng hồi) **rồi** `ReversalBounceAnalysisRunner`. Regime **không** ghi đè `MarketWyckoffPhase` của Top tăng trưởng.

Deploy: `.\scripts\ship-all.ps1`. ML/HPO: xem architecture + stub lịch sử `pipeline-jobs` đã gộp Phase 2–3 vào đây (dataset/train/monitor-ranker).

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-PL-1 | Tên endpoint lịch sử `/jobs/daily-pipeline` không còn — dùng `/jobs/daily` | As-is |
| G-PL-2 | Chưa phát hiện thêm mâu thuẫn lịch vs code trong feature này | Cập nhật khi đổi Quartz |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md), [`reversal-bounce.md`](./reversal-bounce.md)
- [`../architecture.md`](../architecture.md), [`../build-and-deploy.md`](../build-and-deploy.md)
- Index: [`../README.md`](../README.md)

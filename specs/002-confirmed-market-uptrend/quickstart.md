# Quickstart kiểm chứng: 002-confirmed-market-uptrend

Mục tiêu: chứng minh Favorable không còn gắn với nảy 1 phiên; FTD+MA20+HL mới ra TT thuận; UX Attempted không đổ lỗi MA Full.

## Điều kiện tiên quyết

- Repo `D:\Source\StockRadar`
- Spec/plan/research đã có trong `specs/002-confirmed-market-uptrend/`
- Sau implement: API có thể chạy analysis; VNINDEX `HistoryJson` đủ ≥ 60 phiên (Job 1/2 đã từng chạy)

## Kịch bản 1: Unit — classifier (ưu tiên CI)

1. Chạy tests Domain cho `MarketPhaseClassifier` (tên theo tasks).
2. Fixture A: 3 phiên xanh, Close < MA20 → `Unfavorable`.
3. Fixture B: Close > MA20, không FTD → `Neutral`.
4. Fixture C: đủ MA20 + slope + FTD ngày 4–7 + Higher Low → `Favorable`.
5. Fixture D: một phiên +2% nhưng thiếu FTD cửa sổ → **không** Favorable.

**Kỳ vọng**: 100% fixture khớp contracts `market-phase-classification.md`.

## Kịch bản 2: Analysis / Top (staging hoặc local)

1. Restart API (`backend/restart-api.ps1`) sau deploy code.
2. `POST /api/v1/market/jobs/analysis` (header Sync-Key) hoặc đợi job lịch.
3. Xem log: pha từ classifier (không chỉ “VNINDEX Uptrend 0.6%”).
4. `GET /api/v1/opportunities` (hoặc endpoint Top hiện dùng mobile).

**Kỳ vọng**:

- Nếu pha `Neutral`: không thấy lý do chính “Chưa đạt MA stack…” vì thị trường chưa Favorable; thay bằng “Chờ xác nhận thị trường chung” khi cổng liên quan MA.
- Nếu pha `Favorable`: MA Full có hiệu lực; fail MA được phép hiện câu MA.

## Kịch bản 3: Domain living

1. Mở `docs/domain/ma-stack-and-market-phase.md`.
2. Xác nhận mô tả Favorable = MA20+FTD+HL; G-MA-1 resolved.
3. Xác nhận không dùng `ChangePercent > 0.5` làm điều kiện đủ Favorable.

## Kịch bản 4: Hồi quy sóng hồi

1. `GET` regime ReversalBounce (endpoint hiện có).
2. So với trước deploy (hoặc test cố định): logic regime không phụ thuộc classifier mới.

**Kỳ vọng**: SC-005 — không đổi hợp đồng regime.

## Kịch bản 5: Backtest SmartMoney (tuỳ chọn)

1. Chạy backtest SmartMoney khoảng ngày có nảy kỹ thuật vs uptrend thật.
2. Xác nhận ngày “nảy giả” không còn hàng loạt Favorable+Full.

## Liên kết

- [spec.md](./spec.md) — SC-001…SC-006  
- [contracts/market-phase-classification.md](./contracts/market-phase-classification.md)  
- [data-model.md](./data-model.md)

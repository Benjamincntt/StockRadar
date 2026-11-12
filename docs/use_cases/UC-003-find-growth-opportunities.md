# Use Case: Tìm cơ hội tăng trưởng (ĐÃ GỠ)

> **⚠ ĐÃ GỠ TOÀN BỘ — spec 016 (`remove-v1-top`), 2026-11.** UC-003 mô tả danh sách Top của V1 (Buy Score + cổng Top + radar trong phiên). Toàn bộ đường này đã xoá:
> `DailyAnalysisRunner`, `BuyDecisionEngine`, `DailyOpportunities`, API `opportunities*` / `market/jobs/analysis`, khối "Cơ hội tốt nhất" trên Home web + mobile, radar `SessionRadarHits`.
> Cách V1 làm: [`../features/v1-archive/01-cong-loc.md`](../features/v1-archive/01-cong-loc.md) · [`02-cham-diem.md`](../features/v1-archive/02-cham-diem.md) · [`03-xep-hang-ml.md`](../features/v1-archive/03-xep-hang-ml.md).
>
> **Thay thế trên Home:** thẻ "Đang giữ" — vị thế V3 từ `GET /api/v1/v3/hieu-qua/vi-the-dang-giu` (FR-018), cộng thẻ VNINDEX và sparkline. Không còn danh sách mã để mua.
>
> Số hiệu UC-003 / BR-005 / BR-006 / BR-007 giữ làm lịch sử, không tái sử dụng.

# Use Case: Theo dõi cảnh báo và kết quả (ĐÃ GỠ)

> **⚠ ĐÃ GỠ — spec 011 (VIP alerting) và spec 016 (`remove-v1-top`), 2026-11.** Phần cảnh báo VIP (`TopOpportunityVipAlertPublisher`, `MasterAlertPositions`, `PositionSellLegs`, bảng `Alerts`) và phần đo outcome V1/V2 (`SetupTracks`, `KetQuaKichBan`, `OpportunityPerformanceRunner`) đã xoá.
> Cách V1 làm: [`../features/v1-archive/04-radar-lo-lon.md`](../features/v1-archive/04-radar-lo-lon.md) · [`05-do-luong.md`](../features/v1-archive/05-do-luong.md).
>
> **Tương đương hiện tại:**
> - Tab **Hiệu quả** — `GET /api/v1/v3/hieu-qua/tom-tat` · `/lich-su` · `/vi-the-dang-giu` (vị thế V3).
> - Màn **Nhật ký** — `GET /api/v1/nhat-ky/lenh` · `/vi-the` · `/thong-ke` · `/tin-hieu/v3`.
>
> Số hiệu UC-006 / BR-012 / BR-013 giữ làm lịch sử, không tái sử dụng.

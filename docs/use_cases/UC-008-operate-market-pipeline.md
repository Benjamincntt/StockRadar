# Use Case: Vận hành pipeline thị trường

## Tổng quan

**Mã Use Case:** UC-008
**Tên Use Case:** Vận hành pipeline thị trường
**Tác nhân chính:** Vận hành
**Mục tiêu:** Giữ dữ liệu thị trường mới (Job 1, Job 2, KBS sync), chạy 4 nhịp bộ kịch bản V3 và theo dõi sức khoẻ job.
**Trạng thái:** Implemented (bỏ phần V1 — spec 016)

## Điều kiện tiên quyết

- Người vận hành (hoặc Quartz) được phép chạy job: JWT admin hoặc header `X-Sync-Key` khớp `MarketData:SyncApiKey` (`JobTriggerAuthorization`).
- Đã cấu hình truy cập nhà cung cấp dữ liệu thị trường (KBS).

## Luồng thành công chính

1. Bộ lập lịch hoặc người vận hành kích hoạt Job 1 (`POST /api/v1/market/jobs/history` hoặc `/history/night`) để làm mới universe + lịch sử; rescreen universe chạy riêng (`/universe-rescreen`, chỉ đọc DB).
2. Trong khung bảng giá (08:59–11:31 · 12:59–15:10 ngày giao dịch), `kbs-market-sync` đẩy giá vào `QuoteTickCache` và SignalR mỗi 60 giây.
3. Sau đóng cửa, Job 2 chạy 15:05 (trigger riêng), thử lại 15:15/15:30 chỉ cho mã còn thiếu nến; nến ngày T (gồm ATC) được merge vào `Stocks.HistoryJson`.
4. Bốn nhịp V3 tự chạy: 08:30 trước phiên · mỗi 1 phút 09:00–14:45 trong phiên · 14:25 ATC · 16:00 sau phiên — ghi `TinHieuV3`, `ViTheV3`, `ChiSoNgayV3`.
5. Nhịp 16:00 đối chiếu Nhật ký giao dịch (`V3DoiChieuNhatKy`) khi `V3:TaiKhoanDanhMuc` đã đặt.
6. Người vận hành xem trạng thái job qua `GET /api/v1/market/jobs` và `/history/status` (nguồn `JobRunStatuses`, ghi bởi `JobStatusService`).

Chi tiết giờ chạy và file nguồn: [`../domain/pipeline-jobs.md`](../domain/pipeline-jobs.md).

## Luồng thay thế

### A1: Lỗi nhà cung cấp dữ liệu

**Kích hoạt:** Gọi KBS thất bại (bước 1–3)
**Luồng:**

1. Hệ thống ghi nhận thất bại của job trong `JobRunStatuses` và log.
2. Job 2 thử lại 15:15/15:30 cho các mã còn thiếu nến; hết lượt thì chờ phiên sau.
3. Use case kết thúc ở trạng thái thất bại hoặc một phần.

### A2: Không phải ngày giao dịch

**Kích hoạt:** Trigger rơi vào ngày nghỉ (`DailySessionSyncDecision`)
**Luồng:**

1. `kbs-market-sync` bỏ qua (ngoài `IsInQuoteSyncWindow`).
2. Job 2 trả `SkipNotTradingDay`, không ghi nến.
3. Use case kết thúc.

### A3: Chạy tay ngoài khung

**Kích hoạt:** Người vận hành gọi `POST /market/jobs/kbs-sync` ngoài khung bảng giá
**Luồng:**

1. Bộ ghi chặn tạo nến hôm nay ngoài khung (và gọi tay trước 14:45) — không ghi dữ liệu sai phiên.
2. Use case kết thúc mà không đổi `HistoryJson`.

## Điều kiện hậu quả

### Khi thành công

- `Stocks.HistoryJson` có nến ngày T (gồm ATC) trước nhịp V3 16:00.
- `TinHieuV3` / `ViTheV3` / `ChiSoNgayV3` phản ánh đúng các nhịp đã chạy.
- Nhà giao dịch thấy dữ liệu mới trên UC-002 / UC-005 và tab Hiệu quả.

### Khi thất bại

- Job thất bại không ghi đè dữ liệu cũ bằng số bịa; lỗi nằm trong `JobRunStatuses`.

## Quy tắc nghiệp vụ

### BR-016: Thứ tự nhịp dữ liệu

Job 2 phải chạy xong trước 16:00 vì nhịp V3 sau phiên đọc nến ngày T để tính chỉ số và `UNI` phiên sau (`DO-NOT-CHANGE` trong `QuartzSchedulingExtensions.cs`). KBS sync chỉ ghi trong khung bảng giá.

### BR-017: Không tự áp tuning

Không còn HPO/ranker tự áp tham số (gỡ cùng V1, spec 016). Đổi ngưỡng kịch bản V3 phải qua Spec Kit.

### BR-018: Bề mặt job

Kích hoạt vận hành nằm ở `market/jobs/*` (header `X-Sync-Key` hoặc admin), không nằm trên màn duyệt của nhà giao dịch.

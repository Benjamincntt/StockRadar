# Use Case: Vận hành pipeline thị trường

## Tổng quan

**Mã Use Case:** UC-008
**Tên Use Case:** Vận hành pipeline thị trường
**Tác nhân chính:** Vận hành
**Mục tiêu:** Người vận hành và tiến trình lập lịch giữ dữ liệu thị trường mới, tạo phân tích tăng trưởng hàng ngày, và hỗ trợ các job đánh giá chiến lược.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Người vận hành (hoặc bộ lập lịch) được phép chạy job vận hành.
- Đã cấu hình truy cập nhà cung cấp dữ liệu thị trường.

## Luồng thành công chính

1. Bộ lập lịch hoặc người vận hành kích hoạt làm mới universe/lịch sử và đồng bộ phiên khi cần.
2. Hệ thống cập nhật cổ phiếu active và lịch sử chỉ số từ nhà cung cấp dữ liệu.
3. Người vận hành hoặc bộ lập lịch chạy phân tích ngày.
4. Hệ thống chấm điểm universe, lưu snapshot Top 5 cơ hội tăng trưởng (gate stats vào `DAILY_ANALYSIS_RUN.gate_stats_json`), radar phục hồi sớm, radar phiên theo cấu hình, sóng ngành và SetupTracks. Danh sách dự phòng nới lỏng (relaxed fallback) đã gỡ.
5. Song song trong phiên, pipeline kịch bản V2 chạy: Pha 1 trước phiên (08:30), Pha 2 trong phiên (mỗi 1 phút, 09:00–14:45), Pha 3 sau phiên (16:00) — kết quả lưu `KET_QUA_KICH_BAN`; Pha 3 đo outcome sau T+3 phiên.
6. Các job sau đo outcome setup (ví dụ T+2.5) và tùy chọn chạy backtest hoặc huấn luyện/tinh chỉnh mô hình ML. Job làm mới độ tin cậy tiêu chí đã dừng — `STOCK_CRITERION_DETAIL` không còn được ghi.
7. Người vận hành xem trạng thái job / ranker (màn Jobs, nguồn `JOB_RUN_STATUS`) khi kiểm tra sức khỏe hệ thống.

## Luồng thay thế

### A1: Lỗi nhà cung cấp dữ liệu

**Kích hoạt:** Gọi nhà cung cấp dữ liệu thất bại (bước 2)
**Luồng:**

1. Hệ thống ghi nhận thất bại của job.
2. Phân tích phía sau có thể bỏ qua hoặc chạy trên dữ liệu cũ theo thiết kế job.
3. Use case kết thúc ở trạng thái thất bại hoặc một phần.

### A2: Cooldown phân tích thủ công

**Kích hoạt:** Người vận hành yêu cầu phân tích lại quá sớm (bước 3)
**Luồng:**

1. Hệ thống từ chối hoặc trì hoãn theo chính sách cooldown.
2. Use case kết thúc mà không tạo snapshot mới.

## Điều kiện hậu quả

### Khi thành công

- Snapshot theo ngày giao dịch và lịch sử TT phản ánh các job đã hoàn tất.
- Nhà giao dịch có thể dùng UC-002–UC-006 trên dữ liệu mới.

### Khi thất bại

- Job thất bại không âm thầm bịa danh sách cơ hội.

## Quy tắc nghiệp vụ

### BR-016: Thứ tự phân tích ngày

Một lần phân tích ngày: quét ứng viên qua data-quality gates + cổng BuyDecisionEngine → xếp hạng (ML + sector bonus) → hygiene → sector cap → Top 5; tiến triển sóng ngành xuyên phiên và đăng ký SetupTracks. Bước "chấm criterion" (ghi `STOCK_CRITERION_DETAIL`) đã dừng; breadth/regime và quét sóng hồi đã gỡ bỏ (spec `008-remove-reversal-bounce`).

### BR-017: Không tự áp tuning

Đề xuất tinh chỉnh siêu tham số hoặc ranker được báo cho vận hành; không tự áp vào cổng production nếu chưa có thao tác vận hành rõ ràng.

### BR-018: Bề mặt job

Kích hoạt vận hành nằm ở bề mặt job thị trường và ML vận hành, không nằm trên màn duyệt của nhà giao dịch.

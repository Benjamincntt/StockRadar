# Use Case: Vận hành pipeline thị trường

## Tổng quan

**Mã Use Case:** UC-008
**Tên Use Case:** Vận hành pipeline thị trường
**Tác nhân chính:** Vận hành
**Mục tiêu:** Người vận hành và tiến trình lập lịch giữ dữ liệu thị trường mới, tạo phân tích tăng trưởng và sóng hồi hàng ngày, và hỗ trợ các job đánh giá chiến lược.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Người vận hành (hoặc bộ lập lịch) được phép chạy job vận hành.
- Đã cấu hình truy cập nhà cung cấp dữ liệu thị trường.

## Luồng thành công chính

1. Bộ lập lịch hoặc người vận hành kích hoạt làm mới universe/lịch sử và đồng bộ phiên khi cần.
2. Hệ thống cập nhật cổ phiếu active và lịch sử chỉ số từ nhà cung cấp dữ liệu.
3. Người vận hành hoặc bộ lập lịch chạy phân tích ngày.
4. Hệ thống chấm điểm universe, lưu snapshot cơ hội tăng trưởng (nghiêm và/hoặc nới), radar phục hồi sớm, radar phiên theo cấu hình, breadth/regime thị trường, và snapshot ứng viên sóng hồi.
5. Các job sau đo outcome setup (ví dụ T+2.5), làm mới độ tin cậy tiêu chí, và tùy chọn chạy backtest hoặc huấn luyện/tinh chỉnh mô hình.
6. Người vận hành xem trạng thái job / ranker khi kiểm tra sức khỏe hệ thống.

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

Trong một lần phân tích ngày, breadth/regime cho sóng hồi được tính song song với khái niệm pha tăng trưởng; quét ứng viên sóng hồi chạy sau khi đã có đầu vào breadth.

### BR-017: Không tự áp tuning

Đề xuất tinh chỉnh siêu tham số hoặc ranker được báo cho vận hành; không tự áp vào cổng production nếu chưa có thao tác vận hành rõ ràng.

### BR-018: Bề mặt job

Kích hoạt vận hành nằm ở bề mặt job thị trường và ML vận hành, không nằm trên màn duyệt của nhà giao dịch.

# Use Case: Nghiên cứu thị trường và cổ phiếu

## Tổng quan

**Mã Use Case:** UC-002
**Tên Use Case:** Nghiên cứu thị trường và cổ phiếu
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch nắm bối cảnh thị trường hiện tại và xem lịch sử giá cùng giải thích quyết định mua của từng mã.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Dữ liệu thị trường và cổ phiếu đã được đồng bộ cho các phiên liên quan (xem UC-008).
- Nhà giao dịch mở được ứng dụng (đã đăng nhập hoặc bề mặt nghiên cứu công khai theo sản phẩm).

## Luồng thành công chính

1. Nhà giao dịch mở tổng quan thị trường.
2. Hệ thống trình bày mức chỉ số, biến động, điểm xu hướng và bối cảnh liên quan.
3. Nhà giao dịch tìm hoặc chọn một mã.
4. Hệ thống hiển thị danh tính mã, biến động gần đây, lịch sử biểu đồ và ngành.
5. Hệ thống trình bày tóm tắt quyết định mua (điểm, checklist, lý do cổng nếu có, trạng thái giao dịch).
6. Nhà giao dịch dùng thông tin đó để quyết định đào sâu thêm hoặc dừng.

## Luồng thay thế

### A1: Không tìm thấy mã

**Kích hoạt:** Tìm kiếm không khớp (bước 3)
**Luồng:**

1. Hệ thống báo không có mã phù hợp.
2. Use case tiếp tục ở bước 3.

### A2: Thiếu lịch sử cho quyết định đầy đủ

**Kích hoạt:** Mã tồn tại nhưng lịch sử/thanh khoản quá mỏng (bước 5)
**Luồng:**

1. Hệ thống vẫn hiện dữ liệu thị trường có sẵn.
2. Hệ thống giải thích quyết định mua chưa đủ hoặc bị chặn cổng (ví dụ thiếu lịch sử).
3. Use case kết thúc thành công với nghiên cứu một phần.

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch đã xem nghiên cứu TT/mã đã chọn.
- Use case này không bắt buộc thay đổi danh mục.

### Khi thất bại

- Nếu thiếu dữ liệu TT, người dùng thấy lỗi hoặc trạng thái trống, không có quyết định bịa.

## Quy tắc nghiệp vụ

### BR-003: Code hơn tường thuật cũ

Trường quyết định mua hiển thị phải phản ánh lần đánh giá mới nhất theo lịch sử mã và bối cảnh pha thị trường hiện tại.

### BR-004: Đặt tên hộp phẳng

Giải thích cấu trúc phá nền dùng khái niệm hộp tích lũy phẳng (không dùng ngữ nghĩa thẻ `basePrice` cũ).

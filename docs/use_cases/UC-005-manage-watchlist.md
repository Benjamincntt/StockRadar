# Use Case: Quản lý danh sách theo dõi

## Tổng quan

**Mã Use Case:** UC-005
**Tên Use Case:** Quản lý danh sách theo dõi
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch giữ shortlist mã cá nhân để xem lại nhanh.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Nhà giao dịch đã đăng nhập (UC-001), kể cả khách nếu sản phẩm cho phép watchlist khách.

## Luồng thành công chính

1. Nhà giao dịch mở danh sách theo dõi.
2. Hệ thống hiện các mã đã lưu trước đó.
3. Nhà giao dịch thêm mã từ tìm kiếm hoặc chi tiết mã.
4. Hệ thống lưu mã vào danh sách của người đó.
5. Nhà giao dịch có thể gỡ mã khi không còn quan tâm.
6. Hệ thống cập nhật danh sách tương ứng.

## Luồng thay thế

### A1: Thêm trùng

**Kích hoạt:** Mã đã có trong danh sách (bước 3)
**Luồng:**

1. Hệ thống giữ một mục duy nhất cho mã đó.
2. Use case tiếp tục ở bước 2.

### A2: Chưa đăng nhập

**Kích hoạt:** Không có phiên (bước 1)
**Luồng:**

1. Hệ thống yêu cầu xác thực (UC-001).
2. Use case kết thúc hoặc tiếp tục sau đăng nhập.

## Điều kiện hậu quả

### Khi thành công

- Thành viên danh sách khớp thao tác thêm/gỡ gần nhất của nhà giao dịch.

### Khi thất bại

- Danh sách của người dùng khác không đổi.

## Quy tắc nghiệp vụ

### BR-011: Sở hữu danh sách theo dõi

Mỗi mục thuộc đúng một người dùng và một mã.

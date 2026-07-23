# Use Case: Nhật ký và hiệu chỉnh giao dịch

## Tổng quan

**Mã Use Case:** UC-007
**Tên Use Case:** Nhật ký và hiệu chỉnh giao dịch
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch ghi lại quyết định giao dịch cá nhân và xem phản hồi hiệu chỉnh so với engine.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Nhà giao dịch đã đăng nhập (UC-001).

## Luồng thành công chính

1. Nhà giao dịch mở nhật ký giao dịch.
2. Hệ thống liệt kê các mục nhật ký trước đó của người đó.
3. Nhà giao dịch thêm một mục (mã, hành động, ghi chú, tỷ trọng, ngữ cảnh engine tùy chọn).
4. Hệ thống lưu mục đó.
5. Nhà giao dịch có thể mở tóm tắt hiệu chỉnh cá nhân để xem quyết định của mình so với hướng dẫn engine theo thời gian.

## Luồng thay thế

### A1: Thiếu trường bắt buộc

**Kích hoạt:** Mục thiếu mã hoặc hành động bắt buộc (bước 3)
**Luồng:**

1. Hệ thống từ chối lưu và yêu cầu bổ sung trường bắt buộc.
2. Use case tiếp tục ở bước 3.

## Điều kiện hậu quả

### Khi thành công

- Các mục nhật ký được lưu cho nhà giao dịch.
- Trạng thái hiệu chỉnh cá nhân có thể cập nhật khi sản phẩm tính từ lịch sử nhật ký.

### Khi thất bại

- Mục không hợp lệ không được lưu.

## Quy tắc nghiệp vụ

### BR-014: Bảo mật nhật ký

Mục nhật ký thuộc phạm vi người dùng sở hữu.

### BR-015: Engine verdict là ngữ cảnh tùy chọn

Engine verdict trên một dòng nhật ký là ngữ cảnh mô tả; hành động nhà giao dịch ghi lại mới là nguồn lịch sử cá nhân.

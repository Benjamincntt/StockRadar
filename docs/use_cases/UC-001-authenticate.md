# Use Case: Xác thực

## Tổng quan

**Mã Use Case:** UC-001
**Tên Use Case:** Xác thực
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch có phiên đăng nhập để dùng tính năng cá nhân (danh sách theo dõi, nhật ký).
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Ứng dụng truy cập được.
- Nhà giao dịch có email/mật khẩu, hoặc chọn chế độ khách nếu sản phẩm cho phép.

## Luồng thành công chính

1. Nhà giao dịch mở màn hình đăng nhập.
2. Nhà giao dịch cung cấp thông tin đăng nhập (hoặc tiếp tục với tư cách khách khi được phép).
3. Hệ thống kiểm tra thông tin.
4. Hệ thống ghi nhận đăng nhập và đưa người dùng về trải nghiệm chính với tư cách đã xác thực (hoặc khách).

## Luồng thay thế

### A1: Sai thông tin đăng nhập

**Kích hoạt:** Thông tin không khớp tài khoản đã đăng ký (bước 3)
**Luồng:**

1. Hệ thống từ chối đăng nhập.
2. Hệ thống báo xác thực thất bại.
3. Use case kết thúc.

### A2: Đăng ký tài khoản mới

**Kích hoạt:** Nhà giao dịch chọn tạo tài khoản thay vì đăng nhập (bước 1)
**Luồng:**

1. Nhà giao dịch gửi email, tên hiển thị và mật khẩu.
2. Hệ thống ghi nhận tài khoản mới.
3. Use case tiếp tục từ bước 4 luồng chính (hoặc quay lại đăng nhập tùy sản phẩm).

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch được hệ thống nhận diện cho các thao tác cá nhân tiếp theo.
- Tài khoản tồn tại với người dùng không phải khách.

### Khi thất bại

- Không có phiên; dữ liệu cá nhân không bị lộ qua lần thử đó.

## Quy tắc nghiệp vụ

### BR-001: Email duy nhất

Mỗi email tài khoản đăng ký phải duy nhất.

### BR-002: Cờ khách

Có thể tồn tại danh tính khách không đủ quyền đăng ký đầy đủ; hệ thống phân biệt khách và đã đăng ký.

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
2. Hệ thống trình bày mức chỉ số, biến động và biểu đồ chỉ số (nhãn pha thị trường đã gỡ — spec 016).
3. Nhà giao dịch tìm hoặc chọn một mã.
4. Hệ thống hiển thị danh tính mã, biến động gần đây, lịch sử biểu đồ và ngành.
5. Hệ thống trình bày tín hiệu và vị thế V3 mới nhất của mã (`GET /api/v1/stocks/{symbol}/v3`), dòng "mã ngoài universe" nếu có, và sự kiện quyền (`GET /api/v1/stocks/{symbol}/rights-events`). Quyết định mua / Buy Score của V1 đã gỡ (spec 016).
6. Nhà giao dịch dùng thông tin đó để quyết định đào sâu thêm hoặc dừng.

## Luồng thay thế

### A1: Không tìm thấy mã

**Kích hoạt:** Tìm kiếm không khớp (bước 3)
**Luồng:**

1. Hệ thống báo không có mã phù hợp.
2. Use case tiếp tục ở bước 3.

### A2: Dữ liệu mã quá mỏng

**Kích hoạt:** Mã tồn tại nhưng dữ liệu quá mỏng hoặc ngoài universe (bước 5)
**Luồng:**

1. Hệ thống vẫn hiện dữ liệu thị trường có sẵn.
2. Hệ thống giải thích bằng trạng thái universe (`isUniverseActive`, `universeStatusReason`) — không còn cổng chấm điểm nào để giải thích (gỡ theo spec 016).
3. Use case kết thúc thành công với nghiên cứu một phần.

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch đã xem nghiên cứu TT/mã đã chọn.
- Use case này không bắt buộc thay đổi danh mục.

### Khi thất bại

- Nếu thiếu dữ liệu TT, người dùng thấy lỗi hoặc trạng thái trống, không có quyết định bịa.

## Quy tắc nghiệp vụ

### BR-003: Code hơn tường thuật cũ

Trường hiển thị trên chi tiết mã phải phản ánh dữ liệu mới nhất trong `Stocks`, `TinHieuV3`, `ViTheV3`. Cách V1 chấm điểm mua chỉ còn trong [`../features/v1-archive/02-cham-diem.md`](../features/v1-archive/02-cham-diem.md) — không còn code.

### BR-004: Không còn khái niệm hộp phẳng

Hộp tích lũy phẳng / Darvas và API `flatBox` đã gỡ (spec 016); chi tiết mã chỉ còn giá, biểu đồ, V3 và sự kiện quyền.

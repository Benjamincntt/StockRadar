# Use Case: Quản lý danh sách theo dõi

## Tổng quan

**Mã Use Case:** UC-005
**Tên Use Case:** Quản lý danh sách theo dõi (multi-watchlist)
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch giữ **nhiều** danh sách mã (mặc định + tùy chỉnh + danh sách ngành tự động), mỗi mã kèm **cùng một Buy Score** như màn chi tiết mã.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Nhà giao dịch đã đăng nhập (UC-001), kể cả khách nếu sản phẩm cho phép watchlist khách.

## Luồng thành công chính

1. Nhà giao dịch mở tab Watchlist (bottom nav).
2. Hệ thống gọi `GET /api/v1/watchlists`. Lần đầu của mỗi user, hệ thống **lazy seed**: tạo danh sách mặc định + các danh sách ngành theo danh mục ngành chuẩn (`SectorCatalog.DefaultSectors`).
3. Hệ thống hiện các danh sách kèm số mã; nhà giao dịch chọn một danh sách.
4. Hệ thống hiện các mã của danh sách đó, mỗi mã kèm **Buy Score** (0–100) cùng nguồn với chi tiết mã (UC-002 / UC-003). Danh sách ngành hiện toàn bộ mã active của ngành (items động, không lưu DB).
5. Nhà giao dịch thêm mã (từ tìm kiếm hoặc chi tiết mã) vào danh sách thường.
6. Hệ thống lưu mã vào danh sách đó (`PUT /api/v1/watchlists/{id}/items/{symbol}`).
7. Nhà giao dịch có thể gỡ mã khi không còn quan tâm (`DELETE /api/v1/watchlists/{id}/items/{symbol}`).
8. Nhà giao dịch mở một mã từ danh sách và thấy điểm trên chi tiết khớp điểm trên danh sách (cùng thời điểm tải).

Ngoài ra nhà giao dịch có thể tạo danh sách tùy chỉnh (`POST /api/v1/watchlists`), đổi tên (`PATCH /api/v1/watchlists/{id}`) hoặc xóa danh sách tùy chỉnh (`DELETE /api/v1/watchlists/{id}`).

## Luồng thay thế

### A1: Thêm trùng

**Kích hoạt:** Mã đã có trong danh sách đích (bước 5)
**Luồng:**

1. Hệ thống giữ một mục duy nhất cho mã đó **trong mỗi danh sách** (unique `watchlist_id + symbol`).
2. Cùng một mã có thể xuất hiện ở nhiều danh sách khác nhau.
3. Use case tiếp tục ở bước 4.

### A2: Chưa đăng nhập

**Kích hoạt:** Không có phiên (bước 1)
**Luồng:**

1. Hệ thống yêu cầu xác thực (UC-001).
2. Use case kết thúc hoặc tiếp tục sau đăng nhập.

### A3: Thao tác trên danh sách ngành tự động

**Kích hoạt:** Thêm/xóa mã thủ công, đổi tên hoặc xóa một danh sách ngành (bước 5–8)
**Luồng:**

1. Hệ thống từ chối với Bad Request — items của danh sách ngành luôn là toàn bộ mã active của ngành.
2. Use case tiếp tục ở bước 4.

### A4: Xóa danh sách mặc định

**Kích hoạt:** Xóa danh sách mặc định của user (bước 8)
**Luồng:**

1. Hệ thống từ chối với Bad Request ("Không thể xóa danh sách mặc định").
2. Danh sách mặc định vẫn còn nguyên.

## Điều kiện hậu quả

### Khi thành công

- Thành phần danh sách khớp thao tác thêm/gỡ gần nhất của nhà giao dịch.
- Điểm hiển thị trên danh sách là Buy Score tăng trưởng, không phải điểm criterion.
- Danh sách của người dùng khác không đổi.

### Khi thất bại

- Danh sách mặc định / danh sách ngành không bị xóa hay đổi tên.

## Quy tắc nghiệp vụ

### BR-011: Sở hữu danh sách theo dõi

Mỗi danh sách (`WATCHLIST`) thuộc đúng một người dùng. Mỗi mục (`WATCHLIST_ITEM`) thuộc đúng một danh sách và một mã; cùng một mã có thể nằm ở nhiều danh sách. Mỗi user có đúng một danh sách mặc định (`la_mac_dinh`) và không quá một danh sách cho mỗi ngành (unique `user_id + ma_nganh`).

### BR-019: Buy Score trên danh sách theo dõi

Điểm trên mọi danh sách dùng **cùng thang Buy Score** với Top / chi tiết mã (UC-003):
- Mã nằm trong snapshot Top ngày giao dịch đang active → dùng Buy Score đã lưu của snapshot đó.
- Mã không nằm Top → dùng Buy Score tính live từ cùng engine quyết định mua (`BuyDecisionEngine.Evaluate`).
- **Cấm** thay bằng CompositeScore criterion trên cùng pill.

### BR-020: Lazy seeding một lần

Lazy seed chỉ tạo các danh sách còn thiếu (mặc định + ngành); các lần GET sau không tạo lại và giữ nguyên danh sách tùy chỉnh người dùng đã tạo. Danh sách mặc định và danh sách ngành không thể xóa; danh sách ngành không thể đổi tên hay thêm/xóa mã thủ công.

# Use Case: Tìm cơ hội sóng hồi

## Tổng quan

**Mã Use Case:** UC-004
**Tên Use Case:** Tìm cơ hội sóng hồi
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch xem ứng viên sóng hồi (counter-trend) và regime thị trường quyết định ý tưởng đó có actionable hay không.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Breadth / regime và snapshot ứng viên sóng hồi đã được tạo cho phiên (thường sau phân tích ngày — UC-008).
- Nhà giao dịch mở list sóng hồi hoặc chi tiết sóng hồi của mã.

## Luồng thành công chính

1. Nhà giao dịch chuyển sang list sóng hồi trên Home hoặc mở chi tiết sóng hồi của một mã.
2. Hệ thống hiện regime thị trường hiện tại cho giao dịch sóng hồi.
3. Hệ thống liệt kê ứng viên actionable theo tổng điểm, kèm giai đoạn, bằng chứng và kế hoạch giao dịch đơn giản (vào / cắt / đích).
4. Nhà giao dịch mở một mã để xem đánh giá đầy đủ, kể cả phân tích live khi chưa có snapshot.
5. Nhà giao dịch dùng regime + kế hoạch để quyết định setup có khớp rủi ro của mình không.

## Luồng thay thế

### A1: Regime Normal (không thuận sóng hồi)

**Kích hoạt:** Regime thị trường là Normal (bước 2)
**Luồng:**

1. Hệ thống hiện trạng thái trống hoặc giải thích không nhấn mạnh săn sóng hồi.
2. Use case kết thúc mà không đẩy mua sóng hồi actionable.

### A2: Ứng viên không actionable

**Kích hoạt:** Có ứng viên nhưng fail cổng cứng hoặc không actionable (bước 3)
**Luồng:**

1. Hệ thống vẫn có thể hiện chẩn đoán trên màn mã.
2. Ứng viên không xuất hiện như mục actionable trên Home.
3. Use case tiếp tục với các mã khác.

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch hiểu regime sóng hồi và các ứng viên actionable (nếu có).
- Snapshot còn để xem shadow / backtest sau.

### Khi thất bại

- Nội dung sóng hồi không bị trình bày như Top tăng trưởng (UC-003).

## Quy tắc nghiệp vụ

### BR-008: Mô hình regime song song

Regime sóng hồi (Panic / Stabilizing / ReboundConfirmed / Normal) tách khỏi pha tăng trưởng (Favorable / Neutral / Unfavorable). Một bên không được ghi đè bên kia.

### BR-009: List Home chỉ actionable

List sóng hồi trên Home chỉ hiện ứng viên đánh dấu actionable, sắp theo tổng điểm.

### BR-010: Định danh snapshot

Snapshot ứng viên sóng hồi là duy nhất theo ngày giao dịch, mã, phiên bản chiến lược và định danh setup.

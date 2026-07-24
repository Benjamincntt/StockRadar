# Use Case: Tìm cơ hội sóng hồi

## Tổng quan

**Mã Use Case:** UC-004
**Tên Use Case:** Tìm cơ hội sóng hồi
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch xem ứng viên sóng hồi (counter-trend) dưới **cùng nhận định pha thị trường** với Top cơ hội (UC-003), rồi quyết định setup có actionable hay không.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Snapshot ứng viên sóng hồi đã được tạo cho phiên (thường sau phân tích ngày — UC-008); pha TT lấy từ VNINDEX / `MarketPhaseClassifier` (không phụ thuộc breadth label).
- Nhà giao dịch mở list sóng hồi hoặc chi tiết sóng hồi của mã.

## Luồng thành công chính

1. Nhà giao dịch chuyển sang list sóng hồi trên Home hoặc mở chi tiết sóng hồi của một mã.
2. Hệ thống hiện **pha thị trường** (TT thuận / Nỗ lực hồi phục / Điều chỉnh) — cùng nguồn với Top / card VNINDEX; không dùng nhãn breadth “cân bằng / hoảng loạn” làm nhận định TT.
3. Hệ thống liệt kê ứng viên actionable theo tổng điểm, kèm giai đoạn, bằng chứng và kế hoạch giao dịch đơn giản (vào / cắt / đích).
4. Nhà giao dịch mở một mã để xem đánh giá đầy đủ, kể cả phân tích live khi chưa có snapshot.
5. Nhà giao dịch dùng pha TT + kế hoạch để quyết định setup có khớp rủi ro của mình không.

## Luồng thay thế

### A1: Pha TT thuận (không thuận sóng hồi)

**Kích hoạt:** Pha thị trường là Favorable / TT thuận (bước 2)
**Luồng:**

1. Hệ thống báo chưa nên bắt đáy (hoặc giảm nhấn mạnh săn sóng hồi).
2. Use case kết thúc mà không đẩy mua sóng hồi khi TT thuận.

### A2: Ứng viên không actionable

**Kích hoạt:** Có ứng viên nhưng fail cổng cứng hoặc không actionable (bước 3)
**Luồng:**

1. Hệ thống vẫn có thể hiện chẩn đoán trên màn mã.
2. Ứng viên không xuất hiện như mục actionable trên Home.
3. Use case tiếp tục với các mã khác.

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch thấy **cùng pha TT** như Top và các ứng viên sóng hồi actionable (nếu có).
- Snapshot còn để xem shadow / backtest sau.

### Khi thất bại

- Nội dung sóng hồi không bị trình bày như Top tăng trưởng (UC-003) về điểm / cổng Buy Score.

## Quy tắc nghiệp vụ

### BR-008: Một nhận định pha thị trường

Nhãn “Thị trường” trên sóng hồi dùng **cùng** `MarketPhaseClassifier` với Top (Favorable / Neutral / Unfavorable). Breadth `MarketRegime` (Panic / Stabilizing / …) chỉ cho gate/metrics nội bộ — **không** hiện như nhận định TT song song.

### BR-009: List Home — tín hiệu + theo dõi

List sóng hồi trên Home gồm hai khối: **Tín hiệu mua** (actionable / Confirmed đủ plan) và **Theo dõi** (Stage Capitulating / Stabilizing, chưa actionable). Không chỉ hiện actionable.

### BR-010: Định danh snapshot

Snapshot ứng viên sóng hồi là duy nhất theo ngày giao dịch, mã, phiên bản chiến lược và định danh setup.

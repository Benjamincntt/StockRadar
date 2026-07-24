# Use Case: Tìm cơ hội tăng trưởng

## Tổng quan

**Mã Use Case:** UC-003
**Tên Use Case:** Tìm cơ hội tăng trưởng
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch thấy danh sách ngắn các mã vượt quét cơ hội tăng trưởng (SmartMoney / Buy Score) trong phiên, kèm tín hiệu radar sớm và radar phiên liên quan.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Phân tích ngày cho phiên giao dịch đã chạy hoặc có thể được vận hành kích hoạt (UC-008).
- Universe cổ phiếu và lịch sử chỉ số sẵn có.

## Luồng thành công chính

1. Nhà giao dịch mở danh sách “cơ hội tốt nhất” (tăng trưởng) trên Home hoặc màn cơ hội.
2. Hệ thống tải snapshot cơ hội ngày đã lưu cho ngày giao dịch mới nhất.
3. Hệ thống xếp hạng và hiện mã kèm điểm, trạng thái giao dịch và lý do ngắn.
4. Nhà giao dịch có thể mở một mã để xem quyết định mua đầy đủ (UC-002).
5. Tuỳ chọn, nhà giao dịch xem radar phiên và mã phục hồi sớm đã qua lọc xu hướng lỏng nhưng chưa đủ cổng Top.

## Luồng thay thế

### A1: Top nghiêm ngặt trống — nới fallback

**Kích hoạt:** Không mã nào vượt lọc Top nghiêm trong ngày (bước 2)
**Luồng:**

1. Hệ thống có thể hiện danh sách fallback nhỏ hơn khi chế độ đó bật.
2. Nhà giao dịch hiểu kết quả có thể yếu hơn Top nghiêm.
3. Use case tiếp tục ở bước 3.

### A2: Chưa có cơ hội

**Kích hoạt:** Phân tích chưa tạo snapshot (bước 2)
**Luồng:**

1. Hệ thống hiện trạng thái trống hoặc đang chờ.
2. Use case kết thúc.

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch có shortlist hướng tăng trưởng trong ngày.
- Các dòng cơ hội được lưu theo ngày giao dịch để đo hiệu quả sau.

### Khi thất bại

- Không hiện danh sách “chắc chắn thắng” khi thiếu dữ liệu.

## Quy tắc nghiệp vụ

### BR-005: Buy Score và cổng Top

Một mã vào Top nghiêm chỉ khi vượt các cổng Buy Decision đã cấu hình (thanh khoản, phân phối, hộp phẳng/FOMO, MA stack theo pha TT, RS khi Unfavorable, phiên breakout/shakeout, và điểm tối thiểu).

Hiển thị một điểm 0–100 trên Top, chi tiết mã, và danh sách theo dõi (UC-005 / BR-019) — cùng Buy Score; không trộn criterion composite hay điểm sóng hồi.

### BR-006: MA stack theo pha thị trường

Độ chặt MA stack theo pha: Favorable → Full, Neutral → Medium, Unfavorable → Loose (kèm ngưỡng percentile RS và RS5 khi mua ở Unfavorable).

### BR-007: Tách tăng trưởng và sóng hồi

Top tăng trưởng độc lập với ứng viên sóng hồi (counter-trend); không gộp hai list vào một mô hình điểm.

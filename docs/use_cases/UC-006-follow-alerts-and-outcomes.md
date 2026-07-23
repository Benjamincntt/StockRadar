# Use Case: Theo dõi cảnh báo và kết quả

## Tổng quan

**Mã Use Case:** UC-006
**Tên Use Case:** Theo dõi cảnh báo và kết quả
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch xem cảnh báo kịp thời (kể cả hướng dẫn mua kiểu VIP) và sau đó xem kết quả đo được của các ý tưởng đó.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Đã có cảnh báo và/hoặc bản ghi hiệu quả từ các lần quét và job đo trước đó (UC-008).
- Nhà giao dịch mở được màn cảnh báo và hiệu quả.

## Luồng thành công chính

1. Nhà giao dịch mở bảng tin cảnh báo.
2. Hệ thống liệt kê cảnh báo gần đây kèm mã, tiêu đề và ngữ cảnh (khối lượng / sức mạnh tương đối nếu có).
3. Nhà giao dịch mở màn hiệu quả / lịch sử cảnh báo.
4. Hệ thống tóm tắt thắng / ngang / thua theo khoảng thời gian đã chọn với quy tắc outcome của sản phẩm.
5. Nhà giao dịch dùng kết quả để đánh giá độ tin cậy tín hiệu gần đây.

## Luồng thay thế

### A1: Vị thế VIP còn trong các phiên đầu

**Kích hoạt:** Có vị thế mua Master VIP nhưng chưa đến lúc cho phép hướng dẫn bán
**Luồng:**

1. Hệ thống có thể hiện cảnh báo rủi ro mà không ra lệnh bán trước số phiên nắm giữ tối thiểu.
2. Use case tiếp tục ở chế độ theo dõi.

### A2: Không có lịch sử trong khoảng

**Kích hoạt:** Không có lịch sử cảnh báo trong khoảng ngày đã chọn (bước 3)
**Luồng:**

1. Hệ thống hiện trạng thái trống.
2. Use case kết thúc.

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch đã xem cảnh báo và/hoặc tóm tắt outcome đo được.
- Vị thế VIP live vẫn được theo dõi đến khi đóng theo quy tắc vận hành.

### Khi thất bại

- Không gắn nhãn outcome khi chưa chạy đo.

## Quy tắc nghiệp vụ

### BR-012: Số phiên tối thiểu trước hướng dẫn bán

Hướng dẫn bán Master VIP chỉ bắt đầu sau số phiên giao dịch tối thiểu đã cấu hình kể từ vào lệnh; các phiên sớm hơn có thể cảnh báo rủi ro nhưng không được ra lệnh bán.

### BR-013: Nhóm outcome

Outcome cảnh báo đo được dùng ngưỡng Win / Flat / Lose theo sản phẩm hiệu quả (kể cả giả định chi phí khi đã cấu hình).

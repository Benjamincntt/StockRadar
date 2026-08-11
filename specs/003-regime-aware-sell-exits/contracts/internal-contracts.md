# Phase 1 — Contracts

Tính năng không thêm route API và không đổi hợp đồng nào với `mobile/` hay `frontend/`. Ba bề mặt dưới đây là những gì thực sự lộ ra ngoài module.

## C-1. Hàm quyết định tín hiệu bán

Hợp đồng của hàm thuần trong `TopOpportunityVipAlertEvaluator` — đây là điểm neo cho toàn bộ unit test.

**Đầu vào**: tham số cảnh báo, bản ghi vị thế (gồm chế độ, hai cạnh nền, mốc phủ nhận, mốc bắt đầu cửa sổ), dòng bảng giá hiện tại, kết quả quét phân phối, ngày phiên, **pha thị trường hiện tại**, và mốc tham chiếu đã dựng sẵn.

**Đầu ra**: `null` hoặc một trong `CanhBaoRuiRoT0` / `BanNua` / `BanHet`.

**Bất biến**:
- Chưa đủ số phiên tối thiểu ⇒ chỉ có thể trả `CanhBaoRuiRoT0` hoặc `null`, không bao giờ trả kind bán.
- Đã phát `BanNua` ⇒ không phát `BanNua` lần hai cho cùng vị thế.
- Thủng mốc phủ nhận ⇒ trả `BanHet` bất kể mức giảm so với mốc tham chiếu.
- Hàm không đọc đồng hồ, không truy vấn I/O, không phụ thuộc thứ tự gọi ngoài trạng thái truyền vào.

**Thay đổi phá vỡ so với hiện tại**: chữ ký nhận thêm mốc tham chiếu và bỏ phụ thuộc vào `TrailingStopMinPeak`; tham số pha đổi ngữ nghĩa từ pha-lúc-mua sang pha-hiện-tại.

## C-2. Liệt kê hộp phía trên một mức giá

Entry point mới trên `DarvasBreakoutAnalyzer`.

**Đầu vào**: lịch sử OHLCV, tham số hộp dành cho vùng cản, mức giá tham chiếu, độ dài tối thiểu, tuổi tối đa.

**Đầu ra**: hộp gần mức giá nhất trong số hộp đạt chuẩn nằm phía trên, hoặc "không có".

**Bất biến**:
- Không đổi kết quả của `Analyze` và `Evaluate` hiện có — hai hàm này phải cho kết quả y hệt trước và sau thay đổi trên cùng dữ liệu.
- Tiêu chí hình dạng đi qua đúng `BaseQualityEvaluator.PassesDarvasBox`, không có nhánh kiểm tra song song.
- Tham số dùng cho breakout giữ nguyên giá trị; tham số vùng cản là bộ riêng.

## C-3. Nội dung cảnh báo Telegram

Định dạng hiện tại giữ nguyên khung: dòng tiêu đề có biểu tượng, tên mã và nhãn hành động, sau đó là phần lý do, cuối cùng là khối lượng. Phần lý do phải nêu đủ ba thứ theo FR-023.

| Tín hiệu | Tiêu đề | Lý do phải chứa |
|----------|---------|-----------------|
| `BanNua` chế độ Có nền trên | `🟡 <mã>: Bán 1 nửa` | Mục tiêu là cạnh dưới nền, khoảng nền, giá hiện tại |
| `BanNua` chế độ Vượt đỉnh | `🟡 <mã>: Bán 1 nửa` | Mốc tham chiếu, % đã giảm so với mốc, ngưỡng áp dụng, pha |
| `BanHet` do thủng mốc phủ nhận | `🔴 <mã>: Bán hết` | Nêu rõ đã phủ nhận cây vượt đỉnh và mốc bị thủng |
| `BanHet` các trường hợp còn lại | `🔴 <mã>: Bán hết` | Như `BanNua` tương ứng, kèm ngưỡng bán hết |
| `CanhBaoRuiRoT0` | `⚠️ <mã>: CẢNH BÁO RỦI RO T+0` | Mốc đã chạm và câu nêu rõ chưa bán được |

Ràng buộc ngôn ngữ: tin phát trước khi đủ số phiên tối thiểu **không được** chứa chữ "Bán" ở dòng tiêu đề (FR-022).

Endpoint thử nghiệm `POST /api/v1/market-jobs/vip-telegram-test` phải phát mẫu cho cả hai chế độ, thay vì một mẫu bán nửa chung như hiện tại.

## C-4. Khoá cấu hình

Toàn bộ khoá mới nằm dưới section `MasterAlerts` trong `appsettings.json`, liệt kê tại `data-model.md` mục 5. Hợp đồng: đổi giá trị các khoá này phải đổi được hành vi mà không cần build lại, và `docs/architecture.md` phải phản ánh đúng bộ số mặc định.

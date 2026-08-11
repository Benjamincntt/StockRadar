# Phase 0 — Research: Điểm bán 1/2 theo bối cảnh giá

Mọi mục dưới đây đã được xác minh trên code trên disk, không suy luận từ tài liệu.

## R-1. Dùng lại bộ dò hộp thay vì viết detector vùng cản mới

**Decision**: Thêm một entry point trên `DarvasBreakoutAnalyzer` để liệt kê các hộp nằm phía trên một mức giá, dùng lại nguyên `BaseQualityEvaluator.PassesDarvasBox` làm tiêu chí hình dạng.

**Rationale**: `TryFindBoxWindow` gọi `PassesDarvasBox` với `maxBoxHeightPercent` truyền vào chứ không đọc thẳng hằng số, nên nới biên độ cho mục đích vùng cản không đụng tới nhận diện phá vỡ. `minSessions` / `maxSessions` cũng đã là tham số của `Analyze`. Bản thân `PassesDarvasBox` đã kiểm tra đúng thứ cần cho một vùng cản: khung `Close` trong giới hạn, râu nến không vượt biên quá `ShadowTolerancePercent`, và tối thiểu 2 lần chạm mỗi cạnh — chính là phép thử "đi ngang" mà nếu tự viết sẽ trùng lặp logic.

**Alternatives considered**:
- *Detector vùng cản riêng (pivot clustering hoặc volume-at-price)*: chính xác hơn về mặt cung cầu nhưng là engine song song, vi phạm nguyên tắc V và tạo hai định nghĩa "nền" trong cùng sản phẩm.
- *Gọi lại `Analyze` trên lịch sử cắt ngắn*: không cần code mới nhưng phải đoán điểm cắt, và `Analyze` dừng ở hộp đầu tiên tìm được nên không liệt kê được nhiều hộp.

**Ràng buộc phát sinh**: `Analyze` hiện `break` sau hộp gần nhất và luôn đo từ cuối lịch sử; entry point mới phải quét nhiều `boxEnd` và lọc theo mức giá, tái sử dụng phần đo biên hộp thay vì nhân bản.

## R-2. Giới hạn biên độ 15% đo trên giá đóng cửa

**Decision**: `OverheadBoxMaxHeightPercent = 15`, tham số riêng, mặc định breakout giữ nguyên 10%.

**Rationale**: `PassesDarvasBox` tính `coreBoxHeightPct = (maxClose - minClose) / minClose * 100` trên **giá đóng cửa**, còn râu nến được phép vượt biên thêm `ShadowTolerancePercent = 3%`. Một vùng mà người dùng mô tả là "dao động 10–12" thường có khung đóng cửa hẹp hơn đáng kể (cỡ 13–14%) với 10 và 12 là chân/đỉnh râu — nằm gọn trong 15% cộng dung sai râu. Ngưỡng 15% do người dùng chốt.

**Alternatives considered**: 20% (nhận nhiều nền hơn nhưng bắt đầu gom cả những đoạn xu hướng nhẹ); giữ 10% (loại phần lớn vùng cản thực tế của thị trường VN).

## R-3. Một định nghĩa mốc tham chiếu duy nhất

**Decision**: Mốc = `max(High)` trong khoảng từ `max(ngày mua, hôm nay − 20 phiên)` đến phiên hiện tại, gồm cả `row.High` đang chạy.

**Rationale**: Giải quyết cùng lúc ba vấn đề — (a) lệnh mua hồi sâu không bị bắn bán ngay khi cửa sổ bán mở, vì mốc không lấy đỉnh trước ngày mua; (b) không cần luật bảo vệ dự phòng thứ ba; (c) không cần điều khoản riêng cho vị thế chuyển từ chế độ Có nền trên sang Vượt đỉnh. Với lệnh mua đúng lúc vượt đỉnh, mốc trùng với "đỉnh 20 phiên" theo mô tả gốc vì giá vào đã ở vùng đỉnh.

**Alternatives considered**: thuần đỉnh 20 phiên (bắn sai với mua hồi sâu); đỉnh 20 phiên kèm điều kiện "chỉ kích hoạt sau khi giá từng lên sát mốc" (thêm một biến trạng thái mà không mua thêm độ chính xác nào).

**Hệ quả kỹ thuật**: `PeakPriceSinceEntry` hiện có gần đúng nhưng **không** giới hạn cửa sổ 20 phiên và được đo bằng giá chứ không kèm ngày. Cần bổ sung dữ liệu để cắt cửa sổ, xem `data-model.md`.

## R-4. Mẫu số phần trăm chuyển từ giá vốn sang mốc tham chiếu

**Decision**: `drawdownFromAnchor = (anchor − currentPrice) / anchor * 100`.

**Rationale**: Code hiện tại tính `drawdown = peakGain − currentGain`, tức lấy chênh lệch hai tỷ lệ cùng mẫu số là `EntryPrice`. Khi vị thế lãi lớn, cùng một con số phần trăm ứng với biên độ giá khác hẳn ý định "giảm 4% so với giá cao nhất". Đổi mẫu số là điều kiện cần để ngưỡng 4%/6% mang đúng nghĩa.

**Alternatives considered**: giữ mẫu số cũ và hiệu chỉnh ngưỡng theo mức lãi — phức tạp hơn mà vẫn không cố định được ý nghĩa.

## R-5. Nguồn pha thị trường cho vị thế

**Decision**: Dùng pha của phiên hiện tại lấy từ bản ghi Top đã nạp trong vòng quét, fallback `MarketPhaseAtEntry`, cuối cùng `Neutral`.

**Rationale**: Đây là một **thay đổi hành vi**, không phải giữ nguyên. `OpportunityIntradayMonitorRunner` đang truyền `pos.MarketPhaseAtEntry ?? "Neutral"` vào `ProcessPositionAsync`, nên hiện tại pha bị đóng băng tại thời điểm mua. Yêu cầu FR-011 là siết ngưỡng khi thị trường xấu đi giữa kỳ nắm giữ, nên phải lấy pha phiên hiện tại. Runner đã nạp sẵn `topMap` qua `LoadTodayTopMapAsync`, mỗi bản ghi mang `MarketPhase` của phiên và được làm mới ở nhịp refresh Top trong phiên — đủ tươi, không phát sinh truy vấn mới.

**Alternatives considered**: gọi `MarketPhaseClassifier.Classify` ngay trong vòng quét (tốn kém, cần bars VNINDEX mỗi vòng); thêm service pha riêng (thừa, dữ liệu đã có trong luồng).

## R-6. Đảo chiều hệ số pha là an toàn về phạm vi

**Decision**: Đặt Favorable 1.25 / Neutral 1.0 / Unfavorable 0.75, sửa tại `MasterAlertOptions` và `appsettings.json`.

**Rationale**: `MarketPhaseMultipliers` chỉ có hai nơi đọc, cả hai đều thuộc luồng bán — `TopOpportunityVipAlertEvaluator` (quyết định tín hiệu) và `TopOpportunityVipAlertPublisher` (dựng câu lý do). Không có consumer nào ở luồng mua, chấm điểm hay chọn Top, nên đảo chiều không lan sang hợp đồng sản phẩm khác. Hai file tài liệu đang chép lại bộ số cũ phải sửa cùng change set.

## R-7. Chống nhiễu bằng xác nhận nhiều chu kỳ

**Decision**: Yêu cầu giá giữ qua ngưỡng trong `SellConfirmationTicks` vòng quét liên tiếp trước khi phát, mặc định 2, đếm riêng theo cặp mã + loại tín hiệu.

**Rationale**: Luồng mua đã có tiền lệ `RequiredConfirmationTicks = 3` cho cùng vấn đề, nên dùng lại khuôn thay vì phát minh cơ chế mới. Ngưỡng bán ở pha Unfavorable siết còn 3% trong khi biên độ HOSE là ±7%, không có xác nhận sẽ bắn dày. Chọn 2 thay vì 3 vì tín hiệu bán chậm tốn kém hơn tín hiệu mua chậm.

**Alternatives considered**: nới ngưỡng theo ATR (che mất ý định "chợ xấu bán sớm"); chỉ đánh giá trên giá đóng cửa phiên (mất khả năng cảnh báo trong phiên).

## R-8. Di trú vị thế đang mở

**Decision**: Vị thế mở tại thời điểm triển khai được phân loại lại ở lần chạm đầu tiên của monitor: cột chế độ để trống nghĩa là "chưa phân loại", runner sẽ dò nền trên theo giá vốn đã lưu rồi ghi kết quả.

**Rationale**: Migration không thể tự tính chế độ vì cần lịch sử giá. Phân loại lười tránh backfill và tự khỏi khi mọi vị thế cũ đóng lại. `EntryDate` và `EntryPrice` đã có sẵn nên đủ đầu vào.

**Alternatives considered**: backfill trong migration (cần truy cập dữ liệu giá từ migration — sai tầng); đóng cưỡng bức vị thế cũ (mất vị thế thật của người dùng).

## R-9. Cách xác định "phủ nhận hoàn toàn cây vượt đỉnh"

**Decision**: Lưu giá thấp nhất của phiên mở vị thế làm mốc phủ nhận; giá xuống dưới mốc đó thì bán hết ngay, bỏ qua ngưỡng phần trăm.

**Rationale**: Người dùng xác nhận cách hiểu "phủ nhận hoàn toàn cây nến" là giá quay xuống dưới chân cây nến đó. `row.Low` có sẵn trong bảng giá đang quét, không cần nguồn dữ liệu mới. Với vị thế nâng từ Mua 1 lên Mua 2 trong cùng phiên, mốc giữ nguyên theo phiên mở vị thế gốc, thống nhất với cách `EntryDate` và `EntryPrice` đang được giữ.

**Alternatives considered**: dùng giá mở cửa của cây breakout (lỏng hơn, phản ứng chậm); dùng cạnh trên của hộp bị phá (không áp dụng được cho vị thế không có hộp).

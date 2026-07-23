# Research: Canon tài liệu domain

## Quyết định 1: Dùng `docs/README.md` làm cửa vào duy nhất

**Decision**: Tạo `docs/README.md` làm mục lục cấp 1 cho toàn bộ tài liệu sản phẩm.

**Rationale**: Hiện tại luật domain bị rải giữa `CLAUDE.md`, `architecture.md`, các doc feature và doc kỹ thuật. Một cửa vào duy nhất giảm thời gian dò đường và ngăn tài liệu cũ tiếp tục được coi là “đúng”.

**Alternatives considered**:

- Dùng `CLAUDE.md` làm index chính: bị loại vì đây là map agent, không phải tài liệu sản phẩm.
- Dùng `docs/architecture.md` làm index: bị loại vì tài liệu này là kiến trúc tổng quan, không đủ vai trò phân loại living/archive/AIUP.

## Quyết định 2: Tách lớp tài liệu thành Living / AIUP / Archive / Map

**Decision**: Chuẩn hóa bốn lớp rõ ràng:

- `docs/domain/*`: luật sản phẩm living
- `docs/use_cases/*` + `docs/entity_model.md`: artifact AIUP
- `docs/_archive/*`: proposal/audit/lịch sử
- `CLAUDE.md`, `.continue/rules/*`: map agent

**Rationale**: Đây là cách duy nhất để một file không phải vừa là “đang đúng”, vừa là “tham chiếu lịch sử”, vừa là “gợi ý agent”.

**Alternatives considered**:

- Giữ mọi thứ ở `docs/` top-level: bị loại vì tiếp tục phân mảnh.
- Xóa luôn artifact AIUP sau khi trích luật: bị loại vì mất lớp phân tích/ngôn ngữ nghiệp vụ hữu ích cho spec sau này.

## Quyết định 3: Viết domain docs theo chủ đề luật, không theo file code

**Decision**: `docs/domain/*` sẽ gom theo khái niệm nghiệp vụ:

- `buy-decision.md`
- `ma-stack-and-market-phase.md`
- `base-price-flatbox.md`
- `pipeline-jobs.md`
- `reversal-bounce.md`

**Rationale**: Người đọc cần tìm “luật Buy Score” hay “MA stack theo pha”, không cần biết nó đang nằm ở 4 file code hay 3 file docs cũ.

**Alternatives considered**:

- Một file mega-doc duy nhất: bị loại vì quá dài và khó duy trì.
- Một doc cho mỗi code file/engine: bị loại vì cấu trúc thiên kỹ thuật, không phải canon sản phẩm.

## Quyết định 4: Mỗi domain doc phải có mục “As-is” và “Khoảng trống / mâu thuẫn”

**Decision**: Mọi domain doc living đều ghi rõ:

1. Nguồn code entry đã đối chiếu
2. Luật as-is hiện tại
3. Khoảng trống / mâu thuẫn đã biết
4. Tài liệu liên quan / artifact AIUP liên quan

**Rationale**: StockRadar hiện có nhiều điểm “ý định mong muốn” khác “code đang chạy”, đặc biệt quanh MA stack / market phase. Canon phải trung thực với as-is trước khi trở thành nền cho thay đổi mới.

**Alternatives considered**:

- Chỉ viết “luật mong muốn”: bị loại vì sẽ làm người đọc tưởng production đã đúng.
- Nhét gap vào ghi chú rải rác: bị loại vì khó review.

## Quyết định 5: Giảm `CLAUDE.md` và Continue rules thành map ngắn

**Decision**: Giữ `CLAUDE.md` và `.continue/rules/stockradar.md` như bản đồ định hướng, trỏ tới constitution + `docs/README` + vài entry files chủ lực.

**Rationale**: Các file map agent dễ trôi theo thời gian và bị dùng sai vai trò như “luật domain”.

**Alternatives considered**:

- Giữ bảng cổng đầy đủ trong `CLAUDE.md`: bị loại vì tạo duplicate truth.
- Xóa hẳn map agent: bị loại vì vẫn cần định hướng nhanh cho agent mới.

## Quyết định 6: Không tạo contract API/runtime mới; chỉ tạo contract điều hướng tài liệu

**Decision**: Feature này không phát sinh API/runtime contract. Thay vào đó sẽ có một contract điều hướng tài liệu mô tả vai trò từng lớp file và đường dẫn mục tiêu.

**Rationale**: Đây là feature tài liệu nội bộ, không thay giao diện sản phẩm ra bên ngoài.

**Alternatives considered**:

- Bỏ qua contracts hoàn toàn: hợp lý nhưng yếu trong kiểm tra deliverable; contract điều hướng giúp tasks rõ hơn.

## Quyết định 7: `architecture.md` giữ vai trò tổng quan, không là canon cổng domain

**Decision**: `docs/architecture.md` tiếp tục là overview kiến trúc/pipeline lớn, còn cổng domain chuyển sang `docs/domain/*`.

**Rationale**: Một overview tốt nên liên kết, không nên ôm chi tiết từng ngưỡng và ngoại lệ.

**Alternatives considered**:

- Dồn toàn bộ luật vào `architecture.md`: bị loại vì tài liệu phình to và khó review.

## Quyết định 8: Archive là bước bắt buộc trước khi gom canon

**Decision**: Proposal/audit/path cũ phải được chuyển sang `docs/_archive/` hoặc xóa bản trùng trước khi viết `docs/README`.

**Rationale**: Nếu không dọn nhiễu trước, `docs/README` mới vẫn sẽ trỏ tới môi trường lẫn lộn.

**Alternatives considered**:

- Viết canon trước, dọn archive sau: bị loại vì dễ còn link sai và duplicate truth trong lúc chuyển giao.

# Quickstart kiểm chứng: Canon tài liệu domain

Mục tiêu của quickstart này là chứng minh bộ canon tài liệu mới hoạt động end-to-end cho người đọc và AI agent, **không đổi hành vi runtime**.

## Điều kiện tiên quyết

- Đang ở root repo `D:\Source\StockRadar`
- Đã có artifact AIUP (`docs/use_cases*`, `docs/entity_model.md`)
- Đã archive tài liệu lịch sử vào `docs/_archive/`

## Kịch bản xác nhận 1: Tìm đúng tài liệu sống từ cửa vào duy nhất

1. Mở `docs/README.md`.
2. Xác nhận có các nhóm sau:
   - Canon domain living
   - Artifact AIUP
   - Archive lịch sử
   - Governance / Spec Kit
3. Từ `docs/README.md`, đi tới từng chủ đề:
   - Buy Score / Top
   - MA stack & market phase
   - Flat box / base-price
   - Pipeline jobs
   - ReversalBounce

**Kết quả kỳ vọng**:

- Mỗi chủ đề đi tới đúng **một** file `docs/domain/*.md`
- Không có hơn một tài liệu “đang đúng” cho cùng chủ đề

## Kịch bản xác nhận 2: Đối chiếu một mâu thuẫn đã biết

1. Mở `docs/domain/ma-stack-and-market-phase.md`.
2. Tìm mục “Khoảng trống / mâu thuẫn”.
3. Xác nhận có nêu rõ case uptrend một phiên có thể đẩy Favorable → Full dù nhiều mã vẫn fail MA stack.
4. Đối chiếu nhanh với entry files được link trong doc (ví dụ `BuyDecisionEngine`, `SmartMoneyOpportunitySelector`, `SignalAnalyzer`).

**Kết quả kỳ vọng**:

- Tài liệu mô tả đúng as-is
- Gap được ghi nhận rõ, không giả vờ production đã sửa

## Kịch bản xác nhận 3: Tách growth và rebound

1. Mở `docs/domain/buy-decision.md` và `docs/domain/reversal-bounce.md`.
2. Xác nhận growth dùng `MarketWyckoffPhase` / MA stack, còn rebound dùng `MarketRegime` riêng.
3. Mở `docs/use_cases/UC-003-find-growth-opportunities.md` và `docs/use_cases/UC-004-find-rebound-opportunities.md` để xem lớp AIUP hỗ trợ.

**Kết quả kỳ vọng**:

- Hai hệ được mô tả độc lập
- Domain doc chỉ trích lớp AIUP, không bị thay thế bởi AIUP

## Kịch bản xác nhận 4: Map agent đã thu gọn

1. Mở `CLAUDE.md`.
2. Xác nhận file này đóng vai trò bản đồ ngắn, không ôm chi tiết đầy đủ của mọi luật domain.
3. Mở `.continue/rules/stockradar.md`.
4. Xác nhận cả hai đều trỏ về constitution + `docs/README.md` / domain canon.

**Kết quả kỳ vọng**:

- Map agent không còn là luật sản phẩm chi tiết
- Người đọc được dẫn về đúng nơi sống của domain

## Kịch bản xác nhận 5: Archive không còn được dùng như luật sống

1. Mở `docs/_archive/README.md`.
2. Chọn một file archive, ví dụ proposal hoặc audit phase0a.
3. Xác nhận thư mục archive được mô tả là lịch sử / proposal / audit.

**Kết quả kỳ vọng**:

- Archive vẫn tra cứu được
- Không gây hiểu nhầm là tài liệu living hiện tại

## Kiểm tra liên kết thủ công tối thiểu

- `README.md` repo → `docs/build-and-deploy.md`
- `docs/architecture.md` → link đúng đến `_archive/` khi nói về tài liệu cũ
- `docs/features/reversal-bounce/*` → link đúng đến `_archive/phase0a-*` nếu còn tham chiếu audit
- Constitution / rules → `docs/architecture.md`, không trỏ path cũ

## Thành công khi

- `docs/README.md` trở thành điểm vào duy nhất
- Ít nhất 5 file `docs/domain/*.md` tồn tại và có mục gaps
- Không còn link path cũ gây lạc hướng ở các map chính
- Artifact AIUP vẫn đọc được và được phân vai rõ ràng

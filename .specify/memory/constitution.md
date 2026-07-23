<!--
Sync Impact Report
- Version change: 1.0.0 → 1.0.1 (PATCH: Việt hóa toàn văn, không đổi nguyên tắc)
- Modified principles: giữ I–V (tiêu đề song ngữ ngắn / nội dung tiếng Việt)
- Added sections: không
- Removed sections: không
- Templates:
  - .specify/templates/plan-template.md — ⚠ Constitution Check vẫn tiếng Anh (có thể Việt hóa sau)
  - spec/tasks templates — ⚠ không đổi
- Follow-up TODOs:
  - docs/README.md + docs/domain/* (feature 001)
  - Việt hóa plan-template Constitution Check nếu cần
-->

# Hiến pháp StockRadar (JUICE)

## Nguyên tắc cốt lõi

### I. Code là nguồn sự thật runtime

Hành vi runtime do **code trên disk** quyết định, không phải tóm tắt chat, dashboard graph, gói Repomix hay tài liệu cũ.

- Khi `CLAUDE.md`, Continue rules, graph Understand-Anything hoặc `docs/` lệch với triển khai → **tin code**, rồi sửa bản đồ.
- Agent PHẢI đọc file entry liên quan (runner, engine, controller, màn hình) trước khi khẳng định pipeline, cổng hay điểm.
- Docs và map agent là **hỗ trợ điều hướng**; PHẢI được sửa sau thay đổi thiết kế, không coi là sự thật thực thi.

**Lý do**: Monorepo đổi nhanh; “luật hiện tại” ảo từ context cũ gây cổng sai và UX sai.

### II. Spec trước khi đổi thiết kế (BẮT BUỘC)

Thay đổi thiết kế sản phẩm/engine trọng yếu PHẢI đi Spec Kit trước khi implement.

Trọng yếu gồm ít nhất một trong: Buy Score / cổng Top, MA stack hoặc pha thị trường, Base Price / flatBox / Darvas, thứ tự job/pipeline hoặc runner, route API mới hoặc hợp đồng điều hướng mobile, ngữ nghĩa chiến lược ReversalBounce.

Đường đi bắt buộc:

1. Tuân thủ hiến pháp (file này)
2. `/speckit-specify` (what / why — không bàn vòng stack)
3. `/speckit-clarify` khi còn mơ hồ
4. `/speckit-plan` → `/speckit-tasks` → tùy chọn `/speckit-analyze`
5. `/speckit-implement` chỉ khi đã có artifact trên

Sửa bug và chỉnh cục bộ nhỏ theo nguyên tắc III; CÓ THỂ bỏ qua full feature spec **chỉ khi** không đổi ngữ nghĩa cổng/điểm đã công bố.

**Lý do**: Đổi luật theo chat đã làm phân mảnh MA stack, pha và chọn Top. Spec khôi phục ý định bền.

### III. Thay đổi tối thiểu xâm lấn

Chỉ đổi **bề mặt tối thiểu** đạt ý định đã duyệt.

- KHÔNG refactor, đổi tên hay “dọn” code lân cận khi fix hoặc ship feature hẹp.
- Bám naming, layering, thư viện hiện có; KHÔNG thêm dependency nếu plan chưa biện minh.
- Với bug: nêu Change Plan (file, ý định, rủi ro, cách test) trước khi sửa; ưu tiên diff phẫu thuật. Xem thêm `.specify/memory/bug-fix-constitution.md`.
- Lượt chỉ phân tích / “lỗi gì?” KHÔNG được sửa code đến khi user xác nhận (vd. “fix đi”, “implement”, “apply phương án X”).

**Lý do**: Mỗi dòng thừa là rủi ro hồi quy điểm và UX giao dịch.

### IV. Kỷ luật cổng domain giao dịch

Buy Decision, Top cơ hội, cảnh báo và điểm liên quan là **hợp đồng sản phẩm**.

- Đổi cổng, ngưỡng, mapping pha→độ chặt, hoặc điểm đạt PHẢI cập nhật domain living doc và artifact `specs/` trong **cùng change set**.
- Entry engine chuẩn: `BuyDecisionEngine`, `SmartMoneyOpportunitySelector`, `SignalAnalyzer`, `DarvasBreakoutAnalyzer`, `DailyAnalysisRunner`. Ưu tiên hơn tóm tắt phụ.
- `MarketWyckoffPhase` (pro-trend / MA stack) và `MarketRegime` của ReversalBounce là hệ **song song** — KHÔNG gộp hay ghi đè thầm.
- Nhãn UI và tên field API PHẢI khớp (vd. `flatBox`, không ngữ nghĩa thẻ `basePrice` cũ).

**Lý do**: Cổng sai làm Top trống thầm hoặc dẫn sai điểm vào lệnh.

### V. Đơn giản và ngữ cảnh tập trung

Ưu tiên thiết kế nhỏ nhất đủ spec (YAGNI).

- Agent PHẢI Grep/Read có mục tiêu (thường 2–5 file), không quét cả repo. KHÔNG đọc `mobile/build/`, `node_modules/`, `bin/`, `obj/`, `.dart_tool/`.
- KHÔNG chạy `dotnet build` / `flutter analyze` cả solution cho sửa 1–2 file trừ khi user yêu cầu hoặc CI cần.
- KHÔNG bịa abstraction song song khi Domain service/runner đã sở hữu concern đó.
- Độ phức tạp vi phạm các quy trên PHẢI ghi vào **Complexity Tracking** của plan kèm lý do.

**Lý do**: Lãng phí token và over-abstraction tái tạo phân mảnh mà hiến pháp muốn chặn.

## Ranh giới stack & repository

- **Monorepo**: API .NET (`backend/StockRadar.*`), Flutter (`mobile/`), React (`frontend/`), script ops (`scripts/`).
- **API**: REST `/api/v1`; dev local `:5280`; production theo host deploy đã tài liệu hóa. Ưu tiên controller/DTO hiện có.
- **Logic domain** nằm ở Domain engines; Infrastructure runners điều phối job; Application services mở use case — KHÔNG đẩy luật chấm điểm vào code chỉ-UI.
- **Sau đổi backend cần API sống**: agent restart bằng `backend/restart-api.ps1` (không nhắc user tự restart).
- **Ship production**: `scripts/ship-all.ps1` khi user yêu cầu ship; KHÔNG lấy GitHub Actions làm đường chính.
- **Bí mật**: không commit credential; thận trọng thư mục agent theo ghi chú Spec Kit.

## Quy trình Spec Kit & tài liệu

- Feature dùng `specs/NNN-short-name/` (spec → plan → tasks → implement → converge).
- Chuẩn bị BA nặng CÓ THỂ nằm `docs/features/<kebab>/` (quyết định, BR, baseline, câu hỏi mở) **trước** `/speckit-specify`, theo mẫu UCX Bước 0–5.
- Luật sản phẩm living thuộc `docs/` (hướng tới `docs/domain/`); proposal/audit một lần thuộc archive, không cạnh tranh sự thật.
- `CLAUDE.md` và `.continue/rules` là **bản đồ ngắn** trỏ hiến pháp, Spec Kit và entry — KHÔNG nhân đôi bảng cổng đầy đủ.
- Sau đổi pipeline/API/routing trọng yếu, cập nhật map ngắn (và domain docs) trong cùng lần giao; chỉ chạy graph UA structural khi hình dạng kiến trúc đổi.

## Quản trị

- Hiến pháp này **ưu tiên hơn** thói quen chat và docs cũ lệch quy trình.
- **Sửa đổi**: sửa `.specify/memory/constitution.md`, tăng phiên bản (MAJOR = bỏ/định nghĩa lại nguyên tắc; MINOR = thêm nguyên tắc/mục; PATCH = làm rõ chữ), đặt **Last Amended** = hôm nay (`YYYY-MM-DD`), và làm mới Constitution Check trong template plan nếu cổng đổi.
- **Tuân thủ**: mọi `/speckit-plan` PHẢI vượt Constitution Check trước Phase 0; vi phạm cần dòng Complexity Tracking.
- **Review**: PR đổi cổng, điểm hoặc pipeline PHẢI trích `specs/` liên quan hoặc ngoại lệ hiến pháp rõ ràng.
- Bản đồ runtime: `CLAUDE.md`. Review kiến trúc sâu: `docs/architecture.md`.

**Version**: 1.0.1 | **Ratified**: 2026-07-23 | **Last Amended**: 2026-07-23

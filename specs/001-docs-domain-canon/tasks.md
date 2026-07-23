# Tasks: Canon tài liệu domain

**Input**: Design documents from `/specs/001-docs-domain-canon/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Không yêu cầu automated tests trong spec — kiểm chứng thủ công theo `quickstart.md`

**Organization**: Tasks gom theo user story để implement và kiểm thử độc lập

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Chạy song song được (file khác nhau, không phụ thuộc task chưa xong)
- **[Story]**: User story (US1…US5)
- Mỗi task có đường dẫn file cụ thể

## Path Conventions

Feature này chỉ chạm lớp tài liệu:

```text
docs/
├── README.md
├── domain/
├── _archive/
├── architecture.md
├── use_cases/
└── entity_model.md
CLAUDE.md
.continue/rules/stockradar.md
.cursor/rules/
specs/001-docs-domain-canon/
```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Khởi tạo khung thư mục và template domain doc

- [X] T001 Tạo thư mục `docs/domain/` theo `specs/001-docs-domain-canon/plan.md`
- [X] T002 [P] Tạo skeleton `docs/domain/_template.md` với các mục bắt buộc: Mục đích, Nguồn đối chiếu (code entry), Luật as-is, Khoảng trống / mâu thuẫn, Tài liệu liên quan
- [X] T003 [P] Xác nhận `docs/_archive/README.md` đã mô tả đúng vai trò archive (lịch sử / không phải luật sống) theo `contracts/documentation-navigation.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Nền tảng chặn mọi user story — khung index + quy ước supersede

**⚠️ CRITICAL**: Không bắt đầu US1–US5 cho đến khi phase này xong

- [X] T004 Tạo bản nháp `docs/README.md` với 4 nhóm bắt buộc (Domain living, AIUP, Archive, Governance/Spec Kit) theo `contracts/documentation-navigation.md` — chưa cần link domain đầy đủ
- [X] T005 [P] Thêm quy tắc cập nhật vào `docs/README.md`: đổi cổng trọng yếu phải cập nhật `docs/domain/*` trong cùng change set (SC-006 / FR-008)
- [X] T006 [P] Ghi chú trong `docs/_archive/README.md` và/hoặc `docs/README.md`: khi docs lệch code → tin code trên disk
- [X] T007 Liệt kê map supersede (file living top-level cũ → `docs/domain/*`) trong `docs/README.md` hoặc bảng tạm trong cùng file: `opportunity-scan-rules.md`, `smartmoney-checklist.md`, `buy-score-display.md`, `base-price-engine.md`, `pipeline-jobs.md`, `telegram-vip-alerts-flow.md`, `features/reversal-bounce/*`

**Checkpoint**: Foundation sẵn — có thể viết domain docs và hoàn thiện index

---

## Phase 3: User Story 1 — Tìm một cửa vào luật sản phẩm (Priority: P1) 🎯 MVP

**Goal**: `docs/README.md` là cửa vào duy nhất; người mới biết living vs archive vs AIUP trong ≤2 phút

**Independent Test**: Chỉ mở `docs/README.md`; nêu được thư mục living canon và thứ không phải sự thật runtime

### Implementation for User Story 1

- [X] T008 [US1] Hoàn thiện `docs/README.md`: mục Domain living với placeholder 5 chủ đề (buy-decision, ma-stack-and-market-phase, base-price-flatbox, pipeline-jobs, reversal-bounce)
- [X] T009 [P] [US1] Hoàn thiện mục AIUP trong `docs/README.md` trỏ `docs/use_cases.puml`, `docs/use_cases/UC-*.md`, `docs/entity_model.md`
- [X] T010 [P] [US1] Hoàn thiện mục Archive trong `docs/README.md` trỏ `docs/_archive/` + tóm tắt loại nội dung đã archive
- [X] T011 [P] [US1] Hoàn thiện mục Governance trong `docs/README.md` trỏ `.specify/memory/constitution.md`, `specs/001-docs-domain-canon/`, `docs/architecture.md`
- [X] T012 [US1] Thêm mục Ops ngắn trong `docs/README.md` trỏ `docs/build-and-deploy.md`, `docs/ai-context.md`
- [X] T013 [US1] Kiểm tra thủ công SC-001 một phần: từ `docs/README.md` đi được tới đúng nhóm cho mỗi chủ đề (link có thể tạm trỏ file cũ nếu domain chưa viết — ghi chú “sẽ supersede”)

**Checkpoint**: US1 MVP — cửa vào duy nhất dùng được

---

## Phase 4: User Story 2 — Đọc luật tăng trưởng và MA/pha tại một chỗ (Priority: P1)

**Goal**: Hai (hoặc ba) domain doc living cho Buy/Top + MA/pha + flatBox; as-is + gaps (gồm uptrend 1 phiên → Full)

**Independent Test**: Chỉ dùng domain growth + MA/pha giải thích vì sao blue-chip fail MA dù index xanh mạnh

### Implementation for User Story 2

- [X] T014 [P] [US2] Viết `docs/domain/buy-decision.md` as-is từ `BuyDecisionEngine.cs`, `SmartMoneyOpportunitySelector.cs`, `DailyAnalysisRunner.cs` + gộp nội dung living từ `docs/opportunity-scan-rules.md`, `docs/smartmoney-checklist.md`, `docs/buy-score-display.md`
- [X] T015 [P] [US2] Viết `docs/domain/ma-stack-and-market-phase.md` as-is từ `SignalAnalyzer.HasBullishMaStack`, `BuyDecisionEngine.ResolveMaStackStrictness`, `SmartMoneyOpportunitySelector.ClassifyMarket` + config `SmartMoney:MaStack` trong `backend/StockRadar.Api/appsettings.json`
- [X] T016 [P] [US2] Viết `docs/domain/base-price-flatbox.md` as-is từ `DarvasBreakoutAnalyzer.cs` + gộp `docs/base-price-engine.md`
- [X] T017 [US2] Trong `docs/domain/ma-stack-and-market-phase.md` thêm mục **Khoảng trống / mâu thuẫn** gồm gap Uptrend 1 phiên (`ChangePercent > 0.5` → Favorable → Full) vs ý định đa phiên (SC-005)
- [X] T018 [P] [US2] Trong `docs/domain/buy-decision.md` và `docs/domain/base-price-flatbox.md` thêm mục **Khoảng trống / mâu thuẫn** (có thể “chưa phát hiện thêm” nhưng mục bắt buộc có)
- [X] T019 [US2] Cập nhật link Domain living trong `docs/README.md` trỏ đúng 3 file domain mới (T014–T016)
- [X] T020 [US2] Thêm stub chuyển hướng hoặc dòng superseded ở đầu `docs/opportunity-scan-rules.md`, `docs/smartmoney-checklist.md`, `docs/buy-score-display.md`, `docs/base-price-engine.md` trỏ `docs/domain/*` tương ứng (chưa bắt buộc move vào archive trong task này)

**Checkpoint**: US2 — luật growth/MA/flatBox living tại một chỗ

---

## Phase 5: User Story 3 — Tách rõ mô hình sóng hồi và tăng trưởng (Priority: P2)

**Goal**: Domain rebound living; cấm gộp regime ↔ pha; cross-link UC-003/UC-004

**Independent Test**: Từ docs liệt kê 4 regime sóng hồi và 3 pha tăng trưởng, khẳng định độc lập

### Implementation for User Story 3

- [X] T021 [US3] Viết `docs/domain/pipeline-jobs.md` as-is từ `docs/pipeline-jobs.md` + `docs/architecture.md` (phần job) + entry `DailyAnalysisRunner` / MarketJobs — gồm thứ tự breadth → ReversalBounce
- [X] T022 [P] [US3] Viết `docs/domain/reversal-bounce.md` as-is từ `docs/features/reversal-bounce/reversal-bounce.md` (rút gọn living) + link AIUP UC-004; nêu rõ `MarketRegime` ≠ `MarketWyckoffPhase`
- [X] T023 [US3] Trong `docs/domain/reversal-bounce.md` và `docs/domain/buy-decision.md` (hoặc ma-stack) thêm câu cấm gộp hai hệ chấm điểm (BR-007/BR-008)
- [X] T024 [P] [US3] Thêm mục **Khoảng trống / mâu thuẫn** vào `docs/domain/pipeline-jobs.md` và `docs/domain/reversal-bounce.md`
- [X] T025 [US3] Cập nhật `docs/README.md` link đủ 5 domain files; stub superseded trên `docs/pipeline-jobs.md` và ghi chú trên `docs/features/reversal-bounce/reversal-bounce.md` trỏ `docs/domain/reversal-bounce.md`
- [X] T026 [P] [US3] Cross-link nhẹ từ `docs/domain/buy-decision.md` / `reversal-bounce.md` tới `docs/use_cases/UC-003-find-growth-opportunities.md` và `UC-004-find-rebound-opportunities.md` (không chép nguyên UC)

**Checkpoint**: US3 — growth vs rebound tách rõ trong canon

---

## Phase 6: User Story 4 — Thu nhỏ bản đồ agent thành con trỏ (Priority: P2)

**Goal**: `CLAUDE.md` và Continue rules chỉ còn map ngắn; luật chi tiết nằm ở `docs/domain/*`

**Independent Test**: Tìm bảng cổng Buy Score chi tiết → nằm dưới `docs/domain/`, không phải thân chính `CLAUDE.md`

### Implementation for User Story 4

- [X] T027 [US4] Thu gọn `CLAUDE.md`: giữ cấu trúc monorepo ngắn + governance pointer; thay khối “Quyết định mua / điểm” dài bằng link tới `docs/README.md` và các `docs/domain/*`; bỏ essay ReversalBounce implementation dài
- [X] T028 [P] [US4] Thu gọn `.continue/rules/stockradar.md` tương tự — map ngắn + link canon/constitution
- [X] T029 [P] [US4] Cập nhật `.cursor/rules/ai-context-hygiene.mdc` (nếu còn trỏ doc cũ) để ưu tiên `docs/domain/base-price-flatbox.md` / `docs/README.md` thay `docs/base-price-engine.md` làm living
- [X] T030 [US4] Đảm bảo `CLAUDE.md` nhắc: đổi cổng trọng yếu → Spec Kit + cập nhật `docs/domain/*` cùng change set

**Checkpoint**: US4 — agent maps không còn duplicate gate tables

---

## Phase 7: User Story 5 — Lưu trữ nhiễu mà không xóa lịch sử (Priority: P3)

**Goal**: Archive rõ ràng; living top-level cũ không còn cạnh tranh; AIUP giữ và index

**Independent Test**: Mở `docs/_archive/`; proposal/audit có ngữ cảnh lịch sử; AIUP vẫn đọc được từ README

### Implementation for User Story 5

- [X] T031 [US5] Rà soát living top-level đã superseded: move vào `docs/_archive/` hoặc giữ stub 5–10 dòng “superseded by docs/domain/…” cho `opportunity-scan-rules.md`, `smartmoney-checklist.md`, `buy-score-display.md`, `base-price-engine.md`, `pipeline-jobs.md`, `telegram-vip-alerts-flow.md` (chọn một chiến lược nhất quán — ưu tiên stub ngắn tại chỗ + bản đầy đủ đã nằm domain)
- [X] T032 [P] [US5] Cập nhật `docs/_archive/README.md` liệt kê nhóm đã archive + link thay thế living nếu có
- [X] T033 [P] [US5] Xác nhận `docs/use_cases/` + `docs/entity_model.md` không bị archive; vẫn được liệt kê trong `docs/README.md` là lớp AIUP
- [X] T034 [US5] Grep path cũ (`system-architecture`, `DEPLOY-GDATA`, `cursor-token-saving`, `phase0a-audit-reports` ngoài `_archive`) trong `CLAUDE.md`, `README.md`, `.cursor/rules/`, `.continue/rules/`, `docs/architecture.md` — sửa link còn sót

**Checkpoint**: US5 — không còn hai tài liệu “đang đúng” cho cùng chủ đề cổng

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Kiểm chứng end-to-end và đồng bộ nhẹ

- [X] T035 Chạy checklist thủ công theo `specs/001-docs-domain-canon/quickstart.md` (5 kịch bản)
- [X] T036 [P] Đồng bộ `docs/architecture.md`: mục “Tài liệu liên quan” trỏ `docs/README.md` + `docs/domain/*` thay path cũ
- [X] T037 [P] Cập nhật `specs/001-docs-domain-canon/checklists/requirements.md` ghi chú “plan+tasks xong; sẵn sàng implement”
- [X] T038 Xác nhận SC-001…SC-006 trên repo sau khi T035 pass; ghi kết quả ngắn vào cuối `docs/README.md` hoặc checklist Notes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bắt đầu ngay
- **Foundational (Phase 2)**: Phụ thuộc Setup — **BLOCKS** mọi user story
- **US1 (Phase 3)**: Sau Foundational — MVP cửa vào
- **US2 (Phase 4)**: Sau Foundational; nên sau hoặc song song muộn với US1 (cần README để gắn link)
- **US3 (Phase 5)**: Sau Foundational; độc lập nội dung với US2 nhưng nên sau US1 để cập nhật README
- **US4 (Phase 6)**: Nên sau US2+US3 (cần domain files tồn tại để link)
- **US5 (Phase 7)**: Sau US2+US3 (supersede sau khi domain sẵn); có thể song song phần T034 với US4
- **Polish (Phase 8)**: Sau các story đã chọn hoàn thành

### User Story Dependencies

- **US1 (P1)**: Sau Phase 2 — không phụ thuộc story khác
- **US2 (P1)**: Sau Phase 2 — độc lập nội dung; T019 phụ thuộc T008–T012 nếu muốn link sạch
- **US3 (P2)**: Sau Phase 2 — độc lập với US2 về nội dung rebound/pipeline
- **US4 (P2)**: Phụ thuộc soft vào US2+US3 (có gì để trỏ)
- **US5 (P3)**: Phụ thuộc soft vào US2+US3 (có living thay thế)

### Parallel Opportunities

- T002 ∥ T003
- T005 ∥ T006
- T009 ∥ T010 ∥ T011
- T014 ∥ T015 ∥ T016
- T018 (hai file) có thể song song nội dung gaps
- T021 tuần tự trước T025; T022 ∥ T021
- T027 tuần tự; T028 ∥ T029
- T032 ∥ T033
- T036 ∥ T037

---

## Parallel Example: User Story 2

```text
# Song song ba domain docs growth:
Task: "Viết docs/domain/buy-decision.md …"
Task: "Viết docs/domain/ma-stack-and-market-phase.md …"
Task: "Viết docs/domain/base-price-flatbox.md …"

# Sau đó tuần tự:
Task: "Thêm gap MA stack (SC-005) …"
Task: "Cập nhật docs/README.md links …"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup
2. Phase 2 Foundational
3. Phase 3 US1 → **STOP** — validate cửa vào `docs/README.md`
4. Demo: người mới mở README trong 2 phút

### Incremental Delivery

1. Setup + Foundational
2. US1 → cửa vào
3. US2 → growth/MA/flatBox canon
4. US3 → pipeline + rebound tách growth
5. US4 → thu gọn agent maps
6. US5 → supersede/archive dứt điểm
7. Polish + quickstart

### Parallel Team Strategy

- Sau Phase 2: Dev A = US1+US4 maps; Dev B = US2 domain growth; Dev C = US3 rebound/pipeline
- US5 + Polish gom cuối

---

## Notes

- [P] = file khác nhau, không phụ thuộc chưa xong
- Không tạo automated test tasks (spec không yêu cầu)
- Không đổi code runtime Buy Score / MA / Top trong feature này
- Archive phase0a đã làm một phần trước tasks — T031–T034 hoàn tất phần còn lại
- Commit theo nhóm task hoặc theo checkpoint story

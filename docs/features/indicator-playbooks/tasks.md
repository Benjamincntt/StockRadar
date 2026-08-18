---
description: "Task list — Quy hoạch lại chỉ báo theo Playbook"
---

# Tasks: Quy hoạch lại chỉ báo theo Playbook

**Input**: [`spec.md`](./spec.md), [`plan.md`](./plan.md)

**Tests**: Có — spec đổi ngữ nghĩa điểm, constitution §IV coi đây là hợp đồng sản phẩm. Test bắt buộc cho classifier và cho regression SC-004.

**Format**: `[ID] [P?] [Story] Mô tả` — `[P]` = chạy song song được (khác file, không phụ thuộc).

---

## Phase 1: Setup

- [ ] T001 Tạo nhánh `004-indicator-playbooks` từ `master`.
- [ ] T002 Thêm `PlaybookDimensionEnabled` (default `false`) vào `backend/StockRadar.Application/Options/CriterionAccuracyOptions.cs` và `backend/StockRadar.Api/appsettings.json`. Cờ rollback theo plan §Rollback.
- [ ] T003 [P] Tạo thư mục test `backend/StockRadar.Tests/Playbook/`.
- [ ] T004 Đồng bộ default `RequireTrendSetup` trong `backend/StockRadar.Domain/ValueObjects/AnalysisResults.cs:8` từ `true` → `false` cho khớp `appsettings.json:107` (quyết định Q6). Đây là **sửa lệch âm thầm**, không đổi hành vi runtime hiện tại.

---

## Phase 2: Foundational (chặn mọi user story)

**⚠️ Không story nào bắt đầu được trước khi phase này xong.**

- [ ] T005 Tạo `backend/StockRadar.Domain/Enums/PlaybookId.cs`: `BreakoutDarvas`, `PullbackMa20`, `ReversalBounce`, `Unclassified`, `Legacy`. Dùng string id ổn định (`breakout-darvas`, …) khi ghi DB, **không** ghi giá trị int.
- [ ] T006 Mở rộng `BuyDecisionEvaluation` (`backend/StockRadar.Domain/Services/BuyDecisionEngine.cs:17`) thêm các cờ đã tính sẵn trong `Evaluate`: `HasFlatBoxBreakout`, `HasBreakoutEntry`, `HasFlatBoxSetup`, `HasMaStack`. **Chỉ expose**, không đổi logic chấm điểm (constitution §III).
- [ ] T007 Tạo `backend/StockRadar.Domain/Services/PlaybookClassifier.cs` + `IPlaybookClassifier`. Gán **độc quyền** theo thứ tự: `breakout-darvas` (`HasFlatBoxBreakout || HasBreakoutEntry`) → `pullback-ma20` (`HasMaStack && !HasFlatBoxBreakout && !HasBreakoutEntry && !HasFlatBoxSetup`) → `reversal-bounce` (stage từ `ReversalBounceAnalyzer`) → `unclassified`.
- [ ] T008 Đăng ký `IPlaybookClassifier` trong `backend/StockRadar.Application/DependencyInjection.cs`.
- [ ] T009 [P] Test `backend/StockRadar.Tests/Playbook/PlaybookClassifierTests.cs`: (a) mã vừa breakout vừa MA stack → `breakout-darvas`; (b) MA stack thuần → `pullback-ma20`; (c) mã trong setup zone Darvas **không** rơi vào `pullback-ma20`; (d) không khớp gì → `unclassified`.
- [ ] T010 Thêm `PlaybookId` vào entity: cột trên `StockCriterionDetailEntity`, cột **+ composite key** trên `DailyCriterionAccuracyEntity` và `CriterionGroupDailyAccuracyEntity` (`backend/StockRadar.Infrastructure/Persistence/Entities/CriterionEntities.cs`).
- [ ] T011 Cập nhật `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs`: key `{AsOfDate, Horizon, PlaybookId, CriterionId}` (dòng 187) và `{AsOfDate, Horizon, PlaybookId, GroupId}` (dòng 204); `StockCriterionDetails` (dòng 224) **giữ key cũ**, chỉ thêm cột + `HasMaxLength(24)`. Thêm index `{AsOfDate, PlaybookId}`.
- [ ] T012 Sinh migration `AddCriterionPlaybookDimension`. **⚠️ Trong repo này `dotnet ef migrations add` sinh body sai — BẮT BUỘC mở file migration đọc lại và sửa tay trước khi apply.** Default cho hàng cũ: `'legacy'` (quyết định Q4).
- [ ] T013 Cập nhật `backend/StockRadar.Infrastructure/Persistence/Repositories/EfCriterionScoringRepository.cs`: các hàm `Replace*`/`Get*Accuracy*` nhận và lọc theo `playbookId`.
- [ ] T014 Thêm `PlaybookId` vào value object: `StockCriterionDetailRecord`, `CriterionAccuracySnapshot`, `CriterionGroupAccuracySnapshot` (`backend/StockRadar.Domain/ValueObjects/CriterionScores.cs`).

**Checkpoint**: schema + classifier sẵn sàng; chưa đổi số liệu nào.

---

## Phase 3: User Story 1 — Đo chỉ báo đúng sân (P1) 🎯 MVP

**Goal**: mỗi chỉ báo được chấm accuracy/edge trên đúng playbook của nó, có baseline riêng.

**Independent Test**: chạy hậu kiểm một ngày → `DailyCriterionAccuracies` có hàng tách theo `PlaybookId`, mỗi playbook có baseline riêng khác nhau; Top không đổi.

- [ ] T015 [US1] `backend/StockRadar.Domain/Services/TrendSetupEvaluator.cs:62` — `MeasureOutcome` nhận cấu hình theo playbook (horizon, target MFE, mức vô hiệu hóa) thay vì một bộ chung.
- [ ] T016 [US1] Bảng cấu hình outcome theo playbook: `breakout-darvas` T+2.5 / MFE ≥3% / đáy hộp; `pullback-ma20` T+5 / MFE ≥4%; `reversal-bounce` T+3 / MFE ≥3% + RS vs VN. Đặt trong Options để chỉnh không cần build.
- [ ] T017 [US1] `backend/StockRadar.Domain/Services/CriterionMetricsCollector.cs` — `RecordBaseline` và `Record` gom theo `(criterion × playbook × phase)`; baseline tính **trong từng playbook**.
- [ ] T018 [US1] `backend/StockRadar.Infrastructure/MarketData/DailyCriterionScoringRunner.cs:210-321` — gọi `IPlaybookClassifier` cho mỗi mã, truyền `playbookId` xuống `MeasureOutcome` / `collector.Record` / `detailRecords`. Bọc bằng cờ `PlaybookDimensionEnabled`; cờ tắt → ghi `unclassified`.
- [ ] T019 [US1] `backend/StockRadar.Application/Services/CriterionScoringService.cs` + `DTOs/MarketDtos.cs` — `CriteriaSummaryDto` trả dữ liệu nhóm theo playbook (thêm `playbookId` vào `CriterionAccuracyDto`, thêm danh sách playbook + baseline mỗi playbook).
- [ ] T020 [US1] Test `backend/StockRadar.Tests/Playbook/PlaybookOutcomeTests.cs`: cùng một mã cho ra outcome khác nhau khi playbook khác nhau (horizon/target khác).
- [ ] T021 [US1] **Regression SC-004**: test/kịch bản chạy `DailyAnalysisRunner` full trên cùng ngày dữ liệu trước/sau, assert danh sách Top (symbol + Buy Score) **giống hệt**.

**Checkpoint**: US1 chạy độc lập được — số liệu đã đúng sân, UI vẫn cũ.

---

## Phase 4: User Story 2 — Bundle hết mù (P2)

**Goal**: bỏ bundle trung bình cộng chồng lấn; giữ VSA/POC-Delta/SMC làm thành phần có ngưỡng thật.

**Independent Test**: bảng xếp hạng không còn hai dòng chồng lấn >50% thành phần (SC-003); bundle còn lại trả `Neutral` khi thiếu thành phần thay vì điểm 65.

- [ ] T022 [US2] `backend/StockRadar.Domain/Services/IndicatorBundleScorer.cs:19-29` — ngừng phát `BundleBeginner`, `BundleIntermediate`, `BundleAdvanced`. **Giữ nguyên giá trị int trong `CriterionType`** (đánh dấu `[Obsolete]`, không xóa) để hàng DB cũ không lệch mapping.
- [ ] T023 [US2] Chuyển `BundleProfessional` / `BundleInstitutional` / `BundleSmartMoneyConcept` sang gate/veto: thiếu bất kỳ thành phần nào → `Neutral` + clarity 0, thay cho `Math.Round(items.Average(...))` (dòng 47, 131, 162).
- [ ] T024 [US2] Gắn 3 bundle còn lại vào playbook tương ứng thay vì xếp hạng độc lập: VSA → `breakout-darvas`; POC+Delta → `reversal-bounce`; SMC → `breakout-darvas`.
- [ ] T025 [US2] Test `backend/StockRadar.Tests/Playbook/BundleGateTests.cs`: thiếu thành phần → `Neutral`; đủ và đồng thuận → `Bullish`.

**Checkpoint**: US1 + US2 độc lập chạy được.

---

## Phase 5: User Story 3 — Màn hình đọc được (P3)

**Goal**: người dùng thấy đúng bộ chỉ báo cho cách đánh của mình, không đọc nhầm "cao = nên mua".

**Independent Test**: mở màn hình → có tab theo playbook, mỗi tab hiện edge so với baseline của chính playbook đó; không còn cột trộn hướng với độ rõ.

- [ ] T026 [P] [US3] `mobile/lib/core/models/models.dart` — thêm `playbookId`, tách `direction` / `clarity` trong `CriterionAccuracy`; thêm model baseline theo playbook.
- [ ] T027 [US3] `mobile/lib/screens/criteria_screen.dart` — thay 3 nhóm cứng (`_indicatorMaxRank` / `_bundleMaxRank` dòng 12–13, `_criterionGroup` dòng 150–166) bằng `TabBar` theo playbook.
- [ ] T028 [US3] Tách hai trục ở tầng hiển thị: cột hướng (Tăng/Giảm/Trung tính) riêng, cột độ rõ riêng. Bỏ `_scoreColor` ngưỡng 55/45 áp lên số trộn nghĩa (dòng 481).
- [ ] T029 [P] [US3] Gỡ `_bundleComponents` cho 3 bundle trình độ (dòng 15–22).
- [ ] T030 [P] [US3] Sửa typo `Rũi ro` → `Rủi ro` (`mobile/lib/screens/criteria_screen.dart:408`).

**Checkpoint**: cả 3 story độc lập hoạt động.

---

## Phase 6: Polish & tài liệu

- [ ] T031 Cập nhật `docs/domain/buy-decision.md` — ghi rõ chỉ báo **không** vào Buy Score, playbook là chiều đo lường. **Bắt buộc cùng change set** (constitution §IV).
- [ ] T032 [P] Cập nhật `docs/features/indicator-playbooks/spec.md` — đổi Q1–Q6 từ "câu hỏi mở" sang "đã chốt", trỏ sang `plan.md`.
- [ ] T033 [P] Thêm dòng index cho feature này vào `docs/README.md` và bảng "Luật sản phẩm → đọc domain" trong `CLAUDE.md`.
- [ ] T034 Chạy `backend/restart-api.ps1`; verify `GET /api/v1/criteria/summary` trả cấu trúc playbook.
- [ ] T035 Bật `PlaybookDimensionEnabled = true` sau khi verify; theo dõi ≥2 tuần trước khi kết luận SC-002.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: không phụ thuộc.
- **Foundational (P2)**: chặn tất cả story. T010→T011→T012→T013 tuần tự (schema). T005–T009 song song được với nhau.
- **US1 (P3)**: cần Foundational xong. Là MVP — dừng ở đây vẫn có giá trị.
- **US2 (P4)**: cần Foundational; độc lập với US1 nhưng nên sau để số liệu bundle mới đo trên sân đúng.
- **US3 (P5)**: cần US1 (DTO có `playbookId`).
- **Polish (P6)**: sau các story mong muốn.

### Trong từng story

- Test viết trước, phải **FAIL** trước khi implement.
- Entity/VO → repository → service → runner → DTO → UI.

### Parallel Opportunities

- T003, T009 song song trong Foundational.
- T026, T029, T030 song song trong US3.
- T032, T033 song song trong Polish.

---

## Implementation Strategy

### MVP trước (US1)

1. Phase 1 Setup → Phase 2 Foundational.
2. Phase 3 US1.
3. **DỪNG và VERIFY**: chạy T021 regression — Top phải giống hệt trước/sau.
4. Bật cờ, theo dõi số liệu 1 tuần.

### Incremental

US1 (số đúng sân) → US2 (bundle hết mù) → US3 (UI đọc được) → chỉ khi SC-002 đạt mới bàn S5 (ML/veto).

---

## Notes

- Ship: `.\scripts\ship-all.ps1 -Message "..."` khi user yêu cầu.
- **Không** auto-apply bất kỳ thay đổi nào lên Buy Score / Top / Telegram trong change set này.
- Mỗi task commit riêng hoặc theo nhóm logic; dừng được ở mọi checkpoint.

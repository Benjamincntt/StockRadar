# Implementation Plan: Quy hoạch lại chỉ báo theo Playbook

**Branch**: `004-indicator-playbooks` (đề xuất) | **Date**: 2026-08-18 | **Spec**: [`spec.md`](./spec.md)

**Input**: `docs/features/indicator-playbooks/spec.md` (BA prep, Draft)

> **Ghi chú quy trình**: file này soạn tay theo `.specify/templates/plan-template.md` vì `/speckit-plan` không chạy được ở session hiện tại (`.claude/commands/` trống). Khi chạy Spec Kit từ terminal interactive, dùng file này làm input Phase 1.

## Summary

Chỉ báo kỹ thuật hiện là nhánh cụt: 16/25 dòng trên màn hình **Phân tích chỉ báo** được tính, lưu DB, vẽ UI rồi bị bỏ (`AdaptiveScoringProfile.cs:66` — `TryGetValue` fail thì `continue`). Kế hoạch này **không** nối chúng vào Buy Score. Thay vào đó nó sửa **thước đo**: thêm chiều `PlaybookId` để mỗi chỉ báo được chấm trên đúng sân đánh của nó, với outcome và baseline riêng theo playbook.

Cách tiếp cận: nhân đúng pattern `Horizon` đã có (cột thật + nằm trong composite key) cho `PlaybookId`, thêm `IPlaybookClassifier` gán **độc quyền** 1 playbook/mã/phiên, và cho `MeasureOutcome` nhận cấu hình theo playbook. Buy Score / Top / Telegram VIP **không đổi hành vi**.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`); Dart / Flutter SDK `>=3.3.0 <4.0.0`

**Primary Dependencies**: EF Core 10.0.0 (SqlServer), Quartz 3.13.1; Flutter + provider + go_router

**Storage**: SQL Server `StockRadarDb` — bảng `DailyCriterionAccuracies`, `CriterionGroupDailyAccuracies`, `StockCriterionDetails`, `CriterionWeights`

**Testing**: xUnit tại `backend/StockRadar.Tests/` (đã có nhóm `MarketPhase`, `ReversalBounce`, `SellExit`, `VipAlerts`, `RealizedPnl`)

**Target Platform**: API .NET tự host (prod `103.226.248.6`, dev `:5280`); Flutter Android

**Project Type**: monorepo — backend service + mobile app

**Constraints**:

- Buy Score / Top / Telegram VIP **không được đổi hành vi** (SC-004).
- Không phá chế độ intraday light: `DailyAnalysisRunner.RunAsync` có `runPostProcessing` / `includeStructureAndTracking`; refresh 15' gọi cả hai `false`.
- Mọi thay đổi hành vi phải có cờ config để rollback.

**Scale/Scope**: ~25 `CriterionType` × 3 playbook × 3 pha TT × N horizon; universe vài trăm mã/phiên.

## Constitution Check

*GATE: phải qua trước Phase 0. Nguồn: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code as truth**: plan trích entry thật kèm dòng — `BuyDecisionEngine.cs:39/69/78`, `TrendSetupEvaluator.cs:37/62`, `DailyCriterionScoringRunner.cs:251`, `AdaptiveScoringProfile.cs:66`, `ApplicationDbContext.cs:187/224`. Không lấy CLAUDE.md làm sự thật runtime.
- [x] **II. Spec-first**: `spec.md` đã có; Q1 đã chốt (§Decisions); Q2–Q6 giải quyết trong plan này.
- [x] **III. Minimal surface**: không refactor lân cận. `BuyDecisionEngine` chỉ **expose** cờ đã tính sẵn, không đổi logic chấm điểm.
- [x] **IV. Domain gates**: cập nhật `docs/domain/buy-decision.md` cùng change set. `MarketWyckoffPhase` và `MarketRegime` giữ song song — playbook `reversal-bounce` chỉ mượn hạ tầng đo, **không** gộp thang điểm (`docs/domain/reversal-bounce.md`).
- [x] **V. Simplicity**: 1 abstraction mới duy nhất (`IPlaybookClassifier`) — xem Complexity Tracking.
- [x] **Stack**: Domain giữ luật, Infrastructure điều phối runner + EF, Application mở DTO, `mobile/lib` chỉ hiển thị. Backend xong → `backend/restart-api.ps1`.

## Decisions (chốt, thay cho `/speckit-clarify`)

| ID | Câu hỏi | Quyết định |
|----|---------|-----------|
| Q1 | Gán playbook độc quyền hay đa nhãn? | **Độc quyền**, ưu tiên `breakout-darvas` > `pullback-ma20` > `reversal-bounce`. Mã không khớp → `unclassified` |
| Q2 | Playbook classifier nằm ở đâu? | Domain `IPlaybookClassifier`; **đọc cờ từ `BuyDecisionEvaluation`** đã mở rộng, không tính lại (constitution §V) |
| Q3 | Định nghĩa `pullback-ma20` | `HasBullishMaStack` = true **và** không `hasFlatBoxBreakout` **và** không `hasBreakoutEntry` **và** không `hasFlatBoxSetup` (loại setup zone Darvas để không giẫm chân rổ breakout) |
| Q4 | Backfill lịch sử? | **Không** backfill giá trị thật. Hàng cũ nhận `PlaybookId = 'legacy'` để không mất dữ liệu; số liệu playbook tính tiến về trước |
| Q5 | `MinScoreForEvaluation` theo playbook? | Giữ **60 toàn cục** ở slice này. Sau khi bundle hết trung bình cộng thì đo lại rồi mới bàn |
| Q6 | `RequireTrendSetup` chốt gì? | Giữ **`false`** — playbook đã lọc đúng sân. Đồng bộ default trong code về `false` để hết lệch với `appsettings.json` |

### Hệ quả quan trọng của Q1 (độc quyền)

Vì mỗi `(AsOfDate, Symbol)` chỉ thuộc 1 playbook, `PlaybookId` **phụ thuộc hàm** vào khóa của `StockCriterionDetails` → chỉ cần thêm **cột**, **không** đụng primary key bảng đó (`ApplicationDbContext.cs:224` giữ nguyên `{AsOfDate, Horizon, Symbol, CriterionId}`).

Ngược lại `DailyCriterionAccuracies` và `CriterionGroupDailyAccuracies` giờ có một hàng cho mỗi `(criterion × playbook)` → `PlaybookId` **phải** vào composite key (`ApplicationDbContext.cs:187/204`). Đây là lý do độc quyền rẻ hơn đa nhãn: đa nhãn sẽ buộc `StockCriterionDetails` thành quan hệ nhiều-nhiều và phải khử trùng khi tổng hợp.

## Project Structure

### Documentation (this feature)

```text
docs/features/indicator-playbooks/
├── spec.md              # BA prep (đã có)
├── plan.md              # File này
└── tasks.md             # Danh sách task
```

### Source Code (repository root)

```text
backend/
├── StockRadar.Application/      # DTOs/MarketDtos.cs, Services/CriterionScoringService.cs,
│                                #   Abstractions/ICriterionScoring.cs, Options/CriterionAccuracyOptions.cs
├── StockRadar.Domain/           # Enums/PlaybookId.cs (mới), Services/PlaybookClassifier.cs (mới),
│                                #   Services/BuyDecisionEngine.cs, Services/TrendSetupEvaluator.cs,
│                                #   Services/IndicatorBundleScorer.cs, Services/CriterionMetricsCollector.cs
├── StockRadar.Infrastructure/   # MarketData/DailyCriterionScoringRunner.cs,
│                                #   Persistence/{Entities,ApplicationDbContext,Repositories}
└── StockRadar.Tests/            # Playbook/ (mới)

mobile/lib/                      # core/models/models.dart, screens/criteria_screen.dart
docs/domain/                     # buy-decision.md (bắt buộc cập nhật cùng change set)
```

**Structure Decision**: chạm Domain (luật + classifier), Infrastructure (runner + EF + migration), Application (DTO), `mobile/lib` (hiển thị). **Không** chạm `frontend/`, không thêm route mới trong `StockRadar.Api`, không đụng `scripts/`.

## Phased approach

| Slice | Nội dung | Rủi ro chính |
|-------|---------|--------------|
| **S1 — Foundation** | `PlaybookId` enum + `IPlaybookClassifier` + expose cờ qua `BuyDecisionEvaluation` + cột/migration + runner ghi `PlaybookId` | Migration: `dotnet ef migrations add` **sinh body sai** trong repo này → **đọc lại file migration trước khi apply** |
| **S2 — Đo đúng sân** | `MeasureOutcome` theo playbook (horizon/target riêng) + baseline riêng theo playbook + aggregate theo `(criterion × playbook × phase)` | Số liệu đổi mạnh so với hiện tại — đây là kết quả mong đợi, không phải hồi quy |
| **S3 — Bundle** | Gỡ 3 bundle trình độ khỏi output; 3 bundle còn lại chuyển gate/veto (thiếu thành phần → `Neutral`) | `CriterionType` **giữ nguyên giá trị int** (deprecate, không xóa) để hàng DB cũ không lệch mapping |
| **S4 — UI** | Tab playbook, tách trục hướng / độ rõ, typo `Rũi ro` | Không đụng logic |
| **S5 — chỉ khi SC-002 đạt** | Nối ML feature hoặc veto có trần | Ngoài phạm vi change set này |

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Abstraction mới `IPlaybookClassifier` (Domain) | Cần một nơi duy nhất quyết định gán playbook độc quyền + thứ tự ưu tiên; dùng ở `DailyCriterionScoringRunner` và sau này ở shadow/backtest runner | Nhét vào `BuyDecisionEngine` sẽ trộn concern "quyết định mua" với "phân loại để đo lường"; nhét vào runner thì `SmartMoneyBacktestRunner` và `ShadowAnalysisService` phải chép lại luật |
| Thêm cột vào composite key 2 bảng | Playbook là chiều nhóm thật, không phải thuộc tính hiển thị — để trong `BreakdownJson` thì không group-by / index được | `BreakdownJson` đã chứa buckets/phases nhưng chỉ để đọc ra hiển thị; aggregate theo playbook cần cột thật |

## Rollback

- Cờ config `CriterionAccuracy:PlaybookDimensionEnabled`, default `false` khi ship, bật sau khi verify.
- Tắt cờ → runner ghi `PlaybookId = 'unclassified'` cho mọi hàng, aggregate quay về hành vi cũ. **Không** cần rollback migration.

## Verification

- **SC-004 regression (bắt buộc)**: chạy `DailyAnalysisRunner` full trên cùng ngày dữ liệu trước/sau, diff danh sách Top (symbol + Buy Score). Phải **giống hệt**.
- Unit test `backend/StockRadar.Tests/Playbook/`: gán độc quyền, thứ tự ưu tiên, mã không khớp → `unclassified`, mã vừa breakout vừa MA stack → `breakout-darvas`.
- Backend xong → `backend/restart-api.ps1`.

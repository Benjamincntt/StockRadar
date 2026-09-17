# Implementation Plan: Bỏ cơ chế Relaxed Fallback trong Daily Analysis

**Branch**: `004-remove-relaxed-fallback` | **Date**: 2026-08-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-remove-relaxed-fallback/spec.md`

## Summary

Khi strict = 0 mã, `DailyAnalysisRunner` hiện fallback sang rổ relaxed (Buy Score ≥ 45) và Top vẫn hiển thị mã — gây hiểu nhầm vì các mã đó không có noti Telegram tương ứng. Bỏ hẳn đường relaxed: strict = 0 → `OpportunitiesSaved = 0` → Top trả rỗng (`zero_matches`) kèm `StatusBullets` giải thích gate (đã có từ trước, tái dùng nguyên). Dọn theo 3 lớp: (1) runtime behavior ở `DailyAnalysisRunner`, (2) status/message ở `MarketService` + DTO, (3) UI web/mobile theo sau backend. Cột DB `UsedRelaxedFallback` bị xoá qua migration mới (đã chốt với user, chấp nhận rủi ro cao hơn giữ cột).

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript/React (frontend web), Dart/Flutter (mobile)

**Primary Dependencies**: EF Core (migration DROP COLUMN), không thêm dependency mới

**Storage**: SQL Server — bảng `DailyAnalysisRuns`, cột `UsedRelaxedFallback` (bit, not null, default false)

**Testing**: `StockRadar.Tests` (xUnit) cho `DailyAnalysisRunner`/`MarketService`; `dotnet build` + `tsc --noEmit` + `flutter analyze` làm smoke check tối thiểu

**Target Platform**: API .NET, web React (Vite), mobile Flutter — cả 3 đều bị ảnh hưởng

**Project Type**: web-service + web-app + mobile-app (monorepo hiện có)

**Performance Goals**: N/A — không đổi hiệu năng, chỉ bỏ một nhánh code

**Constraints**: Migration EF Core phải qua review theo skill `efcore-migration-review` trước khi coi là xong; không đổi tiêu chí Buy Score/strict hiện có

**Scale/Scope**: 3 platform, ~8 file backend, ~4 file frontend/mobile, 1 migration mới

## Constitution Check

*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code là nguồn sự thật runtime**: Plan trích dẫn đúng file entry đã đọc trực tiếp (`DailyAnalysisRunner.cs`, `MarketService.cs`, `OpportunityAnalysisStatuses.cs`, `MarketJobsOptions.cs`, `DbEntities.cs`, `EfDailyAnalysisRunRepository.cs`, `HomePage.tsx`, `home_screen.dart`, `models.dart`) — không dựa vào tóm tắt chat cũ.
- [x] **II. Spec trước khi đổi thiết kế**: `spec.md` đã có, `NEEDS CLARIFICATION` (FR-007) đã resolve qua câu hỏi trực tiếp với user (2026-08-21) — không cần `/speckit-clarify` thêm vì không còn điểm mơ hồ khác.
- [x] **III. Thay đổi tối thiểu xâm lấn**: Chỉ xoá code đường relaxed đã xác định rõ vị trí (xem Project Structure); không refactor `MarketService`/`DailyAnalysisRunner` ngoài phạm vi đó.
- [x] **IV. Kỷ luật cổng domain giao dịch**: Đây là đổi cổng Top (bỏ fallback hiển thị khi strict=0) — đã đi qua spec.md + plan.md này trong cùng change set; không đổi `BuyDecisionEngine`/tiêu chí strict, chỉ bỏ đường thay thế khi strict=0. Cần cập nhật `docs/domain/buy-decision.md` nếu tài liệu đó có đề cập relaxed fallback (kiểm tra ở Phase Polish).
- [x] **V. Đơn giản**: Không thêm abstraction/dependency mới; chỉ xoá code. Bỏ qua `research.md`/`data-model.md`/`contracts/` vì không có unknown kỹ thuật cần research và không có entity/API mới (chỉ xoá field).

## Project Structure

### Documentation (this feature)

```text
specs/004-remove-relaxed-fallback/
├── spec.md    # đã có
├── plan.md    # file này
└── tasks.md   # tiếp theo
```

*(Không tạo `research.md`/`data-model.md`/`contracts/`/`quickstart.md` — không có unknown kỹ thuật, không có entity hay API contract mới, chỉ xoá field/code đường cũ. Xem Constitution Check V.)*

### Source Code (repository root) — chỉ các nhánh bị đổi

```text
backend/
├── StockRadar.Api/appsettings.json                              # xoá 2 key RelaxedFallback*
├── StockRadar.Application/
│   ├── Options/MarketJobsOptions.cs                              # xoá RelaxedFallbackEnabled/DisabledPhases
│   ├── Common/OpportunityAnalysisStatuses.cs                     # xoá const RelaxedFallback
│   ├── DTOs/MarketJobDtos.cs                                     # xoá UsedRelaxedFallback field
│   ├── Abstractions/IMarketJobServices.cs                        # xoá tham số/field liên quan
│   └── Services/MarketService.cs                                 # ResolveAnalysisStatus, BuildRelaxedFallbackMessage
├── StockRadar.Infrastructure/
│   ├── MarketData/DailyAnalysisRunner.cs                         # bỏ nhánh dựng relaxed candidates
│   ├── Persistence/Entities/DbEntities.cs                        # xoá property UsedRelaxedFallback
│   ├── Persistence/Repositories/EfDailyAnalysisRunRepository.cs  # xoá tham số usedRelaxedFallback
│   └── Migrations/                                               # migration mới: DROP COLUMN UsedRelaxedFallback

frontend/src/
├── types/index.ts       # xoá "relaxed_fallback" khỏi union OpportunityAnalysisStatus
└── pages/HomePage.tsx   # gộp nhánh relaxed_fallback vào zero_matches

mobile/lib/
├── core/models/models.dart   # không đổi field (analysisStatus vẫn String?, không cần đổi model)
└── screens/home_screen.dart  # gộp nhánh relaxed_fallback vào zero_matches
```

**Structure Decision**: Đổi cả backend + frontend web + mobile theo đúng thứ tự phụ thuộc: backend trước (US1, US2) rồi UI (US3), vì UI phụ thuộc vào việc backend ngừng phát `relaxed_fallback`.

## Complexity Tracking

*Không có vi phạm Constitution Check cần biện minh — không có bảng.*

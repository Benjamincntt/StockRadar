---
description: "Task list for feature 004 — remove relaxed fallback"
---

# Tasks: Bỏ cơ chế Relaxed Fallback trong Daily Analysis

**Input**: `specs/004-remove-relaxed-fallback/spec.md`, `plan.md`

**Tests**: Không yêu cầu viết test mới (spec không yêu cầu) — chỉ cần build/typecheck/analyze sạch sau mỗi phase.

## Phase 1: User Story 1 - Top hiển thị đúng lý do rỗng (Priority: P1) 🎯 MVP

**Goal**: Khi strict = 0, không còn dựng danh sách relaxed; Top trả rỗng kèm `StatusBullets`.

- [x] T001 [US1] `backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs`: xoá nhánh gọi `BuildRelaxedCandidates`/`IsRelaxedFallbackDisabled` khi `ordered.Count == 0`; xoá biến `usedRelaxedFallback` và log `" [fallback]"`.
- [x] T002 [US1] `backend/StockRadar.Application/Options/MarketJobsOptions.cs`: xoá `RelaxedFallbackEnabled`, `RelaxedFallbackDisabledPhases` (giữ `FallbackMinScore`/`FallbackMaxResults`/`FallbackMinResults` chỉ nếu còn dùng nơi khác — kiểm tra trước khi xoá, nếu không còn tham chiếu thì xoá luôn theo FR-002).
- [x] T003 [US1] `backend/StockRadar.Api/appsettings.json`: xoá 2 khoá `RelaxedFallbackEnabled`, `RelaxedFallbackDisabledPhases` trong section `DailyAnalysis`.
- [x] T004 [US1] `backend/StockRadar.Application/Common/OpportunityAnalysisStatuses.cs`: xoá const `RelaxedFallback`.
- [x] T005 [US1] `backend/StockRadar.Application/Services/MarketService.cs`: xoá nhánh `if (analysisRun.UsedRelaxedFallback && ...)` trong `ResolveAnalysisStatus`; xoá method `BuildRelaxedFallbackMessage`; xoá đoạn gọi nó trong `GetOpportunitiesAsync`; đổi điều kiện gọi `BuildGateStatusBulletsAsync` từ `RelaxedFallback or ReferenceList` thành chỉ `ZeroMatches or ReferenceList` (US1 FR-006 — bullets phải theo status `zero_matches` sau khi gộp).
- [x] T006 [US1][depends: T004] Xác nhận `zero_matches` early-return path (dòng ~239-259 hiện tại) đã gọi `BuildGateStatusBulletsAsync` — giữ nguyên (đã làm ở phiên trước feature này), không cần đổi.

**Checkpoint**: `dotnet build StockRadar.Application` + `StockRadar.Infrastructure` + `StockRadar.Api` sạch. `GET /opportunities` cho ngày strict=0 trả `zero_matches`, `items=[]`.

---

## Phase 2: User Story 2 - Dọn config + cột DB (Priority: P2)

**Goal**: Không còn cấu hình/field chết trong code và DB.

- [x] T007 [US2][depends: T001] `backend/StockRadar.Application/DTOs/MarketJobDtos.cs`: xoá field `UsedRelaxedFallback` khỏi record kết quả job (dòng ~23).
- [x] T008 [US2][depends: T007] `backend/StockRadar.Application/Abstractions/IMarketJobServices.cs`: xoá tham số/field `UsedRelaxedFallback` liên quan (dòng ~86, 99).
- [x] T009 [US2][depends: T001] `backend/StockRadar.Infrastructure/Persistence/Repositories/EfDailyAnalysisRunRepository.cs`: xoá tham số `usedRelaxedFallback` khỏi ctor/method insert/update và mapping sang `DailyAnalysisRunRecord`.
- [x] T010 [US2][depends: T009] `backend/StockRadar.Infrastructure/Persistence/Entities/DbEntities.cs`: xoá property `UsedRelaxedFallback` khỏi `DailyAnalysisRunEntity`.
- [x] T011 [US2][depends: T010] Tạo EF Core migration mới (`dotnet ef migrations add DropUsedRelaxedFallback` trong `StockRadar.Infrastructure`) — DROP COLUMN `UsedRelaxedFallback` trên bảng `DailyAnalysisRuns`. **Đọc lại file migration sinh ra trước khi apply** (memory: `ef-migrations-add-broken-baseline` — `dotnet ef migrations add` có lịch sử sinh body sai trong repo này).
- [x] T012 [US2][depends: T011] Review migration T011 theo skill `efcore-migration-review` trước khi coi là xong.
- [x] T013 [US2] Kiểm tra `DailyAnalysisResultDto` (`usedRelaxedFallback` optional field ở frontend `types/index.ts` dòng ~232) — nếu backend không còn field này trong response, xoá field tương ứng ở frontend type.

**Checkpoint**: `dotnet build` toàn backend sạch; migration mới tồn tại và đã review; grep `RelaxedFallback` trong `backend/` (loại migration lịch sử) = 0 kết quả (SC-002).

---

## Phase 3: User Story 3 - Dọn UI web + mobile (Priority: P3)

**Goal**: Web/mobile không còn nhánh xử lý `relaxed_fallback`.

- [x] T014 [P] [US3][depends: T005] `frontend/src/types/index.ts`: xoá `"relaxed_fallback"` khỏi union `OpportunityAnalysisStatus`.
- [x] T015 [US3][depends: T014] `frontend/src/pages/HomePage.tsx`: xoá case `"relaxed_fallback"` ở `lastScanLabel` và `analysisBanner` (gộp còn `"zero_matches"`); xoá điều kiện ẩn danh sách mã theo `"relaxed_fallback"` ở phần render list (chỉ còn `"reference_list"`).
- [x] T016 [P] [US3][depends: T005] `mobile/lib/screens/home_screen.dart`: xoá `'relaxed_fallback'` khỏi các điều kiện trong `_analysisBannerText` (gộp còn `'zero_matches'`) và khỏi điều kiện màu banner + điều kiện ẩn list.

**Checkpoint**: `tsc --noEmit` (frontend) và `flutter analyze` (mobile) sạch; grep `relaxed_fallback` trong `frontend/src` + `mobile/lib` = 0 kết quả (SC-003).

---

## Phase 4: Polish

- [x] T017 [P] Kiểm tra `docs/domain/buy-decision.md` có đề cập relaxed fallback không — nếu có, cập nhật theo Nguyên tắc IV (đổi cổng Top phải đi kèm domain doc trong cùng change set).
- [x] T018 Chạy lại `dotnet build` (backend), `npx tsc --noEmit` (frontend), `flutter analyze` (mobile) một lần cuối cho toàn bộ thay đổi.

---

## Dependencies & Execution Order

- Phase 1 (US1) phải xong trước Phase 3 (US3) vì UI phụ thuộc backend ngừng phát `relaxed_fallback`.
- Phase 2 (US2) có thể chạy song song với cuối Phase 1 (T007-T010 không phụ thuộc T002-T006), nhưng T011 (migration) phải chờ T010.
- T014-T016 [P] có thể chạy song song (file khác nhau: web vs mobile).

## Notes

- Không tạo test mới theo yêu cầu; nhưng `StockRadar.Tests` hiện có nếu có test cũ tham chiếu `RelaxedFallback`/`UsedRelaxedFallback` sẽ cần sửa cùng lúc — kiểm tra khi build test project.
- Backend xong → chạy `backend/restart-api.ps1` (theo CLAUDE.md quy ước), không tự ý ship production.

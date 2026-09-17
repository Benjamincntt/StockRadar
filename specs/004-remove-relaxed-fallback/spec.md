# Feature Specification: Bỏ cơ chế Relaxed Fallback trong Daily Analysis

**Feature Branch**: `004-remove-relaxed-fallback`

**Created**: 2026-08-21

**Status**: Draft

**Input**: User description: "Bỏ hẳn cơ chế Relaxed Fallback trong Daily Analysis: khi strict = 0 mã, không còn hiển thị/lưu danh sách Top thay thế từ rổ relaxed (Buy Score ≥ FallbackMinScore, không FOMO/phân phối). Khi strict = 0, Top trả về rỗng (trạng thái zero_matches) thay vì fallback sang relaxed. Phạm vi: tắt RelaxedFallbackEnabled/loại bỏ code đường relaxed trong DailyAnalysisRunner, OpportunityAnalysisStatuses, MarketService (ResolveAnalysisStatus/BuildRelaxedFallbackMessage), cột UsedRelaxedFallback, và dọn UI nhánh relaxed_fallback ở frontend web + mobile không còn giá trị dùng. Lý do: cơ chế relaxed đang khiến Top hiển thị mã cũ/không đáng tin khi bull-trap gate chặn strict liên tục, gây hiểu nhầm và không phản ánh đúng lý do không có tín hiệu Telegram."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Top hiển thị đúng lý do rỗng thay vì mã relaxed gây hiểu nhầm (Priority: P1)

Nhà đầu tư mở Top cơ hội trong lúc bull-trap gate đang chặn liên tục (VNINDEX sát đỉnh kháng cự, pha chưa Favorable) nên không có mã nào đạt strict. Hiện tại hệ thống fallback sang rổ relaxed (Buy Score ≥ 45) và hiển thị các mã đó như thể vẫn là "Top", khiến nhà đầu tư tưởng có cơ hội trong khi thực ra không mã nào đủ chuẩn và cũng không có noti Telegram nào được gửi cho các mã đó. Sau thay đổi, khi strict = 0, Top phải trả về rỗng kèm giải thích rõ ràng (gate nào đang chặn, số liệu cụ thể) thay vì hiện danh sách thay thế.

**Why this priority**: Đây là nguồn gây hiểu nhầm trực tiếp nhất — người dùng thấy mã trong Top nhưng không hề nhận được tín hiệu mua tương ứng qua Telegram, làm giảm niềm tin vào hệ thống. Phải xử lý trước tiên vì ảnh hưởng ngay tới quyết định giao dịch.

**Independent Test**: Với một ngày phân tích có strict = 0 (giả lập hoặc chọn ngày thực tế có bull-trap gate active), gọi `GET /api/v1/opportunities` cho ngày đó và xác nhận `items` rỗng, `analysisStatus` = `zero_matches`, không có mã nào từ rổ relaxed được trả về.

**Acceptance Scenarios**:

1. **Given** `DailyAnalysisRunner` chạy xong một phiên và không có mã nào đạt ngưỡng strict, **When** hệ thống lưu kết quả phân tích, **Then** `OpportunitiesSaved = 0` được lưu và không có bước dựng danh sách thay thế từ rổ relaxed nào chạy.
2. **Given** một ngày có `OpportunitiesSaved = 0`, **When** gọi `GET /api/v1/opportunities` cho ngày đó, **Then** `analysisStatus` trả về `"zero_matches"` và `items` là danh sách rỗng.
3. **Given** bull-trap gate đang active (VNINDEX gần đỉnh kháng cự, pha không Favorable), **When** Top hiển thị trạng thái `zero_matches`, **Then** `statusBullets` giải thích đúng lý do (lookback phiên, band %, deferral, hysteresis) lấy từ cấu hình runtime thật, không phải text tĩnh.

---

### User Story 2 - Vận hành không còn cấu hình fallback gây nhầm lẫn (Priority: P2)

Người vận hành đọc `appsettings.json` / `MarketJobsOptions` không còn thấy `RelaxedFallbackEnabled` / `RelaxedFallbackDisabledPhases` — tránh việc ai đó bật lại nhầm hoặc đọc sai hành vi hệ thống khi tra cứu cấu hình sau này.

**Why this priority**: Dọn cấu hình chết là bước dọn tiếp theo sau khi hành vi runtime đã đổi ở User Story 1; không có giá trị nếu làm trước khi hành vi đã chốt.

**Independent Test**: Build lại `StockRadar.Application` và `StockRadar.Api`; xác nhận `MarketJobsOptions` không còn property `RelaxedFallbackEnabled`/`RelaxedFallbackDisabledPhases`, và `appsettings.json` không còn 2 khoá này trong section `DailyAnalysis`.

**Acceptance Scenarios**:

1. **Given** codebase sau thay đổi, **When** grep `RelaxedFallbackEnabled` trong `backend/`, **Then** không còn kết quả nào ngoài các file lịch sử/migration.
2. **Given** `DailyAnalysisRunner` sau thay đổi, **When** strict = 0, **Then** không có nhánh code nào gọi tới việc dựng danh sách relaxed (đường code đó bị xoá, không phải chỉ tắt cờ).

---

### User Story 3 - Web và mobile không còn hiển thị nhánh UI relaxed đã chết (Priority: P3)

Giao diện web (`HomePage.tsx`) và mobile (`home_screen.dart`) không còn xử lý riêng trạng thái `relaxed_fallback` (banner màu riêng, message cứng "Top relaxed") — toàn bộ hợp nhất xử lý như `zero_matches`, vì backend không còn phát trạng thái này.

**Why this priority**: Dọn UI chỉ có giá trị sau khi backend đã ngừng phát trạng thái `relaxed_fallback`; làm trước sẽ phải sửa lại hai lần.

**Independent Test**: Build frontend (`tsc --noEmit`) và `flutter analyze`; grep `relaxed_fallback` trong `frontend/src` và `mobile/lib` trả về 0 kết quả.

**Acceptance Scenarios**:

1. **Given** union type `OpportunityAnalysisStatus` ở frontend, **When** kiểm tra định nghĩa type, **Then** giá trị `"relaxed_fallback"` không còn tồn tại.
2. **Given** `home_screen.dart` và `HomePage.tsx` sau thay đổi, **When** đọc nhánh xử lý `analysisStatus`, **Then** chỉ còn `not_run | zero_matches | has_results | reference_list`.

---

### Edge Cases

- Các bản ghi `DailyAnalysisRunRecord` lịch sử đã có `UsedRelaxedFallback = true` (trước ngày đổi) — khi người dùng xem lại ngày đó, hệ thống không còn nhánh `relaxed_fallback` để trả về; vì bản ghi đó vẫn có `OpportunitiesSaved > 0` nên sẽ tự rơi vào `has_results`/`reference_list` bình thường, không cần xử lý đặc biệt.
- Nếu `RelaxedFallbackDisabledPhases` từng dùng để tắt fallback theo pha (`Unfavorable`) — sau khi xoá hẳn cơ chế, không còn khái niệm "tắt theo pha" vì không còn fallback để tắt.
- `BuildAnalyzedFallbackNote` (nhánh `reference_list` — hiển thị ngày gần nhất có data khi ngày mục tiêu chưa chạy) **không** thuộc phạm vi xoá — đây là fallback theo **ngày**, khác với fallback theo **rổ điểm** (relaxed) đang bị loại bỏ.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `DailyAnalysisRunner` KHÔNG được dựng danh sách Top thay thế từ rổ relaxed khi strict = 0 mã; khi đó `OpportunitiesSaved = 0` được lưu và không có mã nào được ghi vào `DailyOpportunityRecord` cho ngày đó.
- **FR-002**: `MarketJobsOptions` (và `appsettings.json` tương ứng) PHẢI loại bỏ hẳn `RelaxedFallbackEnabled` và `RelaxedFallbackDisabledPhases` (xoá property, không chỉ đặt `false`).
- **FR-003**: `OpportunityAnalysisStatuses.RelaxedFallback` PHẢI bị xoá; `ResolveAnalysisStatus` trong `MarketService` không còn nhánh kiểm tra `UsedRelaxedFallback`; `BuildRelaxedFallbackMessage` PHẢI bị xoá.
- **FR-004**: API `GET /api/v1/opportunities` KHÔNG còn trả giá trị `"relaxed_fallback"` cho `analysisStatus` trong bất kỳ trường hợp nào sau khi đổi.
- **FR-005**: Frontend web (`HomePage.tsx`, `types/index.ts`) và mobile (`home_screen.dart`, `models.dart`) PHẢI xoá toàn bộ nhánh xử lý riêng cho `"relaxed_fallback"` (giá trị union type, banner màu riêng, message cứng) — hợp nhất xử lý theo `"zero_matches"`.
- **FR-006**: `StatusBullets` (đã có, giải thích bull-trap gate từ cấu hình runtime — xem `MarketService.BuildGateStatusBulletsAsync`) PHẢI tiếp tục được tính và trả về cho trạng thái `zero_matches` sau khi gộp — không bị mất theo khi xoá nhánh relaxed.
- **FR-007**: Cột `UsedRelaxedFallback` trên bảng `DailyAnalysisRuns` PHẢI bị xoá hẳn qua một EF Core migration mới (DROP COLUMN) — đã chốt với người dùng ngày 2026-08-21, không giữ cột chết.

### Key Entities *(include if feature involves data)*

- **DailyAnalysisRunRecord**: một lần chạy phân tích cho một ngày giao dịch; trường `UsedRelaxedFallback` (sẽ luôn `false` sau thay đổi, xem FR-007 về việc giữ/xoá cột) và `OpportunitiesSaved` quyết định trạng thái hiển thị.
- **OpportunitiesListDto**: response API Top; trường `AnalysisStatus` mô tả lý do hiển thị hiện tại — sau thay đổi chỉ còn 4 giá trị: `not_run | zero_matches | has_results | reference_list`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% request `GET /api/v1/opportunities` cho ngày có strict = 0 trả về `analysisStatus = "zero_matches"` và `items = []`; không còn giá trị `"relaxed_fallback"` xuất hiện trong response ở bất kỳ ngày nào sau ngày deploy.
- **SC-002**: Grep `RelaxedFallbackEnabled`, `BuildRelaxedFallbackMessage`, `RelaxedFallback` (status constant) trong `backend/` (loại trừ migration lịch sử) trả về 0 kết quả.
- **SC-003**: `dotnet build` (backend), `tsc --noEmit` (frontend web), `flutter analyze` (mobile) đều pass sau khi xoá nhánh relaxed ở cả 3 nền tảng.

## Assumptions

- Không cần backfill lại dữ liệu lịch sử cho các ngày cũ đã có `UsedRelaxedFallback = true` trước ngày deploy — thay đổi chỉ áp dụng cho hành vi từ nay về sau.
- Không đổi logic bull-trap gate, tiêu chí Buy Score hay ngưỡng strict hiện có — feature này chỉ bỏ đường fallback hiển thị Top khi strict = 0, không đổi bất kỳ tiêu chí chấm điểm nào khác.
- `StatusBullets` (bull-trap explain, đã implement trước feature này) được giữ nguyên logic tính toán, chỉ đổi nơi gắn vào (`zero_matches` thay vì tách riêng `relaxed_fallback`).
- FR-007 đã chốt: tạo migration mới xoá cột `UsedRelaxedFallback` hẳn. Migration PHẢI được review theo skill `efcore-migration-review` trước khi coi là xong (rủi ro cao hơn giữ cột).

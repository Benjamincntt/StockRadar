# Implementation Plan: Điều chỉnh giá khi quyền mua trả tiền

**Branch**: `006-paid-rights-adjust` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-paid-rights-adjust/spec.md`

## Summary

Mở rộng công thức 005: `giaThamChieu = (P − tiền + tỷ lệ mua × giá PH) / (pha loãng + tỷ lệ mua)`. Tỷ lệ 0 → đúng 005 (SSI không đổi). Seed + form ghi HCM 05/02 (tiền 0.4) và 16/07 (tiền 0.4 + mua 4:1 giá 10.0). Nến kho / last / chart vẫn thô. Không crawler, không đổi ngưỡng Top.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript (web form); Dart (mobile form)

**Primary Dependencies**: BCL `System.Text.Json`. Không thêm NuGet.

**Storage**: File seed `Data/su-kien-quyen.json` (thêm field tùy chọn). Không bảng EF.

**Testing**: xUnit `DieuChinhGia` — hồi quy SSI + neo HCM 16/07 (`giaThamChieu` 23.24, % ≈ +9 không −6 không +19)

**Target Platform**: API chấm điểm + màn sự kiện quyền web/mobile

**Project Type**: web-service + clients trong monorepo

**Performance Goals**: Như 005 — vài sự kiện/mã × ~250 nến mỗi `Evaluate`

**Constraints**: Last/chart thô. `tienMat`/`giaPhatHanh` cùng thang Close. ESOP ngoài scope. Không đổi cổng Top.

**Scale/Scope**: Domain record + 1 hàm công thức + parse/ghi JSON + DTO/API additive + 2 form. ~8–12 file.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code as truth**: Entry: `BoDieuChinhGiaTheoQuyen.TinhGiaThamChieu`, `SuKienQuyen`, `FileNguonSuKienQuyen`, `DichVuSuKienQuyen`, `StocksController` rights-events, `RightsEventsPage`, `SuKienQuyenScreen`
- [x] **II. Spec-first**: `specs/006-paid-rights-adjust/spec.md` đủ; không `[NEEDS CLARIFICATION]`
- [x] **III. Minimal surface**: Không engine thứ hai; mở rộng overload mặc định 0. Không rename API `cash`/`dilution`. Identifier mới tiếng Việt không dấu
- [x] **IV. Domain gates**: Đầu vào % đổi khi có quyền mua — cùng change set: `docs/domain/buy-decision.md` + `pipeline-jobs.md` + CLAUDE.md trỏ 006. Không đổi ngưỡng Top/sóng. Wyckoff ≠ Reversal
- [x] **V. Simplicity**: Không project/NuGet mới. Field optional trên record hiện có
- [x] **Stack**: Luật Domain; file Infra; DTO Application; form frontend/mobile. Restart `backend/restart-api.ps1`

**Post–Phase 1**: Cổng pass. Route `GET/POST .../rights-events` additive field; SSI JSON cũ vẫn nạp (field mới mặc định 0).

## Project Structure

### Documentation (this feature)

```text
specs/006-paid-rights-adjust/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/dieu-chinh-gia-quyen-mua.md
└── tasks.md
```

### Source Code (repository root)

```text
backend/StockRadar.Api/Data/su-kien-quyen.json
backend/StockRadar.Domain/ValueObjects/SuKienQuyen.cs
backend/StockRadar.Domain/Services/BoDieuChinhGiaTheoQuyen.cs
backend/StockRadar.Application/DTOs/SuKienQuyenDtos.cs
backend/StockRadar.Application/Services/DichVuSuKienQuyen.cs
backend/StockRadar.Infrastructure/MarketData/FileNguonSuKienQuyen.cs
backend/StockRadar.Tests/DieuChinhGia/
frontend/src/pages/RightsEventsPage.tsx
frontend/src/lib/api.ts
frontend/src/types/index.ts
mobile/lib/screens/su_kien_quyen_screen.dart
mobile/lib/core/models/models.dart
mobile/lib/core/api/api_client.dart
docs/domain/buy-decision.md
docs/domain/pipeline-jobs.md
```

**Structure Decision**: Mở rộng 005 tại chỗ. JSON seed key tiếng Việt; JSON API đã ship giữ English (`oldShares`/`newShares`/`issuePrice`).

## Complexity Tracking

Không có vi phạm hiến pháp cần biện minh.

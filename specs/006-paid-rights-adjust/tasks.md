# Tasks: Điều chỉnh giá khi quyền mua trả tiền

**Input**: Design documents from `specs/006-paid-rights-adjust/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/dieu-chinh-gia-quyen-mua.md, quickstart.md

**Tests**: Có — neo HCM 23.24 / % +9 và hồi quy SSI (spec SC-001…004).

## Phase 1: Setup

- [x] T001 Cập nhật seed `backend/StockRadar.Api/Data/su-kien-quyen.json` — HCM 05/02 (0.4) + 16/07 (0.4, 4:1, 10.0); giữ SSI

## Phase 2: Foundational

- [x] T002 Mở rộng `SuKienQuyen` (`soCoCu`, `soCoMoi`, `giaPhatHanh`, `TyLeQuyenMua`) trong `backend/StockRadar.Domain/ValueObjects/SuKienQuyen.cs`
- [x] T003 Mở rộng `TinhGiaThamChieu` / `TinhHeSoNgayQuyen` / `TaoDayGiaDieuChinh` trong `backend/StockRadar.Domain/Services/BoDieuChinhGiaTheoQuyen.cs`

**Checkpoint**: Công thức Domain đủ để US1 test độc lập

## Phase 3: User Story 1 — Gap quyền mua (P1) 🎯 MVP

**Goal**: HCM 16/07 giá tham chiếu 23.24; % ≈ +9; SSI không đổi.

**Independent Test**: filter `DieuChinhGia` — SC-001…004.

- [x] T004 [US1] Test neo HCM + hồi quy SSI trong `backend/StockRadar.Tests/DieuChinhGia/QuyenMuaTraTienTests.cs`
- [x] T005 [US1] Parse/ghi `soCoCu`/`soCoMoi`/`giaPhatHanh`; từ chối n=0 khi m>0 trong `backend/StockRadar.Infrastructure/MarketData/FileNguonSuKienQuyen.cs`

## Phase 4: User Story 2 — Form ghi quyền mua (P1)

**Goal**: API + web + mobile nhập n:m và giá phát hành.

**Independent Test**: GET HCM có 2 sự kiện; POST thiếu `oldShares` khi `newShares`>0 → 400.

- [x] T006 [US2] DTO + validate `DichVuSuKienQuyen` trong `backend/StockRadar.Application/DTOs/SuKienQuyenDtos.cs` và `backend/StockRadar.Application/Services/DichVuSuKienQuyen.cs`
- [x] T007 [P] [US2] Form web `frontend/src/pages/RightsEventsPage.tsx` + `frontend/src/lib/api.ts` + `frontend/src/types/index.ts`
- [x] T008 [P] [US2] Form mobile `mobile/lib/screens/su_kien_quyen_screen.dart` + `mobile/lib/core/models/models.dart` + `mobile/lib/core/api/api_client.dart`
- [x] T009 [US2] Test dịch vụ từ chối quyền mua thiếu mẫu số trong `backend/StockRadar.Tests/DieuChinhGia/DichVuSuKienQuyenTests.cs`

## Phase 5: User Story 3 — Last/chart thô (P2)

**Goal**: Không đổi persist/chart; xác nhận nến cuối = thô (đã cover T004).

- [x] T010 [US3] Không sửa Job 2 / chart provider — xác nhận test nến cuối thô vẫn pass

## Phase 6: Polish

- [x] T011 Docs `docs/domain/buy-decision.md`, `docs/domain/pipeline-jobs.md`, `CLAUDE.md` trỏ 006 + HCM
- [x] T012 `dotnet test --filter FullyQualifiedName~DieuChinhGia` rồi `backend/restart-api.ps1`

## Dependencies

T001 → T002 → T003 → T004/T005 → T006 → T007∥T008 → T009 → T010 → T011 → T012

## MVP

T001–T005 (công thức + seed + test). Form (T006–T009) cần để SC-006.

## Implementation strategy

Land Domain+seed+test trước; nối file/API/UI; docs + restart.

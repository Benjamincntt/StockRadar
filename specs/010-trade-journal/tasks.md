# Tasks: Nhật ký giao dịch thật (Trade Journal)

**Input**: Design documents from `/specs/010-trade-journal/`

**Prerequisites**: plan.md (Q1–Q7 + N5 đã duyệt 2026-10-21), spec.md, research.md, data-model.md, contracts/nhat-ky-api.md, quickstart.md

**Tests**: CÓ — chủ sản phẩm yêu cầu "Có test". Test viết TRƯỚC, xác nhận fail, rồi implement (pattern repo: xUnit + EF InMemory trong `backend/StockRadar.Tests`).

**Organization**: chia theo user story; mỗi story independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: different files, no dependencies. **[US1]/[US2]/[US3]**: user story label.

## Path conventions

Backend `backend/StockRadar.*`, mobile `mobile/lib/` (theo plan.md §Project Structure).

---

## Phase 1: Setup

**Purpose**: kiểm tra công cụ + DEV DB target trước khi đụng schema.

- [ ] T001 Xác nhận `dotnet ef` CLI hoạt động (`dotnet ef --version` từ `backend/`), ghi nhận connection string DEV (SQLite local — **KHÔNG phải** 103.226.248.6) từ `backend/StockRadar.Api/appsettings.Development.json`

---

## Phase 2: Foundational (blocking — MUST xong trước mọi US)

**Purpose**: 2 bảng mới + phân quyền theo UserId. Không đụng bảng/API/DTO cũ (FR-007). Q1: tạo đủ 2 bảng NGAY migration duy nhất `AddNhatKyGiaoDich` để US2/US3 không cần migration lần hai.

- [ ] T002 [US1] Tạo `backend/StockRadar.Infrastructure/Persistence/Entities/NhatKyEntities.cs`: `NhatKyGiaoDichEntity` + `NhatKyTinHieuBoQuaEntity` đúng data-model.md (cột Việt không dấu pha Anh như repo; không comment từng field)
- [ ] T003 [US1] Đăng ký trong `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs`: 2 DbSet + `OnModelCreating` (HasKey, HasMaxLength 16/8/32/512, precision tiền 18,2 / KL 18,4, FK UserId→Users cascade, index theo data-model.md) — cạnh block `WatchlistEntity`
- [ ] T004 [US1] Migration `dotnet ef migrations add AddNhatKyGiaoDich` (project StockRadar.Infrastructure, startup StockRadar.Api) — **ĐỌC lại file sinh ra** (bẫy đã ghi ở skill efcore-migration-review: tool từng sinh sai body); chỉ tạo 2 bảng mới, không drop/alter gì khác; sửa body nếu sai
- [ ] T005 [US1] Verify migration trên DB DEV: `dotnet ef database update` → xác nhận 2 bảng → rollback về migration trước (`database update --id <trước đó>`) → apply lại; paste output cả 3 bước
- [ ] T006 [P] [US1] Abstraction + DTOs: `backend/StockRadar.Application/Abstractions/INhatKyRepository.cs` (Add/Update/Delete/Get theo Id+UserId, ListTheoKy, raw cho vị thế, BoQua upsert/delete) + `backend/StockRadar.Application/DTOs/NhatKyDtos.cs` theo contracts/nhat-ky-api.md (request không có `userId`/`giaTinHieuChup`)
- [ ] T007 [US1] Repository `backend/StockRadar.Infrastructure/Persistence/Repositories/EfNhatKyRepositories.cs` — mọi query lọc `currentUser.UserId` (pattern `EfWatchlistListRepository`, EfRepositories.cs:149-155) + DI trong `backend/StockRadar.Infrastructure/DependencyInjection.cs`
- [ ] T008 [US1] `backend/StockRadar.Application/Services/NhatKyService.cs` skeleton + DI: dùng `ICurrentUserService`, result nội bộ (NotFound/ValidationFail/OverSell); enforcement [Authorize] test ở T017

**Checkpoint**: Foundation ready — bảng + quyền theo user sẵn sàng; bắt đầu US1.

---

## Phase 3: User Story 1 — Ghi lệnh mua/bán thật (Priority: P1) 🎯 MVP

**Goal**: FR-001 — thêm/sửa/xoá lệnh (mã, chiều, thời điểm, giá khớp, KL, phí, ghi chú, lý do), validation 400/422 (Q7), vị thế projection (FR-004), giá vốn bình quân (Q5), lệnh Điều chỉnh (N7/FR-010), tab mobile "Nhật ký" (Q4).

**Independent Test**: US1 AC1–AC4 + quickstart KS-1, KS-5, KS-6 — thêm mua + bán cùng mã, sửa giá recompute ngay, bán quá vị thế 422 không lưu, user B không thấy/không sửa được lệnh user A.

### Tests for User Story 1 (viết TRƯỚC, phải FAIL trước khi implement) ⚠️

- [ ] T009 [US1] Test validation + bán quá vị thế + bình quân + phí + xoá-recompute trong `backend/StockRadar.Tests/NhatKy/NhatKyLenhTests.cs` (gộp các ca T011–T016 cũ vì cùng file: thiếu `giaKhop`/`khoiLuong` → ValidationFail; thời điểm tương lai → fail; DieuChinh thiếu ghiChu → fail; bán quá/chưa có mua → OverSell kèm `khoLuongHienCo`, không lưu (Q7); mua 2 lô KL khác nhau → BQ theo KL; DieuChinh +KL đổi KL mở + giảm BQ đúng data-model §Bảng 3; sửa giá lô đầu → BQ đổi (AC3); `phi` null → % config 0.15/0.25/0.1, `phi` tiền → % hiệu dụng, P&L khớp `RealizedPnlMath.Compute` cùng input; xoá lô bán → vị thế recompute)
- [ ] T010 [P] [US1] Test cách ly UserId trong `backend/StockRadar.Tests/NhatKy/NhatKyCachLyTests.cs` (N6/FR-009): user B không list được lệnh A; Get/Update/Delete id của A → NotFound (controller map 404, không 403); tạo lệnh bỏ qua userId trong body, luôn dùng token

### Implementation for User Story 1

- [ ] T011 [US1] Domain math: `backend/StockRadar.Domain/Services/TinhGiaVonBinhQuan.cs` (pure — nạp danh lệnh → KL mở + giá vốn BQ theo thời điểm + áp DieuChinh; phần tiền qua `RealizedPnlMath`, không copy công thức) + unit test `backend/StockRadar.Tests/NhatKy/TinhGiaVonBinhQuanTests.cs` (viết trước chính T011)
- [ ] T012 [US1] Hoàn thiện `NhatKyService` (backend/StockRadar.Application/Services/NhatKyService.cs): TaoLenh/SuaLenh/XoaLenh/LayDanhSach/LayViThe — đủ 5 validation rules data-model, OverSell mang `khoLuongHienCo`, chỉ dùng `INhatKyRepository`
- [ ] T013 [US1] `backend/StockRadar.Api/Controllers/NhatKyController.cs`: `[Route("api/v1/nhat-ky")]` + `[Authorize]`; endpoints US1 `GET/POST /lenh`, `GET/PUT/DELETE /lenh/{id}`, `GET /vi-the`; mapping NotFound→404, ValidationFail→400, OverSell→422 `{"detail","khoLuongHienCo"}` (contracts §Mã lỗi)
- [ ] T014 [US1] `dotnet test --filter FullyQualifiedName~NhatKy` xanh → full suite ≥ 349 + test mới; `backend/restart-api.ps1`; smoke: `GET /api/v1/nhat-ky/lenh` không token → **401**

### Mobile for User Story 1

- [ ] T015 [US1] `mobile/lib/core/models/models.dart`: `NhatKyLenh`, `ViTheThat` (fromJson/toJson; UTC↔VN qua `api_date.dart` — Q6); `mobile/lib/core/api/api_client.dart`: `getNhatKyLenh/themLenhNhatKy/suaLenhNhatKy/xoaLenhNhatKy/getViTheNhatKy` (Bearer tự gắn — không đổi auth infra)
- [ ] T016 [US1] Màn hình: `mobile/lib/screens/nhat_ky_screen.dart` (danh sách lệnh + FAB, menu sửa/xoá; pattern `watchlist_screen.dart`) + `mobile/lib/screens/nhat_ky_lenh_screen.dart` (form Mua/Ban/DieuChinh, validate client tối thiểu, hiển thị lỗi 400/422 từ API; chưa có picker tín hiệu — US2)
- [ ] T017 [US1] Navigation (Q4): `mobile/lib/core/navigation/app_router.dart` thêm GoRoute `/journal` trong ShellRoute; `mobile/lib/widgets/app_bottom_nav.dart` thêm nhãn 'Nhật ký' + icon; `mobile/lib/widgets/app_shell.dart` cập nhật mapping index; chưa đăng nhập → màn hiện lời mời đăng nhập, không crash (callback `onUnauthorized` đã có)
- [ ] T018 [US1] `cd mobile; flutter analyze` 0 issue mới; build debug (`flutter build apk --debug` hoặc `flutter run -d windows`) để màn `/journal` render — ghi rõ thiết bị/emu đã dùng

**Checkpoint**: US1 hoàn chỉnh — API + mobile dùng được. **DỪNG, BÁO CÁO** (chỉ đạo: chưa làm US2/US3).

---

## Phase 4: User Story 2 — Gắn lệnh với tín hiệu (Priority: P1)

**Goal**: FR-002/FR-003 — picker V2 + VIP (Q2), giá tín hiệu server tự chụp, cột lệch so tín hiệu, đánh dấu bỏ qua (Q3).

**Independent Test**: US2 AC1–AC3 + quickstart KS-2/KS-3.

### Tests for User Story 2 ⚠️ (viết trước)

- [x] T019 [US2] `backend/StockRadar.Tests/NhatKy/NhatKyTinHieuTests.cs`: seed `KetQuaKichBan DaKichHoat` → gắn V2, server chụp `giaTinHieuChup = GiaVaoThucTe ?? KeHoach.GiaVaoLenhMin` (research D2); client gửi giá chụp bị bỏ qua; `lechSoTinHieu` đúng khi list; tín hiệu không tồn tại → NotFound
- [x] T020 [P] [US2] Test bo-qua (cùng file T019 hoặc `NhatKyBoQuaTests.cs`): upsert idempotent, delete, unique (user+nguon+ma), POST lệnh gắn cùng tín hiệu tự xoá đánh dấu (data-model note)
- [x] T021 [P] [US2] Test nguồn VIP (Q2): seed `MasterAlertPositions` → picker trả `giaTinHieu = EntryPrice`; tín hiệu đổi/xoá sau khi gắn → lệnh vẫn giữ giá chụp (Edge case)

### Implementation for User Story 2

- [x] T022 [US2] Service + repository mở rộng: `LayTinHieuV2Async/LayTinHieuVipAsync` (SELECT-only, không đụng `IHieuQuaKichBanService`/`RealizedPnlService`/runner), `CapNhatBoQuaAsync/XoaBoQuaAsync`, kiểm tín hiệu + chụp giá trong `TaoLenhAsync`; endpoints `GET /nhat-ky/tin-hieu/v2|vip`, `PUT/DELETE /nhat-ky/tin-hieu/{nguon}/{ma}/bo-qua` trong `NhatKyController.cs`
- [x] T023 [US2] Mobile: picker trong `nhat_ky_lenh_screen.dart` (list V2+VIP, chọn → điền sẵn symbol/chiều/giá; nút "Bỏ qua"); cột lệch trên dòng lệnh ở `nhat_ky_screen.dart`
- [x] T031 [US2] Sửa lỗi N8 (chủ sản phẩm chốt 2026-10-22): giá chụp theo chiều + thứ tự lệnh trong chu kỳ (mua1/mua2, bán đầu→bán nửa/bán hết, bán sau→bán hết; VIP theo `PositionSellLegs`, mua2 không có cột → null); bậc thiếu giá → lệch null; thêm/sửa/xoá làm dịch bậc → chụp lại; BQ chỉ gộp cùng chiều + cùng mức chụp — test `backend/StockRadar.Tests/NhatKy/NhatKyN8GiaChupTests.cs` (13 ca, gồm ví dụ lỗi −0.1đ) + sửa service/repository đọc chân giá

**Checkpoint**: US1 + US2 độc lập hoạt động.

---

## Phase 5: User Story 3 — Lãi/lỗ và thống kê tài khoản thật (Priority: P2)

**Goal**: FR-005/FR-006 — P&L ròng vị thế đóng qua `RealizedPnlMath` (Q5/N5), tỷ lệ thắng, tổng P&L theo kỳ, lệch TB + tỷ lệ khớp tín hiệu; kết quả thật cạnh kết quả tín hiệu (US3 AC3).

**Independent Test**: US3 Independent Test (mua 1000@10, bán 500@11, bán 500@9 → ≈ −4.9%/vốn, "Thua") + quickstart KS-4.

### Tests for User Story 3 ⚠️ (viết trước)

- [x] T024 [US3] `backend/StockRadar.Tests/NhatKy/NhatKyThongKeTests.cs`: đóng vị thế 2 nhịp → `ReturnOnDeployedPercent` khớp `RealizedPnlMath.Compute`; Thắng/Thua/Ngang theo ngưỡng `WinThreshold=0`; tỷ lệ thắng loại vị thế mở; kỳ group theo ngày nhịp bán cuối (contracts §5 note)
- [x] T025 [P] [US3] Test `tyLeKhopTinHieu` + `lechTBSoTinHieuPhanTram` theo user/kỳ (dữ liệu kiểu T019–T021)

### Implementation for User Story 3

- [x] T026 [US3] `NhatKyService.LayThongKeAsync(ky)` + `GET /nhat-ky/thong-ke?ky=` trong `NhatKyController.cs` (contracts §5)
- [x] T027 [US3] Mobile: tabs con "Vị thế" + "Tổng hợp" trong `nhat_ky_screen.dart` (cards `glass_card.dart`; format VND; dòng gắn tín hiệu hiện "kết quả thật | kết quả tín hiệu")

**Checkpoint**: đủ 3 story.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T028 [P] Docs: `docs/domain/realized-pnl.md` (mục Nguồn đối chiếu — `PerformanceController` + 2 màn mobile đã xoá ở `183adb5`, docs lệch code), `docs/entity_model.md` (thêm NhatKyGiaoDich/NhatKyTinHieuBoQua + note Q1), `docs/domain/pipeline-jobs.md` nếu liên quan
- [ ] T029 [P] Full `dotnet test` + `flutter analyze` lượt cuối; chạy quickstart KS-2 bước 4 (FR-007: `hieu-qua/tom-tat` trước/sau khi có dữ liệu nhật ký **không đổi**)
- [ ] T030 Quyết định chủ sản phẩm (không tự làm): dọn `TradeJournalEntries` + `ITradeJournalRepository` hay giữ cho tính năng sau (Q1)

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 → Phase 2; **Foundational (T002–T008) chặn mọi user story**.
- US1 (T009–T018) → checkpoint DỪNG theo chỉ đạo; US2 (T019–T023) và US3 (T024–T027) các lượt sau.
- US2 không cần migration thêm (2 bảng đã tạo ở T004). US3 cần US1 (lệnh) + US2 (số liệu tín hiệu cho thống kê khớp).
- Polish cuối.

### Within Each User Story

Tests trước (fail) → Domain math → Service → Controller → smoke → Mobile models → screens → navigation → analyze/build.

### Parallel Opportunities

- T006 song song T002–T005 (khác file, cùng phase 2).
- T010 song song T009 (khác file test).
- T020/T021 song song T019; T025 song song T024; T028/T029 song song.
- US2 ∥ US3 backend sau khi US1 xong (khác endpoint, cùng file service/controller → không [P] giữa T022 và T026).

## Implementation Strategy

1. MVP = Phase 1 + 2 + 3 (US1) → **dừng, validate, báo cáo** — đúng chỉ đạo lượt này.
2. US2 → test KS-2/KS-3 → báo cáo.
3. US3 → KS-4 → Polish.
4. Commit/ship là hành động của chủ sản phẩm, không thuộc tasks.

## Notes

- Baseline trước khi bắt đầu: **349/349** (`dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj`).
- Comment code chỉ 4 nhãn DO-NOT-CHANGE/SECURITY/LIMIT/BUSINESS-RULE, ≤2 dòng.
- EF InMemory không bắt được DDL issues của SQLite — T005 là bước bắt buộc để verify migration thật.

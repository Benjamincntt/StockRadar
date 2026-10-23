# Implementation Plan: Nhật ký giao dịch thật (Trade Journal)

**Branch**: `010-trade-journal` | **Date**: 2026-10-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-trade-journal/spec.md`

**Trạng thái quyết định**: N1–N7 **đã chốt 2026-10-21**; Q1–Q7 và N5 **đã được chủ sản phẩm duyệt 2026-10-21** (xem bảng "Quyết định đã chốt" bên dưới). Phần thiết kế còn lại (tên cột, chi tiết DTO) vẫn có thể điều chỉnh ở bước tasks.

## Quyết định đã chốt (bổ sung 2026-10-21)

Chủ sản phẩm duyệt **cả 7 đề xuất Q1–Q7** ngày 2026-10-21, kèm N5 (xác nhận kết luận research D3):

| # | Quyết định chốt | Trạng thái |
|---|---|---|
| Q1 | Tạo bảng mới `NhatKyGiaoDich`; **giữ nguyên** `TradeJournalEntries` cũ, chờ dọn ở Polish sau khi xác nhận | Đã chốt 2026-10-21 |
| Q2 | Thêm endpoint **chỉ đọc** `GET /api/v1/nhat-ky/tin-hieu/vip` lấy tín hiệu V1 từ `MasterAlertPositions` cho picker; không phục hồi `PerformanceController` | Đã chốt 2026-10-21 |
| Q3 | Bảng riêng `NhatKyTinHieuBoQua` cho "không khớp / bỏ qua"; không dùng "lệnh ma" | Đã chốt 2026-10-21 |
| Q4 | Thêm tab 4 "Nhật ký" (`/journal`) ở bottom nav mobile | Đã chốt 2026-10-21 |
| Q5 | Giá vốn **bình quân**; dùng lại `RealizedPnlMath` (một công thức, một chỗ) | Đã chốt 2026-10-21 |
| Q6 | Lưu **DateTime UTC** theo convention repo; API xuất ISO-8601 UTC, UI hiển thị giờ VN | Đã chốt 2026-10-21 |
| Q7 | Bán quá vị thế / bán khi chưa có mua: **chặn cứng 422**, không lưu | Đã chốt 2026-10-21 |
| N5 | Không dùng chung dữ liệu với "Lợi nhuận thực" V1; dùng lại `RealizedPnlMath`/`FeeProfile`/config `RealizedPnl` | **Đã duyệt 2026-10-21** |

## Summary

Thêm nhật ký lệnh **thật** của tài khoản người dùng (nhập tay, không nối CKB): bảng lệnh mới theo từng user, API REST mới, màn mobile mới. Tính lãi/lỗ ròng sau phí/thuế bằng cách **dùng lại `RealizedPnlMath`** (math thuần, DB-free), nhưng **không dùng chung dữ liệu** với "Lợi nhuận thực" V1. Lệnh có thể gắn tín hiệu V2 (`KetQuaKichBan`) hoặc V1 VIP (`MasterAlertPositions`) kèm ảnh chụp giá tín hiệu lúc gắn → cột "lệch so tín hiệu". Vị thế đang giữ và thống kê (tỷ lệ thắng, tổng P&L theo kỳ, lệch TB, tỷ lệ khớp) tính on-the-fly từ lệnh, không bảng trạng thái.

## Technical Context

**Language/Version**: .NET 10 (backend), Dart/Flutter (mobile) — theo repo hiện có

**Primary Dependencies**: EF Core (SQLite `stockradar.db`), ASP.NET Core JWT Bearer đã cấu hình sẵn (`Program.cs:59-73`); mobile: `go_router`, `http` qua `ApiClient` sẵn có (`mobile/lib/core/api/api_client.dart:126-130` tự gắn `Authorization: Bearer`)

**Storage**: SQLite qua `ApplicationDbContext` — thêm bảng mới, **không** sửa bảng/API/DTO hiện có

**Testing**: xUnit (`backend/StockRadar.Tests`, EF InMemory); Flutter `test` nếu screen có unit-testable logic

**Target Platform**: API trên server Linux production (deploy qua `scripts/ship-all.ps1` — ngoài phạm vi plan này); mobile Flutter (Android ưu tiên)

**Project Type**: Monorepo web-service + mobile app (theo constitution "Ranh giới stack")

**Performance Goals**: Nhật ký cá nhân, cỡ ≤ vài nghìn lệnh/user; endpoint danh sách < 200ms; không yêu cầu đặc biệt

**Constraints**: FR-007 — dữ liệu nhật ký KHÔNG được ảnh hưởng số liệu tín hiệu (V2 Pha 3, V1 realized, T+2.5): chỉ thêm bảng/endpoint mới, không đụng runner/engine hiện có. N4: mobile trước, API đủ để web làm sau. N6: đa người dùng, cách ly theo `UserId`.

**Scale/Scope**: 3 bảng mới + 1 nhóm controller mới + 1 service + 1 repository + ~2 màn mobile (journal + form). Không đổi schema bất kỳ bảng cũ nào.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code as truth**: Plan này trích file:line cụ thể đã đọc trực tiếp (research.md §Bằng chứng code). Phát hiện quan trọng: `docs/domain/realized-pnl.md:19-20` mô tả `PerformanceController.cs` và `performance_screen.dart`/`alert_history_screen.dart` **đã bị xoá** ở commit `183adb5` ("delete V1 redundant screens") → docs đó lệch code; tin code. `docs/domain/*` cần cập nhật sau implement (đã ghi ở quickstart mục follow-up).
- [x] **II. Spec-before-design-change**: Route API mới + màn mobile mới + bảng mới là "trọng yếu" theo nguyên tắc II → đã có `spec.md` (N1–N7 chốt); plan này là bước `/speckit-plan`, tasks sẽ qua `/speckit-tasks`.
- [x] **III. Minimal surface**: Chỉ thêm mới: bảng `NhatKy*`, controller `NhatKyController`, service/repository mới, màn mobile mới. Không refactor, không đổi DTO/API/endpoint hiện có. `TradeJournalEntries` (bảng dormancy — xem D4) **không bị xoá/dùng lại** trong lượt này; đề xuất xử lý ở câu hỏi Q1.
- [x] **IV. Domain gates**: Không đổi Buy Score / Top / MA·phase / flatBox / luật tín hiệu. FR-007 đảm định số liệu tín nguyên vẹn. Không cần cập nhật docs cổng domain; chỉ thêm doc feature mới (`docs/` cập nhật ở bước ship).
- [ ] → [x] **V. Simplicity**: Không thêm dependency mới. MỘT điểm cân nhắc: repo đã có bảng `TradeJournalEntries` (Guid Id, UserId, Symbol, TradeDate, Action, SizePercent, EngineVerdict, Note, BuyScore, PredictedHit, SetupDna — `PerformanceEntities.cs:259-273`) **không có giá khớp/khối lượng/phí** và **không có consumer nào** (DI chỉ tại `DependencyInjection.cs:202`; không controller/service nào gọi `ITradeJournalRepository`). Tạo bảng song song là nguy cơ "two structures for one concept" → ghi dòng Complexity Tracking bên dưới, chờ Q1.
- [x] **Stack**: Domain (math/const) → Application (service, DTOs, abstraction) → Infrastructure (EF entity, repository, migration) → Api (controller) → `mobile/lib` (screen, api_client, router). Đúng ranh giới constitution; restart/ship scripts chỉ chạy khi implement, không phải plan.

**Re-check sau Phase 1**: data-model + contracts chỉ thêm mới, không route/DTO cũ bị chạm → gates vẫn pass. Migration EF Core mới là thủ tục thường lệ (pattern 80+ migrations hiện có trong `backend/StockRadar.Infrastructure/Migrations/`), không phải vi phạm.

## Project Structure

### Documentation (this feature)

```text
specs/010-trade-journal/
├── spec.md              # đã có (N1–N7)
├── plan.md              # file này
├── research.md          # Phase 0 — kết luận đối chiếu code (D1–D7)
├── data-model.md        # Phase 1 — 3 bảng mới + validation + công thức
├── quickstart.md        # Phase 1 — cách chạy kiểm chứng
├── contracts/
│   └── nhat-ky-api.md   # Phase 1 — REST /api/v1/nhat-ky
└── tasks.md             # Phase 2 (/speckit-tasks — KHÔNG tạo trong lượt này)
```

### Source Code (repository root)

```text
backend/
├── StockRadar.Api/              # + NhatKyController (route "api/v1/nhat-ky", [Authorize] — pattern WatchlistsController.cs:8-19)
│   └── appsettings.json         # (không cần sửa — phí mặc định đã có ở section "RealizedPnl", dòng 356-364)
├── StockRadar.Application/      # + DTOs/NhatKyDtos.cs, + Abstractions/INhatKyRepository.cs,
│                                # + Services/NhatKyService.cs (ICurrentUserService.UserId — N6)
├── StockRadar.Domain/           # (không mới — dùng lại RealizedPnlMath.cs, FeeProfile; + TenBang/NhatKyHangSo nếu cần const)
├── StockRadar.Infrastructure/   # + Persistence/Entities/NhatKyEntities.cs, + Repository, + Migration
└── StockRadar.Tests/            # + NhatKy/ (service tests: average cost, P&L, lệch, cách ly user)

mobile/lib/
├── screens/nhat_ky_screen.dart          # màn chính: danh sách lệnh + vị thế + summary
├── screens/nhat_ky_lenh_screen.dart     # form thêm/sửa lệnh + picker tín hiệu
├── core/api/api_client.dart             # + methods get/post/put/delete /nhat-ky/*
├── core/models/models.dart              # + models NhatKyLenh, ViTheThat, NhatKyThongKe
└── core/navigation/app_router.dart      # + route /journal (pattern /performance — dòng 60-66)
```

**Structure Decision**: Tính năng đi theo đường watchlist hiện có (controller mỏng → service Application → repository Infrastructure → `ICurrentUserService`), không introduces pattern mới.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (V) Bảng nhật ký mới song song với `TradeJournalEntries` đang tồn tại | `TradeJournalEntries` (`PerformanceEntities.cs:259-273`) thiếu hoàn toàn các trường bắt buộc của N2 (giá khớp, khối lượng, phí, tham chiếu tín hiệu + giá chụp) và **không có consumer nào** (chỉ DI `DependencyInjection.cs:202`) | (a) Reuse/sửa bảng cũ: phải thêm cột + đổi semantics của table đã ghi trong `docs/entity_model.md:289` trong khi không rõ ai đang cần nó — vi phạm III (minimal surface) và mạo hiểm dữ liệu tương lai; (b) Delete bảng cũ: cần migration + chủ sản phẩm đồng ý → chuyển thành **Q1**, chưa tự làm. |

## Câu hỏi cần chủ sản phẩm chốt

*(Theo yêu cầu: mỗi câu kèm 1 đề xuất + lý do. Bản plan này KHÔNG tự chốt.)*

**Q1 — Bảng `TradeJournalEntries` dormancy.** Repo đã có bảng + repository `ITradeJournalRepository` nhưng không controller/service nào gọi (bằng chứng ở Complexity Tracking). Số liệu tín hiệu không dùng nó → có vẻ là placeholder từ tính năng "Personal calibration" chưa bao giờ được nối.
→ **Đề xuất**: tạo bảng mới `NhatKyGiaoDich` như thiết kế này, **giữ nguyên** bảng cũ, đánh dấu "deprecated, chờ dọn" trong docs; dọn ở Polish pass sau khi anh xác nhận không cần. *Lý do: xoá/reuse bảng cũ cần migration + quyết định ngoài phạm vi N1–N7; giữ lại không gây hại vì không có consumer.*

**Q2 — Tín hiệu V1 VIP: source nào khi API cũ đã bị xoá?** `docs/domain/realized-pnl.md` ghi `PerformanceController` + `GET /performance/alert-history` là nguồn VIP alert history, nhưng controller đã bị xoá ở commit `183adb5`; `mobile/lib/core/api/api_client.dart:451-476` vẫn còn method `getAlertHistory` gọi endpoint 404. Dữ liệu vẫn còn trong `MasterAlertPositions` (+ `PositionSellLegs`, `VipAlertFires`).
→ **Đề xuất**: thêm endpoint **read-only mới, tối giản** `GET /api/v1/nhat-ky/tin-hieu/vip` đọc thẳng `MasterAlertPositions` (Symbol, EntryDate, EntryPrice = giá tín hiệu MUA, trạng thái mở/đóng), KHÔNG phục hồi `PerformanceController` hay màn V1 đã xoá. *Lý do: N1 chốt là picker gồm cả V1; đây là route mới thêm (không vi phạm "chỉ thêm mới"), và tránh tái sinh cả màn V1 redundant đã chủ đích gỡ.*

**Q3 — Gán "không khớp / bỏ qua" cho tín hiệu mà không có lệnh (FR-003).**
→ **Đề xuất**: bảng nhỏ `NhatKyTinHieuBoQua` (UserId, nguồn, MãTinHieu, LýDo, NgàyBỏQua) — đánh dấu ngay trên picker, không tạo "lệnh ma". *Lý do: lệnh ma (khối lượng 0) sẽ làm ô nhiễm các tổng hợp P&L/vị thế; bảng tách rời giữ công thức lệnh sạch và tỷ lệ khớp = số tín hiệu có lệnh / (có lệnh + bỏ qua) tính rõ ràng.*

**Q4 — Vị trí màn nhật ký trên navigation mobile.** Bottom nav hiện đúng 3 tab: Trang chủ / Watchlist / Hiệu quả (`app_router.dart:33-37`, `app_bottom_nav.dart`).
→ **Đề xuất**: tab thứ 4 "Nhật ký" (`/journal`). *Lý do: N4 đặt mobile làm nền tảng chính của feature; giấu trong menu phụ khiến ghi lệnh >30s (SC-001) khó đạt.*

**Q5 — Khớp lệnh lãi/lỗ theo bình quân hay FIFO.** Spec N3/US3 nói "giá vốn bình quân"; SC-002 muốn khớp sao kê CKB ±0.01%.
→ **Đề xuất**: **bình quân** (weighted average cost), dùng thẳng `RealizedPnlMath.Compute(giaVonBinhQuan, legs, fees)` với mỗi nhịp bán là một `SellLeg`. *Lý do: khi đóng hết vị thế, tổng P&L ròng của bình quân và FIFO **trùng nhau** (tổng tiền vào − tổng tiền ra như nhau); bình quân đơn giản hơn, khớp đúng cụm từ "giá vốn bình quân" trong FR-004, và CKB VN tính phí/thuế trên giá trị từng lệnh nên sai số chỉ ở làm tròn ≤0.01%.*

**Q6 — Múi giờ thời điểm lệnh.** `KetQuaKichBanEntity` đang lưu UTC (`ThoiGianKichHoat` DateTime UTC) trong khi người dùng nghĩ theo giờ VN.
→ **Đề xuất**: cột `ThoiDiemKhop` lưu **DateTime UTC** như convention hiện có, API nhận/gửi kèm offset rõ (`ApiDate` helpers mobile đã có — `mobile/lib/core/time/api_date.dart`), UI hiển thị giờ VN. *Lý do: thống nhất với toàn bộ schema cũ, tránh cùng DB hai convention.*

**Q7 — Khối lượng chặn bán quá vị thế (AC4 US1) cứng hay mềm.**
→ **Đề xuất**: **cứng** — 422 không lưu (đúng AC4 "cảnh báo, không lưu"); riêng "bán khi chưa có mua" (edge case) là **cứng** vì cùng bản chất sai số liệu. *Lý do: cho qua dữ liệu bẩn sẽ làm giá vốn bình quân âm/null và phá mọi thống kê; người dùng sửa lệnh sau đó được (AC3).*

## Design Decisions Summary (đề xuất, chờ duyệt qua Q1–Q7)

1. **3 bảng mới**: `NhatKyGiaoDich` (lệnh), `NhatKyTinHieuBoQua` (đánh dấu bỏ qua — Q3), *không* bảng vị thế (tính on-the-fly). FK `UserId → Users` cascade, index `(UserId, ThoiDiemKhop)` — pattern `Watchlists` (`ApplicationDbContext.cs:109-120`).
2. **Cách ly user (N6)**: controller `[Authorize]`; service lấy `ICurrentUserService.UserId` (`IAuth.cs:3-7`, impl `AuthInfrastructure.cs:44-61` đọc `ClaimTypes.NameIdentifier` từ JWT — claims do `JwtTokenService.CreateToken` ghi tại `AuthInfrastructure.cs:26-31`). Query luôn lọc `UserId`; sửa/xoá lệnh của user khác → **404** (không lộ sự tồn tại). Đây chính xác là pattern `EfWatchlistListRepository` (`EfRepositories.cs:149-155`).
3. **Tín hiệu & lệch (N1/N3/FR-002)**: lệnh lưu `NguonTinHieu` ("V2"/"VIP"), `MaTinHieu` (string hoá: V2 = `KetQuaKichBan.Id`; VIP = position buy `MasterAlertPositions.Id` — sell leg quy về cycle của position cha), `GiaTinHieuChup` (decimal, chụp lúc gắn; tín hiệu bị xoá/huỷ sau đó vẫn giữ — edge case). `Lech = GiaKhop − GiaTinHieuChup`; `% = Lech / GiaTinHieuChup`. Nhiều lệnh gắn một tín hiệu (edge case chia nhỏ) → lệch theo giá khớp bình quân của các lệnh cùng tín hiệu.
4. **Công thức (data-model.md §Công thức)**:
   - Giá vốn bình quân sau phí mua: `buyCost = giá × (1 + PhiMua%/100)` (đúng `RealizedPnlMath.NetLegReturnPercent:42`); bình quân trọng số theo khối lượng.
   - Lãi/lỗ ròng nhịp bán `i`: `R_i` = `RealizedPnlMath.NetLegReturnPercent(giaVonBinhQuan, giaBan_i, fees)`; vị thế đóng → `ReturnOnDeployedPercent` = tổng hợp trọng số size (`RealizedPnlMath.Compute:51-93`). Thang/Thua/Ngang theo ngưỡng `RealizedPnl:WinThresholdPercent` (=0).
   - Phí per-order (FR-005): cột `PhiMua`/`PhiBan` **bằng tiền (VND), nullable** — null → dùng % config (`RealizedPnl` section: 0.15/0.25/0.1 — `appsettings.json:356-364`); có tiền → dùng tiền thực (quy đổi `%hiệu dụng = Phi / (giá × KL) × 100` khi gọi math).
   - Tỷ lệ khớp tín hiệu = số tín hiệu có ≥1 lệnh / (số có lệnh + số bỏ qua) trong kỳ, theo user.
5. **Lệnh "điều chỉnh" (N7/FR-010)**: `LoaiLenh` = `Mua | Ban | DieuChinh`. `DieuChinh`: khối lượng ±, giá vốn điều chỉnh ±, **bắt buộc ghi chú**, không giá/phí; áp dụng cuối chuỗi theo thời điểm, chỉ ảnh hưởng vị thế đang mở của mã đó.
6. **Không đụng tín hiệu (FR-007)**: feature chỉ *đọc* `KetQuaKichBan` (endpoint tín hiệu V2 tái dùng pattern `HieuQuaController.cs:34-43` `/hieu-qua/lich-su` — hoặc read thẳng table qua repository mới, không sửa service cũ) và `MasterAlertPositions`. Không migration trên bảng cũ, không đổi DTO cũ.
7. **Mobile**: 2 màn mới + `ApiClient` methods + route `/journal` (Q4). Form có picker tín hiệu gần đây (V2 + VIP theo Q2). Summary cards dùng widget sẵn có (`glass_card.dart`, `score_pill.dart`).

## Post-Design Constitution Re-check

- Route mới `/api/v1/nhat-ky/*` không trùng 17 controller hiện có (đã liệt kê `[Route("...")]` trong `StockRadar.Api/Controllers`). ✔
- Thêm bảng + migration: đúng layer Infrastructure; math ở Domain (tái sử dụng), use-case ở Application — constitution "Ranh giới stack". ✔
- Không drive-by refactor; `TradeJournalEntries` để nguyên (Q1). ✔
- Đơn giản: không abstraction mới ngoài pattern repository/service đã chuẩn; 1 dòng Complexity Tracking đã ghi. ✔

**Next step (ngoài phạm vi lượt này)**: `/speckit-tasks` sau khi chủ sản phẩm trả lời Q1–Q7 (hoặc xác nhận "làm theo đề xuất").

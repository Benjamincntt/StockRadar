# Phase 0 Research: Nhật ký giao dịch thật

**Branch**: `010-trade-journal` | **Date**: 2026-10-21
**Method**: đọc code trực tiếp trên disk (Hiến pháp I — "code là nguồn sự thật runtime"), mỗi kết luận kèm file:line. Không chạy build/production; toàn bộ là đọc tĩnh.

## D1 — Cách lấy UserId từ token và phân quyền hiện có (N6)

**Decision**: tái sử dụng nguyên vẹn stack JWT + `ICurrentUserService`; controller mới gắn `[Authorize]`, service/repository lọc theo `ICurrentUserService.UserId`.

**Bằng chứng code**:
- JWT Bearer cấu hình trong `backend/StockRadar.Api/Program.cs:59-73` (ValidateIssuer/Audience + `JwtOptions.Secret`); `app.UseAuthentication(); app.UseAuthorization();` tại dòng 165-166.
- Token chứa `ClaimTypes.NameIdentifier = user.Id` (Guid) khi đăng nhập: `JwtTokenService.CreateToken` — `backend/StockRadar.Infrastructure/Identity/AuthInfrastructure.cs:26-31`. Endpoint phát token: `POST /api/v1/auth/tokens` (`AuthController.cs:13-25`); đăng ký `POST /api/v1/users` (`UsersController.cs:13-28`).
- Đọc UserId request-scoped: `CurrentUserService` — `AuthInfrastructure.cs:44-61`: `httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)` → `Guid`, fallback `GuestUser.Id` khi không token. Interface `ICurrentUserService` tại `backend/StockRadar.Application/Abstractions/IAuth.cs:3-7`; DI `AddScoped<ICurrentUserService, CurrentUserService>()` — `backend/StockRadar.Infrastructure/DependencyInjection.cs:231`.
- **Pattern chuẩn cho dữ liệu theo user**: `EfWatchlistListRepository` (`backend/StockRadar.Infrastructure/Persistence/Repositories/EfRepositories.cs:149-155`) — mỗi method lấy `currentUser.UserId` rồi `Where(x => x.UserId == userId)`; entity `WatchlistEntity` có `UserId` + FK cascade tới `Users` (`ApplicationDbContext.cs:109-120`). Controller `WatchlistsController` **không** có `[Authorize]` (accept cả guest) — nhưng nhật ký là dữ liệu riêng tư ⇒ đề xuất `[Authorize]` bắt buộc đăng nhập (tránh ghi vào `GuestUser.Id` vô chủ). Đây là điểm khác biệt có chủ đích duy nhất so với pattern watchlist.
- Cách ly khi sửa/xoá: repository kiểm `Id` thuộc `UserId` hiện tại, không khớp → trả null → controller 404 (không lộ sự tồn tại của lệnh user khác).

**Alternatives considered**: (a) truyền `userId` trong query string — bác bỏ, dễ giả mạo; (b) chính sách `[Authorize(Policy=...)]` phân vai — quá dư cho mô hình "mỗi user một nhật ký" của N6.

## D2 — Nguồn tín hiệu để gắn lệnh (N1)

**Decision**: picker đọc **V2** từ bảng `KetQuaKichBan` (đã kích hoạt) và **V1 VIP** từ bảng `MasterAlertPositions` qua endpoint read-only mới của feature này.

**Bằng chứng code**:
- V2: `KetQuaKichBanEntity` — DbSet `KetQuaKichBan` (`ApplicationDbContext.cs:42, 485-515`), bảng cùng tên, các trường cần cho picker: `Symbol`, `LoaiKichBan`, `TrangThai` (`DaKichHoat`), `NgayDanhGia`, `ThoiGianKichHoat`, `KeHoachGiaoDichJson` (vùng vào/SL/CL — `GiaVaoLenhMin/Max`, `GiaDungLo`, `GiaChotLoi1/2`, `GiaMuaDiem2`), `GiaVaoThucTe`, `KetQuaDoLuong` ("Thắng/Thua/Ngang" — C-27/C-33). Giá tín hiệu để chụp: `GiaVaoThucTe` nếu có, nếu không thì lấy plan `GiaVaoLenhMin` (đúng cách `LichSuLenhDto.GiaVao` chọn — `backend/StockRadar.Application/DTOs/HieuQuaDtos.cs:44-50`).
- V2 history đã có API đọc sẵn không cần sửa: `GET /api/v1/hieu-qua/lich-su?page=&size=` (`HieuQuaController.cs:34-43`) — picker có thể dùng trực tiếp **hoặc** qua endpoint mới `/nhat-ky/tin-hieu/v2` trả kèm `GiaVaoThucTe`; đề xuất endpoint mới vì `LichSuLenhDto` hiện thiếu `GiaVaoThucTe` và không phân trang theo ngày (ràng buộc "không đổi DTO hiện có").
- V1 VIP: bảng `MasterAlertPositions` (`ApplicationDbContext.cs:29, 322-345`) — "1 dòng = 1 lệnh" theo `docs/domain/realized-pnl.md:68`; trường `Symbol`, `EntryDate`, `EntryPrice` (giá tín hiệu MUA), `IsClosed`, `RealizedReturnOnDeployedPercent` (kết quả tín hiệu để so song song — US3 AC3). Nhịp bán của tín hiệu: `PositionSellLegs` (`ApplicationDbContext.cs:30, 347-362`, `Signal` = `SellPoint1Half`/`SellAll`, `SellPrice`).
- ⚠️ **API V1 cũ đã bị xoá**: `docs/domain/realized-pnl.md:19-20` tham chiếu `PerformanceController.cs` + mobile `performance_screen.dart`/`alert_history_screen.dart`, nhưng glob toàn backend **không còn** `PerformanceController.cs`; git xác nhận xoá ở commit `183adb5` *"feat(v2): delete V1 redundant screens + add Pha3 outcome tracking + HieuQua UI"*. Hệ quả: `mobile/lib/core/api/api_client.dart:445-511` vẫn gọi `/performance/summary`, `/performance/alert-history`, `/performance/realized-trades` → nay là 404 (code chết phía mobile, nằm ngoài phạm vi spec này — ghi nhận, không "dọn" theo Hiến pháp III). Bảng `VipAlertFires` (`ApplicationDbContext.cs:31, 364-396`) chứa fires intraday (Signal, FirePrice, SessionDate) — **không** chọn làm nguồn picker vì mỗi phiên có thể nhiều fire lặp lại cùng lệnh; `MasterAlertPositions` mới là "1 dòng = 1 lệnh".
- Docs `realized-pnl.md` **lệch code** ở mục nguồn đối chiếu → theo Hiến pháp I, tin code; cần sửa docs khi ship feature này.

**Alternatives considered**: lưu snapshot tín hiệu vào hàng lệnh lúc gắn (clone JSON) — bán thiết kế: chỉ chụp **giá + tham chiếu Id**, vì kế hoạch đầy đủ hiển thị từ bảng gốc theo thời gian thực tốt hơn ảnh chụp; edge case "tín hiệu bị xoá" đã phủ bằng cột `GiaTinHieuChup` (spec Edge Cases).

## D3 — N5: có dùng lại được gì từ "Lợi nhuận thực" V1?

**Decision**: **XÁC NHẬN N5** — không dùng chung dữ liệu; dùng lại được `RealizedPnlMath` + `FeeProfile` + section config `RealizedPnl`. Chờ chủ sản phẩm duyệt vì N5 vốn là "đánh giá của trợ lý".

**Bằng chứng code**:
- `RealizedPnlMath` là math thuần, không DbContext, nằm ở Domain: `backend/StockRadar.Domain/Services/RealizedPnlMath.cs:34` (record `FeeProfile` dòng 6-19; `NetLegReturnPercent` 37-45; `Compute` 51-93; `Classify` 99-108). Đầu vào đúng hình dạng dữ liệu nhật ký cần: `entryPrice`, danh sách `SellLeg(SellDate, SellPrice, SoldSize)`, `FeeProfile` → **dùng lại trực tiếp**, không cần copy công thức (đúng tinh thần C-33: một công thức, một chỗ).
- Phí/thuế mặc định đã có sẵn config: `RealizedPnlOptions` (`backend/StockRadar.Application/Options/MasterAlertOptions.cs:232-255`): BuyFee 0.15%, SellFee 0.25%, SellTax 0.1%, WinThreshold 0 — khớp Assumption spec dòng 121; giá trị trong `appsettings.json:356-364`.
- **Dữ liệu V1 realized không trộn được**: `RealizedPnlService` (Application) ghi kết quả vào `MasterAlertPositionEntity.Realized*` — đo **giá tại tín hiệu** VIP, không có khái niệm user/lệnh thật; mọi aggregate V1 tính từ `MasterAlertPositions` (chống đếm trùng — `docs/domain/realized-pnl.md:66-70`, code `EfPerformanceRepositories.cs:440-455`). Nhật ký là dữ liệu **theo user** ⇒ bảng riêng, chỉ *đọc song song* `RealizedReturnOnDeployedPercent` của vị thế V1 để hiện cạnh kết quả thật (US3 AC3) — không ghi ngược.
- FR-007 thoả hiển nhiên: feature không gọi `RealizedPnlService`, không đụng `OpportunityPerformanceRunner`, không sửa bảng V1/V2.

**Alternatives considered**: (a) bảng `TradeJournalEntries` có sẵn — bác bỏ vì thiếu trường core và dormancy (D4); (b) viết math riêng cho nhật ký — bác bỏ, vi phạm nguyên tắc "một công thức một chỗ" đã áp dụng ở spec 009 C-33 (`ChamKetQuaChuKy`).

## D4 — Bảng `TradeJournalEntries` hiện có: dormancy, không tái sử dụng

**Decision**: không dùng, không sửa, không xoá trong feature này; đưa thành Q1.

**Bằng chứng code**: entity `TradeJournalEntryEntity` (`backend/StockRadar.Infrastructure/Persistence/Entities/PerformanceEntities.cs:259-273`) có `UserId` nhưng thiếu **giá khớp, khối lượng, phí, tham chiếu tín hiệu** — không đáp ứng N2. Model của nó là "snapshot quyết định theo tín hiệu" (BuyScore, PredictedHit, SetupDna, EngineVerdict). Repo `EfTradeJournalRepository` (`EfPerformanceRepositories.cs:1228-1273`) + DI (`DependencyInjection.cs:202`) tồn tại nhưng grep toàn backend **không có consumer** (không controller/service nào inject `ITradeJournalRepository`). `docs/entity_model.md:8,289` đã ghi TRADE_JOURNAL_ENTRY — tức từng được dự tính dùng; không rõ ai sẽ dùng ⇒ hỏi chủ sản phẩm (Q1).

## D5 — Công thức nghiệp vụ

**Decision**:

```
PhiMuaTyLeHieuDung  = PhiMuaTien (VND) / (GiaKhop × KhoiLuong) × 100   [nếu người dùng nhập tiền phí]
buyCost_per_share   = GiaKhop × (1 + PhiMua%/100)                       [khớp RealizedPnlMath:42]

Giá vốn bình quân   = Σ(buyCost_i × KL_i) / Σ KL_i        (chỉ lệnh MUA + DieuChinh còn hiệu lực)
Vị thế mở (KL)      = Σ KL_mua − Σ KL_ban  (theo Symbol, theo user, xử lý theo ThoiDiemKhop)

R_i (nhịp bán)      = (GiaBan_i × (1 − (PhiBan% + Thue%)/100) − GiaVonBQ) / GiaVonBQ × 100
                    = RealizedPnlMath.NetLegReturnPercent(GiaVonBQ, GiaBan_i, fees)
P&L ròng vị thế     = RealizedPnlMath.Compute(GiaVonBQ, legs, fees).ReturnOnDeployedPercent
Thắng/Thua/Ngang    = RealizedPnlMath.Classify(ReturnOnDeployed, WinThreshold)   (Good/Flat/Failed)

Tỷ lệ thắng (kỳ)    = số vị thế đóng "Thắng" / số vị thế đã đóng trong kỳ
Lệch so tín hiệu    = GiaKhop − GiaTinHieuChup ; % = lệch / GiaTinHieuChup × 100
Lệch theo tín hiệu (nhiều lệnh chia nhỏ) = GiáKhớpBinhQuân(các lệnh cùng tín hiệu) − GiaTinHieuChup
Tỷ lệ khớp tín hiệu = tín hiệu có ≥1 lệnh / (tín hiệu có lệnh + tín hiệu bỏ qua) trong kỳ
```

**Rationale**: mọi phép tiền đều quy về `RealizedPnlMath` (đã có 17 test xanh trong `StockRadar.Tests/RealizedPnl/RealizedPnlMathTests.cs`); bình quân thay vì FIFO vì khi đóng vị thế hai cách cho cùng tổng lãi/lỗ (xem Q5); phí per-order nhập **bằng tiền** vì sao kê CKB in ra tiền, đổi ra % hiệu dụng ngay tại service, math không đổi.

**Alternatives considered**: FIFO (khớp từng lệnh con với giá vốn từng lô — chỉ khác ở vị thế chưa đóng, và phức tạp không cần cho mục đích "so với tín hiệu"); chuẩn hoá lot 10 (le10 — bỏ qua, cho nhập khối lượng tự do, validate > 0).

## D6 — Validation phía ghi lệnh (US1 AC2/AC4, Edge cases)

- Bắt buộc: `Symbol` (≤16 ký tự, uppercase — chuẩn mọi bảng tickers hiện có `HasMaxLength(16)`), `LoaiLenh ∈ {Mua, Ban, DieuChinh}`, `ThoiDiemKhop`, `GiaKhop > 0`, `KhoiLuong > 0` với Mua/Ban ⇒ thiếu → 400.
- Bán quá vị thế / bán khi chưa có mua: tính tồn kho theo user+symbol tới thời điểm lệnh (sau khi trừ các lệnh trước, cộng DieuChinh) → vi phạm → **422** kèm số hiện có, không lưu (Q7 đề xuất cứng).
- `DieuChinh`: bắt buộc `GhiChu`; `KhoiLuongDieuChinh` ≠ 0 và không làm tồn kho âm.
- Sửa lệnh: recompute tự nhiên vì mọi thứ tính on-the-fly từ danh sách lệnh (AC3 US1 không cần code riêng) — đây là lý do **không** bảng hoá vị thế.

## D7 — Mobile: màn hình, router, API client

**Bằng chứng code**:
- Router: `mobile/lib/core/navigation/app_router.dart` — ShellRoute 3 tab `/` `/watchlist` `/performance` (dòng 29-67; label tab `app_bottom_nav.dart:13` `['Trang chủ','Watchlist','Hiệu quả']`); màn pushed ngoài shell theo path `/stocks/:symbol` (dòng 69-78). Màn mới thêm GoRoute trong shell (Q4 đề xuất tab 4) + form là pushed page.
- Màn tham chiếu cùng kiểu (danh sách + form + summary): `hieu_qua_screen.dart` (771 dòng — list + summary cards), `watchlist_screen.dart` (747 dòng — CRUD + dialog). Không có `performance_screen.dart`/`alert_history_screen.dart` (đã xoá ở `183adb5`) — docs stale (D2).
- API client: `ApiClient` singleton qua `app_services.dart`; `_request` tự thêm `Authorization: Bearer $_token` (dòng 126-130), `onUnauthorized` callback đã có (dòng 26-27) ⇒ login-state hoạt động sẵn; chỉ cần thêm methods `/nhat-ky/*` + models trong `models.dart`.
- Hiển thị giờ VN: `mobile/lib/core/time/api_date.dart` đã có helper; tiền: format Vietnamese trong các screen hiện có.

**Decision**: 2 màn (`nhat_ky_screen.dart` tabs con: Lệnh / Vị thế / Tổng hợp; `nhat_ky_lenh_screen.dart` form + picker tín hiệu), không state library mới (setState/ FutureBuilder như các màn hiện có).

## Câu hỏi còn mở

→ Chuyển toàn bộ vào plan.md §"Câu hỏi cần chủ sản phẩm chốt" (Q1–Q7). Không còn NEEDS CLARIFICATION kỹ thuật nào khác; các quyết định thiết kế còn lại đều có mặc định đề xuất.

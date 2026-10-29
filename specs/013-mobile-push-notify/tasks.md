# Tasks: Thông báo đẩy V3 + HPO lên app mobile (song song Telegram)

**Input**: Design documents in `specs/013-mobile-push-notify/` — **Revision 3**, Step 0 approved with four adjustments (29/10/2026). Update documents before coding, then proceed directly to T001–T003; no additional documentation stop.

**Prerequisites**: [plan.md](./plan.md) (decisions a–h + Revision 3 adjustments), [spec.md](./spec.md), [contracts/thiet-bi-api.md](./contracts/thiet-bi-api.md), [contracts/push-delivery.md](./contracts/push-delivery.md)

**Tests**: Required. Cover independent channel failures, hung push returning ≤ 2.5s, successful Telegram delivery after 5s, five-message batches with hung push returning ≤ 3s (push once, Telegram five times, one warning), connection-failure circuit behavior, token-error exceptions to that circuit, missing configuration, token cleanup, idempotent registration, `tokenCu` ownership, and URL-log leakage. Write tests first and record **FAIL → PASS** (xUnit + EF InMemory).

**Organization**: chia theo user story; mỗi story independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: khác file, không phụ thuộc. **[US1]/[US2]/[US3]/[US4]**: nhãn user story.

## Path conventions

Backend `backend/StockRadar.{Api,Application,Domain,Infrastructure,Tests}`, mobile `mobile/lib` + `mobile/android`.

## Baseline trước khi bắt đầu

- `dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj` ⇒ **284/284**
- `cd mobile; flutter test` ⇒ **9/9**; `flutter analyze` ⇒ 0 issue
- Ghi lại log "nhịp chạy hết … ms" của một nhịp V3 trong phiên (đối chiếu SC-004 sau khi nối kênh đẩy)

## Luật dừng (không được bỏ qua)

- **STOP-1 passed**: owner approved Step 0 and the dependency list, with the four Revision 3 adjustments. Complete those documentation edits before Phase 1; summarize them at STOP-Phase-1.
- **STOP-Phase-1** (sau T003): **dừng báo cáo**, owner tự **commit + ship Phase 1**. **Không làm T004** cho tới khi owner báo đã ship — lý do: migration (T006) chưa được tạo trong working tree khi Phase 1 lên server.
- **STOP-2** (T006): migration tạo xong ⇒ đọc lại toàn bộ file, **gửi owner**, **không** chạy API/restart (`DatabaseInitializer.cs:17` tự `MigrateAsync` ⇒ áp DB ngay khi khởi động).
- **STOP-3** (T031): owner tự tạo dự án Firebase + đặt `google-services.json` + đặt key service account trên server ⇒ báo cáo rồi **chờ** owner xác nhận xong trước khi test thật (T032).

---

## Phase 1: Chặn rò log trước tiên (FR-012 — độc lập, không cần Firebase)

**Purpose**: Stop HttpClient URL leakage without changing delivery behavior. Phase 1 source changes are limited to `backend/StockRadar.Infrastructure/DependencyInjection.cs` and `backend/StockRadar.Tests/Notifications/TelegramNotifierLogTests.cs`. No timeout/configuration/package changes, API restart, DB access, or migration. The new 10s Telegram limit belongs to Phase 2.

For builds, pass `-p:ImportDirectoryBuildTargets=false`: `backend/Directory.Build.targets` otherwise runs `stop-api.ps1` when building the API. This command-line override avoids stopping the running API without editing build files.

- [x] T001 [US1] **Tách hàm đăng ký thật, chưa sửa lỗi**: trong `backend/StockRadar.Infrastructure/DependencyInjection.cs`, chuyển dòng **130** (`services.AddHttpClient<ITelegramNotifier, TelegramNotifier>();`) vào extension mới `public static IServiceCollection AddTelegramNotifier(this IServiceCollection services)` (class `DependencyInjection`, **chưa** thêm `RemoveAllLoggers()`), `AddInfrastructure` gọi `services.AddTelegramNotifier();`. Mục đích: test và API production đi qua **cùng một** hàm. `dotnet build` phải còn 0 lỗi (chưa đổi hành vi).
- [x] T002 [US1] Viết test **fail trước**: `backend/StockRadar.Tests/Notifications/TelegramNotifierLogTests.cs`. Test **cấm** tự gọi `AddHttpClient(...)/RemoveAllLoggers()` trong body (làm vậy là test kiểm chính nó). Cấu trúc bắt buộc:
  - `new ServiceCollection().AddLogging()` → gọi **`AddTelegramNotifier()`** thật;
  - `services.Configure<TelegramNotifyOptions>` với `Enabled=true`, `BotToken="TOK-SECRET-123"`, `ChatId="chat-test"` (**phải bật tay**: `TelegramNotifyOptions.Enabled` mặc định `false` — `HyperparameterTuningOptions.cs:33`, nếu để false thì `SendAsync` trả về sớm và test PASS rỗng, không kiểm được gì);
  - Replace the primary handler with a fake returning 200 via `Configure<HttpClientFactoryOptions>(nameof(ITelegramNotifier), ...)`: the typed client is named after TClient, not TImplementation. Assert the fake receives **exactly one request**; use no real credentials or network transport.
  - `AddProvider(captureLogger)`; resolve `ITelegramNotifier`, gửi 1 tin;
  - assert **không** dòng log nào chứa `api.telegram.org/bot` và không dòng nào chứa `TOK-SECRET-123`.
  - Chạy test ⇒ **PHẢI FAIL**. **Lưu nguyên output** để đưa vào báo cáo (không có FAIL thì chưa được coi là test thật). Nếu không thay được handler với cấu hình tối thiểu ⇒ giữ nguyên `AddTelegramNotifier()` (T001) làm điểm gọi chung, không tự nới phạm vi.
- [x] T003 [US1] Sửa `AddTelegramNotifier()` → `services.AddHttpClient<ITelegramNotifier, TelegramNotifier>().RemoveAllLoggers();` (extension đã có trong `Microsoft.Extensions.Http` net10, **không** thêm package). Chạy lại T002 ⇒ **PASS**, lưu output. `dotnet build` 0 lỗi + `dotnet test` ≥ 284 + test mới; ghi output. **Không** chạy/restart API ở Phase 1.
- [x] T003-STOP **DỪNG**: báo diff + output FAIL→PASS + kết quả build/test + bản nháp commit `[FIX][telegram] tắt log URL gửi Telegram, hết lộ token bot`. **Chờ owner commit + ship Phase 1 và báo đã ship** rồi mới xuống T004.

**Checkpoint — stopped after T003**: baseline 284/284; real-registration regression test **FAIL before** (`URL=2, token=2`, values withheld) → **PASS after** (1/1); final solution build 0 errors, 6 existing NU1902/NU1903 package warnings; full suite **285/285**, 0 skipped. No API stop/restart, DB access, timeout/configuration change, commit, or shipment. Production log verification remains pending the owner's shipment. **T004 is blocked until the owner confirms Phase 1 shipped**, before any migration is created.

---

## Phase 2: Foundational (blocking — phải xong trước mọi US)

**Purpose**: bảng thiết bị + kênh đẩy + lớp phát tin chung (gom tin → lưu → gửi). Thiếu cấu hình Firebase ⇒ kênh đẩy tắt, mọi thứ khác y như cũ (SC-005).

- [ ] T004 [P] Domain/Application skeleton: `backend/StockRadar.Domain/Enums/LoaiTinV3.cs` (`Mua, Ban, HuyMua, AtoN3`); `backend/StockRadar.Application/Abstractions/IGuiThongBao.cs` per **Revision 3**: `void DatTinV3(LoaiTinV3, string, LoaiKichBanV3, string telegramHtml)`, `void DatTinHeThong(string, string)`, `Task GuiTatCaAsync(CancellationToken)`, `Task GuiHeThongAsync(string, string, CancellationToken)` (gom + gửi ngay) + `record TinDaGom`; `Abstractions/INguiTinDay.cs` (`DangHoatDong`, `GuiAsync`, `DayTinNhan`, `DayTinKetQua`); `Abstractions/IThietBiNhanThongBaoRepository.cs` (`DangKyAsync(userId, token, nenTang, tokenCu)`, `XoaAsync`, `XoaCacTokenAsync`, `LayTokenTheoUserAsync`, `LayTokenCacMayAsync`) — **không** có `GuiV3Async` gửi ngay; `Options/PushNotifyOptions.cs` (`SectionName="Push"`, `Enabled`, `Firebase.CredentialPath`, **`TimeoutGiay=2`**) theo [contracts/push-delivery.md](./contracts/push-delivery.md) §1; add `TelegramTimeoutOptions` beside `TelegramNotifyOptions` in `Options/HyperparameterTuningOptions.cs` (`SectionName="Telegram"`, `TimeoutGiay=10`); bind both timeout options beside `Configure<TelegramNotifyOptions>` in `backend/StockRadar.Application/DependencyInjection.cs:32`. Keep the existing `TelegramNotify` section unchanged; 10s and 2s are AI proposals adopted by the owner
- [ ] T005 [US3] `backend/StockRadar.Infrastructure/Persistence/Entities/ThietBiNhanThongBaoEntity.cs` (Id long identity, UserId Guid, Token, NenTang, NgayDangKy, LanCapNhatGanNhat — **không** comment từng field) + đăng ký `DbSet` và `OnModelCreating` trong `ApplicationDbContext.cs` (cạnh block V3 dòng 47-50; `HasMaxLength(512)` Token, `HasIndex(Token).IsUnique()`, `HasIndex(UserId)`, FK `Users` cascade) theo plan §(d)
- [ ] T006 [US3] `dotnet ef migrations add AddThietBiNhanThongBao` (project `StockRadar.Infrastructure`, startup `StockRadar.Api`) ⇒ **MỞ FILE SINH RA ĐỌC TOÀN BỘ `Up`/`Down`** (bẫy `efcore-migration-review`: tool từng sinh sai body ở repo này, xem `Migrations/20261026130015_AddV3Tables.cs:10`); chỉ được có `CreateTable` + 2 `CreateIndex`, không drop/alter gì khác; sửa tay nếu sai. **STOP-2: gửi owner file migration, chưa chạy API/restart, chưa áp vào DB nào.**
- [ ] T007 [P] Tests fail trước cho dispatcher + repo, cùng folder `backend/StockRadar.Tests/Notifications/`:
  - `GuiThongBaoDocLapTests.cs` — fake `INguiTinDay` ném ⇒ `ITelegramNotifier.SendAsync` vẫn được gọi **đúng 1 lần** và dispatcher không ném; fake `ITelegramNotifier` ném ⇒ `GuiAsync` vẫn gọi; cả hai ném ⇒ `GuiTatCaAsync` trả về bình thường (FR-002, US1 AC2/AC3)
  - `GuiThongBaoTimeoutTests.cs` — separate limits: `Push:TimeoutGiay=2`, `Telegram:TimeoutGiay=10`. (1) One message, fake push `Task.Delay(10s, ct)`, immediate Telegram ⇒ `GuiTatCaAsync` completes **≤ 2.5s**, Telegram **once**. (2) Fake Telegram `Task.Delay(5s, ct)` ⇒ successful completion, **not canceled**. (3) **Five messages**, hanging push and immediate Telegram ⇒ push **once**, Telegram **five times**, total **≤ 3s**, exactly **one `LogWarning`**. Also verify a subsequent batch attempts push again, a connection failure skips push for the rest of its batch, and tick cancellation stops the remaining messages without throwing into the runner (FR-002/SC-004).
  - `GuiThongBaoTamTests.cs` — **(mới, chống gửi trùng)** `DatTinV3` chưa gọi `GuiTatCaAsync` ⇒ **0** lời gọi telegram/đẩy; sau `GuiTatCaAsync` ⇒ đủ số tin, **đúng thứ tự gom**; gọi `GuiTatCaAsync` hai lần ⇒ không gửi lại lần hai (buffer được xoá sau khi gửi)
  - `ThietBiDangKyTests.cs` — EF InMemory: `DangKyAsync` cùng token 2 lần ⇒ `Count()==1` (FR-008); token đăng ký cho user B sau user A ⇒ 1 dòng, `UserId`=B; **`tokenCu` thuộc user hiện tại ⇒ bị xoá**, **`tokenCu` thuộc user khác ⇒ còn nguyên** (2 ca)
  - `ThietBiXoaTokenTests.cs` — `Unregistered`/`InvalidArgument` populate `TokenHong`; remove only the invalid token from DB and the current batch's token list. Assert push continues for healthy tokens and later messages: token-specific errors **do not open the batch circuit** (FR-009).
  - `GuiThongBaoCacMayTests.cs` — 2 user × 1 máy ⇒ `GuiAsync` nhận đủ 2 token (FR-007a)
  - `KenhDayTinFcmDisabledTests.cs` — `Push:Enabled=true` nhưng `CredentialPath=""` / file không tồn tại ⇒ `DangHoatDong==false`, `GuiAsync` không ném; **gửi 3 lần vẫn đúng 1** `LogWarning` (các lần sau `LogDebug`) — chứng minh cảnh báo ra ở **lần gửi đầu**, không phải lúc khởi động (FR-013/SC-005)
  - `V3PushFormatterTests.cs` — `TieuDe` ra `MUA · HPG · N1` / `BÁN · HPG` / `HUỶ MUA · HPG` / `ATO · HPG · N3`; `NoiDung(FormatMua(...))` không còn `<b>`, `<code>`, `&amp;` và vẫn chứa giá + lý do (FR-003); **Telegram formatter không đổi** → `V3TelegramFormatterHoangLoanTests` + `V3DanhMucXepHangTests` phải vẫn xanh không cần sửa
- [ ] T008 [US1] `backend/StockRadar.Infrastructure/Notifications/V3PushFormatter.cs` (TieuDe + NoiDung bỏ thẻ/giải entity/gộp dòng/cắt ~240 ký tự) và `KenhDayTinFcm.cs` (`INguiTinDay`, singleton: init `FirebaseApp` **lazily ngay lần gửi đầu** trong try/catch từ `GoogleCredential.FromFile(CredentialPath)`, cờ "đã cảnh báo" để chỉ 1 `LogWarning`; `Message{Token, AndroidConfig{Priority, Notification{ChannelId,Title,Body}, Ttl = 1h cho V3/thử, 12h cho HPO}, Data}`, `SendAsync`, bắt `FirebaseMessagingException` `Unregistered|InvalidArgument` → gom vào `DayTinKetQua.TokenHong`; log only the last five token characters). Propagate connection failures to the dispatcher so it opens the batch circuit; do not duplicate the dispatcher's outage warning. Thêm package **`FirebaseAdmin` 3.7.0** vào `StockRadar.Infrastructure.csproj`; **nếu không cài được: báo bản thực tế đã cài + lý do, không tự đổi gói**
- [ ] T009 [US1] `backend/StockRadar.Infrastructure/Notifications/GuiThongBao.cs` (`IGuiThongBao`, scoped, `List<TinDaGom>`): buffer only in `DatTinV3`/`DatTinHeThong`; send buffered messages in order through `Task.WhenAll`, with independent linked CTS `CancelAfter` limits (**Telegram 10s, push 2s**) and channel-local catches. First push timeout/connection failure ⇒ skip push for the rest of this `GuiTatCaAsync` batch, **exactly one `LogWarning`**, Telegram still attempts all messages. Reset this flag next batch; no retries. `Unregistered`/`InvalidArgument` only remove affected tokens from DB and cached tokens, never open the circuit. Distinguish tick cancellation from channel timeout; swallow cancellation inside channel tasks, stop remaining messages on tick cancellation, and clear the buffer. DI: `AddSingleton<INguiTinDay, KenhDayTinFcm>`, `AddScoped<IGuiThongBao, GuiThongBao>`, `AddScoped<IThietBiNhanThongBaoRepository, EfThietBiNhanThongBaoRepository>` (+ repository in `Persistence/Repositories/`).
- [ ] T010 [US1] Add `Push` (`Enabled=true`, `Firebase.CredentialPath=""`, **`TimeoutGiay=2`**) and **`Telegram:TimeoutGiay=10`** to `backend/StockRadar.Api/appsettings.json`. Keep `TelegramNotify` credentials/enabled binding unchanged. Both limits are AI proposals adopted by the owner. Empty dev credential disables push; no key files, no `appsettings.Production.json*` edits. **Phase 2 only.**
- [ ] T011 Chạy `dotnet build` + `dotnet test` ⇒ toàn bộ test Phase 2 PASS (284 + test mới)

**Checkpoint**: nền tảng xong, chưa nối runner. Kênh push đang "tắt" vì thiếu cấu hình ⇒ mọi hành vi cũ còn nguyên.

---

## Phase 3: User Story 1 — Nhận tin V3 trên thanh thông báo (Priority: P1) 🎯 MVP

**Goal**: FR-001, FR-002, FR-003, FR-004, FR-005 cho 7 chỗ tin V3; Telegram vẫn nhận y như cũ; **tin đi sau khi nhịp đã lưu DB**.

**Independent Test**: After the migration is approved, use local DB test positions/quotes and a registered device. Live verification of the intraday **save → send** order requires **trading hours**: `POST /api/v1/market/jobs/v3-trong-phien` still returns early when the market is closed (`V3TrongPhienRunner.cs:42`; `V3Jobs.cs:37-39`). The pre-market, ATC, and post-market runners require a trading day. Outside trading hours, use unit tests and explicitly distinguish dispatcher coverage from runner coverage. Do not add a flag bypassing market hours.

### Implementation for User Story 1

- [ ] T012 [US1] Nối 7 chỗ V3 theo bảng plan §(b): đổi constructor `ITelegramNotifier telegram` → `IGuiThongBao guiTin`; thay 7 lời gọi thành `guiTin.DatTinV3(...)` tại **đúng vị trí cũ** (`V3TruocPhienRunner.cs:73,111`, `V3TrongPhienRunner.cs:79,119`, `V3AtcRunner.cs:118,165`, `V3SauPhienRunner.cs:69`); thêm **một** `await guiTin.GuiTatCaAsync(ct);` ngay **sau** `engine.LuuAsync(ct)` (`V3TruocPhienRunner.cs:126`, `V3TrongPhienRunner.cs:126`, `V3AtcRunner.cs:171`, `V3SauPhienRunner.cs:103`). **Không** đổi thứ tự hay số lần `LuuAsync`; **không** đổi chuỗi `V3TelegramFormatter` truyền vào; không để lại `using` thừa
- [ ] T013 [US1] Verify chống trùng + không đứt nhịp: đọc lại diff 4 runner, xác nhận 4 chỗ `GuiTatCaAsync` đều nằm **sau** `LuuAsync` và không còn lời gọi `telegram.SendAsync` nào trong runner (grep). Test buffer đã có ở T007 (`GuiThongBaoTamTests`). **Nếu** không dựng được runner thật + `V3Engine` mà không cần SQL Server (`V3Engine` cần `ApplicationDbContext` + `KbsPriceBoardClient`) ⇒ **chấp nhận** test ở mức dispatcher và **ghi rõ vào báo cáo**: "chưa kiểm ở mức runner + lý do cần DB SQL Server / không có fake repo cho V3"; **không** được lặng lẽ bỏ qua. Live intraday verification uses `POST /api/v1/market/jobs/v3-trong-phien` on local DB after STOP-2 **during trading hours only**. Other V3 routes require a trading day. Outside hours, rely on unit tests and state the coverage limit; no calendar-bypass flag
- [ ] T014 [US1] `dotnet test` xanh; `backend/restart-api.ps1` **chỉ sau STOP-2 (owner duyệt migration)**; smoke local: `POST /api/v1/thiet-bi/gui-thu` với 1 user + 1 token giả ⇒ 200 `soThietBi:1`, log có `SoMayNhan=1` và không có token đầy đủ

**Checkpoint US1**: Report server implementation and evidence before US2. Do not claim intraday end-to-end save-before-send verification outside trading hours: the route respects market-open guards. Use unit tests outside hours and label dispatcher-only coverage; other V3 runners require a trading day. No market-hours bypass.

---

## Phase 4: User Story 3 — Quản lý thiết bị nhận tin (Priority: P3, làm sớm vì là đường test)

**Goal**: FR-006, FR-007, FR-008, FR-009, FR-010 — đăng ký/gỡ/gửi thử, 3 route `[Authorize]`; US1 AC4 (nhắc bật quyền) và hướng dẫn vivo.

**Independent Test**: đăng nhập app ⇒ có dòng trong `ThietBiNhanThongBao`; **bấm** Đăng xuất ⇒ mất dòng; hết phiên (JWT hết hạn) ⇒ dòng **còn**; bấm "Gửi thông báo thử" ⇒ notify trong vài giây.

### Implementation for User Story 3 — backend

- [ ] T015 [P] [US3] `backend/StockRadar.Application/DTOs/ThietBiDtos.cs` (`DangKyThietBiRequest{token, nenTang, tokenCu?}`, `GoThietBiRequest{token}`, `DangKyThietBiResponse{daDangKy,maHoaThongBao}`, `GuiThuResponse{guiDuoc,soThietBi}` — không comment từng field, key camelCase English theo `NhatKyDtos`) + `backend/StockRadar.Application/Services/ThietBiService.cs` (dùng `ICurrentUserService`; validate token 20–512, `nenTang` chỉ `android`; upsert kèm xử lý `tokenCu` chỉ xoá khi thuộc user hiện tại; `GuiThuAsync` chỉ theo user hiện tại, trả `KhongGuiDuoc` khi `DangHoatDong==false`) + DI `AddScoped<ThietBiService>`
- [ ] T016 [P] [US3] Test `backend/StockRadar.Tests/Notifications/ThietBiDangKyRouteTests.cs` theo contracts §"Test phải phủ" (6 mục, gồm 2 ca `tokenCu`): validate 400, upsert 1 dòng, gỡ idempotent, `gui-thu` 503-mapping khi kênh đẩy tắt, 2 máy ⇒ 2 token; cộng 1 test phản chiếu `ThietBiController` có `[Authorize]`
- [ ] T017 [US3] `backend/StockRadar.Api/Controllers/ThietBiController.cs` — `[Route("api/v1/thiet-bi")]`, `[Authorize]`, 3 POST (`dang-ky`, `go`, `gui-thu`), map invalid/503 đúng contracts; **thêm đúng một dòng** `// SECURITY: go xoá theo token, không kiểm chủ — ai biết token máy khác thì gỡ được máy đó khỏi danh sách nhận tin, không lộ dữ liệu.` tại action `go`

### Implementation for User Story 3 — mobile

- [ ] T018 [US3] Thêm gói vào `mobile/pubspec.yaml` `dependencies` (chỉ 3 dòng, giữ nguyên `version: 3.0.1+23` + `dependency_overrides`): `firebase_core: ^5.1.0`, `firebase_messaging: ^17.0.1`, `flutter_local_notifications: ^22.3.3`; `cd mobile; flutter pub get`; thêm `mobile/android/app/google-services.json` vào `mobile/.gitignore`. **Nếu pub get không cài được version nào ⇒ báo bản thực tế đã cài + lý do, không tự đổi sang gói khác, không tự đổi version app**
- [ ] T019 [US3] Gradle thiếu-json-vẫn-build: root gradle thêm `id("com.google.gms.google-services") version "4.5.0" apply false`; `mobile/android/app/build.gradle.kts` `plugins` thêm `id("com.google.gms.google-services") apply false` rồi khối `if (file("google-services.json").exists()) { apply(plugin = "com.google.gms.google-services") }`; build debug để xác nhận **không có json vẫn build được** (ghi rõ kết quả; AGP 9 xung đột thì báo owner — rủi ro 1 trong plan)
- [ ] T020 [US3] `mobile/lib/core/api/api_client.dart`: `dangKyThietBi(String token, {String? tokenCu})`, `goThietBi(String token)`, `guiThuThongBao()` qua `_request` sẵn có (Bearer tự gắn)
- [ ] T021 [US3] `mobile/lib/core/services/push_service.dart` (MỚI): **không** gọi `Firebase.initializeApp()` ở đây (đã làm ở `main.dart` — plan §(g)); hàm **top-level** `_tinDayNen` cho `FirebaseMessaging.onBackgroundMessage`; `taoKenhAndroid()` (`v3-signal` max+âm, `he-thong` default); `xinQuyen()` (Android 13+, ghi `SharedPreferences`, exposes `daDuocQuyen`); `dangKyToken()` (+`onTokenRefresh`: lấy `tokenCu` từ prefs → `dang-ky {token, tokenCu}` → ghi prefs); `goToken()`; `bool get kenhDaSanSang`; `String? duongDanTuTin(Map<String,String> data)` **pure**; `onMessage` foreground ⇒ notify địa phương đúng kênh; `getInitialMessage`/`onMessageOpenedApp` ⇒ điều hướng
- [ ] T021a [US3] `mobile/lib/main.dart` (theo đúng phân công plan §(g)): `ensureInitialized()` → `Firebase.initializeApp()` trong `try/catch` → `FirebaseMessaging.onBackgroundMessage(_tinDayNen)` → tất cả **trước `runApp`**; thiếu json/không init được thì app vẫn chạy
- [ ] T022 [US3] Vòng đời phiên (`mobile/lib/core/services/app_services.dart`, plan §(h)): đổi `logout()` → **`logout({bool goMay = false})`**; khi `goMay == true` gọi `goToken()` **trước** `_api.setToken(null)`, bọc `try/catch`, **không thử lại**, lỗi không chặn đăng xuất; **hai đường hết phiên giữ nguyên**: `onUnauthorized` (dòng 11-15) và `load()` khi JWT hết hạn (dòng 38) gọi `logout()` **không** `goMay` ⇒ **không** sinh vòng lặp 401. `AuthService` nhận callback `onLoggedIn`/`onLoggingOut` (không import Firebase trong file này); `mobile/lib/app.dart` tạo `PushService`, `_bootstrap()` gọi `pushService.khoiDong()` + `taoKenhAndroid()`; nút Đăng xuất trong `app_shell.dart:142-155` đổi thành `logout(goMay: true)`
- [ ] T023 [US3] Nút test: `mobile/lib/widgets/app_shell.dart` (cạnh nút Đăng xuất 142-155) thêm `IconButton` `Icons.notifications_outlined`, tooltip `Gửi thông báo thử`, `visible: auth.isLoggedIn`, gọi `guiThuThongBao()` + `SnackBar` kết quả (`Đã gửi tới n máy` / lỗi 503 `Chưa bật kênh đẩy trên server`); vùng chạm 48dp
- [ ] T023a [US3] **(bổ sung cho tới spec, chỗ ít xâm lấn nhất)** tại chính snackbar của T023 và menu drawer cạnh nút Đăng xuất: (a) nếu `pushService.daDuocQuyen == false` ⇒ snackbar nhắc `Thông báo đang bị chặn — bật trong Cài đặt app.` + **nút `Mở cài đặt`** (`openAppSettings()`); (b) một dòng `Tắt tối ưu pin cho JUICE` (edge vivo). Không thêm màn hình mới, không popup tự động
- [ ] T024 [US3] Manifest: `mobile/android/app/src/main/AndroidManifest.xml` thêm `<uses-permission android:name="android.permission.POST_NOTIFICATIONS"/>`
- [ ] T025 [US3] `mobile/test/push_service_test.dart` (test **trước** cho phần pure): `duongDanTuTin({'symbol':'HPG'})=='/stocks/HPG'`; không `symbol` ⇒ `null`; symbol viết thường ⇒ hoa hoá; **`logout()` không tham số không gọi `goThietBi`, `logout(goMay: true)` có gọi** (fake ApiClient); token đổi ⇒ gửi `dang-ky` kèm `tokenCu`. Chạy `cd mobile; flutter test` ⇒ 9 + test mới PASS; `flutter analyze` ⇒ 0 issue

**Checkpoint**: US1+US3 dùng được trên DB local với token giả. **Báo owner + diff.**

---

## Phase 5: User Story 2 — Chạm thông báo mở đúng màn (Priority: P2)

**Goal**: FR-005/US2 AC1–AC2: app tắt hẳn và app đang chạy nền đều tới Chi tiết mã; phiên hết hạn ⇒ qua đăng nhập rồi tới mã đã chờ.

- [ ] T026 [US2] Kiểm tra đường dẫn điều hướng trong `push_service.dart` dùng `GoRouter.push` (không `go`) để nút Quay lại về màn trước (AC2); route `/stocks/:symbol` đã có ở `app_router.dart:79-88`, không thêm route mới
- [ ] T027 [US2] `pendingRoute`: khi `getInitialMessage` có `symbol` mà `AuthService.isLoggedIn==false` ⇒ lưu 1 lần, đăng nhập xong đẩy về `/stocks/:symbol` rồi xoá; test pure thêm 2 ca vào `mobile/test/push_service_test.dart` (có/không có pending)

---

## Phase 6: User Story 4 (FR-001a) — Tin HPO vào kênh "Hệ thống"

**Goal**: 4 loại tin HPO (`HyperparameterTuningRunner.cs:37,97,109,130`) cũng đẩy, kênh `he-thong`, tiêu đề `HPO · …`, **TTL 12 giờ**; Telegram giữ nguyên từng chữ.

- [ ] T028 [US4] Test `backend/StockRadar.Tests/Notifications/GuiThongBaoHeThongTests.cs`: `GuiHeThongAsync` ⇒ telegram nhận `parseMode: null` + đúng chuỗi cũ; push nhận `Kanh=="he-thong"`, priority `normal`, `Symbol==null`, title `HPO · xong|lỗi|quá giờ|bỏ qua`; kênh đẩy ném ⇒ HPO runner vẫn chạy hết (không đổi exit code/logic)
- [ ] T029 [US4] `backend/StockRadar.Infrastructure/MarketData/HyperparameterTuningRunner.cs`: đổi constructor sang `IGuiThongBao guiTin`, thay 4 chỗ theo bảng plan §(b); không đổi `FormatTelegramMessage`
- [ ] T030 [US4] `dotnet test` xanh. **Không có route HTTP nào trigger HPO** (`MlController` chỉ có `tune/evaluate` cho Optuna, `MarketJobsController` không có job tuning — đã kiểm tra) ⇒ 4 tiêu đề HPO kiểm bằng unit test T028; kiểm thủ công trên máy thật **phụ thuộc nhịp weekly** hoặc owner tự trigger. Ghi trung thực vào báo cáo "chưa kiểm thủ công + lý do", **không** thêm route mới ngoài phạm vi đã duyệt

---

## Phase 7: Polish & cấu hình thật (có 2 DỪNG)

- [ ] T031 **STOP-3**: báo owner (a) tạo dự án Firebase, package `com.baobiantea.juice`, (b) tải `google-services.json` đặt `mobile/android/app/`, (c) đặt file service account ngoài repo trên server (vd `/var/www/secrets/stockradar-firebase-sa.json`) + khai `Push:Firebase:CredentialPath` trong config production. **Chờ owner xác nhận xong** — AI không chạm key, không tạo dự án Firebase
- [ ] T032 Test thật trên DB local + máy `10AE4G1ZE2000K8` (adb): đăng nhập (owner tự nhập) ⇒ cấp quyền ⇒ tắt app ⇒ bấm "Gửi thông báo thử" ⇒ notify hiện; chạm ⇒ mở Chi tiết đúng mã; **bấm Đăng xuất** ⇒ gửi thử lần 2 báo `soThietBi:0` và không notify; **để JWT hết hạn** ⇒ máy vẫn còn dòng đăng ký (không bị gỡ)
- [ ] T033 Nghiệm thu SC-002/SC-004: so timestamp notify ↔ Telegram trên ≥ 5 tin (ngưỡng 60s — đề xuất của AI, owner dùng); so log "nhịp chạy hết … ms" với baseline đã ghi ở đầu (ngưỡng 1s — đề xuất của AI); TTL 1h kiểm bằng gửi tin khi máy ngủ > 5 phút (thủ công, ghi rõ kết quả); SC-003: `journalctl` chỉ kiểm được **sau khi owner deploy** ⇒ ghi rõ "chưa kiểm, chờ deploy"
- [ ] T034 Dọn dẹp: đếm `SELECT COUNT(*) FROM ThietBiNhanThongBao` trước/sau ⇒ xoá token test trên DB local, **kết quả sau = 0**; `adb reverse --remove-all`; dừng API local
- [ ] T035 [P] Docs sau code (`doc-update-after-code`): cập nhật `docs/telegram-vip-alerts-flow.md` (luồng gom→lưu→gửi 2 kênh), `docs/build-and-deploy.md` (bước đặt credential + `google-services.json`), `docs/domain/pipeline-jobs.md` nếu mô tả chỗ gửi tin; **không** đổi `docs/domain/*` về cổng/điểm (không đụng cổng nào)
- [ ] T036 Kiểm cuối: `dotnet build` 0 lỗi; `dotnet test` (284 + test mới) pass; `cd mobile; flutter analyze` 0 issue, `flutter test` pass; `npm run build` chỉ nếu có sửa kiểu dữ liệu web (dự kiến **không**); review diff theo `secrets-config-review`: không key/token/URL có khoá trong file nào
- [ ] T037 Bản nháp commit (**không** commit/push — owner làm): `[FIX][telegram] tắt log URL gửi Telegram, hết lộ token bot` + `[FEAT][notify] thông báo đẩy V3 và HPO lên app song song Telegram`, mỗi dòng ≤ 72 ký tự, không thân, không `Co-Authored-By`

---

## Dependencies & Execution Order

- Phase 1 **đầu tiên** (FR-012), độc lập, không cần Firebase. **T003-STOP**: owner commit + ship Phase 1 **trước khi** T004/T006 tạo migration.
- Phase 2 **blocking** cho mọi US: dispatcher + kênh đẩy + bảng + migration.
- Phase 3 (US1) cần T006 đã qua **STOP-2** (owner duyệt migration); **cấm** chạy/restart API trước thời điểm đó (`DatabaseInitializer.cs:17`).
- Phase 4 (US3) song song được Phase 5 (US2) sau khi T021 xong; US2 gần như chỉ là điều hướng mobile.
- Phase 6 (US4 reuse dispatcher). Phase 7 cần **STOP-3** (owner cấu hình Firebase) trước T032.
- Trong mỗi story: test trước ⇒ implement ⇒ build/test.

## Thứ tự song song cơ hội

T004 ∥ T005; T015 ∥ T016; T018 ∥ T019 ∥ T024 (khác file). Các T sửa cùng `DependencyInjection.cs` (T004, T009) và cùng `pubspec.yaml`/gradle (T018, T019) **không** song song.

## Notes

- Comment code **chỉ 4 nhãn** `BUSINESS-RULE` / `DO-NOT-CHANGE` / `SECURITY` / `LIMIT`, ≤ 2 dòng, có hậu quả cụ thể, không kể lịch sử; DTO không comment từng field.
- Identifier mới: tiếng Việt không dấu (`.cursor/rules/vietnamese-identifiers.mdc`); giữ tên thư viện/API cũ.
- Cấm: commit, push, `ship-all.ps1`, `build-apk.ps1`, sửa `pubspec.yaml` ngoài 3 gói đã duyệt, đổi version app, chạm DB/config production, tự tạo dự án Firebase, sửa `V3TelegramFormatter` hay luật V3, **đổi thứ tự/số lần `LuuAsync` khi chưa hỏi**.
- Không in mật khẩu/chuỗi kết nối/token/key Firebase ra log hay báo cáo; log chỉ 5 ký tự cuối device token; owner tự đăng nhập app khi test.
- Gói không cài được ⇒ báo bản thực tế đã cài + lý do, **không** tự đổi sang gói khác.

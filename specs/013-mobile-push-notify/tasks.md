# Tasks: Thông báo đẩy V3 + HPO lên app mobile (song song Telegram)

**Input**: Design documents in `specs/013-mobile-push-notify/` — **Revision 3**, Step 0 approved with four adjustments (29/10/2026). Update documents before coding, then proceed directly to T001–T003; no additional documentation stop.

**Prerequisites**: [plan.md](./plan.md) (decisions a–h + Revision 3 adjustments), [spec.md](./spec.md), [contracts/thiet-bi-api.md](./contracts/thiet-bi-api.md), [contracts/push-delivery.md](./contracts/push-delivery.md)

**Owner changed 29/10/2026**: Phase 2 review corrections supersede the earlier InvalidArgument cleanup, body clipping, and per-message channel barrier. This revision stops before T012. The original verification evidence below is historical; the review-correction evidence records the current results.

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

- [x] T004 [P] Domain/Application skeleton: `backend/StockRadar.Domain/Enums/LoaiTinV3.cs` (`Mua, Ban, HuyMua, AtoN3`); `backend/StockRadar.Application/Abstractions/IGuiThongBao.cs` per **Revision 3**: `void DatTinV3(LoaiTinV3, string, LoaiKichBanV3, string telegramHtml)`, `void DatTinHeThong(string, string)`, `Task GuiTatCaAsync(CancellationToken)`, `Task GuiHeThongAsync(string, string, CancellationToken)` (gom + gửi ngay) + `record TinDaGom`; `Abstractions/INguiTinDay.cs` (`DangHoatDong`, `GuiAsync`, `DayTinNhan`, `DayTinKetQua`); `Abstractions/IThietBiNhanThongBaoRepository.cs` (`DangKyAsync(userId, token, nenTang, tokenCu)`, `XoaAsync`, `XoaCacTokenAsync`, `LayTokenTheoUserAsync`, `LayTokenCacMayAsync`) — **không** có `GuiV3Async` gửi ngay; `Options/PushNotifyOptions.cs` (`SectionName="Push"`, `Enabled`, `Firebase.CredentialPath`, **`TimeoutGiay=2`**, `KhoiTaoLaiSauGiay=300`) theo [contracts/push-delivery.md](./contracts/push-delivery.md) §1; add `TimeoutGiay=10` to existing `TelegramNotifyOptions` and reuse its binding; bind `PushNotifyOptions` beside it. Preserve Telegram credentials/Enabled. Owner's Phase 2 update supersedes the separate Telegram section; 10s and 2s are AI proposals adopted by the owner
- [x] T005 [US3] `backend/StockRadar.Infrastructure/Persistence/Entities/ThietBiNhanThongBaoEntity.cs` (Id long identity, UserId Guid, Token, NenTang, NgayDangKy, LanCapNhatGanNhat — **không** comment từng field) + đăng ký `DbSet` và `OnModelCreating` trong `ApplicationDbContext.cs` (cạnh block V3 dòng 47-50; `HasMaxLength(512)` Token, `HasIndex(Token).IsUnique()`, `HasIndex(UserId)`, FK `Users` cascade) theo plan §(d)
- [x] T006 [US3] `dotnet ef migrations add AddThietBiNhanThongBao` (project `StockRadar.Infrastructure`, startup `StockRadar.Api`) ⇒ **MỞ FILE SINH RA ĐỌC TOÀN BỘ `Up`/`Down`** (bẫy `efcore-migration-review`: tool từng sinh sai body ở repo này, xem `Migrations/20261026130015_AddV3Tables.cs:10`); chỉ được có `CreateTable` + 2 `CreateIndex`, không drop/alter gì khác; sửa tay nếu sai. **STOP-2: gửi owner file migration, chưa chạy API/restart, chưa áp vào DB nào.**
- [x] T007 [P] Tests fail trước cho dispatcher + repo, cùng folder `backend/StockRadar.Tests/Notifications/`:
  - `GuiThongBaoDocLapTests.cs` — fake `INguiTinDay` ném ⇒ `ITelegramNotifier.SendAsync` vẫn được gọi **đúng 1 lần** và dispatcher không ném; fake `ITelegramNotifier` ném ⇒ `GuiAsync` vẫn gọi; cả hai ném ⇒ `GuiTatCaAsync` trả về bình thường (FR-002, US1 AC2/AC3)
  - `GuiThongBaoTimeoutTests.cs` — separate limits: `Push:TimeoutGiay=2`, `TelegramNotify:TimeoutGiay=10`. (1) One message, fake push `Task.Delay(10s, ct)`, immediate Telegram ⇒ `GuiTatCaAsync` completes **≤ 2.5s**, Telegram **once**. (2) Fake Telegram `Task.Delay(5s, ct)` ⇒ successful completion, **not canceled**. (3) **Five messages**, hanging push and immediate Telegram ⇒ push **once**, Telegram **five times**, total **≤ 3s**, exactly **one `LogWarning`**. Also verify a subsequent batch attempts push again, a connection failure skips push for the rest of its batch, and tick cancellation stops the remaining messages without throwing into the runner (FR-002/SC-004).
  - `GuiThongBaoTamTests.cs` — **(mới, chống gửi trùng)** `DatTinV3` chưa gọi `GuiTatCaAsync` ⇒ **0** lời gọi telegram/đẩy; sau `GuiTatCaAsync` ⇒ đủ số tin, **đúng thứ tự gom**; gọi `GuiTatCaAsync` hai lần ⇒ không gửi lại lần hai (buffer được xoá sau khi gửi)
  - `ThietBiDangKyTests.cs` — EF InMemory: `DangKyAsync` cùng token 2 lần ⇒ `Count()==1` (FR-008); token đăng ký cho user B sau user A ⇒ 1 dòng, `UserId`=B; **`tokenCu` thuộc user hiện tại ⇒ bị xoá**, **`tokenCu` thuộc user khác ⇒ còn nguyên** (2 ca)
  - `ThietBiXoaTokenTests.cs` — **owner changed 29/10/2026**: only `Unregistered` enters `TokenHong` and removes that registration from DB/cache. `InvalidArgument` preserves it; invalid/oversized payloads preserve every registration. Use typed SDK errors offline plus EF InMemory to verify healthy devices and subsequent messages still receive delivery, without opening the circuit (FR-009).
  - `GuiThongBaoCacMayTests.cs` — 2 user × 1 máy ⇒ `GuiAsync` nhận đủ 2 token (FR-007a)
  - `KenhDayTinFcmDisabledTests.cs` — `Push:Enabled=true` nhưng `CredentialPath=""` / file không tồn tại ⇒ `DangHoatDong==false`, `GuiAsync` không ném; **gửi 3 lần vẫn đúng 1** `LogWarning` (các lần sau `LogDebug`) — chứng minh cảnh báo ra ở **lần gửi đầu**, không phải lúc khởi động (FR-013/SC-005)
  - `V3PushFormatterTests.cs` — unchanged four-title mapping; HTML/entity/line normalization preserves the full body, including all eight real buy lines, “Bán realtime”, and the backtest warning. No clipping or ellipsis. Existing Telegram formatter and V3 tests remain unchanged.
  - `PhamViDbThongBaoTests.cs` — read/cleanup scope survives cancellation until actual completion; runner/push context instances differ; runner still queries after dispatcher timeout; at most one unfinished notification DB operation across batches; late faults observed safely. InMemory does not prove SQL Server cancellation behavior.
  - `GuiThongBaoDieuKhienTests.cs` — independent ordered loops, no cross-channel message barrier, safe stage/type diagnostics, per-message payload rejection, and slow initialization bounded by virtual 2s without replay.
  - `KiemTraTinFcmTests.cs`, `KhoiTaoFcmTests.cs`, `GuiFcmMotLanTests.cs` — routing/size validation, initialization lifecycle/cooldown, and no SDK delivery retry. Use fake clocks and completion gates; no new NuGet package. Only the three acceptance cases in `GuiThongBaoTimeoutTests` use real time and `[Trait("Category", "ThoiGianThat")]`.
- [x] T008 [US1] **Owner changed 29/10/2026**: `V3PushFormatter.NoiDung` strips HTML/decodes entities/normalizes lines without the former 240-element limit. `KenhDayTinFcm` validates nonblank title/body, routing metadata, and conservative UTF-8 serialized payload size ≤4096 bytes ([Firebase source](https://firebase.google.com/docs/cloud-messaging/error-codes), keys and values included); oversized messages skip push, never truncate. Only `Unregistered` populates `TokenHong`; `InvalidArgument` retains registration and logs last-five-character suffix/error metadata. Shared task owns synchronous Firebase initialization and resource leases; fixed failures disable until restart, network/timeouts may retry on a later send after cooldown 300s, single flight, no old-message replay. `LoiKenhDayTinFcm` retains sanitized diagnostic data only; `BoTaoHttpFcm` disables SDK delivery retries. Android priority/TTL/data mapping unchanged. Keep **FirebaseAdmin 3.7.0**, no new package or credential access.
- [x] T009 [US1] **Owner changed 29/10/2026**: `GuiThongBao` snapshots/clears its scoped buffer, then awaits two independent ordered loops with `Task.WhenAll`. Per-message linked CTS deadlines use injected `TimeProvider`: **Telegram 10s, push 2s**. First push timeout/connection failure skips remaining push messages with exactly one warning; Telegram continues. Tick cancellation stops both loops without throwing. `PhamViDbThongBao` owns fresh async DI scopes for lookup/cleanup, keeps each scope alive until actual EF completion, and allows at most one unfinished DB operation across batches. Occupied slot means skip push/warn once/no new scope. Retain late-fault observation. Validate before lookup; only `Unregistered` removes DB/cache entries, while payload rejection and `InvalidArgument` preserve registrations. DI adds singleton `TimeProvider.System` and `PhamViDbThongBao`, keeping singleton FCM and scoped dispatcher/repository. Trading repositories and runners remain unchanged.
- [x] T010 [US1] Add `Push` (`Enabled=true`, `Firebase.CredentialPath=""`, **`TimeoutGiay=2`**, **`KhoiTaoLaiSauGiay=300`**) and **`TelegramNotify:TimeoutGiay=10`** to `backend/StockRadar.Api/appsettings.json`. Timeouts/cooldown are AI proposals adopted by the owner; cooldown added by the 29/10/2026 review correction. Preserve Telegram credentials/enabled binding. Empty dev credential disables push; no key files or production configuration changes. **Phase 2 only.**
- [x] T011 Chạy `dotnet build` + `dotnet test` ⇒ toàn bộ test Phase 2 PASS (285 + new tests)

**Phase 2 checkpoint (2026-10-29)**: owner confirmed Phase 1 shipped. Baseline rerun: 285 passed, 0 failed/skipped. T004–T006 scaffolded; safe API build: 0 errors, 9 existing warning emissions. Migration `20261029100754_AddThietBiNhanThongBao`: exactly one CreateTable and two CreateIndex operations; Down only DropTable. Snapshot: +48 lines, only the new entity and FK. STOP-2 reported; approval/application pending. DB-free T007–T011 may proceed, but no API start/restart, DB update, or Phase 3.

**T011 — STOP (2026-10-29)**: foundation implemented; runners, TelegramNotifier, AddTelegramNotifier, mobile, and production untouched. FirebaseAdmin requested/resolved **3.7.0**. No host/API start, restart, or database migration application. STOP-2 approval remains pending; **T012 and Phase 3 not started**.

### Phase 2 verification evidence — original T004–T011 (historical)

Tests were written against throwing stubs and run before implementing their respective components. DI initially failed because `IGuiThongBao` was unregistered. The four model/entity tests passed at baseline because T004–T006 had already established the schema; they are not claimed as red tests.

| Group | Before: failed / passed | After: failed / passed |
|---|---:|---:|
| GuiThongBaoDocLapTests | 6 / 0 | 0 / 6 |
| GuiThongBaoTimeoutTests | 9 / 0 | 0 / 9 |
| GuiThongBaoTamTests | 3 / 0 | 0 / 3 |
| ThietBiDangKyTests | 30 / 4 | 0 / 34 |
| ThietBiXoaTokenTests | 2 / 0 | 0 / 2 |
| GuiThongBaoCacMayTests | 1 / 0 | 0 / 1 |
| KenhDayTinFcmDisabledTests | 11 / 0 | 0 / 11 |
| V3PushFormatterTests | 17 / 0 | 0 / 17 |
| FcmMessageBuilderTests | 6 / 0 | 0 / 6 |
| KenhDayTinFcmTokenTests | 10 / 0 | 0 / 10 |
| DangKyKenhThongBaoTests | 1 / 0 | 0 / 1 |
| **New tests total** | **96 / 4** | **0 / 100** |

Actual red output excerpts:
```text
System.NotImplementedException : The method or operation is not implemented.
System.InvalidOperationException : No service for type 'StockRadar.Application.Abstractions.IGuiThongBao' has been registered.
Test Run Failed. Total tests: 22. Failed: 22. (dispatcher/integration groups)
Test Run Failed. Total tests: 34. Passed: 4. Failed: 30. (repository/model group)
```
The four FCM/formatter groups independently reported `Failed!` with 11, 17, 6, and 10 failures before implementation, then `Passed!` with the same counts.

Final commands (the build override prevents the repository build target from stopping the API):
```powershell
dotnet build backend/StockRadar.slnx --no-incremental --nologo -v minimal -p:ImportDirectoryBuildTargets=false --tl:off
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --no-build --no-restore --nologo -v minimal -p:ImportDirectoryBuildTargets=false --tl:off
```
Actual output:
```text
Build succeeded.
19 Warning(s)
0 Error(s)
Passed! - Failed: 0, Passed: 385, Skipped: 0, Total: 385, Duration: 15 s
```
Warnings: 12 existing NU1902/NU1903 restore/build emissions, one existing MarketService obsolete-API warning, and six new FirebaseAdmin `Message.Token` deprecation warnings (one production and five test references). The contract-required token API remains unchanged; no warning suppression or package substitution.

| Expected | Verified result |
|---|---|
| Hung push: one Telegram send, at most 2.5s | PASS; independent 2s push budget |
| Telegram takes 5s without cancellation | PASS; observed 5017 ms while push timed out separately |
| Five messages: push once, Telegram five times, at most 3s | PASS; observed 2012 ms; exactly one warning |
| Next batch attempts push again | PASS |
| Connection outage skips remaining push attempts | PASS; exactly one warning |
| Token rejection keeps healthy devices and later messages | PASS; real typed SDK errors tested offline, DB/cache removal tested with EF InMemory |
| Original cancellation discards remaining buffer without throwing | PASS; no push-outage warning |
| No Firebase configuration: warn on first send only | PASS; constructor/status checks silent; no device query |
| Additive migration and snapshot | One CreateTable + two CreateIndex; Down only DropTable; snapshot +48/-0, parity PASS, zero connection attempts |
| Existing baseline 285 plus new tests | 385/385, zero skipped |
| Sensitive-keyword scan over backend changes | Zero added BotToken/private_key/AIza lines, including all 25 new backend files; values withheld |
| Protected files and scope | Only Phase 2 changes beyond the five preexisting owner-owned paths; git diff --check passed |

Not verified: SQL Server apply/rollback, concurrent SQL constraints in a real DB, live FCM/Telegram delivery, mobile receipt, runner save-before-send ordering, and production logs. These require forbidden DB/API/credential access or later phases. IDE diagnostics were unavailable; compiler/tests are the verification source. No commit or push performed.

Commit draft: `[FEAT][notify] thêm nền tảng đẩy tin và bảng thiết bị`

### Phase 2 review-correction evidence — owner changed 29/10/2026

Baseline rerun before these corrections: **385 passed, 0 failed/skipped**. Current total: **482 = 385 + 97** (79 channel/formatter additions, 15 lifetime/dispatcher additions, 3 supplemental SDK/EF integration cases). No tests were removed; approved behavior assertions were updated as listed below.

#### Red before green

These are overlapping staged runs, not totals to add together. All have zero skipped. Red tests ran before their corresponding implementation changes; the three supplemental integration cases were added afterward and are not claimed as red-before-green evidence.

| Staged group | Before: failed / passed / total | After: failed / passed / total |
|---|---:|---:|
| Payload/token/full-body formatter | 42 / 45 / 87 | 0 / 87 / 87 |
| Initialization/lifetime | 15 / 23 / 38 | 0 / 38 / 38 |
| Retry-focused regressions | 2 / 12 / 14 | 0 / 14 / 14 |
| Offline SDK HTTP/retry behavior | 3 / 25 / 28 | All 28 pass within the final combined 123-case run |
| Final combined FCM/formatter group | Earlier staged failures above | 0 / 123 / 123, repeated three times |
| PhamViDbThongBaoTests | 6 / 0 / 6 | 0 / 6 / 6 |
| GuiThongBaoDieuKhienTests | 9 / 0 / 9 | 0 / 9 / 9 |
| GuiThongBaoTimeoutTests, excluding real time | 3 / 3 / 6 | 0 / 6 / 6 |
| Combined dispatcher/lifetime/virtual-time run | 18 / 3 / 21 | 0 / 21 / 21, 652 ms |
| Supplemental ThietBiXoaTokenTests additions | Not claimed | 0 / 3 / 3, included in full suite |

Abridged red-run failure/output summaries (sensitive values withheld):
```text
Assert.NotSame() Failure: Values are the same instance.
Assert.False() Failure: Expected False, Actual True. (premature scope disposal)
Expected: doc_token / gui / don_token; Actual: null. (missing diagnostic stage)
Expected: 3 Telegram messages; Actual: 1. (per-message channel barrier)
Expected: 8; Actual: 7. (real buy-message lines)
Expected: 1; Actual: 2. (SDK delivery attempts)
Slow synchronous initializer blocked its caller for 2007 ms.
Failed: 18, Passed: 3, Skipped: 0, Total: 21
Passed: 21, Failed: 0, Skipped: 0, Total: 21, Duration: 652 ms
```

One interim full run was **478 passed / 1 failed / 479**: the newly virtualized `QuaHanTelegramKhongNgatKenhDayHoacTinSau` advanced the clock before the asynchronous notification DB worker/push sends ran. Its completion gate now waits for both push sends before advancing Telegram deadlines. This was a modified fixture race under the approved scheduling change; no unchanged baseline regression was bypassed or timeout relaxed. After that fixture correction and three added integration cases: **482/482**.

#### Existing tests changed

Approved behavior assertions:
- `V3PushFormatterTests.TinMuaTelegramThatGiuGiaVaLyDo` → `TinMuaTelegramThatGiuDuTamDongVaCanhBao`: full eight-line body and both warnings.
- `V3PushFormatterTests.CatDungBienDoDai` → `GiuDuNoiDungQuaBienCu` (239/240/241): retain complete body.
- `V3PushFormatterTests.CatUnicodeKhongTachEmoji` → `GiuDuUnicodeVaDuoi` (five Unicode cases): retain all clusters and trailing text.
- `KenhDayTinFcmTokenTests.TokenHongKhongChanMayKhoeVaTinSau`: InvalidArgument retains registration; Unregistered behavior unchanged.

Timing/fixture/naming-only changes, preserving behavioral assertions:
- `GuiThongBaoTimeoutTests.HuyNhipBoTinConLaiVaXoaBoDem`: cancellation completion gate.
- `GuiThongBaoTimeoutTests.QuaHanTelegramKhongNgatKenhDayHoacTinSau`: virtual deadlines and ordered completion gates.
- `GuiThongBaoTimeoutTests.DocTokenTreoVanGuiTelegramVaGiuHanCho`: virtual lookup timeout.
- `GuiThongBaoTimeoutTests.KenhBoQuaHuyVanKhongGiuNhipQuaHan`: virtual cancellation-unaware transport timeout.
- `GuiThongBaoTimeoutTests.DayTreoMuoiGiayKetThucTrongHaiGiayRuoi`, `TelegramNamGiayVanXongKhongBiHuy`, `NamTinDayTreoChiGoiMotLanVaDotSauThuLai`: added real-clock category; owner bounds unchanged.
- `KenhDayTinFcmTokenTests.HuyTrongGuiTruyenTokenVaDungCacMaySau`: completion gate replaces infinite delay.
- Existing `GuiThongBaoTamTests` channel assertion uses `Kenh`; `FcmMessageBuilderTests` additionally checks payload validity. No existing V3 rule or Telegram formatter test changed.

#### Final commands and measured results

Every build/test uses `-p:ImportDirectoryBuildTargets=false`; no API stop target is run.
```powershell
dotnet build backend/StockRadar.slnx --no-incremental -p:ImportDirectoryBuildTargets=false --nologo -v minimal --tl:off
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj -p:ImportDirectoryBuildTargets=false --nologo -v minimal --tl:off
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --no-build --no-restore -p:ImportDirectoryBuildTargets=false --filter Category=ThoiGianThat --logger "console;verbosity=detailed"
```
```text
Build succeeded. 22 Warning(s), 0 Error(s). Elapsed 00:00:09.88.
Passed! - Failed: 0, Passed: 482, Skipped: 0, Total: 482, Duration: 9 s.
Category=ThoiGianThat: Total tests: 3, Passed: 3, Total time: 9.5853 Seconds.
Category!=ThoiGianThat: 479/479, 0 failed/skipped, repeated three times, 1 s each.
```
Final broad filter `FullyQualifiedName~Fcm|FullyQualifiedName~V3PushFormatter`: **124/124**, 0 failed/skipped, 117 ms. This includes the 123 channel/formatter cases plus the dispatcher's slow-FCM-initialization case.

Warnings: 12 existing NU1902/NU1903 emissions for `System.Security.Cryptography.Pkcs` 9.0.4; one existing `MarketService` CS0618; nine `Message.Token` CS0618 references. **Three additional warnings versus the 385-test Phase 2 baseline**: `KhoiTaoFcmTests` one, `ThietBiXoaTokenTests` two. No suppression, dependency upgrade, or new package. FirebaseAdmin remains **3.7.0**.

| Owner real-clock expectation | Measured result |
|---|---|
| Push hangs 10s, return ≤2500ms, Telegram once | **2005ms**, push 1, Telegram 1, warning 1 |
| Telegram takes 5s and is not canceled | **5017ms**, completed successfully under 10s |
| Five messages, hung push, return ≤3000ms | **2002ms**, push 1, Telegram 5, warning 1; next batch attempts push for new messages |

#### Steps 1–7: expected versus actual

| Step | Expected | Actual |
|---|---|---|
| 1 | Independent DB scope; lifetime through actual completion; runner query safe; at most one outstanding operation | PASS: read/cleanup cancellation gates, distinct context identities, runner concurrency query, subsequent-batch capacity, late-fault handling |
| 2 | Invalid payload preserves all devices; only Unregistered removes; healthy delivery continues | PASS: local validation and real typed SDK errors offline with EF InMemory; InvalidArgument retains both registrations |
| 3 | Full eight-line buy body, realtime-sell and backtest warning retained | PASS; no clipping; oversized messages skipped only on push |
| 4 | Independent ordered channel loops, tick cancellation stops both | PASS: Telegram message 2 proceeds while push message 1 waits, and reverse; per-channel ordering retained |
| 5 | Sanitized diagnostics; fixed/transient initialization classification; cooldown 300s; single flight; bounded wait | PASS offline: no exception message/path leakage, permanent failures do not retry, transient failures retry after cooldown, virtual 2s bounds slow initialization, no old-message replay |
| 6 | Controlled time without package; three real-clock acceptance tests | PASS: custom clocks/gates; only the three tagged tests contain delays; 479 non-real-clock cases pass repeatedly |
| 7 | Kenh naming, plan references, concise labeled comments, Vietnamese comment/XML wording | PARTIAL: naming/references/labels/consequences corrected; comments and XML remain English, so Vietnamese-only wording is not fulfilled |

#### Scope and file inventory

Working tree relative to HEAD: **7 modified backend files + 34 added backend files + 3 modified Phase 2 documents**. No deletions. This includes the original uncommitted Phase 2 work; the current correction adds **9 files** beyond the original 25. Protected migration/designer/snapshot are unchanged from the revision's starting hashes, including snapshot +48/-0. All **192 protected owner hash/path pairs** match; no protected membership additions/removals. All 14 runners, 21 existing repository-related files, `TelegramNotifier`, `AddTelegramNotifier()`, and `V3TelegramFormatter` match HEAD.

Modified backend paths (relative to `backend/`):
- `StockRadar.Api/appsettings.json`
- `StockRadar.Application/DependencyInjection.cs`, `StockRadar.Application/Options/HyperparameterTuningOptions.cs`
- `StockRadar.Infrastructure/DependencyInjection.cs`, `StockRadar.Infrastructure/StockRadar.Infrastructure.csproj`, `StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs`, `StockRadar.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

Added backend paths (group prefixes apply to every filename in that bullet):
- `StockRadar.Application/Abstractions/`: `IGuiThongBao.cs`, `INguiTinDay.cs`, `IThietBiNhanThongBaoRepository.cs`; `StockRadar.Application/Options/PushNotifyOptions.cs`
- `StockRadar.Domain/Enums/LoaiTinV3.cs`
- `StockRadar.Infrastructure/Migrations/`: `20261029100754_AddThietBiNhanThongBao.cs`, `20261029100754_AddThietBiNhanThongBao.Designer.cs` (preexisting/protected during this correction)
- `StockRadar.Infrastructure/Notifications/`: `GuiThongBao.cs`, `KenhDayTinFcm.cs`, `V3PushFormatter.cs`, **`PhamViDbThongBao.cs`**, **`LoiKenhDayTinFcm.cs`**, **`BoTaoHttpFcm.cs`**
- `StockRadar.Infrastructure/Persistence/Entities/ThietBiNhanThongBaoEntity.cs`, `StockRadar.Infrastructure/Persistence/Repositories/EfThietBiNhanThongBaoRepository.cs`
- `StockRadar.Tests/Notifications/`: `BoThuGuiThongBao.cs`, `BoThuLogFcm.cs`, `DangKyKenhThongBaoTests.cs`, `FcmMessageBuilderTests.cs`, `GuiThongBaoCacMayTests.cs`, `GuiThongBaoDocLapTests.cs`, `GuiThongBaoTamTests.cs`, `GuiThongBaoTimeoutTests.cs`, `KenhDayTinFcmDisabledTests.cs`, `KenhDayTinFcmTokenTests.cs`, `ThietBiDangKyTests.cs`, `ThietBiXoaTokenTests.cs`, `V3PushFormatterTests.cs`, **`DongHoGia.cs`**, **`GuiThongBaoDieuKhienTests.cs`**, **`PhamViDbThongBaoTests.cs`**, **`KiemTraTinFcmTests.cs`**, **`KhoiTaoFcmTests.cs`**, **`GuiFcmMotLanTests.cs`**

Bold added filenames identify the 9 files created by this correction. Revised documents: `plan.md` (Revision 3 item 2/table row 3, §b/§c/§g item 4 and maps), `contracts/push-delivery.md` (§1 flow/token/DB policy, §2 full body/payload size, §4 initialization, §5 tests), and this `tasks.md` (T004/T007–T010, T028 spelling, historical/current verification evidence).

Required `git grep -n "BotToken\|private_key\|AIza" -- backend` ran with raw values suppressed. Existing tracked matches: 7, unchanged from HEAD; **0 matching added lines** and **0 matching lines across all 34 untracked backend files**. Broader signature scan: 0 findings in changes. This is a bounded heuristic check, not proof of secret absence; ignored files were excluded. `git diff --check` passed, and `git diff --no-index --check` passed for all 34 untracked backend files. Git reported only existing LF-to-CRLF normalization notices.

#### Unverified and handoff

- SQL Server cancellation/termination latency, apply/rollback, and real-provider concurrency: prohibited DB/API access; InMemory cannot establish these properties.
- Real credential parsing/Firebase initialization, FCM/Telegram delivery, mobile receipt/navigation, production logs: no live transport or credentials used.
- Runner save-before-send end-to-end and SC-004 full-tick performance: T012 is not started; existing runners are unchanged.
- IDE diagnostics could not run because the provider was unavailable; compiler and tests supplied verification.
- Vietnamese-only comment/XML wording remains incomplete as stated in step 7.

No API start/stop/restart, migration application, production change, or T012 work. Migration approval remains pending.

Phase 2 commit created as `d5357ad` with message `[FEAT][notify] sửa vòng gửi độc lập và bảo vệ token thiết bị`; not pushed.

### Phase 2 follow-up evidence — owner changed 30/10/2026

Scope: correct warning labels, raise skip/rejection/cleanup log levels, and make the notification DB slot wait instead of throwing. Comments and documents stay English; new identifiers use Vietnamese without accents.

#### Changes

- `BoTaoHttpFcm.cs`: `BUSINESS-RULE:` → `DO-NOT-CHANGE:`; consequence states that restoring the SDK's default four retries can resend an accepted notification.
- `KenhDayTinFcm.cs` (`LayViecKhoiTao`): `BUSINESS-RULE:` → `DO-NOT-CHANGE:`; consequence states that a canceled caller must not abort the shared initialization worker.
- `PhamViDbThongBao.cs`: `LIMIT:` now states the single-slot threshold and that a competing flush waits at most its 2-second push budget before its batch skips push with `qua_han`.
- `GuiThongBao.cs`: skipped invalid/oversized payload logs at Warning; diagnostic template gains only the allowlisted stock `Symbol` field.
- `KenhDayTinFcm.cs`: skipped payload, device rejection, and token-cleanup failure now log at Warning; payload skip adds `KieuLoi` and `Symbol`.
- `PhamViDbThongBao.cs`: `Interlocked` flag and immediate `InvalidOperationException` replaced by `SemaphoreSlim(1, 1)`; a competing flush waits with the linked push-budget token, so contention surfaces as `qua_han` with exactly one Warning and never as `mang`.

#### Red before green

```text
New slot tests before the SemaphoreSlim change:
Failed! - Failed: 3, Passed: 5, Skipped: 0, Total: 8, Duration: 744 ms
  ChotDauXongTrongMotGiayThiDotSauVanDayDuoc            Expected 2, Actual 1 (push batches)
  ChotTreoQuaHaiGiayThiDotSauBoKenhDayVoiQuaHan        Expected qua_han, Actual mang
  BaDotDongThoiKhongBaoGioChayHaiThaoTacDbCungLuc      Expected 3, Actual 1 (DB operations entered)
After the SemaphoreSlim change:
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 721 ms
```

Level changes required these assertion updates (behavioral expectations, not weakened checks): `KiemTraTinFcmTests` skipped payload 0→1 Warning; `KenhDayTinFcmTokenTests` rejection Debug→Warning and 1→2 Warnings for InvalidArgument (the healthy follow-up send retries the retained token); `KhoiTaoFcmTests` cleanup failure Debug→Warning; `ThietBiXoaTokenTests` oversized-payload batch 0→1 Warning. Allowlists in `BoThuLogFcm` and `GuiThongBaoDieuKhienTests` now permit `Symbol`; `GuiThongBaoDieuKhienTests` additionally asserts the payload-skip warning carries the stock symbol. The dispatcher tests still see 0 channel-level warnings because those fixtures use a throwaway adapter logger.

#### Verification

```text
dotnet build backend/StockRadar.slnx --no-incremental -p:ImportDirectoryBuildTargets=false
Build succeeded. 22 Warning(s), 0 Error(s). No new warnings; unchanged NU1902/NU1903 and CS0618 inventory.
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj -p:ImportDirectoryBuildTargets=false
Passed! - Failed: 0, Passed: 484, Skipped: 0, Total: 484, Duration: 9 s.
Category=ThoiGianThat: Total tests: 3, Passed: 3, Elapsed 5010/2017/2015 ms.
```

484 = the previous 482 plus two net additions from replacing one immediate-failure slot test with three slot tests.

Protected migration files were re-hashed after these changes: migration `7c4e9ef1`, designer `589f3898`, snapshot `76eeef62` — unchanged from `d5357ad`. `git grep -n "BotToken\|private_key\|AIza" -- backend` produced no credentials, and `git diff --check` reported no whitespace errors.

#### Unverified

SQL Server cancellation latency and real contention behavior, real credential/FCM delivery, production log volume from the raised levels, and runner-level integration remain unverified for the reasons already recorded above. Push is deliberately not performed because the server cron applies the unapproved migration on `master`.

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

- [ ] T028 [US4] Test `backend/StockRadar.Tests/Notifications/GuiThongBaoHeThongTests.cs`: `GuiHeThongAsync` ⇒ telegram nhận `parseMode: null` + đúng chuỗi cũ; push nhận `Kenh=="he-thong"`, priority `normal`, `Symbol==null`, title `HPO · xong|lỗi|quá giờ|bỏ qua`; kênh đẩy ném ⇒ HPO runner vẫn chạy hết (không đổi exit code/logic)
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

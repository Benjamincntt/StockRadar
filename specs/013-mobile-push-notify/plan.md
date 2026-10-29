# Implementation Plan: Thông báo đẩy V3 + HPO lên app mobile (song song Telegram)

**Branch**: `master` (làm như spec 012/013 — không tạo branch feature) | **Date**: 2026-10-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification từ `specs/013-mobile-push-notify/spec.md`

**Status**: **Revision 3** — Step 0 approved with four adjustments (29/10/2026). Apply the documentation adjustments, then T001–T003 without another document-approval stop. STOP-Phase-1 still requires the owner to commit, ship, and confirm shipment before T004; STOP-2 (migration) and STOP-3 (Firebase configuration) remain.

## Revision 3 — approved adjustments

**Owner changed 29/10/2026**: Phase 2 review corrections below supersede the original token-error policy and per-message channel barrier. Complete T004–T011 corrections only; T012, migration application, API operations, and production access remain prohibited.

1. Separate per-channel limits: `TelegramNotify:TimeoutGiay=10`, `Push:TimeoutGiay=2`. Both are AI proposals adopted by the owner. Configuration and dispatcher changes belong to Phase 2, not Phase 1.
2. During one `GuiTatCaAsync` batch, the first push timeout or connection failure disables push for the remaining messages and emits exactly one `LogWarning`; Telegram still attempts every message. **Owner changed 29/10/2026**: only `Unregistered` removes the affected token; `InvalidArgument` retains it and logs safe metadata. Invalid local payloads skip only that push message, preserving every registration.
3. Existing HTTP triggers do not bypass the market calendar. Verify the intraday runner's save-before-send behavior live only during trading hours; use unit tests outside those hours. Do not add an hours-bypass flag.
4. Only the missing-Firebase edge case and FR-013 in `spec.md` change: warn once on the first send, not at startup.

## Sửa đổi 2 (theo rà soát của owner — thay cho bản đề xuất trước)

| # | Điểm sửa | Bản cũ nói sai/thiếu | Bản này |
|---|---|---|---|
| 1 | Chống gửi trùng | Viết rằng `KhopBanAsync` ghi cờ `DaBanTinThoat` **trước** khi gửi tin, nên thứ tự gửi hiện tại đã an toàn | **Sai sự thật**: `KhopBanAsync` (`V3Engine.cs:534-554`) chỉ gán cờ **trong bộ nhớ**; DB chỉ nhận tại `engine.LuuAsync` cuối nhịp (`V3TrongPhienRunner.cs:126`), **sau** lệnh gửi → runner đổi sang **gom tin → `LuuAsync` → gửi** (§Constraints, §b, push-delivery §1) |
| 2 | Test chống rò T001 | Test tự dựng `AddHttpClient(...).RemoveAllLoggers()` trong chính body test ⇒ xoá `RemoveAllLoggers()` ở `DependencyInjection.cs:130` test **vẫn xanh** (test kiểm chính nó, không kiểm code thật) | Test gọi **hàm đăng ký thật** của `StockRadar.Infrastructure`; tách `AddTelegramNotifier()` khỏi `AddInfrastructure` để cả DI thật lẫn test cùng gọi một đường (§a) |
| 3 | Independent channel loops | Per-message `Task.WhenAll` delayed the next Telegram message behind push | **Owner changed 29/10/2026**: `Task.WhenAll` awaits two independent ordered loops. Per-message TimeProvider deadlines: Telegram 10s, push 2s; push failures open a batch-local circuit (§b, §g). Telegram message 2 never waits for push message 1 |
| 4 | Đăng xuất / hết phiên | `logout()` luôn gọi `go` trước khi xoá Bearer ⇒ JWT hết hạn → `go` nhận 401 → `onUnauthorized` → `logout()` → **vòng lặp** | `logout({bool goMay = false})`; **chỉ** nút Đăng xuất truyền `goMay: true`; hai đường hết phiên (`app_services.dart:11-15` và `:38`) giữ nguyên hành vi cũ, không gọi `go` (§b-mobile, push-delivery §3) |
| 5 | TTL tin đẩy | V3 + tin thử `00:05:00` | V3 + tin thử `01:00:00` (máy vivo ngủ sâu quá 5 phút ⇒ Firebase bỏ hẳn tin, trái SC-001); HPO giữ `12:00:00` (push-delivery §2) |
| 6 | Khởi tạo Firebase | `app.dart` đăng ký `onBackgroundHandler` (tên **sai**, API thật là `FirebaseMessaging.onBackgroundMessage`) | Một cách duy nhất ghi giống nhau ở 3 file: `main.dart` = `ensureInitialized()` → `Firebase.initializeApp()` trong `try/catch` → `FirebaseMessaging.onBackgroundMessage(<hàm top-level>)`, **tất cả trước `runApp`**; `app.dart` = tạo kênh Android + lắng nghe + điều hướng (§g, push-delivery §3) |
| 7 | Thiếu so với spec | US1 AC4 (quyền bị từ chối ⇒ chỗ nhắc + nút mở cài đặt) và edge vivo (hướng dẫn tắt tối ưu pin) chưa có task | Bổ sung T023a, vị trí: snackbar của nút gửi thử + mục trong menu drawer (§mobile) |
| 8 | Token Firebase tự đổi | `dang-ky` chỉ upsert theo token mới ⇒ token cũ còn thành hàng gà | `dang-ky` nhận thêm `tokenCu` (tuỳ chọn): xoá dòng `tokenCu` **chỉ khi nó thuộc user hiện tại**, rồi upsert token mới (§d, thiet-bi-api §1) |
| 9 | Cảnh báo thiếu cấu hình | "ghi một `LogWarning` **lúc khởi động**" | Kênh đẩy init **lazily lúc cần lần đầu** ⇒ cảnh báo ra ở **lần gửi đầu tiên**, vẫn **đúng một** dòng dù gửi nhiều lần (§g, push-delivery §4) |
| 10 | Route `go` | diễn giải bằng thòng | Giữ hành vi xoá theo token không kiểm chủ, thêm **đúng một** dòng `// SECURITY:` nêu hậu quả cụ thể trong controller (§e, thiet-bi-api §2) |
| 11 | Ngưỡng | dễ đọc thành owner tự đặt | 1 hour (TTL), 10s (Telegram), 2s (push), SC-002 60s, SC-004 1s **are AI proposals adopted by the owner** — ghi rõ ở mọi chỗ |
| 12 | T013 | mặc định dựng được runner thật trong unit test | Nếu không dựng `V3Engine` mà không cần SQL Server ⇒ test ở mức dispatcher + báo **"chưa kiểm ở mức runner + lý do"**, không lặng lẽ bỏ qua (tasks T013) |


## Tóm tắt

Preserve Telegram message content and runner schedules; delivery must follow persistence. Later runner integration replaces 11 `telegram.SendAsync` sites (7 V3 + 4 HPO) with `IGuiThongBao`; it is not part of this Phase 2 correction. The dispatcher runs **two independent ordered loops**, awaiting both via `Task.WhenAll`, with per-message `try/catch` and TimeProvider-backed linked cancellation limits: **Telegram 10s, push 2s** (FR-002). Notification DB tasks own isolated scopes until actual completion, with at most one outstanding operation per service provider. The first push timeout or connection failure skips push for the rest of that batch, with exactly one warning; Telegram continues. Required tick ordering remains **buffer → save DB → send**
(xem §Constraints). Mobile Android đăng ký FCM token theo tài khoản, tạo 2 kênh Android
("Tín hiệu V3" ưu tiên cao + âm, "Hệ thống" ưu tiên thường), chạm tin V3 mở `/stocks/:symbol`.
Song song đó chặn lỗi bảo mật đang chạy trên production: HttpClient ghi URL
`api.telegram.org/bot<token>/sendMessage` ra log mức Information (FR-012/SC-003).

## Bối cảnh kỹ thuật

**Language/Version**: .NET 10 (backend), Dart/Flutter (mobile). Máy dev: Flutter **3.47.4**, Dart **3.13.3** (`flutter --version`)

**Primary Dependencies**: EF Core 10.0.0 + **SQL Server** (`DependencyInjection.cs:26-28` — `UseSqlServer`, không phải SQLite), Quartz (`MarketJobs`/V3 jobs), ASP.NET Core JWT Bearer (`Program.cs:59-73`), xUnit + EF InMemory

**Gói mới (duyệt — xem bảng §Gói mới)**: `FirebaseAdmin` 3.7.0 (backend) + 3 gói pub.dev (mobile). **Không** thêm gói nào cho việc chặn log: `RemoveAllLoggers()` đã có sẵn trong `Microsoft.Extensions.Http` (đã kiểm chứng trong ref assembly `microsoft.extensions.http/10.0.8/lib/net10.0`, member `HttpClientBuilderExtensions.RemoveAllLoggers`) → không cần `Microsoft.Extensions.Http.Diagnostics`.

**Storage**: SQL Server qua `ApplicationDbContext`. Thêm **1 bảng mới** `ThietBiNhanThongBao`; **không** sửa bảng/cột cũ.

**Testing**: `dotnet test backend/StockRadar.Tests` (chặng này baseline **284/284**), `flutter test mobile` (baseline **9/9**), `flutter analyze`, `npm run build` (chỉ khi đụng kiểu dữ liệu web — dự kiến **không**).

**Target Platform**: API chạy Linux (`/var/www/publish/stockradar-api`, log qua `journalctl -u stockradar`); mobile chỉ **Android** (máy owner: vivo, Android 16, có Google Play Services).

**Project Type**: monorepo API + mobile + web.

**Performance Goals**: mỗi nhịp V3 không chậm quá **1 giây** (SC-004); tin đẩy không trễ quá **60s** so với Telegram (SC-002). *Cả hai ngưỡng là đề xuất của AI, owner dùng.*

**Constraints** (*10s Telegram / 2s push / 1h TTL / 60s / 1s are AI proposals adopted by the owner*):
- Không đổi luật V3, giờ nhịp, nội dung tin Telegram (spec 012). Không đổi **thứ tự hay số lần** `LuuAsync` khi chưa hỏi owner.
- **Tin chỉ gửi SAU khi nhịp đã lưu DB** (owner chốt). Sự thật đã kiểm trong code: `V3Engine.KhopBanAsync` (`V3Engine.cs:534-554`) chỉ gán `DaBanTinThoat` **trong bộ nhớ** (`:544`); giá trị đó chỉ xuống DB tại `engine.LuuAsync` cuối nhịp (`V3TrongPhienRunner.cs:126`), tức **sau** lệnh gửi hôm nay. Vì vậy runner phải **gom tin vào danh sách → `LuuAsync` → gửi từng tin theo đúng thứ tự gom**; nếu không, API sập giữa nhịp khi tin đã đi sẽ khiến đúng mã đó bị bán lại và báo **lần hai** ở nhịp kế.
- `LuuAsync` vẫn là lần `SaveChangesAsync` cuối nhịp mà runner gọi; **ngoài** nó, `engine.GhiTinHieuAsync` (`V3Engine.cs:365`, `:375`) tự `SaveChangesAsync` bên trong — xem bảng §(b2) để biết số lần lưu thực tế của từng runner.
- FCM failures must not break the tick or block Telegram (FR-002): two independent ordered loops joined by `Task.WhenAll`, separate per-message TimeProvider-backed linked CTS limits (`TelegramNotify:TimeoutGiay=10`, `Push:TimeoutGiay=2`), no exceptions escaping to the runner. One push timeout/connection failure opens a circuit for the current batch only; exactly one push-failure warning. Token-specific failures and invalid local payloads do not open the circuit. No unfinished notification DB task may borrow the runner's context.
- Khoá / token / URL có khoá **không** vào log, comment, file. Log chỉ in vài ký tự cuối của device token.
- **LIMIT** của gửi-sau-lưu: API sập giữa `SaveChanges` và bước gửi ⇒ tin của nhịp đó **mất** (tin bán tự bắn lại phút sau vì vị thế vẫn mở; tin mua ATO/ATC chỉ bắn lại nếu nhịp sau còn chấm tín hiệu). Chủ đích chọn mất tin thay vì trùng tin.
- `mobile/pubspec.yaml`: chỉ thêm 3 gói đã duyệt, **không** đụng `version: 3.0.1+23`.
- **Không** commit, push, chạy `ship-all.ps1`/`build-apk.ps1`, **không** chạm DB/config production.

**Scale/Scope**: ~10 file backend sửa + ~6 file backend mới; 1 migration; 3 endpoint; ~7 file mobile sửa + 2 file mobile mới + 2 file gradle.

## Quyết định thiết kế (chốt a–g theo yêu cầu)

### (a) Gửi FCM từ .NET: dùng `FirebaseAdmin`, không tự viết FCM HTTP v1

**Quyết định**: `FirebaseAdmin` **3.7.0**, gọi `FirebaseMessaging.SendAsync(Message)` (bên trong đã là FCM HTTP v1).

**Lý do**:
- Tự viết v1 = tự ký JWT RS256 bằng private key + gọi `oauth2.googleapis.com/token` + cache access token ~55'. ~150 dòng code bảo mật tự viết, đúng chỗ dễ rò key nhất. Bỏ.
- `FirebaseAdmin` exposes typed `FirebaseMessagingException.MessagingErrorCode`: only `Unregistered` proves the registration should be removed (FR-009). **Owner changed 29/10/2026**: `InvalidArgument` may describe a server-rejected payload, so preserve the registration even after local validation.
- Key nằm trong **Authorization header do SDK tự gắn**, không bao giờ xuất hiện trong URL → không có dạng rò "key trong URL" như Telegram.
- SDK **không** dùng `IHttpClientFactory` (tự tạo HttpClient nội bộ trong `Google.Apis`), nên không có default logger `System.Net.Http.HttpClient.*.LogicalHandler` — chính là thủ phạm ghi URL Telegram ra `journalctl`.

**Phòng rò (FR-012)** — áp cho **cả hai** kênh:

```csharp
// backend/StockRadar.Infrastructure/DependencyInjection.cs — TÁCH dòng 130 thành hàm riêng
// để API production và test cùng đi qua MỘT đường đăng ký:
public static IServiceCollection AddTelegramNotifier(this IServiceCollection services)
{
    services.AddHttpClient<ITelegramNotifier, TelegramNotifier>().RemoveAllLoggers();
    return services;
}

// trong AddInfrastructure: thay dòng 130 bằng
services.AddTelegramNotifier();
```

(test gọi `services.AddTelegramNotifier()` với `using StockRadar.Infrastructure;` — **không** tạo bản sao đăng ký riêng trong file test.)

- `TelegramNotifier` tự ghi log của nó (`TelegramNotifier.cs:31-53`) đã **không** chứa URL/token → giữ nguyên, chỉ tắt logger của HttpClient.
- FCM does not use `AddHttpClient`. Logs allow only `GiaiDoan` (`khoi_tao`, `doc_token`, `gui`, `don_token`), `LoaiLoi` (`qua_han`, `mang`, `xac_thuc`, `payload`, `token`, `token_hoac_payload`), allowlisted exception type, `MessagingErrorCode`, elapsed milliseconds, device/result counts, and stock `Symbol`. Token rejection may additionally log only the last five token characters. Never log title/body, exception objects/messages, response bodies, credential paths, or full tokens. `DayTinNhan.Kenh` is the internal channel property, not a diagnostic field. **Owner changed 30/10/2026**: skipped payloads and token-cleanup failures log at Warning; device rejection is Warning once per token, then Debug across messages/batches. `Unregistered` logs `LoaiLoi=token`; `InvalidArgument` logs `LoaiLoi=token_hoac_payload` because FCM cannot distinguish the two causes here. The singleton adapter's thread-safe RAM-only token set resets on restart; successful `Unregistered` DB cleanup removes its entry. Per-batch repeated failure details stay at Debug.
- **Test chống rò phải kiểm code thật** (bài học từ bản 1): `backend/StockRadar.Tests/Notifications/TelegramNotifierLogTests.cs` **không** được tự viết `AddHttpClient(...).RemoveAllLoggers()` trong body test (làm vậy là test kiểm chính nó — xoá `RemoveAllLoggers()` ở `DependencyInjection.cs:130` test vẫn xanh). Test phải:
  1. `new ServiceCollection().AddLogging()` rồi gọi **`services.AddTelegramNotifier()`** (extension thật trong `StockRadar.Infrastructure`, cùng đường với API production);
  2. nạp `IOptions<TelegramNotifyOptions>{Enabled=true, BotToken="TOK-SECRET-123", ChatId="chat-test"}` — **`Enabled` mặc định `false`** (`HyperparameterTuningOptions.cs:33`), để false thì `SendAsync` trả về sớm và test PASS rỗng; phải bật tay và assert handler giả **đã nhận đúng 1 request**;
  3. Replace the Telegram client's primary handler with a fake returning 200: `services.Configure<HttpClientFactoryOptions>(nameof(ITelegramNotifier), o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = fake))`. The installed `Microsoft.Extensions.Http` documentation names the client after **TClient (`ITelegramNotifier`)**, not TImplementation. Assert exactly one fake request; do not use a network transport or load real credentials.
  4. resolve `ITelegramNotifier`, gửi 1 tin, assert **không** dòng log nào chứa `api.telegram.org/bot` và không dòng nào chứa `TOK-SECRET-123`.
  Nếu cách (3) không dựng được với cấu hình tối thiểu ⇒ dùng chính phương án tách `AddTelegramNotifier()` ở trên (đã chốt trong code) để test và DI thật cùng gọi một hàm.
- **Bắt buộc theo thứ tự**: chạy test **trước** khi sửa dòng 130 ⇒ **FAIL**, ghi lại nguyên output; sửa xong ⇒ PASS. Hai output này phải có trong báo cáo chặng 2 (không có FAIL thì chưa được coi là test thật).

### (b) Một lớp phát tin chung: `IGuiThongBao`

**Vì sao bắt buộc lớp hoá**: 11 chỗ gọi, mỗi chỗ 1 dòng; sửa tay từng chỗ thành 2 khối `try/catch` là 22 chỗ dễ sót, và runner sẽ biết về FCM (vi phạm ranh giới Application/Infrastructure). Một chỗ gọi → cả hai kênh, logic lỗi chỉ viết **một lần** và test được độc lập.

```csharp
// backend/StockRadar.Application/Abstractions/IGuiThongBao.cs (mới)
public interface IGuiThongBao
{
    /// Gom một tin V3 vào danh sách chờ của nhịp — CHƯA gửi ngay (xem §Constraints).
    void DatTinV3(LoaiTinV3 loai, string symbol, LoaiKichBanV3 kichBan, string telegramHtml);

    /// Gom một tin hệ thống (HPO) vào danh sách — cùng chữ thường cho cả hai kênh, kênh Android "Hệ thống".
    void DatTinHeThong(string tieuDeDay, string noiDung);

    /// Flush two independent ordered channel loops after the runner's save completes.
    Task GuiTatCaAsync(CancellationToken cancellationToken = default);

    /// HPO dùng: gom + gửi ngay một dòng (không có trạng thái DB nào phải chờ lưu).
    Task GuiHeThongAsync(string tieuDeDay, string noiDung, CancellationToken cancellationToken = default);
}

public sealed record TinDaGom(LoaiTinV3? LoaiV3, string? Symbol, LoaiKichBanV3? KichBan,
                              string? TieuDeHeThong, string NoiDungTelegram);

// backend/StockRadar.Application/Abstractions/INguiTinDay.cs (mới) — kênh đẩy, hạ tầng FCM ẩn sau nó
public interface INguiTinDay
{
    bool DangHoatDong { get; }
    Task<DayTinKetQua> GuiAsync(IReadOnlyList<string> tokens, DayTinNhan tin,
                                CancellationToken cancellationToken = default);
}

public sealed record DayTinNhan(string TieuDe, string NoiDung, string Kenh, string? Symbol,
                                LoaiTinV3? LoaiV3 = null);
public sealed record DayTinKetQua(int SoDaGui, IReadOnlyList<string> TokenHong);
```

`DayTinNhan.LoaiV3` is optional internal metadata so the FCM adapter can emit the required `loaiTin` value without parsing presentation titles; null with `he-thong` means system, null with `v3-signal` means test.

`LoaiTinV3 { Mua, Ban, HuyMua, AtoN3 }` đặt ở `backend/StockRadar.Domain/Enums/` (enum thuần, không logic). Đệm tin dùng `List<TinDaGom>` trong instance scoped của `GuiThongBao` (một runner = một scope = một nhịp), không có trạng thái tĩnh.

**Implementation**: `backend/StockRadar.Infrastructure/Notifications/GuiThongBao.cs`, scoped via `AddScoped<IGuiThongBao, GuiThongBao>`. Dependencies: `ITelegramNotifier`, `INguiTinDay`, singleton `PhamViDbThongBao`, `TimeProvider`, both channel options, and `ILogger<GuiThongBao>`. Register `TimeProvider.System` with `TryAddSingleton`. Snapshot and clear the buffer before delivery; cache materialized tokens once per batch after valid payload/availability checks.

1. **Owner changed 29/10/2026**: start one ordered Telegram loop and one ordered push loop; `Task.WhenAll` awaits the loops, not a barrier per message. Each loop preserves 1 → 2 → 3 independently. Telegram keeps `HTML` for V3 and `null` for system messages.
2. Every message creates a deadline with `new CancellationTokenSource(TimeSpan, TimeProvider)` linked to the tick token: Telegram 10s, push 2s. Both thresholds are AI proposals adopted by the owner. The push budget includes lookup, initialization, sequential device sends, and cleanup. `WaitAsync` bounds waiting even when underlying work ignores cancellation; observe late faults and never replay an ambiguous send.
3. The first push timeout/connection failure opens a batch-local circuit and emits exactly one warning. Skip remaining push messages; Telegram continues. Reset the circuit next batch, but do not bypass an outstanding DB operation. Later repeated failure diagnostics in the same batch use Debug. **Owner changed 30/10/2026**: skipped invalid/oversized payloads and token-cleanup failures log at Warning; each token's first FCM rejection logs Warning and its later rejections log Debug. An ordinary cleanup failure still lets healthy recipients continue.
4. **Owner changed 29/10/2026**: only `Unregistered` enters `TokenHong` and is removed from DB and cached tokens. `InvalidArgument` retains the registration, logs a suffix/error code, and continues devices/messages. Validate payload before device lookup and again in the public adapter; rejection skips only that message, never token cleanup. Local validation is not proof of server acceptance. **Owner changed 30/10/2026**: after successful DB removal, the operation-owned cleanup task calls `KenhDayTinFcm.QuenCanhBaoTokenDaXoa` to reset only logging history, including when completion is later than the caller's timeout. Failed/canceled cleanup retains the history; re-registration after successful cleanup can warn again. No deletion rule, send behavior, or channel deadline changes.
5. Catch channel cancellation locally. Telegram timeout affects one message; push timeout opens its circuit. Original tick cancellation stops both loops and discards remaining messages without warning about an outage or throwing into the runner.
6. `PhamViDbThongBao` owns `IServiceScopeFactory`, a `SemaphoreSlim(1, 1)` slot, and each read/cleanup task. Its explicit worker task creates an async DI scope, resolves the notification repository/context there, awaits actual EF completion, then disposes the scope and releases the slot. Never borrow a runner-scoped repository. Cancellation requests termination but does not dispose pending work. **Owner changed 30/10/2026**: a new batch waits for the slot with the linked push-budget token; if the budget expires first it skips push for that batch with `LoaiLoi=qua_han` and exactly one Warning, opens no extra scope, and never classifies the contention as `mang`. Two operations never run concurrently. Do not change trading repositories.
7. Timeout/cancellation/circuit tests use the custom `DongHoGia` and completion gates without a new package. Keep only the three acceptance cases below under `[Trait("Category", "ThoiGianThat")]`. EF InMemory plus scoped concurrency gates verifies isolation/lifetime, not SQL Server cancellation latency.

Required tests (`GuiThongBaoTimeoutTests`):
- One message, push delays 10s with the supplied cancellation token, Telegram completes immediately ⇒ `GuiTatCaAsync` completes **≤ 2.5s**, Telegram called **once**.
- Telegram delays **5s** with the supplied cancellation token ⇒ it **completes successfully without cancellation** under its 10s limit.
- **Five messages**, push hangs, Telegram completes immediately ⇒ push called **once**, Telegram **five times**, total **≤ 3s**, exactly **one** warning; a subsequent batch attempts push again.
- A push connection failure has the same batch-local skip behavior; token-specific errors do not skip healthy tokens or remaining messages.

**Sửa 11 chỗ**: 7 chỗ V3 đổi thành **gom tin** (`guiTin.DatTinV3(...)`) tại đúng vị trí cũ, cộng **một** chỗ `GuiTatCaAsync` sau `engine.LuuAsync` cho mỗi runner; 4 chỗ HPO đổi thành `await guiTin.GuiHeThongAsync(...)` (gom + gửi ngay một dòng) vì HPO không có DB state chờ lưu:

| File | Dòng | Đổi thành |
|---|---|---|
| `V3TruocPhienRunner.cs` | 73 | `guiTin.DatTinV3(LoaiTinV3.AtoN3, vt.Symbol, LoaiKichBanV3.N3, msg);` |
| `V3TruocPhienRunner.cs` | 111 | `guiTin.DatTinV3(LoaiTinV3.Mua, lenh.Symbol, lenh.Loai, tin);` |
| `V3TrongPhienRunner.cs` | 79 | `guiTin.DatTinV3(LoaiTinV3.HuyMua, vt.Symbol, vt.Loai, <msg>);` |
| `V3TrongPhienRunner.cs` | 119 | `guiTin.DatTinV3(LoaiTinV3.Ban, vt.Symbol, vt.Loai, msg);` |
| `V3AtcRunner.cs` | 118 | `guiTin.DatTinV3(LoaiTinV3.Mua, lenh.Symbol, lenh.Loai, tin);` |
| `V3AtcRunner.cs` | 165 | `guiTin.DatTinV3(LoaiTinV3.Ban, vt.Symbol, vt.Loai, msg);` |
| `V3SauPhienRunner.cs` | 69 | `guiTin.DatTinV3(LoaiTinV3.HuyMua, vt.Symbol, vt.Loai, <msg>);` |
| `V3TruocPhienRunner.cs` | 126 (sau `LuuAsync`) | `await guiTin.GuiTatCaAsync(ct);` |
| `V3TrongPhienRunner.cs` | 126 (sau `LuuAsync`) | `await guiTin.GuiTatCaAsync(ct);` |
| `V3AtcRunner.cs` | 171 (sau `LuuAsync`) | `await guiTin.GuiTatCaAsync(ct);` |
| `V3SauPhienRunner.cs` | 103 (sau `LuuAsync`) | `await guiTin.GuiTatCaAsync(ct);` |
| `HyperparameterTuningRunner.cs` | 37 | `await guiTin.GuiHeThongAsync("HPO · bỏ qua", <text>, ct);` |
| `HyperparameterTuningRunner.cs` | 97 | `await guiTin.GuiHeThongAsync("HPO · quá giờ", <text>, ct);` |
| `HyperparameterTuningRunner.cs` | 109 | `await guiTin.GuiHeThongAsync("HPO · lỗi", <text>, ct);` |
| `HyperparameterTuningRunner.cs` | 130 | `await guiTin.GuiHeThongAsync("HPO · xong", message, ct);` |

5 runner đổi tham số constructor `ITelegramNotifier telegram` → `IGuiThongBao guiTin`. **Đã kiểm chứng**: không test nào khởi tạo 5 runner này (`grep ITelegramNotifier` chỉ ra 5 consumer + DI), nên đổi constructor không phá fixture. `ITelegramNotifier` giữ nguyên interface (chỉ `TelegramNotifier` và DI đổi). Các chỗ gọi `HuyChoMuaAsync`, `GhiTinHieuAsync`, `TaoChoMua`, `KhopBanAsync` đều **chỉ gom**, không gửi — tin đi sau khi đã lưu.

### (b2) Số lần lưu DB thực tế của từng runner (đã đọc kỹ cả 4 runner — không đổi thứ tự/số lần lưu)

| Runner | `engine.LuuAsync` (cuối nhịp) | `SaveChangesAsync` khác ngoài `LuuAsync` | Điểm gửi mới |
|---|---|---|---|
| `V3TruocPhienRunner` | 1 lần, dòng **126** | 0 lần gọi trực tiếp, nhưng `engine.GhiTinHieuAsync` (`V3Engine.cs:365`, `:375`) tự lưu **mỗi lần chấm tín hiệu** (dòng 92, 120) | sau 126 |
| `V3TrongPhienRunner` | 1 lần, dòng **126** | không gọi `GhiTinHieuAsync` → **chỉ 1 lần lưu** | sau 126 |
| `V3AtcRunner` | 1 lần, dòng **171** | `engine.GhiTinHieuAsync` tự lưu (dòng 81, 134) | sau 171 |
| `V3SauPhienRunner` | 1 lần, dòng **103** | không gọi `GhiTinHieuAsync` → **chỉ 1 lần lưu** | sau 103 |

**Báo owner**: mỗi runner chỉ có **một** `LuuAsync`, nên điểm gửi mới là duy nhất và rõ ràng. Điểm cần chủ ý: `GhiTinHieuAsync` lưu **hàng `TinHieuV3`** (bảng tín hiệu) **trước** khi gom tin — đó không phải trạng thái chống trùng; trạng thái chống trùng nằm ở `ViTheV3` (`DaBanTinThoat`, `TrangThai`) và chỉ xuống DB tại `LuuAsync`. `engine.HuyLenhTranAsync` (`V3TrongPhienRunner.cs:69`) sửa bộ nhớ mà không bắn tin (đúng như hôm nay) → không ảnh hưởng.

Existing HTTP triggers (`POST /api/v1/market/jobs/v3-truoc-phien`, `v3-trong-phien`, `v3-atc`, `v3-sau-phien`, `MarketJobsController.cs:200-251`, JWT or `X-Sync-Key`) **do not bypass calendar guards**. `v3-trong-phien` returns early outside trading hours (`V3TrongPhienRunner.cs:42`; scheduled job also checks at `V3Jobs.cs:37-39`). The other three runners require a trading day (`V3TruocPhienRunner.cs:40`, `V3AtcRunner.cs:42`, `V3SauPhienRunner.cs:40`). Live verification of the intraday tick's **save → send** ordering is possible **only during trading hours**, with suitable test positions/quotes. Outside those hours, use unit tests and report their scope honestly. **Do not add a flag to bypass market hours.**

### (c) Nội dung tin đẩy: chữ thường, có tiêu đề + thân; Telegram không đổi

`V3TelegramFormatter` **không sửa một ký nào** (test `V3TelegramFormatterHoangLoanTests`, `V3DanhMucXepHangTests` giữ nguyên). Thêm `backend/StockRadar.Infrastructure/Notifications/V3PushFormatter.cs`:

- `TieuDe(LoaiTinV3 loai, string symbol, LoaiKichBanV3 kichBan)` → `MUA · HPG · N1`, `BÁN · HPG`, `HUỶ MUA · HPG`, `ATO · HPG · N3` (FR-003).
- **Owner changed 29/10/2026**: `NoiDung(string telegramHtml)` strips HTML, decodes entities, normalizes line endings/blank lines, and preserves the complete body and emoji. Remove the old 240-text-element limit (`GioiHanPhanTu`); retain all eight buy-message lines, including “Bán realtime” and the backtest warning. Telegram formatting is unchanged.
- System messages keep title `HPO · …` and the complete existing plain-text Telegram body. Oversized V3/HPO messages skip only push, never truncate or modify Telegram.
- Validate nonblank title/body and channel/type/symbol mapping from push-delivery §2. V3 requires a defined type and uppercase ASCII alphanumeric symbol (starts with a letter, length 1–16); HPO/test have neither. [Firebase documents 4096 bytes](https://firebase.google.com/docs/cloud-messaging/error-codes) for ordinary per-token messages, including keys and values (topics: 2048 bytes, not used). The conservative local UTF-8 serialized bound includes Android/data fields, JSON punctuation, and message envelope, excluding the recipient token and transport/authentication envelope. It may overcount and does not guarantee FCM acceptance.
- `Symbol` truyền trong `data` của FCM (`symbol`, `loaiTin`) để app điều hướng (FR-005). `android.notification.channel_id` = `v3-signal` cho tin V3 (priority `high`, FR-004) và `he-thong` cho tin HPO (priority `normal`, FR-001a). Chạm tin HPO chỉ mở app (không có `symbol` → app ở lại màn hiện tại / màn đầu). TTL: V3 + tin thử `01:00:00`, HPO `12:00:00` (chốt theo owner — xem push-delivery §2; ngưỡng 1 giờ là đề xuất của AI, owner dùng).

### (d) Bảng `ThietBiNhanThongBao` + migration

Entity `backend/StockRadar.Infrastructure/Persistence/Entities/ThietBiNhanThongBaoEntity.cs`:

| Cột | Kiểu SQL | Ràng buộc |
|---|---|---|
| `Id` | `bigint identity` | PK |
| `UserId` | `uniqueidentifier` | NOT NULL, FK → `Users`, `DeleteBehavior.Cascade` |
| `Token` | `nvarchar(512)` | NOT NULL, **UNIQUE** (FR-008: một mã chỉ thuộc một bản ghi) |
| `NenTang` | `nvarchar(16)` | NOT NULL, mặc định `android` |
| `NgayDangKy` | `datetime2` | NOT NULL (UTC) |
| `LanCapNhatGanNhat` | `datetime2` | NOT NULL (UTC) |

`OnModelCreating`: `HasKey(Id)`, `HasMaxLength(512/16)`, `HasIndex(Token).IsUnique()`, `HasIndex(UserId)`, FK cascade (xoá tài khoản → sạch máy). Đăng ký `DbSet<ThietBiNhanThongBaoEntity> ThietBiNhanThongBao` cạnh block V3 (`ApplicationDbContext.cs:47-50`).

Upsert (FR-008 + edge "token Firebase tự làm mới"): `DangKyAsync(userId, token, nenTang, tokenCu)` —
1. nếu có `tokenCu` (trim, khác `token`): **xoá** dòng `tokenCu` **chỉ khi dòng đó thuộc `userId` hiện tại** (đúng luật chủ sở hữu — không cho phép xoá máy của user khác bằng token của mình);
2. tìm theo `Token`; tồn tại → cập nhật `UserId` + `LanCapNhatGanNhat`; không → chèn mới.
**Không** bao giờ có 2 dòng cùng token. `tokenCu` là trường **tuỳ chọn** ⇒ app cũ không gửi vẫn chạy đúng (additive). App lấy `tokenCu` từ `SharedPreferences` khi `onTokenRefresh` báo token mới (xem push-delivery §3).

Migration: `dotnet ef migrations add AddThietBiNhanThongBao` (project `StockRadar.Infrastructure`, startup `StockRadar.Api`, dùng `DesignTimeDbContextFactory`). **Bẫy đã ghi ở skill `efcore-migration-review`**: tool này từng sinh `Up`/`Down` sai trong repo (`20261026130015_AddV3Tables.cs:10` ghi rõ migration viết TAY). Bắt buộc mở file đọc lại toàn bộ, chỉ có `CreateTable` + 2 `CreateIndex`, không drop/alter gì khác.
**Cảnh báo quan trọng khi áp**: `DatabaseInitializer.cs:17` gọi `db.Database.MigrateAsync()` **lúc khởi động API** → chỉ cần chạy API local là migration tự áp vào DB. Vì vậy tasks tách bước "tạo migration + đọc file + gửi owner" khỏi bước "áp migration", và **không** chạy API local cho tới khi owner duyệt file.

### (e) 3 endpoint dưới `api/v1`, cần đăng nhập

Controller mới `backend/StockRadar.Api/Controllers/ThietBiController.cs` — `[Route("api/v1/thiet-bi")]` + `[Authorize]` (pattern `NhatKyController.cs:14-19`). `UserId` **luôn** lấy từ JWT qua `ICurrentUserService`, không bao giờ từ body (SECURITY). Chi tiết trong [contracts/thiet-bi-api.md](./contracts/thiet-bi-api.md).

| Method | Path | Việc |
|---|---|---|
| `POST` | `/api/v1/thiet-bi/dang-ky` | upsert device token của user hiện tại, nhận thêm `tokenCu` tuỳ chọn để xoá token cũ đúng chủ (FR-007, FR-008) |
| `POST` | `/api/v1/thiet-bi/go` | xoá device token (FR-007, idempotent → 204) |
| `POST` | `/api/v1/thiet-bi/gui-thu` | gửi tin thử cho chính các máy của user đang đăng nhập (US3 AC4) |

`go` **giữ** hành vi xoá theo token mà **không** kiểm chủ sở hữu, để đăng xuất luôn gỡ được máy. Đổi lại phải ghi **đúng một** dòng comment tại chỗ trong controller (theo luật comment 4 nhãn):

```csharp
// SECURITY: go xoá theo token, không kiểm chủ — ai biết token máy khác thì gỡ được máy đó khỏi danh sách nhận tin, không lộ dữ liệu.
```

App chỉ gọi `go` khi **bấm Đăng xuất** (`goMay: true`); phiên hết hạn **không** gọi (xem §mobile dưới đây và push-delivery §3).

`gui-thu`: `200 {guiDuoc, soThietBi}` khi có máy; `200 {guiDuoc:false, soThietBi:0}` khi user chưa có máy; `503` khi kênh đẩy tắt (thiếu cấu hình Firebase) — app báo rõ "chưa bật kênh đẩy", không im lặng.

Không đụng endpoint/DTO cũ → không breaking change cho 2 client đã ship (`api-contract-review`: additive-only). Web `frontend/` không dùng 3 route này → **không** cần `npm run build` (vẫn chạy nếu có sửa model chung).

### (f) Gói Flutter cần thêm (pubspec `dependencies`)

| Gói | Phiên bản | Việc |
|---|---|---|
| `firebase_core` | `^5.1.0` | khởi tạo FirebaseApp (Android). Đã kiểm tra khớp Dart 3.13.3 |
| `firebase_messaging` | `^17.0.1` | lấy/refresh FCM token, foreground/background handler, `getInitialMessage`/`onMessageOpenedApp` cho điều hướng |
| `flutter_local_notifications` | `^22.3.3` | tạo 2 kênh Android (`v3-signal` có âm + high importance, `he-thong` thường), xin quyền `POST_NOTIFICATIONS`, hiển thị tin foreground vào đúng kênh |

Gradle: plugin `com.google.gms.google-services` **4.5.0** (phiên bản Google services plugin mới nhất theo release notes Play services). Nạp **có điều kiện** để thiếu `google-services.json` vẫn build được (xem §(g)-mobile dưới đây). `dependency_overrides` và `version: 3.0.1+23` giữ nguyên.

### (g) Khoá cấu hình Firebase trên server + kênh đẩy tự tắt

```jsonc
"Push": {
  "Enabled": true,
  "Firebase": {
    "CredentialPath": ""
  },
  "TimeoutGiay": 2,
  "KhoiTaoLaiSauGiay": 300
},
"TelegramNotify": {
  "TimeoutGiay": 10
}
```

**Phase 2 owner update (2026-10-29)**: add `Push` to `backend/StockRadar.Api/appsettings.json` and `TimeoutGiay=10` to the existing `TelegramNotify` section/options. Preserve its credentials and Enabled value; do not create separate Telegram options. Bind `PushNotifyOptions` beside the existing `TelegramNotifyOptions` binding. The dispatcher enforces independent 10s/2s linked-CTS limits, both AI proposals adopted by the owner. Phase 1 is shipped. After reporting STOP-2, DB-free work may continue through T011 only; migration application and Phase 3 still require owner approval.

**Owner changed 29/10/2026 — server initialization and token policy**:
1. `KenhDayTinFcm` is singleton and initializes lazily through one shared, lifecycle-managed task. Synchronous credential/app creation runs off the caller stack, so the dispatcher's 2s wait also covers slow initialization. Caller cancellation never cancels shared initialization or starts a duplicate.
2. Fixed disabled/missing-file/malformed-JSON/configuration failures remain disabled until restart. Construction/status reads are silent; exactly one unavailable warning occurs on the first attempted send, then Debug. Transient network/timeouts allow another initialization only on a later send after `Push:KhoiTaoLaiSauGiay=300` (AI proposal adopted by owner). At most one initializer runs; successful recovery sends no old messages.
3. Initialization and active sends hold resource leases. Disposal releases the Firebase app only when those operations actually finish. Logs contain only the allowlisted fields in §a, never raw exceptions or credential paths. `BoTaoHttpFcm` sets SDK `NumTries=1`; offline tests verify no delivery retry.
4. Only `Unregistered` removes registrations. `InvalidArgument` keeps them, logs a last-five-character suffix and safe error metadata, and continues. **Owner changed 30/10/2026**: atomic insertion into `ConcurrentDictionary<string, byte>` selects Warning for the first rejection per full token, Debug thereafter; successful DB cleanup removes the entry. Error categories are `token` for `Unregistered` and `token_hoac_payload` for `InvalidArgument`. Invalid/oversized local payloads skip one push message and preserve all devices. The adapter does not duplicate the dispatcher's outage warning.
5. `KenhDayTinFcmDisabledTests`, `KhoiTaoFcmTests`, and `GuiThongBaoDieuKhienTests` verify first-send warning, permanent/transient classification, cooldown, single flight, lifetime, sanitized diagnostics, and bounded initialization. No real credential is read by tests.
6. Credentials remain owner-managed outside the repository. No production configuration/key access is authorized in this revision.

**Phân công khởi tạo Firebase phía app (chọn MỘT cách, ghi giống nhau ở plan/contracts/tasks)**:

| File | Việc (đúng thứ tự) |
|---|---|
| `mobile/lib/main.dart` | `WidgetsFlutterBinding.ensureInitialized()` → `Firebase.initializeApp()` trong `try/catch` → `FirebaseMessaging.onBackgroundMessage(_tinDayNen)` với `_tinDayNen` là **hàm top-level** trong `push_service.dart`; **tất cả trước `runApp`** |
| `mobile/lib/app.dart` | `PushService.khoiDong()`: tạo 2 kênh Android (`flutter_local_notifications`), `FirebaseMessaging.instance.requestPermission()`, lắng nghe `onMessage` / `onMessageOpenedApp` / `getInitialMessage`, điều hướng `pendingRoute` |
| `mobile/lib/core/services/push_service.dart` | logic token (`getToken`, `onTokenRefresh`, lưu `SharedPreferences`), hàm top-level `_tinDayNen` cho background, **không** khởi tạo `FirebaseApp` ở đây (`main.dart` đã làm) |

API thật là `FirebaseMessaging.onBackgroundMessage(...)` — **không** có `onBackgroundHandler` (tên sai, đã sửa ở contracts).

Phía mobile cũng "thiếu json vẫn chạy": `mobile/android/build.gradle.kts` thêm plugin vào `plugins { … apply false }`, `mobile/android/app/build.gradle.kts` chỉ `apply` khi `file("google-services.json").exists()`; `push_service.dart` bọc `Firebase.initializeApp()` trong `try/catch` → không init được thì `PushService.kenhDaSanSang=false`, app chạy bình thường, chỉ tắt phần đẩy. `mobile/android/app/google-services.json` thêm vào `mobile/.gitignore` (config theo dự án, không commit).

### (h) Vòng đời phiên đăng nhập phía app (owner chốt: chỉ bấm Đăng xuất mới gỡ máy)

`AuthService.logout()` hiện được gọi từ **ba** nơi (`mobile/lib/core/services/app_services.dart`):
1. người dùng bấm nút Đăng xuất (`app_shell.dart:142-155`);
2. `ApiClient.onUnauthorized` khi gặp 401 (dòng **11-15**);
3. `load()` khi JWT hết hạn (dòng **38**).

Nếu `logout()` luôn gọi `POST /thiet-bi/go` thì đường (3) — và cả (2) — sinh **vòng lặp**: `go` nhận 401 (JWT đã hết hạn) → `onUnauthorized` → `logout()` → `go` again. Cách sửa (chốt):

```dart
Future<void> logout({bool goMay = false}) async {
  if (goMay) {
    try { await _api.goThietBi(tokenDay); } catch (_) { /* không chặn đăng xuất */ }
  }
  // ... các bước cũ: setToken(null), xoá prefs, notifyListeners
}
```

- **Chỉ** nút Đăng xuất truyền `goMay: true`. Hai đường hết phiên (2) và (3) gọi `logout()` **không tham số** ⇒ hành vi y hệt hôm nay, không thêm request mới.
- Khi `goMay == true`: gọi `go` **lúc Bearer token còn hiệu lực** (trước `setToken(null)`), bọc `try/catch`, **không thử lại**, lỗi **không** chặn đăng xuất.
- Hệ quả chủ đích: máy còn đăng ký khi phiên hết hạn ⇒ mở app lại đăng nhập cùng tài khoản thì `load()`/`_persist` gửi `dang-ky` lại (idempotent, 1 dòng). Tin vẫn tới máy user đã thoát phiên trên app — đúng ý owner "chỉ bấm Đăng xuất mới gỡ".

Hai mục spec chưa có task ở bản 1, nay bổ sung (T023a) — đặt ở **chỗ ít xâm lấn nhất**:
- **US1 AC4** (quyền thông báo bị từ chối): snackbar sau khi bấm "Gửi thông báo thử" nếu `PushService.daDuocQuyen == false`, kèm **nút mở cài đặt của app** (`openAppSettings()` của `flutter_local_notifications`); và một dòng nhắc trong drawer menu cạnh nút đó.
- **Edge vivo**: một dòng hướng dẫn `Tắt tối ưu pin cho JUICE` — cùng hai vị trí trên (snackbar + menu drawer), không thêm màn hình mới, không popup tự động.

## Gói mới (tóm tắt để owner duyệt)

Owner đã duyệt danh sách này (29/10/2026). **Quy tắc khi cài**: gói nào không cài được (version không tồn tại, xung đột Dart/AGP) ⇒ **báo lại bản thực tế đã cài và lý do**, **không** tự đổi sang gói khác, không tự hạ/đổi lên version khác ngoài phạm vi 3 gói + 1 plugin đã liệt kê.

| Nền tảng | Gói | Phiên bản | Ở đâu | Lý do bắt buộc |
|---|---|---|---|---|
| .NET | `FirebaseAdmin` | `3.7.0` | `StockRadar.Infrastructure.csproj` | gửi FCM HTTP v1 + OAuth, lỗi có kiểu để xoá token hỏng (FR-009) |
| Flutter | `firebase_core` | `^5.1.0` | `mobile/pubspec.yaml` | nền tảng khởi tạo Firebase |
| Flutter | `firebase_messaging` | `^17.0.1` | `mobile/pubspec.yaml` | token + luồng nhận tin + điều hướng khi chạm |
| Flutter | `flutter_local_notifications` | `^22.3.3` | `mobile/pubspec.yaml` | 2 kênh Android theo FR-004/FR-001a + xin quyền |
| Gradle | `com.google.gms.google-services` | `4.5.0` | `mobile/android/*.gradle.kts` (apply có điều kiện) | đọc `google-services.json` |

**Không** thêm package chặn log (dùng `RemoveAllLoggers()` has-in-box). **Không** broker/queue mới (đúng skill `integration-event-design`).

## Hiến pháp kiểm (Constitution Check)

*Gate phải pass trước Phase 0 design; re-check sau Phase 1.*
Nguồn: `.specify/memory/constitution.md`

- [x] **I. Code as truth**: mọi chi tiết ở trên trích file:line đã đọc trực tiếp (`TelegramNotifier.cs:35`, `DependencyInjection.cs:130`, `HyperparameterTuningRunner.cs:37/97/109/130`, `V3Engine.cs:365/375/534-554/557`, `V3TruocPhienRunner.cs:73/111/126`, `V3TrongPhienRunner.cs:69/79/114/119/126`, `V3AtcRunner.cs:118/160/165/171`, `V3SauPhienRunner.cs:69/103`, `MarketJobsController.cs:200-251`, `DatabaseInitializer.cs:17`, `app_services.dart:11-15/38/74-83`, `app_shell.dart:142-155`, gradle, `AndroidManifest.xml`).
- [x] **II. Spec-first**: bảng mới + route mới + nền tảng push = trọng yếu → đã có `spec.md` (owner chốt 29/10/2026), plan này là bước `/speckit-plan`.
- [x] **III. Minimal surface**: không refactor ngoài phạm vi; `ITelegramNotifier`, `V3TelegramFormatter`, luật V3, giờ nhịp, **thứ tự/số lần `LuuAsync`** giữ nguyên; đổi 11 dòng gọi + 4 dòng gửi cuối nhịp + 5 constructor. Tách `AddTelegramNotifier()` là di chuyển 1 dòng đăng ký, không phải refactor.
- [x] **IV. Domain gates**: không đổi Buy Score / cổng Top / MA·pha / flatBox → không cần cập nhật `docs/domain/*` về cổng. **Có** cập nhật docs sau code: `docs/telegram-vip-alerts-flow.md`/`docs/build-and-deploy.md` (bước đặt credential) — giao cho `doc-update-after-code` ở Polish.
- [x] **V. Simplicity**: 1 dependency backend mới (có row Complexity Tracking bên dưới); abstraction `INguiTinDay` tồn tại vì runner không được biết Firebase và vì test FR-002/FR-013 cần fake. Đệm tin (`DatTinV3` + `GuiTatCaAsync`) bắt buộc theo §Constraints — không phải làm thêm.
- [x] **Stack**: Domain (enum) → Application (interface, DTO, options) → Infrastructure (entity, repository, FCM, dispatcher, migration) → Api (controller) → `mobile/lib` + `mobile/android`. Restart `backend/restart-api.ps1` **chỉ sau khi** owner duyệt migration.

## Cấu trúc dự án (file sẽ chạm)

```text
backend/
├── StockRadar.Api/
│   ├── Controllers/ThietBiController.cs                      # MỚI — 3 route, [Authorize], 1 dòng // SECURITY
│   └── appsettings.json                                      # Phase 2: Push:TimeoutGiay=2, TelegramNotify:TimeoutGiay=10, empty dev CredentialPath
├── StockRadar.Application/
│   ├── Abstractions/IGuiThongBao.cs                          # MỚI — lớp phát tin chung
│   ├── Abstractions/INguiTinDay.cs                           # MỚI — kênh đẩy + DayTinNhan/DayTinKetQua
│   ├── Abstractions/IThietBiNhanThongBaoRepository.cs         # MỚI
│   ├── DTOs/ThietBiDtos.cs                                   # MỚI (request/response 3 route)
│   ├── Options/PushNotifyOptions.cs                          # NEW — Phase 2
│   ├── Options/HyperparameterTuningOptions.cs                 # Phase 2: TimeoutGiay=10 in existing TelegramNotifyOptions
│   └── DependencyInjection.cs                                # Phase 2: Configure<PushNotifyOptions>; reuse TelegramNotifyOptions binding
├── StockRadar.Domain/
│   └── Enums/LoaiTinV3.cs                                    # MỚI (Mua/Ban/HuyMua/AtoN3)
├── StockRadar.Infrastructure/
│   ├── Notifications/GuiThongBao.cs                          # Independent ordered loops and TimeProvider deadlines
│   ├── Notifications/PhamViDbThongBao.cs                     # Operation-owned scopes; singleton slot, competing flush waits within its push budget
│   ├── Notifications/KenhDayTinFcm.cs                        # Payload validation and shared initialization lifecycle
│   ├── Notifications/LoiKenhDayTinFcm.cs                     # Sanitized initialization diagnostics
│   ├── Notifications/BoTaoHttpFcm.cs                         # Disable SDK delivery retries
│   ├── Notifications/V3PushFormatter.cs                      # MỚI — tiêu đề + thân chữ thường
│   ├── Persistence/Entities/ThietBiNhanThongBaoEntity.cs      # MỚI
│   ├── Persistence/Repositories/EfThietBiNhanThongBaoRepository.cs # MỚI
│   ├── Persistence/ApplicationDbContext.cs                    # + DbSet + OnModelCreating
│   ├── MarketData/V3{TruocPhien,TrongPhien,Atc,SauPhien}Runner.cs # 7 chỗ gom tin + 4 chỗ gửi sau LuuAsync + constructor
│   ├── MarketData/HyperparameterTuningRunner.cs               # 4 dòng gửi + constructor
│   ├── Migrations/<ts>_AddThietBiNhanThongBao.cs              # MỚI — ĐỌC LẠI trước khi áp
│   └── DependencyInjection.cs                                # + AddTelegramNotifier() (RemoveAllLoggers), + DI kênh đẩy/dispatcher/repo
└── StockRadar.Tests/
    └── Notifications/                                         # See tasks: custom clock, lifetime/order, payload/token, initialization tests

mobile/
├── pubspec.yaml                                    # chỉ + 3 gói (không đụng version/name/overrides)
├── lib/core/services/push_service.dart             # MỚI — init, quyền, token→server, 2 kênh, điều hướng
├── lib/core/api/api_client.dart                    # + dangKyThietBi/goThietBi/guiThuThongBao
├── lib/core/services/app_services.dart             # logout({goMay}) — chỉ nút Đăng xuất gỡ máy
├── lib/app.dart                                    # tạo kênh Android + lắng nghe tin + điều hướng
├── lib/main.dart                                   # ensureInitialized → initializeApp(try/catch) → onBackgroundMessage, trước runApp
├── lib/widgets/app_shell.dart                      # nút "Gửi thông báo thử" + snackbar nhắc quyền/mở cài đặt + dòng vivo + menu drawer
├── android/build.gradle.kts / app/build.gradle.kts # plugin google-services, apply có điều kiện
├── android/app/src/main/AndroidManifest.xml        # + uses-permission POST_NOTIFICATIONS
├── android/app/google-services.json                # OWNER tạo — đã thêm vào .gitignore
└── test/push_service_test.dart                     # MỚI — logic thuần (đường đích từ tin, phát hiện token đổi)
```

## Độ phức tạp phát sinh (Complexity Tracking)

| Vi phạm | Vì sao cần | Phương án đơn giản hơn đã bác bỏ vì |
|---|---|---|
| (V) Thêm dependency `FirebaseAdmin` 3.7.0 | FR-009 cần lỗi có kiểu (`MessagingErrorCode.Unregistered`) để xoá token; FR-011/FR-012 yêu cầu key không vào URL/log — OAuth + key handling phải là code đã kiểm chứng, không phải code ta viết | Tự viết FCM HTTP v1 + ký JWT RS256 + cache access token (~150 dòng crypto/bảo mật tự viết, rủi ro rò key cao hơn, vẫn phải tự parse lỗi để xoá token) |
| (V) Hai abstraction mới `IGuiThongBao` + `INguiTinDay` | FR-002 yêu cầu 2 kênh độc lập ở **11 chỗ gọi**; abstraction để runner không biết Firebase và để test ép kênh này lỗi mà kênh kia vẫn gửi | Gọi `ITelegramNotifier` + FCM trực tiếp từng runner: 22 khối try/catch lặp, dễ sót, không test được FR-002/FR-013 |
| (V) Đệm tin một nhịp (`DatTinV3` + `GuiTatCaAsync`) thay vì gửi ngay | Owner chốt: tin phải đi **sau khi** `LuuAsync` (cờ `DaBanTinThoat` chỉ xuống DB ở đó — `V3TrongPhienRunner.cs:126`), nếu không sẽ báo trùng khi sập giữa nhịp | Giữ gửi ngay như hôm nay và thêm một cờ "đã gửi" riêng: thành state thứ hai dễ lệch với DB, vi phạm "dùng cờ đã có sẵn" |

## Bảng FR / SC ⇔ cách kiểm

| FR/SC | Cách kiểm (tự động trừ khi ghi chú) | File test / bước |
|---|---|---|
| FR-001 | 7 chỗ V3 đều `DatTinV3`; `GuiTatCaAsync` được gọi **sau** `LuuAsync` ở cả 4 runner; body đẩy sinh từ đúng chuỗi Telegram | `GuiThongBaoV3Tests` + grep còn 0 `telegram.SendAsync` trong runner + đọc diff 4 runner |
| FR-001a | 4 loại HPO → `GuiHeThongAsync`, kênh `he-thong`, title `HPO · …` | `GuiThongBaoHeThongTests` |
| FR-002 | Independent channel failures; push hangs 10s → return ≤ 2.5s, Telegram once; Telegram takes 5s → succeeds; five messages with hung push → push once, Telegram five times, ≤ 3s, one warning; token errors do not open circuit | `GuiThongBaoDocLapTests` + `GuiThongBaoTimeoutTests` |
| FR-003 | Strip HTML/decode entities without clipping; all eight buy lines remain, including realtime-sell and backtest warnings | `V3PushFormatterTests` |
| FR-004 | `AndroidConfig.Priority=high`, `ChannelId=v3-signal`, `TimeToLive` — kiểm ở lớp xây message (không gọi mạng) | `FcmMessageBuilderTests` |
| FR-005 | `data["symbol"]` có mặt với tin V3, vắng với tin HPO | `FcmMessageBuilderTests` |
| FR-006 | Mobile: xin quyền sau lần đăng nhập đầu (thủ công trên máy owner); **quyền bị từ chối ⇒ snackbar nhắc + nút mở cài đặt app** (US1 AC4) | quickstart bước M-2 + T023a |
| FR-007 | `dang-ky` khi đăng nhập + khi token đổi (kèm `tokenCu`); `go` **chỉ** khi bấm Đăng xuất (`goMay: true`), không gọi khi hết phiên | `push_service_test.dart` (token đổi, `goMay`) + test thủ công M-3/M-5 |
| FR-007a | Gửi cho **mọi** token của **mọi** user | `GuiThongBaoCacMayTests` |
| FR-008 | Đăng ký lại cùng token → 1 dòng, `UserId` đổi theo user mới; **`tokenCu` thuộc user khác → không bị xoá** | `ThietBiDangKyTests` (2 ca) |
| FR-009 | Owner changed 29/10/2026: only Unregistered removes DB/cache entries; InvalidArgument and invalid payloads preserve registrations and healthy delivery | `ThietBiXoaTokenTests`, `KenhDayTinFcmTokenTests`, `KiemTraTinFcmTests` |
| FR-010 | 3 route: 401 khi không token; 200/204/400/503 đúng contract. **Không** thêm `Microsoft.AspNetCore.Mvc.Testing` (repo chưa có, kiểm `StockRadar.Tests.csproj:11-15`) ⇒ test service + test phản chiếu `[Authorize]`, mapping HTTP check bằng smoke script | `ThietBiDangKyRouteTests` + bước K/T014 |
| FR-011 | Không file/key nào trong diff; `CredentialPath` mặc định rỗng; key file nằm ngoài repo | review diff + `git status` |
| FR-012 | Sau `SendAsync`, **không** dòng log nào chứa `api.telegram.org/bot` hoặc token — test gọi **hàm đăng ký thật** `AddTelegramNotifier()`, không tự dựng `RemoveAllLoggers()` trong test | `TelegramNotifierLogTests` (FAIL trước khi sửa dòng 130 ⇒ PASS sau khi sửa, có output) (SC-003) |
| FR-013 | Không cấu hình → `DangHoatDong=false`, **gửi nhiều lần vẫn đúng 1** `LogWarning` (ra ở lần gửi đầu, không phải lúc khởi động), Telegram + runner chạy bình thường | `KenhDayTinFcmDisabledTests` |
| SC-001 | 5 phiên đối chiếu từng tin đẩy ↔ Telegram | thủ công trên máy owner (M-6) |
| SC-002 | So timestamp tin Telegram và timestamp notify ≥95% <60s *(ngưỡng 60s: đề xuất của AI, owner dùng)* | thủ công (M-6) |
| SC-003 | `journalctl -u stockradar` không còn `api.telegram.org/bot` | owner chạy trên server sau ship |
| SC-004 | Compare full-tick duration before/after; target increase ≤ 1s (AI proposal adopted by owner). Independent loops: Telegram 10s, push 2s with one timeout per batch. These bounds do not prove SC-004; measure during trading hours after authorized runner integration | Before/after runtime logs; controlled-time logic and three real-clock acceptance tests |
| SC-005 | Bỏ `Push` config → 0 lỗi, Telegram như cũ | `KenhDayTinFcmDisabledTests` + chạy local |

## Rủi ro đã biết (báo owner ngay từ plan)

1. **AGP 9.0.1 + Kotlin 2.3.20 rất mới** (`settings.gradle.kts:22-23`). `google-services` 4.5.0 / Firebase Android BoM chưa được kiểm chứng tương thích với AGP 9 → bước build APK đầu tiên có thể phải chỉnh version. **Chỉ phát hiện khi build**; không phải quyết định nghiệp vụ. Nếu không cài được: **báo bản thực tế đã cài**, không tự đổi gói.
2. **vivo tiết kiệm pin** giết tiến trình nền → tin tới trễ (edge case spec). Giảm bằng `priority=high` + `default_priority` + TTL 1 giờ; từ chặng này app **có** một dòng hướng dẫn "Tắt tối ưu pin cho JUICE" (T023a).
3. App bản cũ (`3.0.1+23` trở về trước) không đăng ký thiết bị → chỉ nhận Telegram. Đúng assumption của spec.
4. `DatabaseInitializer.MigrateAsync()` tự áp migration khi API khởi động → **không** restart API local trước khi owner duyệt file migration. **Phase 1 không tạo migration ⇒ owner commit + ship Phase 1 trước khi có migration trong working tree** (owner chốt).
5. Owner nên **đổi token bot Telegram** qua BotFather sau FR-012 (token cũ đã nằm trong log production — assumption 4 của spec).
6. Gửi-sau-lưu đổi một hệ quả: nếu runner ném exception **trước** `LuuAsync`, toàn bộ tin đã gom của nhịp đó **mất** (hôm nay tin đã đi trước khi lưu). Đã ghi ở §Constraints; test T013 phải nêu rõ hành vi này.

## Chiến lược triển khai (tóm tắt)

Chủ sản phẩm đã duyệt vào chặng 2 (29/10/2026). Thứ tự có điểm dừng:
1. **Step 0 approved**: apply the four Revision 3 documentation adjustments, including exactly two edits in `spec.md`; proceed directly to Phase 1. Summarize documentation changes in the Phase 1 report, without another document-review stop.
2. **Phase 1** (T001–T003): chặn log rò token + test kiểm code thật (FAIL trước/PASS sau) → **DỪNG**, kèm bản nháp commit `[FIX][telegram]…`. **T004 chưa làm** cho tới khi owner báo đã ship Phase 1 (owner tự commit + ship, vì migration chưa được tạo).
3. **Còn lại** (T004–T037): dispatcher + kênh đẩy + bảng + endpoint + nối runner + mobile, giữ STOP-2 (duyệt file migration — không chạy/restart API trước đó) và STOP-3 (owner cấu hình Firebase) → test thật DB local + máy `10AE4G1ZE2000K8` → dọn dẹp.

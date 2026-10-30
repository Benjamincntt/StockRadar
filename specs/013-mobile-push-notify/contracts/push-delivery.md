# Contract: Luồng phát tin (dispatcher) ⇔ kênh Telegram ⇔ kênh đẩy FCM ⇔ app Android

**Branch**: `013-mobile-push-notify` | **Date**: 2026-10-29 | **Status**: **Revision 3, owner changed 29/10/2026** — Phase 2 review corrections approved; STOP-2 and STOP-3 remain. T012 is not authorized by these corrections.

Đây là hợp đồng **nội bộ + client-server cho phần đẩy**, bổ sung cho [thiet-bi-api.md](./thiet-bi-api.md).
Mục tiêu: runner chỉ gọi **một** chỗ; hai kênh độc lập; app cũ không hỏng; không có đường nào để
tin đẩy và tin Telegram lệch nội dung.

## 1. Luồng

```text
V3*Runner / HyperparameterTuningRunner   (Infrastructure, scoped)
        │  CHUỖI ĐÚNG: gom tin ──▶ engine.LuuAsync() ──▶ gửi
        │  IGuiThongBao.DatTinV3 | DatTinHeThong   (chỉ gom, chưa gọi mạng)
        │  IGuiThongBao.GuiTatCaAsync            (gọi SAU khi nhịp đã lưu DB — chủ đích)
        ▼
GuiThongBao (mới, Infrastructure/Notifications)
   ├── kênh 1: ITelegramNotifier.SendAsync(html, parseMode)   [try/catch → LogWarning]
   │            └─ HttpClient đăng ký qua AddTelegramNotifier() + RemoveAllLoggers() (FR-012)
   └── kênh 2: INguiTinDay.GuiAsync(tokens, DayTinNhan)        [try/catch → LogWarning]
                ├─ đọc token: IThietBiNhanThongBaoRepository.LayTokenCacMayAsync()
                ├─ KenhDayTinFcm (FirebaseAdmin 3.7.0, FCM HTTP v1, init lazy)
                └─ DayTinKetQua.TokenHong → repository.XoaCacTokenAsync()   (FR-009)

Owner changed 29/10/2026: Task.WhenAll awaits two independent ordered loops, one per channel.
Telegram message 2 never waits for push message 1; each loop preserves its own message order.
Per-message TimeProvider deadlines + linked CTS: TelegramNotify:TimeoutGiay=10, Push:TimeoutGiay=2.
First push timeout/connection failure: skip push for the rest of this batch, warn once; Telegram continues.
```

Ràng buộc (skill `integration-event-design`):
- **Thứ tự lưu/gửi (chống gửi trùng)** — sự thật trong code, đã kiểm: `V3Engine.KhopBanAsync` (`V3Engine.cs:534-554`) chỉ gán `DaBanTinThoat` **trong bộ nhớ** (`:544`); giá trị xuống DB tại `engine.LuuAsync` = `SaveChangesAsync` (`V3Engine.cs:557`) ở **cuối nhịp** (`V3TrongPhienRunner.cs:126`, `V3TruocPhienRunner.cs:126`, `V3AtcRunner.cs:171`, `V3SauPhienRunner.cs:103`). Bản hợp đồng trước ghi "cờ ghi trước khi gửi" là **sai**. Hợp đồng mới:
  1. runner **gom** tin (`DatTinV3`) thay vì gửi ngay, tại đúng vị trí cũ;
  2. runner gọi `engine.LuuAsync` (không đổi thứ tự/số lần lưu);
  3. sau khi lưu xong, `await guiTin.GuiTatCaAsync(ct)` gửi **tất cả** tin đã gom theo đúng thứ tự gom.
  Áp cho **cả 4 runner V3**; các chỗ quanh `HuyChoMuaAsync`, `GhiTinHieuAsync`, `TaoChoMua`, `KhopBanAsync` đều **chỉ gom**.
  Lý do: API sập giữa nhịp mà tin đã đi ⇒ cờ chống trùng chưa xuống DB ⇒ phút sau đúng mã đó bị bán lại và báo **lần hai**.
  HPO (`HyperparameterTuningRunner`) dùng `GuiHeThongAsync` (gom + gửi ngay) vì không có trạng thái DB nào phải chờ lưu.
  **LIMIT**: khoảng giữa `SaveChanges` và bước gửi (hoặc runner ném trước `LuuAsync`) ⇒ tin nhịp đó **mất**; chủ đích chọn mất tin thay vì trùng tin.
- **Timeouts (Phase 2 only; owner changed 29/10/2026)**: each channel has its own ordered loop, `try/catch`, and linked CTS backed by `new CancellationTokenSource(TimeSpan, TimeProvider)`: **`TelegramNotify:TimeoutGiay=10`**, **`Push:TimeoutGiay=2`**, used with `Task.WhenAll`. Both values are **AI proposals adopted by the owner**. Add `TimeoutGiay=10` to the existing `TelegramNotifyOptions` and reuse its binding, per the owner's Phase 2 update. Do not rename or move `TelegramNotify` credentials. Telegram previously had the default 100s `HttpClient.Timeout`; 2s would cancel slow fallback messages. Delivery now follows `LuuAsync`, so slow sends do not delay the current tick's sell persistence. **Phase 1 only fixes logging.**
- **Batch push circuit**: within one `GuiTatCaAsync`, the first push **timeout or connection failure** disables push for all remaining messages and emits **exactly one `LogWarning`** for that failure. Telegram still attempts every message; a Telegram error does not stop push. The flag is local to the batch and resets on the next call. No retry or duplicate warning in the FCM adapter. This prevents 30 × 2s ≈ 60s of push waiting from blocking the next minute's `[DisallowConcurrentExecution]` job.
- **Token errors — owner changed 29/10/2026, logging changed 30/10/2026**: only `MessagingErrorCode.Unregistered` populates `TokenHong`, removing that token from DB and the current batch cache. `InvalidArgument` retains the registration and logs only its last five characters plus allowlisted error metadata. Both continue healthy devices and later messages without opening the batch circuit. Local payload validation does not prove FCM acceptance. **Owner changed 30/10/2026**: the singleton adapter remembers warned tokens in a thread-safe RAM-only set: first rejection per token is Warning, later rejections are Debug across messages/batches; restart resets the set. Successful `Unregistered` DB cleanup removes the token from this set, including cleanup completing after the caller timed out; failed or canceled cleanup does not reset it. A re-registered token can then warn again.
- **Database lifetime — owner changed 29/10/2026, slot policy changed 30/10/2026**: notification reads and cleanup execute inside operation-owned tasks with fresh `IServiceScopeFactory.CreateAsyncScope()` scopes. Their context is never the runner's context; disposal occurs only after the actual database task finishes. A singleton `PhamViDbThongBao` allows at most one unfinished operation, including disposal. A competing batch waits for the slot within its own push budget; when that budget expires it skips push for that batch with `LoaiLoi=qua_han` and exactly one warning, opens no extra scope, and no longer reports the contention as `mang`. Two operations never run concurrently. Cancellation requests termination, while late faults remain observed. This does not prove SQL Server cancellation latency.
- **Payload rejection — owner changed 29/10/2026**: validate nonblank title/body, supported channel/type/symbol combinations, and UTF-8 serialized payload size before device lookup and again in the public adapter. Invalid payloads skip only that push message, leave all registrations unchanged, and do not affect Telegram or subsequent messages. No truncation.
- **Cancellation**: catch `OperationCanceledException`/`TaskCanceledException` inside channel tasks, never throw into the runner. A Telegram timeout affects one message; push timeout opens its batch circuit. Original tick cancellation is distinct: log once and stop both channels for remaining messages, without treating shutdown as a push outage.
- **Không broker/queue** (repo không có; thêm là đổi kiến trúc).
- **Observability — owner changed 29/10/2026, levels and classification changed 30/10/2026**: allowlisted diagnostic fields only: `GiaiDoan` (`khoi_tao`, `doc_token`, `gui`, `don_token`), `LoaiLoi` (`qua_han`, `mang`, `xac_thuc`, `payload`, `token`, `token_hoac_payload`), `KieuLoi`, `MessagingErrorCode`, `ThoiGianMs`, stock `Symbol`, and recipient/result counts. Token-rejection events may additionally contain `TokenDuoi` (last five characters; short tokens redacted). Never log exception objects/messages, response bodies, credential paths, full tokens, titles, or message bodies. **Owner changed 30/10/2026**: `Unregistered` uses `LoaiLoi=token`; `InvalidArgument` uses `LoaiLoi=token_hoac_payload` because FCM does not distinguish a rejected token from a rejected payload here. Each token's first rejection logs Warning and subsequent rejections log Debug until successful DB cleanup resets its history. Skipped invalid/oversized payloads and token-cleanup failures remain Warning; at most one push-failure warning per batch and later repeated failure details in that batch use Debug. The internal record property is `Kenh`, not the former misspelling.

## 2. Hợp đồng kênh đẩy (server → FCM)

Cấu trúc message gửi đi (`KenhDayTinFcm` dựng, kiểm chứng bằng test thuần, không gọi mạng):

| Thành phần | Tin V3 (mua/bán/huỷ mua/ATO N3) | Tin hệ thống (HPO) | Tin thử |
|---|---|---|---|
| `AndroidConfig.Priority` | `high` (FR-004) | `normal` (FR-001a) | `high` |
| `AndroidConfig.Notification.ChannelId` | `v3-signal` | `he-thong` | `v3-signal` |
| `AndroidConfig.Notification.Title` | `MUA · HPG · N1` / `BÁN · HPG` / `HUỶ MUA · HPG` / `ATO · HPG · N3` | `HPO · xong` / `HPO · lỗi` / `HPO · quá giờ` / `HPO · bỏ qua` | `THỬ · JUICE` |
| `AndroidConfig.Notification.Body` | thân chữ thường, sinh bằng `V3PushFormatter.NoiDung(telegramHtml)` (strip HTML, decode entities, normalize lines, preserve the complete body; owner changed 29/10/2026) | đúng chuỗi Telegram HPO hiện tại | `Thông báo thử từ app — nếu bạn thấy dòng này là kênh đẩy hoạt động.` |
| `AndroidConfig.TimeToLive` | `01:00:00` *(ngưỡng 1 giờ: đề xuất của AI, owner dùng — máy vivo ngủ sâu quá 5 phút thì Firebase bỏ hẳn tin, trái SC-001)* | `12:00:00` | `01:00:00` |
| `Data` | `symbol` (vd `HPG`), `loaiTin` (`mua`/`ban`/`huy_mua`/`ato_n3`), `nguon` (`v3`) | `nguon` (`he_thong`) | `nguon` (`thu`) |
| `Topic` / `Condition` | **không dùng** — gửi per-token để xoá được token hỏng | không dùng | không dùng |

- `Data` là **hợp đồng với app**: app đổi hành vi điều hướng theo `symbol`; thêm khoá mới là additive.
- Một message = một token (vòng lặp tuần tự, số máy hiện ≤ 1). `MulticastSendAsync` không cần thiết ở quy mô này; nếu dùng sau này thì API đó tự cắt 500 token/lần.
- TTL chốt theo owner: **tin V3 và tin thử sống 1 giờ trên Firebase**, **tin HPO giữ 12 giờ**.
- **Size source**: [Firebase FCM error codes](https://firebase.google.com/docs/cloud-messaging/error-codes) documents **4096 bytes** for ordinary messages, **2048 bytes** for topics, including keys and values. This system sends per-token, not topics. The conservative local bound measures UTF-8 serialized Android/data fields and the message envelope (priority, TTL, channel, title, full body, routing metadata, JSON punctuation); it excludes the target token and HTTP/authentication envelope. JSON escaping may overcount; no claim of exact server-side accounting. Over-limit messages are skipped, never clipped.
- V3 symbols: uppercase ASCII letters/digits, start with a letter, 1–16 characters. V3 requires a defined `LoaiTinV3` and symbol; HPO/test have neither. `LoaiTinV3` maps only to the four `loaiTin` values above. Internal channel property: `DayTinNhan.Kenh`.

## 3. Hợp đồng app Android (channel + điều hướng + vòng đời token)

### Phân công khởi tạo (chọn MỘT cách — ghi giống nhau ở plan §(g), tasks T021/T022)

| File | Việc, đúng thứ tự |
|---|---|
| `mobile/lib/main.dart` | `WidgetsFlutterBinding.ensureInitialized()` → `Firebase.initializeApp()` trong `try/catch` → `FirebaseMessaging.onBackgroundMessage(_tinDayNen)` với `_tinDayNen` là **hàm top-level** trong `push_service.dart` → tất cả **trước `runApp`** |
| `mobile/lib/app.dart` | `PushService.khoiDong()`: tạo 2 kênh Android, `requestPermission()`, lắng nghe `onMessage`/`onMessageOpenedApp`/`getInitialMessage`, điều hướng + `pendingRoute` |
| `mobile/lib/core/services/push_service.dart` | logic token, hàm top-level `_tinDayNen`, `duongDanTuTin` pure; **không** gọi `Firebase.initializeApp()` ở đây |

API đúng là `FirebaseMessaging.onBackgroundMessage(...)` — **không tồn tại** `onBackgroundHandler` (tên sai ở bản hợp đồng trước, đã sửa).

### Kênh Android (tạo ở `app.dart` mỗi lần app chạy, qua `flutter_local_notifications`)

| `channelId` | Tên hiển thị | Importance | Âm | Dùng cho |
|---|---|---|---|---|
| `v3-signal` | **Tín hiệu V3** | `max` (có âm, pop-on screen) | âm hệ thống `Notification` | FR-004 — mọi tin V3 + tin thử |
| `he-thong` | **Hệ thống** | `default` (có âm thường) | âm hệ thống mặc định | FR-001a — tin HPO |

Hai kênh tạo **mỗi lần app chạy** (idempotent) và **trước** khi xin quyền; tồn tại độc lập với việc
Firebase có init được hay không (thiếu `google-services.json` ⇒ vẫn build/chạy, phần đẩy tắt).

### Điều hướng khi chạm (FR-005, US2)

Hàm thuần `duongDanTuTin(Map<String,String> data)` trong `push_service.dart` (test được, không cần Firebase):

| `data` | Đường đích |
|---|---|
| `symbol=HPG` (mọi `loaiTin`) | `/stocks/HPG` — `GoRouter.push` (app đang chạy nền: `onMessageOpenedApp`; app bị giết: `getInitialMessage`; app đang foreground: hiển thị notify địa phương rồi vẫn mở khi chạm) |
| không có `symbol` (`he_thong`, `thu`) | không điều hướng — chỉ mở app, ở lại màn hiện tại/`initialLocation` |
| token hết hạn phiên khi mở từ tin | về `/login` qua `onSessionExpired` đã có (`app.dart:42-52`), **sau đó** tới `/stocks/:symbol` khi đăng nhập xong (đề xuất: lưu `pendingRoute` một lần, không tự refresh) |

### Vòng đời token (FR-006, FR-007) — owner chốt: **chỉ bấm Đăng xuất mới gỡ máy**

| Thời điểm | Hành động |
|---|---|
| `main()` | như bảng phân công trên: `ensureInitialized` → `initializeApp`(try/catch) → `onBackgroundMessage(_tinDayNen)` → `runApp` |
| Đăng nhập/đăng ký thành công (`AuthService._persist`) | xin quyền `POST_NOTIFICATIONS` (Android 13+) **một lần** kèm giải thích ngắn → `getToken()` → `POST /thiet-bi/dang-ky {token, nenTang}` (lưu token vào `SharedPreferences`). Từ chối quyền ⇒ app không sập, có chỗ nhắc + nút mở cài đặt (US1 AC4, xem dưới) |
| `onTokenRefresh` | `POST /thiet-bi/dang-ky {token: mới, tokenCu: cũ lấy từ SharedPreferences}` ⇒ server xoá token cũ **nếu thuộc user hiện tại** rồi upsert token mới; lưu token mới vào prefs. So với token đã lưu để không gọi vô ích |
| Mở app trở lại đã đăng nhập (`AuthService.load`) | nếu token khác bản đã lưu → `dang-ky` lại (kèm `tokenCu`); JWT hết hạn → `logout()` **không** `goMay` (hành vi cũ, không gọi `go`) |
| **Bấm nút Đăng xuất** (`app_shell.dart:142-155`) | `logout(goMay: true)` → `POST /thiet-bi/go {token}` **lúc Bearer còn hiệu lực** (trước `setToken(null)`), bọc `try/catch`, **không thử lại**, lỗi **không** chặn đăng xuất → rồi mới xoá prefs |
| Phiên hết hạn (401 → `onUnauthorized` `app_services.dart:11-15`; hoặc JWT hết hạn `:38`) | gọi `logout()` **không tham số** ⇒ **không** gọi `go`. Lý do: `go` với JWT đã hết hạn ⇒ 401 ⇒ `onUnauthorized` ⇒ `logout()` ⇒ **vòng lặp**. Máy còn đăng ký là chủ đích |

**US1 AC4 — quyền bị từ chối** (ít xâm lấn nhất, không màn mới, không popup tự động):
- snackbar sau khi bấm "Gửi thông báo thử" nếu `daDuocQuyen == false`, nội dung `Thông báo đang bị chặn — bật trong Cài đặt app.` + **nút `Mở cài đặt`** (`openAppSettings()` của `flutter_local_notifications`);
- một dòng nhắc tương tự trong **menu drawer** cạnh nút Đăng xuất.

**Edge vivo** (cùng hai vị trí trên, không thêm chỗ nào khác): một dòng chữ ngắn `Tắt tối ưu pin cho JUICE` — chỉ dẫn, không phải code.

### Quyền & manifest

- `android/app/src/main/AndroidManifest.xml`: thêm `<uses-permission android:name="android.permission.POST_NOTIFICATIONS"/>` (Android 13+; Android ≤12 không cần).
- `applicationId` `com.juice.stockradar` phải khớp `package_name` trong `google-services.json` (owner tạo dự án Firebase với đúng package này).
- vivo/Android 16: một dòng hướng dẫn "Tắt tối ưu pin cho JUICE" ở snackbar nút gửi thử + menu drawer (xem trên).

## 4. Hành vi khi thiếu cấu hình (FR-013, SC-005)

| Thành phần | Không có cấu hình / thiếu file |
|---|---|
| `KenhDayTinFcm.DangHoatDong` | Fixed disabled/missing-file/malformed-configuration errors remain disabled until restart; status reads never initialize or log. Exactly one unavailable warning on the first attempted send; later attempts log Debug. |
| `GuiThongBao` | Sends unchanged Telegram content; invokes the disabled adapter without a device lookup so the adapter owns the first-send warning and later Debug events |
| Runner V3/HPO | không đổi hành vi, không thêm exception |
| `POST /thiet-bi/gui-thu` | `503` + detail rõ |
| `POST /thiet-bi/dang-ky` | `200 {daDangKy:true, maHoaThongBao:false}` (vẫn lưu máy, để bật sau không cần đăng nhập lại) |
| App không có `google-services.json` | build được (gradle apply có điều kiện), app chạy, PushService báo "chưa bật", nút gửi thử hiện thông báo lỗi 503 |

**Initialization — owner changed 29/10/2026**: one shared lifecycle-managed task runs synchronous credential/app initialization off the caller stack. Independent caller waits remain bounded; cancellation never replays an old message or starts duplicate initialization. Network/timeout failures may retry only on a later send after `Push:KhoiTaoLaiSauGiay` (default **300**, AI proposal adopted by owner); fixed errors do not retry. Disposal waits logically for owned initialization/sends to finish before releasing the Firebase app. No startup initialization, real credential access during tests, or delivery retry. The SDK HTTP factory sets `NumTries=1`, verified with an offline failure response.

## 5. Test map (theo chỉ đạo "Kiểm")

| Ca kiểm | Loại | Ghi chú |
|---|---|---|
| Kênh đẩy ném ⇒ Telegram vẫn gửi (`GuiThongBaoDocLapTests`) | unit, fake `INguiTinDay` throw | đếm 1 lần gọi telegram |
| Telegram ném ⇒ kênh đẩy vẫn gửi | unit, fake `ITelegramNotifier` throw | dispatcher không ném ra ngoài |
| Push hangs 10s ⇒ `GuiTatCaAsync` returns ≤ 2.5s, Telegram called once | unit, fake `Task.Delay(10s, ct)`, immediate Telegram | `Push:TimeoutGiay=2`, `TelegramNotify:TimeoutGiay=10`; real-clock category `ThoiGianThat` |
| Telegram takes 5s ⇒ send completes successfully, not canceled | unit, fake `Task.Delay(5s, ct)` | Telegram uses its own 10s limit |
| Five messages, hung push ⇒ push once, Telegram five times, total ≤ 3s, exactly one warning | unit, immediate Telegram | Next batch attempts push again |
| Push connection failure ⇒ skip push for remaining messages, Telegram receives all | unit | Exactly one warning per failed batch |
| Tick cancellation ⇒ no exception into runner, remaining messages skipped | unit, controlled time/completion gates | Distinguish original cancellation from channel timeout; logic tests do not sleep. Three wall-clock acceptance tests remain in `Category=ThoiGianThat`. |
| Tin được **gom** rồi mới gửi: `GuiTatCaAsync` chưa gọi ⇒ 0 request; sau `LuuAsync` ⇒ đủ theo thứ tự | unit dispatcher | chống gửi trùng — nếu không dựng được runner thật (cần SQL Server) thì test ở mức dispatcher và **báo "chưa kiểm ở mức runner + lý do"** |
| Thiếu cấu hình Firebase ⇒ `DangHoatDong=false`, không ném, **gửi nhiều lần vẫn đúng 1** logWarning | unit (`KenhDayTinFcmDisabledTests`) | `CredentialPath` trỏ file không tồn tại |
| Token classification/logging — owner changed 30/10/2026 | typed SDK errors offline + EF InMemory + concurrent sends | Only `Unregistered` removes DB/cache entries; `InvalidArgument` retains entries; healthy devices and later messages continue. First rejection per token warns, repeats use Debug, error categories are distinct, and successful cleanup permits a new warning after re-registration |
| Invalid/oversized payload | formatter/adapter/dispatcher + EF InMemory | All registrations remain; only that push message is skipped; Telegram and the next message continue |
| Database lifetime/capacity | DI scopes + controlled stalled operations | Runner context differs; cancellation does not dispose pending work; runner still queries; no second outstanding notification DB operation |
| Independent ordered loops | completion gates | Telegram message 2 does not wait for push message 1, and vice versa |
| Initialization/cooldown/logging | injected initializer + controlled clocks | Permanent versus transient failures, single flight, bounded wait, no old-message replay or sensitive log fields |
| Đăng ký lại cùng token ⇒ 1 dòng | unit EF InMemory | FR-008 |
| **`tokenCu` thuộc user khác ⇒ không bị xoá; thuộc đúng user ⇒ bị xoá** | unit EF InMemory | FR-008 + edge token đổi |
| Gửi cho mọi máy của mọi user | unit | 2 user × 1 máy ⇒ `GuiAsync` nhận 2 token |
| Không dòng log nào chứa `api.telegram.org/bot` sau khi gửi | unit với `ILogger` capture + **hàm đăng ký thật `AddTelegramNotifier()`** + handler giả | FR-012/SC-003; **FAIL trước khi sửa `DependencyInjection.cs:130`, PASS sau khi sửa** |
| `V3PushFormatter` không còn HTML, đúng format tiêu đề | unit thuần | FR-003 |
| `duongDanTuTin` map `/stocks/HPG` / null | `flutter test` | FR-005 |
| **`logout()` không tham số không gọi `go`; `logout(goMay: true)` mới gọi** | `flutter test` (fake ApiClient) | chống vòng lặp 401 |
| Tin thật trên máy `10AE4G1ZE2000K8`, app tắt | thủ công M-6 | SC-001/SC-002 |

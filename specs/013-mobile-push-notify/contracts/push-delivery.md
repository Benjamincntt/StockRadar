# Contract: Luồng phát tin (dispatcher) ⇔ kênh Telegram ⇔ kênh đẩy FCM ⇔ app Android

**Branch**: `013-mobile-push-notify` | **Date**: 2026-10-29 | **Status**: **Revision 3** — Step 0 approved with four adjustments; STOP-Phase-1, STOP-2, and STOP-3 remain.

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

Each message starts both channel tasks via Task.WhenAll.
Separate linked CTS + CancelAfter: Telegram:TimeoutGiay=10, Push:TimeoutGiay=2.
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
- **Timeouts (Phase 2 only)**: each channel has its own `try/catch`, linked CTS, and `CancelAfter`: **`Telegram:TimeoutGiay=10`**, **`Push:TimeoutGiay=2`**, used with `Task.WhenAll`. Both values are **AI proposals adopted by the owner**. Add `TelegramTimeoutOptions` (`SectionName="Telegram"`, default 10s) beside `TelegramNotifyOptions` in `Application/Options/HyperparameterTuningOptions.cs`, and bind it beside the existing options registration. Do not rename or move `TelegramNotify` credentials. Telegram previously had the default 100s `HttpClient.Timeout`; 2s would cancel slow fallback messages. Delivery now follows `LuuAsync`, so slow sends do not delay the current tick's sell persistence. **Phase 1 only fixes logging.**
- **Batch push circuit**: within one `GuiTatCaAsync`, the first push **timeout or connection failure** disables push for all remaining messages and emits **exactly one `LogWarning`** for that failure. Telegram still attempts every message; a Telegram error does not stop push. The flag is local to the batch and resets on the next call. No retry or duplicate warning in the FCM adapter. This prevents 30 × 2s ≈ 60s of push waiting from blocking the next minute's `[DisallowConcurrentExecution]` job.
- **Token errors**: `Unregistered`/`InvalidArgument` only remove the affected token from DB and the current batch's cached tokens; continue sending to healthy tokens and subsequent messages. These errors **do not open the batch circuit**. Connection failures must reach the dispatcher.
- **Cancellation**: catch `OperationCanceledException`/`TaskCanceledException` inside channel tasks, never throw into the runner. A Telegram timeout affects one message; push timeout opens its batch circuit. Original tick cancellation is distinct: log once and stop both channels for remaining messages, without treating shutdown as a push outage.
- **Không broker/queue** (repo không có; thêm là đổi kiến trúc).
- **Observability**: log mỗi tin gồm `LoaiTin`, `Symbol`, `Kanh`, `SoMayNhan`, `TokenHongSoLuong`, và token **chỉ 5 ký tự cuối**. Không log token đầy đủ, không log URL Telegram, không log nội dung key.

## 2. Hợp đồng kênh đẩy (server → FCM)

Cấu trúc message gửi đi (`KenhDayTinFcm` dựng, kiểm chứng bằng test thuần, không gọi mạng):

| Thành phần | Tin V3 (mua/bán/huỷ mua/ATO N3) | Tin hệ thống (HPO) | Tin thử |
|---|---|---|---|
| `AndroidConfig.Priority` | `high` (FR-004) | `normal` (FR-001a) | `high` |
| `AndroidConfig.Notification.ChannelId` | `v3-signal` | `he-thong` | `v3-signal` |
| `AndroidConfig.Notification.Title` | `MUA · HPG · N1` / `BÁN · HPG` / `HUỶ MUA · HPG` / `ATO · HPG · N3` | `HPO · xong` / `HPO · lỗi` / `HPO · quá giờ` / `HPO · bỏ qua` | `THỬ · JUICE` |
| `AndroidConfig.Notification.Body` | thân chữ thường, sinh bằng `V3PushFormatter.NoiDung(telegramHtml)` (bỏ thẻ `b`/`code`, giải `&amp;/&lt;/&gt;`, cắt ~240 ký tự) | đúng chuỗi Telegram HPO hiện tại | `Thông báo thử từ app — nếu bạn thấy dòng này là kênh đẩy hoạt động.` |
| `AndroidConfig.Ttl` | `01:00:00` *(ngưỡng 1 giờ: đề xuất của AI, owner dùng — máy vivo ngủ sâu quá 5 phút thì Firebase bỏ hẳn tin, trái SC-001)* | `12:00:00` | `01:00:00` |
| `Data` | `symbol` (vd `HPG`), `loaiTin` (`mua`/`ban`/`huy_mua`/`ato_n3`), `nguon` (`v3`) | `nguon` (`he_thong`) | `nguon` (`thu`) |
| `Topic` / `Condition` | **không dùng** — gửi per-token để xoá được token hỏng | không dùng | không dùng |

- `Data` là **hợp đồng với app**: app đổi hành vi điều hướng theo `symbol`; thêm khoá mới là additive.
- Một message = một token (vòng lặp tuần tự, số máy hiện ≤ 1). `MulticastSendAsync` không cần thiết ở quy mô này; nếu dùng sau này thì API đó tự cắt 500 token/lần.
- TTL chốt theo owner: **tin V3 và tin thử sống 1 giờ trên Firebase**, **tin HPO giữ 12 giờ**.

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
- `applicationId` `com.baobiantea.juice` phải khớp `package_name` trong `google-services.json` (owner tạo dự án Firebase với đúng package này).
- vivo/Android 16: một dòng hướng dẫn "Tắt tối ưu pin cho JUICE" ở snackbar nút gửi thử + menu drawer (xem trên).

## 4. Hành vi khi thiếu cấu hình (FR-013, SC-005)

| Thành phần | Không có cấu hình / thiếu file |
|---|---|
| `KenhDayTinFcm.DangHoatDong` | `false`; kênh init **lazy lần đầu cần dùng** ⇒ **một** `LogWarning` ở **lần gửi đầu tiên** (không phải lúc host khởi động), các lần sau chỉ `LogDebug`; gửi bao nhiêu lần vẫn **đúng một** dòng Warning |
| `GuiThongBao` | vẫn gửi Telegram y hệt hôm nay; log `SoMayNhan=0 (kenh day tat)` một dòng/debug |
| Runner V3/HPO | không đổi hành vi, không thêm exception |
| `POST /thiet-bi/gui-thu` | `503` + detail rõ |
| `POST /thiet-bi/dang-ky` | `200 {daDangKy:true, maHoaThongBao:false}` (vẫn lưu máy, để bật sau không cần đăng nhập lại) |
| App không có `google-services.json` | build được (gradle apply có điều kiện), app chạy, PushService báo "chưa bật", nút gửi thử hiện thông báo lỗi 503 |

## 5. Test map (theo chỉ đạo "Kiểm")

| Ca kiểm | Loại | Ghi chú |
|---|---|---|
| Kênh đẩy ném ⇒ Telegram vẫn gửi (`GuiThongBaoDocLapTests`) | unit, fake `INguiTinDay` throw | đếm 1 lần gọi telegram |
| Telegram ném ⇒ kênh đẩy vẫn gửi | unit, fake `ITelegramNotifier` throw | dispatcher không ném ra ngoài |
| Push hangs 10s ⇒ `GuiTatCaAsync` returns ≤ 2.5s, Telegram called once | unit, fake `Task.Delay(10s, ct)`, immediate Telegram | `Push:TimeoutGiay=2`, `Telegram:TimeoutGiay=10`, `Task.WhenAll` |
| Telegram takes 5s ⇒ send completes successfully, not canceled | unit, fake `Task.Delay(5s, ct)` | Telegram uses its own 10s limit |
| Five messages, hung push ⇒ push once, Telegram five times, total ≤ 3s, exactly one warning | unit, immediate Telegram | Next batch attempts push again |
| Push connection failure ⇒ skip push for remaining messages, Telegram receives all | unit | Exactly one warning per failed batch |
| Tick cancellation ⇒ no exception into runner, remaining messages skipped | unit | Distinguish original cancellation from channel timeout |
| Tin được **gom** rồi mới gửi: `GuiTatCaAsync` chưa gọi ⇒ 0 request; sau `LuuAsync` ⇒ đủ theo thứ tự | unit dispatcher | chống gửi trùng — nếu không dựng được runner thật (cần SQL Server) thì test ở mức dispatcher và **báo "chưa kiểm ở mức runner + lý do"** |
| Thiếu cấu hình Firebase ⇒ `DangHoatDong=false`, không ném, **gửi nhiều lần vẫn đúng 1** logWarning | unit (`KenhDayTinFcmDisabledTests`) | `CredentialPath` trỏ file không tồn tại |
| Invalid FCM token removed; push continues for healthy tokens and later messages | unit EF InMemory | `Unregistered`/`InvalidArgument` populate `TokenHong`; remove from DB and cached tokens, do not open circuit |
| Đăng ký lại cùng token ⇒ 1 dòng | unit EF InMemory | FR-008 |
| **`tokenCu` thuộc user khác ⇒ không bị xoá; thuộc đúng user ⇒ bị xoá** | unit EF InMemory | FR-008 + edge token đổi |
| Gửi cho mọi máy của mọi user | unit | 2 user × 1 máy ⇒ `GuiAsync` nhận 2 token |
| Không dòng log nào chứa `api.telegram.org/bot` sau khi gửi | unit với `ILogger` capture + **hàm đăng ký thật `AddTelegramNotifier()`** + handler giả | FR-012/SC-003; **FAIL trước khi sửa `DependencyInjection.cs:130`, PASS sau khi sửa** |
| `V3PushFormatter` không còn HTML, đúng format tiêu đề | unit thuần | FR-003 |
| `duongDanTuTin` map `/stocks/HPG` / null | `flutter test` | FR-005 |
| **`logout()` không tham số không gọi `go`; `logout(goMay: true)` mới gọi** | `flutter test` (fake ApiClient) | chống vòng lặp 401 |
| Tin thật trên máy `10AE4G1ZE2000K8`, app tắt | thủ công M-6 | SC-001/SC-002 |

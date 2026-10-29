# API Contract: `/api/v1/thiet-bi` (Thiết bị nhận thông báo đẩy)

**Branch**: `013-mobile-push-notify` | **Date**: 2026-10-29 | **Trạng thái**: **Bản sửa đổi 2** — owner đã duyệt vào chặng 2

Controller mới `backend/StockRadar.Api/Controllers/ThietBiController.cs` — `[Route("api/v1/thiet-bi")]`, `[Produces("application/json")]`, `[Tags("ThietBi")]`, **`[Authorize]` bắt buộc** (pattern `NhatKyController.cs:14-19`). JSON camelCase theo mặc định toàn app (`Program.cs:50-55`).

**Toàn bộ 3 endpoint là mới. Không endpoint/DTO/field hiện có nào đổi hoặc bỏ** → không breaking change cho 2 client đã ship (`mobile/` Flutter và `frontend/` React) theo skill `api-contract-review`. Web `frontend/` không gọi 3 route này.

## Quy ước chung

- **SECURITY**: `UserId` **luôn** lấy từ JWT qua `ICurrentUserService`, không bao giờ từ body/query. Endpoint không nhận `userId`.
- **Token FCM** là opaque string do Firebase sinh (dài ~150–200 ký tự, hiện có thể tới ~512). Server chỉ lưu + so khớp, **không** parse. Log chỉ in **5 ký tự cuối**.
- **Idempotent**: đăng ký lại cùng token ⇒ 200, **không** nhân bản dòng (FR-008). Gỡ token không tồn tại ⇒ vẫn 204 (client đăng xuất không cần biết trạng thái).
- **Lỗi chuẩn**: 400 validation (ProblemDetails `{"title","status","detail"}` — `ExceptionHandlingMiddleware.cs:51-63`), 401 chưa đăng nhập, 503 kênh đẩy chưa bật. `ApiClient` mobile đã biết đọc `detail` (`api_client.dart:82-90`).
- Không phân trang, không file upload, không SSE/websocket. Không có API nào cho phép gửi tin tuỳ ý tới user khác.

---

## 1. Đăng ký / cập nhật mã thiết bị

`POST /api/v1/thiet-bi/dang-ky`

Body (`DangKyThietBiRequest`):

```json
{ "token": "fcm_reg_token_dai_khoang_150_ky_tu", "nenTang": "android", "tokenCu": "fcm_reg_token_cu_khoang_150_ky_tu" }
```

| Trường | Bắt buộc | Ràng buộc |
|---|---|---|
| `token` | có | trim, 20–512 ký tự, không khoảng trắng; sai ⇒ **400** `detail: "Mã thiết bị không hợp lệ."` |
| `nenTang` | không | `android` (mặc định). Giá trị khác `android` ⇒ **400** (chỉ Android — owner chốt) |
| `tokenCu` | không | 20–512 ký tự hoặc null/rỗng. **Additive** ⇒ app cũ không gửi vẫn chạy đúng (edge "mã thiết bị FCM đổi — Firebase tự làm mới") |

`200 OK`:

```json
{ "daDangKy": true, "maHoaThongBao": true }
```

`maHoaThongBao` = kênh đẩy đang bật trên server (`Push:Enabled` + credential hợp lệ). `false` ⇒ app hiển thị nhắc "máy chủ chưa bật kênh đẩy" thay vì im lặng (FR-013). Không trả token về client.

Hành vi: upsert theo `Token` (xem plan §(d)) — token đã thuộc user khác ⇒ **ghi đè** `UserId` về user hiện tại (một máy chỉ nhận tin của tài khoản đang đăng nhập gần nhất).
Nếu có `tokenCu` (khác `token` sau trim): **xoá dòng `tokenCu` chỉ khi dòng đó thuộc `userId` hiện tại** — `tokenCu` của user khác thì bỏ qua, không xoá, không báo lỗi. Sau đó mới upsert `token`. Lý do: tránh token cũ thành "hàng gà" vẫn nhận tin khi Firebase làm mới token, đồng thời không cho phép xoá máy của tài khoản khác chỉ bằng một token.

## 2. Gỡ mã thiết bị

`POST /api/v1/thiet-bi/go`

Body (`GoThietBiRequest`):

```json
{ "token": "fcm_reg_token_dai_khoang_150_ky_tu" }
```

- `204 No Content` — đã xoá theo `Token` (bất kể token có tồn tại hay không, bất kể thuộc user nào trên phương diện response).
- `400` — `token` rỗng/short.
- **SECURITY** (ghi **đúng một** dòng comment tại chỗ trong controller): `go` xoá theo token mà **không kiểm chủ** ⇒ ai biết token máy khác thì gỡ được máy đó khỏi danh sách nhận tin, **không lộ dữ liệu** (response rỗng, không endpoint liệt kê token). Đổi lại đăng xuất luôn gỡ được máy — hành vi được chủ đích giữ.
- App **chỉ** gọi `go` khi **bấm nút Đăng xuất** (`AuthService.logout(goMay: true)`), lúc Bearer còn hiệu lực. Hai đường hết phiên (`app_services.dart:11-15` 401 và `:38` JWT hết hạn) **không** gọi `go`: gọi với JWT hết hạn ⇒ 401 ⇒ `onUnauthorized` ⇒ `logout()` ⇒ **vòng lặp**. Xem plan §(h) và push-delivery §3.
- Lỗi mạng/không 2xx khi gỡ: **không thử lại**, không chặn đăng xuất (chấp nhận máy còn treo trong bảng; token sẽ bị xoá khi FCM báo hỏng — FR-009).

## 3. Gửi thông báo thử

`POST /api/v1/thiet-bi/gui-thu`

Không body (không query). Chỉ gửi cho **các thiết bị của chính user đang đăng nhập** (FR-010) — không có tham số nào cho phép bắn tới user/máy khác.

`200 OK` (có thiết bị, kênh đẩy bật):

```json
{ "guiDuoc": true, "soThietBi": 2 }
```

`200 OK` (user chưa đăng ký máy nào):

```json
{ "guiDuoc": false, "soThietBi": 0 }
```

`503 Service Unavailable` — `Push:Enabled=false` hoặc thiếu/sai credential Firebase:

```json
{
  "type": "https://httpstatuses.com/503",
  "title": "Kênh đẩy chưa bật",
  "status": 503,
  "detail": "Chưa cấu hình Firebase trên server, không gửi được thông báo thử."
}
```

Nội dung tin thử (đánh rõ là tin thử — US3 AC4): tiêu đề `THỬ · JUICE`, thân `Thông báo thử từ app — nếu bạn thấy dòng này là kênh đẩy hoạt động.`, kênh Android `v3-signal`, `data.symbol` **không** đặt ⇒ chạm chỉ mở app.

---

## Ánh xạ nghiệm thu

| Scenario spec | Route / hành vi |
|---|---|
| US3 AC1 (đăng nhập lưu token) | `POST /dang-ky` ⇒ 200, 1 dòng `ThietBiNhanThongBao` |
| US3 AC2 (đăng xuất gỡ token) | `POST /go` ⇒ 204, dòng bị xoá — **chỉ** từ nút Đăng xuất |
| Edge "mã thiết bị FCM đổi" | `POST /dang-ky` kèm `tokenCu` ⇒ token cũ bị xoá (đúng chủ), token mới tồn tại 1 dòng |
| US3 AC3 (token hỏng bị xoá) | không qua route — server tự xoá khi FCM báo `Unregistered` (FR-009, xem `push-delivery.md`) |
| US3 AC4 (nút gửi thử) | `POST /gui-thu` ⇒ 200 `guiDuoc:true`, máy nhận tin trong vài giây |
| FR-006/FR-013 (chưa bật kênh đẩy) | `dang-ky` ⇒ `maHoaThongBao:false`; `gui-thu` ⇒ 503 |

## Test phải phủ (theo chỉ đạo)

`backend/StockRadar.Tests/Notifications/`. **Không** dùng `WebApplicationFactory`: repo chưa có `Microsoft.AspNetCore.Mvc.Testing` và không test controller bằng host thật (kiểm tra `StockRadar.Tests.csproj:11-15` — chỉ xUnit + EF InMemory) → thêm package đó không thuộc phạm vi đã duyệt. Cách kiểm:

1. **401**: test phản chiếu (`typeof(ThietBiController).GetCustomAttributes(AuthorizeAttribute)`) có `[Authorize]` + smoke thật `curl` không token ⇒ 401 (bước K-2 quickstart).
2. `dang-ky` token rỗng / `nenTang:"ios"` ⇒ 400 (test service/repository, không qua HTTP).
3. `dang-ky` lặp cùng token ⇒ đúng 1 dòng, `UserId` theo lần cuối (FR-008) — EF InMemory.
4. `dang-ky` kèm `tokenCu` **thuộc user hiện tại** ⇒ token cũ bị xoá; `tokenCu` **thuộc user khác** ⇒ không bị xoá (1 ca test riêng cho mỗi nhánh).
5. `go` token lạ ⇒ không ném, 0 dòng bị xoá.
6. `gui-thu` khi `DangHoatDong=false` ⇒ báo `KhongGuiDuoc`/503 mapping; khi user có 2 máy ⇒ `GuiAsync` nhận đủ 2 token (FR-007a).

Controller mỏng (chỉ map kết quả service → 200/204/400/503) nên phần logic nằm hết ở service test được; mapping HTTP check bằng smoke script. Riêng **test chống rò log** (FR-012) nằm ở `TelegramNotifierLogTests.cs` và phải gọi **hàm đăng ký thật** `AddTelegramNotifier()` — xem plan §(a).

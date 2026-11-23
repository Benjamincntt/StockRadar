# Feature Specification: Vá bảo mật API — bắt đăng nhập, đóng đăng ký, chặn dò mật khẩu

**Feature Branch**: `021-bao-mat-api` (làm trên nhánh `feat/sr-verify-skill`)

**Created**: 2026-11-23

**Status**: Đã làm (23/11/2026) — owner chỉ đạo làm ngay, bỏ quy trình Spec Kit đầy đủ (không có plan/tasks/checklist riêng); spec này ghi lại luật đã cài.

**Input**: Owner (20–23/11/2026): "vì việc này liên quan tới tiền nên tôi cần bọc tất cả các lỗ hổng của server và của api trước" · "App này tôi sẽ không public nên hãy làm bảo mật tốt để tôi dùng". Lý do: app sắp nối đặt lệnh chứng khoán thật qua API (TCBS).

## Mục tiêu

1. Mọi endpoint dữ liệu đều phải có JWT hợp lệ; chỉ để mở đúng cửa vào (đăng nhập) và các endpoint tự kiểm quyền riêng.
2. Đóng đăng ký công khai bằng cấu hình (mặc định tắt) — API một người dùng, không public.
3. Chặn dò mật khẩu ở cửa đăng nhập (5 lần/phút/IP).
4. Chỉ admin ghi được sự kiện quyền (dữ liệu làm đổi giá điều chỉnh).
5. Mobile: chưa đăng nhập thì mọi route về `/login`.

## Giả định (AI đề xuất, owner chưa nói chi tiết)

- **G1**: mọi endpoint bắt đăng nhập, trừ đăng nhập. Vì app chỉ một người dùng, không public.
- **G2**: hub SignalR `/hubs/market` giữ không cần đăng nhập. Hub chỉ đẩy giá thị trường công khai; bắt token cho WebSocket phải đưa token qua query string, mà nginx ghi query string vào access log — rò token vào log.
- **G3**: đóng đăng ký bằng cấu hình `Auth:ChoPhepDangKy`, mặc định `false`.
- **G4**: giới hạn đăng nhập 5 lần / 60 giây / IP.
- **G5**: không đổi thời hạn JWT (1440 phút).

## Luật đã cài

| Phần | Luật |
|---|---|
| Chính sách mặc định | `FallbackPolicy = RequireAuthenticatedUser()` — endpoint nào không có metadata phân quyền đều trả 401 khi thiếu token |
| Đăng nhập | `POST /api/v1/auth/tokens` — `[AllowAnonymous]` + rate limit fixed window 5/60 giây theo IP, vượt ngưỡng trả 429 |
| Đăng ký | `POST /api/v1/users` — `[AllowAnonymous]` để trả 403 rõ ràng; `Auth:ChoPhepDangKy = false` → 403 ProblemDetails "Đăng ký đã tắt". Không xoá tài khoản nào trong DB |
| Sự kiện quyền | `POST /api/v1/stocks/{symbol}/rights-events` — bắt đăng nhập; chỉ `AdminUser.Id` ghi được, người dùng thường nhận 403 |
| Job thủ công | Các `POST` của `MarketJobsController` — `[AllowAnonymous]` + `JobTriggerAuthorization` (sync key HOẶC JWT admin) giữ nguyên; bỏ `[AllowAnonymous]` thì script `X-Sync-Key` nhận 401 trước khi tới bước kiểm sync key |
| Hub | `/hubs/market` — `.AllowAnonymous()` (G2) |
| Chỉ đường | `GET /` và `GET /api/v1` — `[AllowAnonymous]`, không có dữ liệu |
| IP cho rate limit | `RemoteIpAddress` sau `UseForwardedHeaders`; `UseRateLimiter` đặt SAU `UseForwardedHeaders` (đặt sai thứ tự thì mọi client chung IP nginx, khoá cùng lúc) |
| Mobile | `createAppRouter(auth)`: chưa đăng nhập → mọi route về `/login`; đã đăng nhập vào `/login` → về Home. Giữ xử lý 401 sẵn có |

## Endpoint mở sau thay đổi

| Endpoint | Kiểu | Lý do |
|---|---|---|
| `POST /api/v1/auth/tokens` | `[AllowAnonymous]` | cửa vào duy nhất |
| `POST /api/v1/users` | `[AllowAnonymous]` | trả 403 "Đăng ký đã tắt" thay vì 401 gây khó hiểu |
| `POST /api/v1/market/jobs/*` (12 action POST) | `[AllowAnonymous]` + sync key/JWT admin | script và nút chạy job gọi bằng `X-Sync-Key`, không có JWT |
| `GET /` · `GET /api/v1` | `.AllowAnonymous()` | chỉ chỉ đường, không dữ liệu |
| `/hubs/market` | `.AllowAnonymous()` | G2 |

## Cấu hình mới

- `Auth:ChoPhepDangKy` (bool, mặc định `false`) → `AuthOptions` trong `Application/Options`, đọc qua `IOptions<AuthOptions>` ở `UsersController`.

## Cách kiểm

1. `dotnet build backend/StockRadar.slnx` sạch; `dotnet test` 0 fail (không thêm gói).
2. Chạy API local (`backend/restart-api.ps1`, cổng 5280) và gọi curl theo bảng kỳ vọng: GET dữ liệu thiếu token = 401 / có token = 200; `POST /users` = 403; `POST rights-events` thiếu token = 401, user thường = 403; `POST jobs/kbs-sync` với sync key local khác 401; 6 lần sai mật khẩu liên tiếp → lần 6 = 429; `GET /api/v1` = 200.
3. Tạo token user thường bằng cách chạy API local với `Auth__ChoPhepDangKy=true` (chỉ lúc kiểm), tài khoản `test-baomat-<ngẫu nhiên>@example.test`, dọn sạch sau khi kiểm (đếm `Users WHERE Email LIKE 'test-baomat-%'` = 0).
4. `flutter analyze` các file Dart đã sửa: không lỗi.

## Giới hạn đã biết

- `WatchlistService` không tự lọc theo người dùng, nhưng `EfWatchlistListRepository` lọc theo `ICurrentUserService.UserId` ở mọi truy vấn → dữ liệu watchlist của người khác không đọc được. Không sửa trong việc này.
- Rate limit là fixed window trong bộ nhớ của một tiến trình: restart API xoá bộ đếm; nhiều instance thì mỗi instance đếm riêng.
- Web frontend không có route guard: chưa đăng nhập vào `/` sẽ thấy khung lỗi 401 (có sẵn `/login`). Ngoài phạm vi việc này.
- Không đổi thời hạn JWT, không bắt token cho hub.

Kết quả backtest/kiểm thử không phải lời khuyên đầu tư.

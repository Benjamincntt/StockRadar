# ĐÃ GỠ — spec 011 + spec 016, 2026-11

Luồng VIP alert của V1 (`MasterAlerts`, `DarvasBreakoutAlertPublisher`, `VipAlerts`, nhóm `alerts` API, gói quyền VIP) đã xoá: spec 011 gỡ đường VIP V1, spec 016 dọn phần còn lại.

Luồng thông báo còn chạy hôm nay:

- **Telegram** — tin mua / bán / huỷ / nhắc ATO N3 do 4 runner V3 gửi qua `IGuiThongBao.DatTinV3` → `GuiThongBao` → `ITelegramNotifier`. Định dạng tin: `V3TelegramFormatter`.
- **Push FCM** — đăng ký thiết bị qua `POST /api/v1/thiet-bi/dang-ky`, gỡ qua `/thiet-bi/go`, gửi thử qua `/thiet-bi/gui-thu`.
- Bảng đích: `ThietBiNhanThongBao`; khoá cấu hình: `Push` và `TelegramNotify`.

Cách V1 phát alert: [`features/v1-archive/04-radar-lo-lon.md`](./features/v1-archive/04-radar-lo-lon.md). Kiến trúc hiện tại: [`architecture.md`](./architecture.md).

File này giữ path cũ để tránh gãy bookmark. Index: [`README.md`](./README.md).

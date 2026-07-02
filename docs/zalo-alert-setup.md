# Cảnh báo trong app (mặc định)

Job 3 lưu cảnh báo vào bảng `Alerts` và push realtime qua SignalR (`AlertCreated`).

## Xem trên UI

1. Chạy API + frontend
2. Tab **Cảnh báo** (icon chuông) — tự cập nhật khi Job 3 phát hiện đột biến
3. Poll mỗi 15s + SignalR khi app mở

## Điều kiện alert

Mã trong `DailyOpportunities` (watchlist) thỏa:

- `|thay đổi %| ≥ 3`
- `KL phiên ≥ 1,000,000`
- Cooldown 30 phút / mã

## Test

```powershell
cd D:\Source\StockRadar\scripts
.\run-opportunity-monitor.ps1
```

Mở http://localhost:5173/alerts — thấy cảnh báo mới.

## Zalo (tùy chọn, đã tắt mặc định)

Chỉ bật nếu cần gửi ra ngoài app:

```json
"ZaloNotify": {
  "Enabled": true,
  "WebhookUrl": "https://..."
}
```

Xem thêm flow webhook ở phần dưới (n8n / Zalo OA).

---

# Zalo webhook (tùy chọn)

StockRadar không gửi trực tiếp vào app Zalo cá nhân. Cần automation + `WebhookUrl`.

Payload POST:

```json
{
  "phone": "0968992857",
  "message": "...",
  "symbol": "HPG",
  "sentAt": "2026-06-24T08:30:00Z"
}
```

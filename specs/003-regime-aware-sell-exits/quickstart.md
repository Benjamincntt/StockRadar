# Phase 1 — Quickstart: kiểm chứng luật bán mới

Hướng dẫn xác nhận tính năng chạy đúng đầu-cuối. Không chứa code triển khai.

## Chuẩn bị

- SQL Server dev đã áp migration mới nhất.
- `backend/StockRadar.Api/appsettings.Development.json` bật `MasterAlerts.Enabled` và `TelegramNotify.Enabled` với bot/chat test.
- Có ít nhất một mã trong Top của phiên hiện tại để monitor có việc làm.

## 1. Unit test — nhanh nhất, chạy trước

```powershell
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --filter "FullyQualifiedName~SellExit"
```

Bộ test phải phủ các kịch bản chấp nhận trong `spec.md`:

| Kịch bản | Kỳ vọng |
|----------|---------|
| Pha Neutral, mốc 100, giá 96 | `BanNua` |
| Đã bán nửa, giá xuống 94 | `BanHet` |
| Pha Unfavorable, mốc 100, giá 97 | `BanNua` (ngưỡng siết còn 3%) |
| Pha Favorable, mốc 100, giá 96 | `null` (ngưỡng nới thành 5%) |
| Giá thủng mốc phủ nhận | `BanHet` dù chưa đủ 6% |
| Vị thế lỗ chưa từng lãi, rơi qua ngưỡng | `BanNua` (không còn điều kiện lãi tối thiểu) |
| Mua 8.5, đỉnh 20 phiên trước ngày mua là 12, giá vẫn 8.5 | `null` |
| Chưa đủ số phiên tối thiểu, giá thủng ngưỡng | `CanhBaoRuiRoT0`, không phải kind bán |
| Chế độ Có nền trên, giá chạm dưới cạnh dưới nền | `BanNua` |
| Đã bán nửa, đóng cửa lại dưới cạnh dưới nền | `BanHet` |
| Đã bán nửa, giá vượt cạnh trên nền kèm vol | `null` + chế độ chuyển sang Vượt đỉnh |

Test dò nền chạy trên chuỗi OHLCV dựng tay: một nền 20+ phiên trong khoảng 10–12, vài phiên gãy, rồi hồi. Kỳ vọng trả về hộp với cạnh dưới đúng bằng giá đóng cửa thấp nhất của nền.

Test hồi quy bắt buộc: `DarvasBreakoutAnalyzer.Analyze` và `Evaluate` cho kết quả không đổi trên dữ liệu cũ.

## 2. Xác nhận không vỡ luồng hiện tại

```powershell
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj
```

## 3. Chạy API và gửi mẫu Telegram

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "backend/restart-api.ps1"
```

Sau khi API lên, gọi endpoint gửi mẫu:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5280/api/v1/market-jobs/vip-telegram-test"
```

Kiểm tra trong Telegram: mẫu bán 1 nửa của **cả hai** chế độ đều hiện, mỗi tin nêu đủ chế độ, mốc tham chiếu và pha.

## 4. Kiểm chứng trong phiên

Trong khung 9:00–14:45, theo dõi `backend/logs/api-dev.log`:

- Mỗi vị thế mở phải có một dòng ghi chế độ đã phân loại ở lần chạm đầu tiên.
- Vị thế cũ tạo trước khi triển khai phải được phân loại lười, không bị bỏ qua.
- Không có cảnh báo bán nào phát ngay ở vòng quét đầu tiên sau khi cửa sổ bán mở, trừ khi giá thực sự đã rơi qua ngưỡng tính từ ngày mua.

## 5. Đối chứng luật cũ với luật mới

Chạy backtest trên khoảng 12 tháng với cả hai bộ luật rồi so ba con số theo `spec.md`: tỷ lệ lệnh lỗ vượt 8% (mục tiêu giảm ≥50%), lãi trung bình mỗi lệnh thắng (không giảm quá 10%), và số cảnh báo trung bình mỗi vị thế (≤3). Dùng kết quả này để dò lại hệ số Unfavorable trong vùng 0.75–0.85 trước khi bật production.

## 6. Sau khi đạt

Cập nhật `docs/domain/buy-decision.md` và bảng tham số trong `docs/architecture.md` trong cùng change set, theo nguyên tắc IV của hiến pháp. Ship bằng `scripts/ship-all.ps1` khi được yêu cầu.

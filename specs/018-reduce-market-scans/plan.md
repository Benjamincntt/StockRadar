# Implementation Plan: Giảm số lần quét giá — phần A (spec 018)

**Spec**: [spec.md](./spec.md) · **Created**: 2026-11-11 · **Status**: Owner duyệt 11/11/2026 ("Duyệt plan A") → [tasks.md](./tasks.md). Phạm vi: **chỉ phần A** (US1, FR-001…FR-005, FR-011…FR-013). Phần B lập plan riêng sau khi A đã chạy và đo 2 phiên (Q5).

## Kiểm lại trước khi lập plan (đọc code và server 11/11/2026)

| Điểm cần kiểm | Kết quả | Hệ quả |
|---|---|---|
| Vì sao KBS sync chạy cả đêm | `KbsMarketSyncJob` chỉ bỏ qua khi `!IsMarketOpen() && !ForceSyncOutsideHours`. `MarketDataOptions.ForceSyncOutsideHours` **mặc định `true`** (`AppOptions.cs:36`). Server không ghi đè (`appsettings.json` và `appsettings.Production.json` trên server đều không có khoá này). | D1: bỏ hẳn tuỳ chọn này, thay bằng khung giờ cố định (Q1). |
| Job 2 lặp 5 phút đến từ đâu | `MarketJobs:DailySession:IntervalMinutes = 5` trong `backend/StockRadar.Api/appsettings.json`. Server Production chỉ có `DailySession.Enabled`. Khi `IntervalMinutes > 0` mới có trigger 15:05 (`QuartzSchedulingExtensions.cs:99-117`). | D2: bỏ trigger lặp và tuỳ chọn `IntervalMinutes`/`ForceRunOutsideHours`. Trigger 15:05 thành trigger chính, không phụ thuộc interval nữa. |
| Lọc lại danh sách mã gọi ở đâu | Cuối `DailySessionSyncRunner.RunAsync` (luôn chạy). Ngoài ra còn endpoint `POST market/jobs/universe-rescreen` và `HistoryBackfillRunner`. | D3: chỉ lần 15:05 và endpoint thủ công `POST market/jobs/session` mới lọc lại; lần thử lại không lọc. |
| Bộ ghi có chặn ngoài giờ không | `EfMarketDataWriter.UpsertQuotesAsync` chỉ bỏ qua khi `Volume <= 0` **và** ngoài giờ. Ban đêm bảng giá KBS còn khối lượng phiên trước, nên vẫn ghi (log 03:00 ngày 11/11: `stocks=605`). Lúc 08:58 ngày 11/11 log ghi `stocks=0`: KBS đã xoá khối lượng trước giờ mở cửa. | D4: chặn ở bộ ghi theo khung giờ. Từ 08:59 tới khi khớp lệnh, bộ ghi giữ đúng hành vi hiện tại. |
| Endpoint thủ công | `POST market/jobs/kbs-sync` gọi thẳng runner, không qua job. `POST market/jobs/session` gọi thẳng `DailySessionSyncRunner`. | Sau D4, gọi tay `kbs-sync` ngoài khung chỉ đẩy realtime, không ghi DB. Đây là chủ đích, vì gọi tay ban đêm chính là cách tạo nến giả. |
| Test hiện có | `StockRadar.Tests/Scheduling/DailySessionSyncDecisionTests.cs`, 6 test cho `DailySessionSyncDecision`, gồm cả nhánh interval và `ForceRunOutsideHours`. Bộ test hiện tại 517 pass / 1 skip. | D2: thay hàm quyết định và viết lại test theo luật mới. Không giữ test của nhánh đã bỏ. |
| Cấu hình phải dọn | Repo: `appsettings.json` (`IntervalMinutes: 5`), `appsettings.Development.json` (`ForceSyncOutsideHours: true`). Server: không có khoá nào trong ba khoá trên. | Không phải sửa file cấu hình trên server. |

## Thiết kế

### D1 — Khung giờ KBS sync (FR-001, Q1)

- `VietnamMarketCalendar`: thêm `IsInQuoteSyncWindow(DateTime nowVietnam)`. Trả `true` khi là ngày giao dịch và giờ nằm trong **08:59–11:31** hoặc **12:59–15:10**, tính cả hai đầu mút.
  - Có một bản không tham số, dùng giờ hiện tại.
  - Bản nhận tham số để test biên.
- `KbsMarketSyncJob.Execute`: thay điều kiện hiện tại bằng `IsInQuoteSyncWindow()`. Ngoài khung thì `return`, không gọi KBS.
- Xoá `MarketDataOptions.ForceSyncOutsideHours` và khoá `ForceSyncOutsideHours` trong `appsettings.Development.json`. Theo luật xoá sạch, không để lại tuỳ chọn chết.
- Đặt một dòng `// BUSINESS-RULE:` ngay trên các mốc giờ, ghi rõ khung giờ do owner chốt ở Q1 spec 018.

### D2 — Job 2 chỉ chạy sau đóng cửa, có hai lần thử lại (FR-002, FR-005, Q2, Q4)

- `QuartzSchedulingExtensions.ConfigureDailySessionJob`:
  - Bỏ trigger `-interval` và nhánh cron `-trigger` (`IntervalMinutes == 0`).
  - Giữ một trigger chính `-close` lúc `Hour:Minute + 5`, tức 15:05.
  - Thêm `-retry-1` lúc `+15` (15:15) và `-retry-2` lúc `+30` (15:30). Hai trigger này mang cờ `thuLai` trong `JobDataMap`.
- `DailySessionJobOptions`: xoá `IntervalMinutes` và `ForceRunOutsideHours`. Xoá `IntervalMinutes` trong `appsettings.json`.
- `DailySessionSyncDecision`: thu về một quyết định duy nhất.
  - Không phải ngày giao dịch thì bỏ qua.
  - Còn lại thì chạy: `LanChinh` hoặc `ThuLai` theo cờ.
  - Dòng `// DO-NOT-CHANGE:` hiện có được giữ và sửa lời cho đúng luật mới: không chặn theo giờ khớp lệnh, vì mọi trigger đều nằm ngoài giờ.
- Trạng thái lần thử lại, lưu trong bộ nhớ, singleton `Job2TrangThaiNgay`:
  - Lưu ngày phiên và danh sách mã **còn thiếu**. Mã thiếu là mã có trong lô gửi KBS nhưng không có dòng trả về. Khi KBS lỗi, client trả danh sách rỗng, nên cả lô tính là thiếu.
  - Lần chính ghi trạng thái này. Lần thử lại đọc trạng thái:
    - Không còn mã thiếu: bỏ qua, ghi log "Job 2 thử lại: đủ nến, bỏ qua".
    - Còn mã thiếu: chỉ lấy giá các mã đó, rồi cập nhật lại danh sách thiếu.
    - Chưa có trạng thái cho hôm nay (server khởi động lại sau 15:05): chạy đầy đủ như lần chính nhưng **không** lọc lại danh sách mã.
  - Sau lần thử cuối (15:30) mà vẫn còn mã thiếu: log mức cảnh báo, kèm số mã và tối đa 20 tên mã.
- Không lưu trạng thái xuống DB. Lý do: chỉ có tác dụng trong 25 phút. Khởi động lại thì rơi vào nhánh chạy đầy đủ, vẫn đúng kết quả.

### D3 — Lọc lại danh sách mã một lần mỗi ngày (FR-003)

- `DailySessionSyncRunner.RunAsync` nhận một tuỳ chọn `CheDoJob2`: `LanChinh` (mặc định), `ThuLai` hoặc `ThuLaiDayDu`.
  - Chỉ `LanChinh` gọi `universeRescreen.RunAsync`.
  - Endpoint `POST market/jobs/session` dùng `LanChinh`, nên hành vi gọi tay giữ nguyên.
- `IDailySessionSyncService.RunAsync` thêm tham số có giá trị mặc định. Không đổi hợp đồng HTTP.

### D4 — Bộ ghi chặn nến hôm nay ngoài khung (FR-004)

- `MarketSyncRequest` thêm trường cuối `bool SauDongCua = false`. `MarketSyncService.ApplyAsync` truyền xuống bộ ghi.
- `IMarketDataWriter.UpsertQuotesAsync` và `UpsertIndexAsync` thêm tham số `choPhepSauDongCua`. Bộ ghi chỉ ghi khi một trong hai điều sau đúng:
  - đang trong khung KBS sync (D1);
  - `choPhepSauDongCua` và giờ hiện tại ≥ 14:45 của ngày giao dịch đó.
- Ngoài hai trường hợp trên thì bỏ qua và trả 0 mã đã ghi. Realtime (`QuotesUpdated`) và bộ nhớ đệm vẫn chạy như cũ, vì nằm ở `MarketSyncService`, trước bước ghi.
- `DailySessionSyncRunner` luôn gửi `SauDongCua = true`, vì Job 2 giờ chỉ chạy sau đóng cửa. `KbsMarketSyncRunner` gửi `false`.
- Đặt một dòng `// DO-NOT-CHANGE:` ngay tại điều kiện: bỏ điều kiện này thì gọi tay hoặc chạy ban đêm sẽ ghi nến hôm nay mang giá phiên trước.
- Điều kiện cũ (`Volume <= 0` và ngoài giờ thì bỏ) giữ nguyên.

### D5 — Log để đo (FR-012)

- `KbsMarketSyncRunner`: dòng `KBS sync:` thêm thời gian chạy (ms).
- `DailySessionSyncRunner`: dòng `Job 2 xong:` thêm chế độ (`LanChinh`/`ThuLai`), thời gian chạy (ms) và số mã còn thiếu.
- Không thêm log mỗi phút ngoài khung, để tránh làm đầy journal.

### D6 — Tài liệu (FR-013)

- `docs/domain/pipeline-jobs.md`: sửa dòng Job 2 và KBS sync theo lịch mới. Tài liệu này còn mô tả V1, sẽ viết lại ở đợt 5 spec 016 (T042+). Ở đây chỉ sửa đúng hai dòng của job bị đổi, không viết lại cả trang.
- `CLAUDE.md`: mục Pipeline thêm một câu về khung giờ KBS sync và Job 2 chỉ chạy sau đóng cửa.
- `spec.md` spec 016 có câu "`KbsMarketSyncJob` cũng dừng 14:45" sai so với code. **Không sửa.** Đây là spec lịch sử, chỉ nêu trong báo cáo.

## Kiểm chứng

| Mức | Cách | Kỳ vọng |
|---|---|---|
| Unit | Test `IsInQuoteSyncWindow` ở các mốc 08:58:59, 08:59, 11:31, 11:31:01, 12:58:59, 12:59, 15:10, 15:10:01, ngày nghỉ, cuối tuần | Đúng khung Q1 |
| Unit | Test quyết định Job 2: ngày nghỉ bỏ qua; ngày giao dịch chạy; cờ `thuLai` cho chế độ đúng | Pass |
| Unit | Test trạng thái thử lại: lô KBS trả thiếu 3 mã thì lần thử lại chỉ lấy 3 mã đó; đủ thì bỏ qua; chưa có trạng thái thì chạy đầy đủ, không lọc lại | Pass |
| Unit | Test bộ ghi (InMemory): ngoài khung, không cờ thì 0 mã ghi và `HistoryJson` không đổi; trong khung thì ghi; 15:15 có cờ thì ghi; 03:00 có cờ thì không ghi | Pass |
| Toàn bộ | `dotnet build` + `dotnet test` | 517 + test mới pass, 1 skip như cũ |
| Local | Chạy API, đọc log `QuartzSchedulerStartupLogger` | Có `daily-session-sync-close`, `-retry-1`, `-retry-2`; không còn `-interval` |
| Production, 2 phiên sau deploy | Đếm log | KBS sync ~282 lần/ngày (SC-001); 0 dòng `KBS sync:` từ 00:00 tới 08:58; Job 2 chạy 15:05 và (nếu đủ) 2 dòng "thử lại: bỏ qua"; đúng 1 lần lọc lại (SC-002); nến 5 mã mẫu khớp giá đóng cửa; V3 16:00 bình thường; không có nến hôm nay trước 09:00 (SC-003) |
| Production, CPU | Đọc `utime+stime` của tiến trình trong `/proc/<pid>/stat` lúc 08:30 và 15:30, cùng các khoảng tương ứng ban đêm | Báo số giây CPU dùng trong phiên và ban đêm, so với số nền đo phiên 12/11 (trước deploy) |

## Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Lần 15:05 lỗi, hai lần thử cũng lỗi: V3 16:00 tính trên giá 14:45 | Log cảnh báo có tên mã. Hiện tại cũng không có gì bảo đảm hơn, vì KBS sync ghi bù chỉ là tình cờ. |
| App mở sau 15:10 không còn giá nhảy | Đúng chủ đích: sau 14:45 không còn khớp lệnh. Giá ATC đã vào lúc ≤ 15:10 và lần 15:05. |
| Bỏ `ForceSyncOutsideHours` làm dev local không còn giá ngoài giờ | Dev muốn có giá thì gọi `POST market/jobs/session` (lần chính, ghi được sau 14:45). Ghi vào báo cáo cho owner. |
| Server khởi động lại giữa 15:05 và 15:30 | Lần thử lại chạy đầy đủ (D2). Ghi lại nến cùng ngày, không nhân đôi (`MergeBars` thay nến cùng ngày). |

## Không làm trong A

- Phần B: cột "giá hôm nay", ghi theo lô, không đụng `HistoryJson` trong phiên.
- V3 trong phiên / ATC gọi KBS riêng.
- Job 1 đang tắt.
- Sửa spec 016.

## Thứ tự giao

Code A chỉ bắt đầu sau khi T038 (spec 016) đánh dấu xong (Q5). Riêng T001 (đo nền) chạy trong phiên 12/11, cùng lúc với T038, vì đó là phiên cuối cùng còn chạy theo lịch cũ.

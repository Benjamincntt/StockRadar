# Tasks: Giảm số lần quét giá — phần A (spec 018)

**Input**: [spec.md](./spec.md), [plan.md](./plan.md) — owner duyệt plan 11/11/2026 ("Duyệt plan A").

`[P]` = làm song song được (khác file, không phụ thuộc nhau). Chỉ commit khi owner nói "commit đi".

## Đợt 0 — Đo nền (phiên 12/11/2026, trước khi đổi gì)

- [ ] ~~T001~~ (bỏ theo owner 11/11: "code luôn đi. kiểm làm gì nữa") Đọc log production phiên 12/11 (chỉ đọc), ghi lại số nền vào báo cáo:
  - số dòng `KBS sync:` trong ngày, và từ 00:00 tới 08:58;
  - số lần Job 2 chạy và số lần lọc lại danh sách mã;
  - thời gian chạy mỗi loại;
  - giây CPU của tiến trình API (`utime+stime` trong `/proc/<pid>/stat`) lúc 08:30 và 15:30, cùng một khoảng ban đêm;
  - có nến ngày 12/11 trong `HistoryJson` của HPG, FPT, TCB, VCB, DRI trước 09:00 hay không.

  Làm cùng lúc kiểm T038 spec 016.

## Đợt 1 — Code phần A (bắt đầu sau khi T038 spec 016 đánh dấu xong)

- [x] T002 D1: `VietnamMarketCalendar.IsInQuoteSyncWindow` (có bản nhận giờ để test): 08:59–11:31 và 12:59–15:10, chỉ ngày giao dịch. Một dòng `// BUSINESS-RULE:` trên các mốc giờ.
- [x] T003 D1: `KbsMarketSyncJob` dùng `IsInQuoteSyncWindow()`. Xoá `MarketDataOptions.ForceSyncOutsideHours` (`backend/StockRadar.Application/Options/AppOptions.cs`) và khoá này trong `backend/StockRadar.Api/appsettings.Development.json`.
- [x] T004 [P] Test T002: các mốc biên trong bảng Kiểm chứng của plan, ngày nghỉ, cuối tuần.
- [x] T005 D2: `QuartzSchedulingExtensions.ConfigureDailySessionJob`:
  - bỏ trigger `-interval` và nhánh `-trigger`;
  - giữ `-close` (15:05);
  - thêm `-retry-1` (15:15) và `-retry-2` (15:30) mang cờ `thuLai`.

  Xoá `DailySessionJobOptions.IntervalMinutes` và `ForceRunOutsideHours`, và `IntervalMinutes` trong `backend/StockRadar.Api/appsettings.json`.
- [x] T006 D2: rút gọn `DailySessionSyncDecision` và `DailySessionSyncJob`:
  - ngày nghỉ thì bỏ qua;
  - còn lại chọn chế độ `LanChinh` hoặc `ThuLai` theo cờ.

  Sửa lời dòng `// DO-NOT-CHANGE:` cho đúng luật mới.
- [x] T007 D2 + D3: `DailySessionSyncRunner` nhận chế độ (`LanChinh` / `ThuLai` / `ThuLaiDayDu`).
  - Thêm singleton `Job2TrangThaiNgay` (ngày phiên, các mã còn thiếu).
  - Lần thử lại chỉ lấy mã thiếu. Chưa có trạng thái thì chạy đầy đủ.
  - Chỉ `LanChinh` gọi lọc lại danh sách mã.
  - Hết lần 15:30 mà còn thiếu thì log cảnh báo kèm số mã và tối đa 20 tên mã.
  - `IDailySessionSyncService.RunAsync` thêm tham số mặc định `LanChinh`. Endpoint `POST market/jobs/session` không đổi.
- [x] T008 [P] Viết lại `backend/StockRadar.Tests/Scheduling/DailySessionSyncDecisionTests.cs` theo luật mới; xoá test của nhánh interval / `ForceRunOutsideHours`. Thêm test trạng thái thử lại:
  - thiếu 3 mã thì chỉ lấy 3 mã;
  - đủ thì bỏ qua;
  - chưa có trạng thái thì chạy đầy đủ, không lọc lại.
- [x] T009 D4: `MarketSyncRequest.SauDongCua` (mặc định `false`).
  - `MarketSyncService` truyền xuống bộ ghi.
  - `IMarketDataWriter.UpsertQuotesAsync` / `UpsertIndexAsync` thêm `choPhepSauDongCua`.
  - `EfMarketDataWriter` chỉ ghi khi trong khung, hoặc (có cờ và ≥ 14:45 cùng ngày giao dịch). Một dòng `// DO-NOT-CHANGE:` tại điều kiện.
  - `DailySessionSyncRunner` gửi `true`, `KbsMarketSyncRunner` gửi `false`.
- [x] T010 [P] Test T009 (InMemory): ngoài khung không cờ thì 0 mã ghi và `HistoryJson` không đổi; trong khung thì ghi; 15:15 có cờ thì ghi; 03:00 có cờ thì không ghi; realtime vẫn được đẩy khi bộ ghi bỏ qua.
- [x] T011 D5: log thời gian chạy ở dòng `KBS sync:`; dòng `Job 2 xong:` thêm chế độ, thời gian chạy, số mã còn thiếu.
- [x] T012 `dotnet build` + `dotnet test` (kỳ vọng 517 + test mới pass, 1 skip). Chạy API local, đọc log Quartz: có `-close`, `-retry-1`, `-retry-2`, không còn `-interval`. Grep backend không còn `ForceSyncOutsideHours`, `ForceRunOutsideHours`, `IntervalMinutes` (ngoài `V3:IntervalPhut`). Báo số test.
- [x] T013 D6: `docs/domain/pipeline-jobs.md` sửa hai dòng Job 2 và KBS sync; `CLAUDE.md` mục Pipeline thêm một câu. Không viết lại phần V1 (đợt 5 spec 016).
- [ ] T014 **Điểm dừng**: AI báo diff, kết quả test, bản nháp commit message. Owner duyệt, commit, ship.

## Đợt 2 — Đo sau deploy

- [ ] T015 AI đọc (chỉ đọc) log production 2 phiên liên tiếp sau deploy, so với T001:
  - SC-001: số lần KBS sync, và không có dòng nào từ 00:00 tới 08:58;
  - SC-002: đúng 1 lần lọc lại danh sách mã;
  - SC-003: không có nến hôm nay trước 09:00 ở 5 mã mẫu;
  - nến 5 mã mẫu khớp giá đóng cửa;
  - hai lần thử lại chạy đúng nhánh;
  - V3 16:00 bình thường;
  - giây CPU trong phiên và ban đêm.
- [ ] T016 **Điểm dừng**: báo bảng trước/sau cho owner; owner quyết định lập plan phần B.

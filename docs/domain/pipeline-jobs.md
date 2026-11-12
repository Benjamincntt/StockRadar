# Pipeline jobs (dữ liệu & phân tích)

## Mục đích

Trách nhiệm và giờ chạy **as-is** của: Job 1 (universe + lịch sử), Job 2 (nến phiên T), KBS sync (giá trong phiên), bốn nhịp bộ kịch bản V3, và nhịp nạp chuỗi chỉ số. V1 (phân tích Top, ML/HPO, radar, theo dõi lô lớn) đã gỡ toàn bộ theo [spec 016](../features/v1-archive/README.md).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `backend/StockRadar.Infrastructure/Scheduling/QuartzSchedulingExtensions.cs` | Lịch Quartz thật (múi giờ VN) — **nguồn sự thật** cho mọi giờ chạy |
| 2 | `Scheduling/Jobs/DailySessionSyncJob.cs` + `DailySessionSyncDecision.cs` + `MarketData/DailySessionSyncRunner.cs` | Job 2: chế độ lần chính / thử lại, rescreen universe |
| 3 | `Scheduling/Jobs/KbsMarketSyncJob.cs` + `MarketData/VietnamMarketCalendar.cs` (`IsInQuoteSyncWindow`) | Khung giờ lấy bảng giá |
| 4 | `Scheduling/Jobs/HistoryBackfillJob.cs` (cron tuần) | Job 1 |
| 5 | `MarketData/V3TruocPhienRunner` · `V3TrongPhienRunner` · `V3AtcRunner` · `V3SauPhienRunner` | Bốn nhịp V3 |
| 6 | `backend/StockRadar.Api/Controllers/MarketJobsController.cs` | Trigger thủ công (header `X-Sync-Key`) |
| 7 | `backend/StockRadar.Api/appsettings.json` | Giá trị mặc định của repo |

> Khi docs lệch code → **tin code trên disk**.
>
> **Giá trị dưới đây là mặc định trong repo.** Production đọc `appsettings.Production.json` **trên server**, khác bản repo (không commit) — giờ chạy và ngưỡng trên production có thể khác. Muốn biết số thật thì đọc file đó trên server.

## Luật as-is

| Job | Khi chạy | Việc | Ghi DB / output |
|-----|----------|------|-----------------|
| **Job 1** `history-backfill` | Thủ công (`POST market/jobs/history`) hoặc cron tuần **CN 02:00** VN (`WeeklyRefreshEnabled`, `WeeklyRefreshDay/Hour/Minute`); `RunOnStartup` mặc định **tắt** | Listing + backfill OHLCV 2000→T-1 (chế độ đêm khi chạy cron) → lọc universe | `Stocks`, `HistoryJson`, `IsActive` |
| **Job 2** `daily-session-sync` | **15:05** + thử lại **15:15** và **15:30**, T2–T6, chỉ ngày giao dịch (`DailySession.Hour:Minute` + 5/15/30 phút) | Append nến phiên T; lần chính lọc lại universe **một lần/ngày** (`FR-003`); thử lại chỉ lấy mã còn thiếu nến | `Stocks.HistoryJson` |
| **KBS sync** `kbs-market-sync` | Mỗi **60 s** (`MarketData:SyncIntervalSeconds`), nhưng chỉ chạy trong khung **08:59–11:31** và **12:59–15:10** ngày giao dịch; ngoài khung job thoát ngay, không gọi KBS | Quote cache + SignalR + ghép nến trong ngày | Bộ nhớ đệm, SignalR `QuotesUpdated` / `IndexUpdated` |
| **V3 trước phiên** `v3-truoc-phien` | **08:30** T2–T6 | Nhịp nhẹ (~200 mã `UNI` đã lưu): chấm KB1/KB2 trên giá đóng cửa phiên trước → tín hiệu + vị thế CHỜ MUA ATO + Telegram mua | `TinHieuV3` / `ViTheV3` |
| **V3 trong phiên** `v3-trong-phien` | Cron `0 0/{V3:IntervalPhut} 9-14` T2–T6 (mặc định **1 phút**); tự bỏ qua ngoài **09:00–14:45** | Nhịp nhẹ nhất: chỉ quote mã đang giữ → khớp CHỜ MUA tại giá mở cửa (hủy nếu đúng giá trần) + xét BÁN realtime theo `NguongChamMa` đã lưu | `ViTheV3` (update) + Telegram bán |
| **V3 ATC** `v3-atc` | **14:25** T2–T6 | Nhịp nhẹ: ~200 mã UNI đã lưu + mã đang giữ; ghép nến hôm nay bằng khối lượng khớp tích luỹ, chỉ số cộng điểm hôm nay → KB4/N1/N3, mua ATC, bán hết hạn | `TinHieuV3` / `ViTheV3` |
| **V3 sau phiên** `v3-sau-phien` | **16:00** T2–T6 | Nhịp **nạp đầy đủ duy nhất**: `LayCacMaAsync` toàn universe → lưu chỉ số hôm nay + `UNI` phiên sau; đối chiếu Nhật ký (spec 014); tính sẵn ngưỡng bán / giá tham chiếu / sàn; huỷ lệnh CHỜ MUA quá phiên | `ChiSoNgayV3` + `ViTheV3` (update) |
| **Nạp chuỗi chỉ số** `v3-nap-chi-so` | Chạy tay khi deploy (`POST market/jobs/v3-nap-chi-so?tuNgay=`, mặc định `V3:TuNgayNapChiSo` = 2023-01-01) | Dựng sẵn chuỗi `ChiSoNgayV3` + `UNI` từng ngày; **không** tạo tín hiệu/vị thế | `ChiSoNgayV3` |

Trigger thủ công (header `X-Sync-Key`), đúng như `MarketJobsController`: `POST market/jobs/history` · `history/night` · `session` · `universe-rescreen` · `kbs-sync` · `v3-truoc-phien` · `v3-trong-phien` · `v3-atc` · `v3-sau-phien` · `v3-nap-chi-so`. `GET market/jobs` trả trạng thái job.

### Job 2 — vì sao chỉ chạy sau đóng cửa

- Cả ba trigger đều **ngoài giờ khớp lệnh**. `DailySessionSyncDecision.Decide(ngày giao dịch?, đến từ trigger thử lại?)` là hàm thuần: sai ngày → `SkipNotTradingDay`; thử lại → `CheDoJob2.ThuLai`; còn lại → `CheDoJob2.LanChinh`. Không còn kiểm giờ, không còn `IntervalMinutes`, không còn cờ chạy ngoài giờ (spec 018 Q2).
- Thiếu lần chạy 15:05 thì `HistoryJson` không có nến ATC và nhịp V3 16:00 tính trên giá 14:45 — xem `// DO-NOT-CHANGE:` trong `DailySessionSyncDecision.cs`.
- Thử lại 15:15/15:30 đọc danh sách mã còn thiếu từ `Job2TrangThaiNgay` (singleton theo ngày); server khởi động lại giữa hai lần thì chạy lại đầy đủ (`ThuLaiDayDu`), không lọc lại universe.
- Lần chính lấy **toàn bộ mã active + mã inactive** (`WarmInactiveUniverse`, mặc định bật) để lịch sử mã bị loại không đóng băng; rescreen cuối lần chính có thể khôi phục mã đã đạt thanh khoản trở lại.
- **Cron tuần Job 1 (CN 02:00)** phá vòng chết còn lại: mã inactive không có nến mới thì rescreen không bao giờ tự đánh giá lại. Tắt bằng `MarketJobs:History:Enabled=false` hoặc `WeeklyRefreshEnabled=false`.

### KBS sync — khung giờ lấy bảng giá (spec 018 Q1)

- Khung: **08:59–11:31** và **12:59–15:10**, tính cả hai đầu mút, chỉ ngày giao dịch (`VietnamMarketCalendar.IsInQuoteSyncWindow`). Job kiểm khung trước khi gọi `KbsMarketSyncRunner`; bộ ghi (`EfMarketDataWriter`) kiểm lại lần nữa để không tạo nến hôm nay ngoài khung.
- Ngoài khung không có dòng log `KBS sync:` nào. Trong khung log dạng `KBS sync: stocks=…, index=…, … ms`.
- Chu kỳ tối thiểu 30 s (`SyncIntervalSeconds` bị chặn sàn 30).

### Nến ngày và điều chỉnh quyền

- `Stocks.HistoryJson` lưu **giá khớp thô** (nghìn đồng). Job 1 / Job 2 không nhân hệ số vào nến.
- `BoDieuChinhGiaTheoQuyen` + `NguonSuKienQuyen` (`Data/su-kien-quyen.json`, nhập ở chi tiết mã → **Sự kiện quyền**) vẫn nằm trong code cho [spec 015](../../specs/015-v3-rights-adjusted-prices/spec.md); hiện **chưa nhịp nào của V3 gọi** bộ điều chỉnh này.

### Luồng bộ kịch bản V3 — **SỰ THẬT CHUẨN DUY NHẤT**

> Mục này là canon **duy nhất** cho luồng vận hành V3 as-is (hiệu lực từ spec 012, 2026-10-26 — V2 gỡ toàn bộ theo spec 012, V1 gỡ toàn bộ theo spec 016). Mọi chỗ khác (architecture, README, ai-context, [spec 012](../../specs/012-v3-scenario-engine/spec.md)) chỉ tóm tắt/index và **trỏ về đây** — đổi hành vi V3 → sửa mục này trước tiên. Nguồn luật chuẩn (normative): [`../features/v3-scenario-engine/research/`](../features/v3-scenario-engine/research/) (01-phuong-phap · 02-so-phat-hien · 03-ket-qua) + Phụ lục A/C/D/H spec 012.

V3 là **hệ chọn mã duy nhất** sau spec 016, gồm 5 kịch bản **KB1** (hoảng loạn) · **KB2** (chỉ số thủng MA50) · **KB4** (ngành bán quá mức) · **N1** (giải chấp cạn kiệt) · **N3** (đón dòng tiền đầu tháng), trên bảng `TinHieuV3` (khoá tự nhiên **Symbol + Loai + NgayTinHieu**) + `ViTheV3` (vị thế) + `ChiSoNgayV3` (chỉ báo thị trường ngày, upsert một lần/phiên).

**Vòng đời**: tín hiệu `DaBao` → vào lệnh `DaVaoLenh` (vị thế CHỜ MUA → ĐANG GIỮ → ĐÃ THOÁT; hoặc `BiHuy` vì giá mở cửa đúng giá trần — FR-014) → thoát realtime/ATC → `V3ChamKetQua` chốt kết quả Thắng/Thua/Hoà vốn (lãi ròng so ngưỡng ±0,05%, phí khứ hồi 0,4%).

**Quy tắc danh mục (Phụ lục A/C):** mỗi kịch bản tối đa **top 5** ứng viên/phiên; ưu tiên KB1 > KB2 > KB4 > N1 > N3; dedup một mã chỉ thuộc kịch bản ưu tiên cao nhất; tối đa **5 vị thế**; mỗi lệnh = giá trị danh mục / 5, riêng **KB1/KB2/N1 ×1,5** (≈30% danh mục), KB4/N3 ×1,0; không vượt tiền mặt còn lại; không mua mã đang giữ.

**Chỉ báo toàn thị trường (FR-001/FR-003):** chỉ số bình quân đều cộng lợi nhuận ngày của các mã **thuộc UNI tính tại phiên liền trước** (research 01: `R.where(UNI.shift())` — TV20 ≥ 5 tỷ và close ≥ 5 cùng phiên đó), **không** dùng tập UNI của phiên tín hiệu cho cả lịch sử. Luật này nằm trong `V3ChiSoToanThiTruong.TinhDiemTheoTruc` — **một luật, hai cửa gọi**: `Tinh` (một phiên, cho bộ kịch bản) và `TinhChuoi` (mọi phiên trên trục, cho nhịp nạp chuỗi). **SECR20 = trung vị RET20 của ngành**; mã **chưa có ngành** → SECR20 = null (không lấy RET20 của chính nó thay thế) nên **rớt KB4 và N1**.

**Sắp xếp tải dữ liệu (owner chốt 26/10/2026):** chỉ **một nhịp nạp đầy đủ** là 16:00; ba nhịp còn lại đọc kết quả đã lưu.

| Nhịp | Tập mã nạp | Chỉ số / MA50 | Bán realtime |
|------|-----------|---------------|--------------|
| 16:00 (`DayDu`) | `LayCacMaAsync` toàn universe (một lần duy nhất trong ngày) | Tính từ đầu chuỗi + **ghi `ChiSoNgayV3`**: giá trị hôm nay **và `DanhSachUni` cho phiên sau** | Tính sẵn `NguongChamMa` (TB 9 hoặc 19 đóng cửa trước) + `GiaThamChieu` + `Exchange` → ghi `ViTheV3` |
| 08:30 · 14:25 (`NhipNhe`) | `LayMaNhipNheAsync` — chỉ ~200 mã theo `DanhSachUni` đã lưu (+ mã đang giữ ở nhịp ATC) | **Đọc chuỗi đã lưu** (`ChiSoNgayV3`, 60 phiên gần nhất), không ghi lại lịch sử; riêng nhịp ATC **cộng điểm hôm nay** = mức lưu × (1 + `ChiSoLoiNhuanHomNay`) | Đọc `NguongChamMa` đã lưu |
| Mỗi phút (`NhipNhe`) | **KHÔNG nạp lịch sử, KHÔNG gọi `LayCacMaAsync`** — chỉ quote cho mã đang giữ | không cần | So giá khớp với `NguongChamMa` đã lưu (`V3LuatBanThucTe.XetTheoNguongDaLuu`) + trần/sàn từ `GiaThamChieu`, `Exchange` |

Nhịp 14:25 ghép nến hôm nay bằng **khối lượng khớp tích luỹ** từ bảng giá (không truyền `0L`) → TV20/UNI của phiên ATC đúng. Mỗi nhịp **log thời gian chạy** (`Stopwatch` → "V3 …: nhịp chạy hết {Ms} ms").

**Luật bán realtime (FR-015..018, V3-B1):** bán khi **giá khớp ≥ max(ngưỡng chạm MAn, giá vào ×1,004)**; MAn theo kịch bản — KB1/KB2/KB4 MA10, N1 MA20, N3 không có MA (chỉ ATO theo lịch TDM=2 tháng sau); giữ tối đa KB1/KB2/N1 15 · KB4 10 · N3 12 phiên → hết hạn bán ATC; **T+2.5 (FR-017) = đúng phiên T+2 chỉ bán từ đầu buổi chiều (`V3:GioBanPhieuTplus2`, mặc định 13:00), từ phiên T+3 bán cả ngày**; **sàn trắng** (khối lượng phiên = 0) chặn mọi lệnh bán; không bao giờ bán theo "giá đóng cửa rồi phiên sau bán".

**Lý do thoát `ChotLoiHoangLoan` (bổ sung v3, FR-015a/FR-016a, research PH-21):** lệnh hoảng loạn sâu (KB1/KB2/KB4/N1 có `BREADTH50` phiên tín hiệu < 0,15; chốt cờ một lần lúc tạo vị thế) bán realtime khi **giá khớp ≥ giá vào × 1,15** (bỏ ngưỡng MA), lý do `ChotLoiHoangLoan` thay vì `ChamMA`, giữ tối đa **40 phiên** → hết hạn bán ATC; `Xet`/`XetTheoNguongDaLuu` nhận cờ `laHoangLoanSau`. Nhịp 16:00 **không** ghi đè ngưỡng của lệnh này (`V3SauPhienRunner`). Tham số `V3:HoangLoanSauBreadth50Max` · `HoangLoanSauChotLoi` · `HoangLoanSauGiuToiDa`.

**Kết nối:** Home (mobile + web) hiện **danh sách vị thế V3 đang giữ**, không còn khối Top V1 (spec 016). V3 phục vụ: `GET /api/v1/v3/hieu-qua/tom-tat` · `lich-su` · `vi-the-dang-giu`, `GET /stocks/{sym}/v3` (chi tiết mã), `GET /nhat-ky/tin-hieu/v3` (nhật ký — FR-040). Config keys: section `V3` trong `appsettings.json` (`Enabled` · `GioBatDau/GioKetThuc` · `IntervalPhut` · `TuNgayNapChiSo` · ngưỡng từng kịch bản `Kb1*/Kb2*/Kb4*/N1*/N3*` · `BanPhanTramGiaVao` · `GiuToiDa*` · `Tplus25Sessions` · `GioBanPhieuTplus2` · `MaxViThe` · `TrongSoNhan` · `PhiKhuriHoi` · `NguongThang` · `VonBanDau` · `TaiKhoanDanhMuc` = Guid tài khoản Nhật ký dùng làm danh mục thật, spec 014 — trống là bỏ bước đối chiếu).

**Trạng thái DB (12/11/2026):** migration `20261112064612_RemoveV1Tables` đã apply trên production — 26 bảng V1 ở Phụ lục A spec 016 đã xoá sau khi xuất dữ liệu ra file; 13 bảng còn lại gồm bảng dùng chung + 3 bảng V3. Bảng V2 cũ `KetQuaKichBan` **vẫn còn** (quyết định xoá riêng, chưa có); các dòng nhật ký gắn tín hiệu V2 cũ giữ nguyên (phương án A — owner chốt 26/10/2026).

Deploy: `.\scripts\ship-all.ps1`.

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-PL-1 | Endpoint lịch sử `/jobs/daily`, `/jobs/analysis`, `/jobs/opportunity-monitor` không còn — dùng `session` + các `v3-*` | Gỡ cùng V1, spec 016 |
| G-PL-2 | Chưa phát hiện mâu thuẫn lịch vs code khác | Cập nhật khi đổi Quartz |
| G-PL-3 | Job 2 chỉ OHLCV thô | Đúng thiết kế — điều chỉnh quyền tách riêng (spec 015) |
| G-PL-4 | Giá trị mặc định trong repo khác production | Đọc `appsettings.Production.json` trên server khi cần số thật |

## Tài liệu liên quan

- Lưu trữ V1: [`../features/v1-archive/`](../features/v1-archive/README.md)
- Điều chỉnh quyền: [`../../specs/005-ohlcv-corporate-adjust/spec.md`](../../specs/005-ohlcv-corporate-adjust/spec.md)
- [`../architecture.md`](../architecture.md), [`../build-and-deploy.md`](../build-and-deploy.md)
- Index: [`../README.md`](../README.md)

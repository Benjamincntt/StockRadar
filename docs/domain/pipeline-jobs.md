# Pipeline jobs (dữ liệu & phân tích)

## Mục đích

Thứ tự và trách nhiệm Job 1 / Job 2 / phân tích daily / monitor intraday / ML·HPO — **as-is** vận hành.

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | Market job controllers / `POST /api/v1/market/jobs/*` | Trigger thủ công |
| 2 | `DailySessionSyncRunner` | Job 2 + Darvas alerts |
| 3 | `DailyAnalysisRunner` | Top V1 + criterion + sector wave + SetupTracks |
| 4 | `OpportunityIntradayMonitorRunner` | Job 3 intraday ~60s (quét lô lớn/VSA/dòng tiền → SignalR; bắn tin VIP đã gỡ — spec 011) |
| 5 | `V3TruocPhienRunner` / `V3TrongPhienRunner` / `V3AtcRunner` / `V3SauPhienRunner` | Bộ kịch bản V3 (spec 012 — V2 đã gỡ toàn bộ) |
| 6 | `QuartzSchedulingExtensions.cs` | Lịch Quartz thật (VN timezone) |
| 7 | `docs/architecture.md` | Tổng quan |

> Khi docs lệch code → **tin code trên disk**.

## Luật as-is

| Job | Khi | Việc | DB / output |
|-----|-----|------|-------------|
| **Job 1** | Thủ công + **cron tuần** (CN 02:00 VN, chế độ đêm, `MarketJobs:History:WeeklyRefreshEnabled`) | Listing + backfill OHLCV + universe | `Stocks`, `IsActive` |
| **Job 2** | ~5 phút trong giờ GD (`IntervalMinutes`) + cron 15:00 | Append nến T **thô**; Darvas alert | History ngày T |
| **Phân tích V1** | ~11:30 + ~15:05 VN; **intraday 15'** (9:00–11:30 & 13:00–14:45, selection-only) | SmartMoney Top (gate chia chác FireAnt đầu loop → rank ML + bonus ngành → hygiene → sector cap → MaxResults=5); criterion T+2.5; sector wave; SetupTracks | `DailyOpportunities`, `DailyAnalysisRuns.GateStatsJson` (`sap-chot-quyen`) |
| **Monitor** | ~60s trong phiên T+1 | Quét intraday Top: lô lớn / VSA / dòng tiền → SignalR (KHÔNG còn bắn tin VIP / vị thế — spec 011) | SignalR (`TradeEventCreated`) |
| **V3 trước phiên** | **08:30** T2–T6 | Nhịp nhẹ (~200 mã UNI đã lưu): chấm KB1/KB2 trên giá đóng cửa phiên trước (chỉ số/MA50 đọc chuỗi đã lưu) → chọn lệnh theo danh mục → tín hiệu + vị thế CHỜ MUA ATO + Telegram mua | `TinHieuV3` / `ViTheV3` |
| **V3 trong phiên** | **mỗi 1 phút** 09:00–14:45 (`V3:IntervalPhut`) | Nhịp nhẹ nhất (chỉ quote mã đang giữ, không nạp lịch sử): khớp CHỜ MUA tại giá mở cửa (hủy nếu đúng giá trần FR-014) + xét **BÁN realtime** so giá khớp với **ngưỡng đã lưu** `NguongChamMa` (hoặc N3 ATO theo lịch) | `ViTheV3` (update) + Telegram bán |
| **V3 ATC** | **14:25** T2–T6 | Nhịp nhẹ (~200 mã UNI đã lưu + mã đang giữ): chấm KB4/N1/N3 bằng giá khớp hôm nay (nến ghép với **khối lượng khớp tích lũy**, chỉ số **cộng điểm hôm nay**) → mua ATC ngay phiên tín hiệu; vị thế hết hạn giữ → bán ATC | `TinHieuV3` / `ViTheV3` |
| **V3 sau phiên** | **16:00** T2–T6 | Nhịp nạp **đầy đủ duy nhất**: quét toàn universe một lần → lưu chỉ số hôm nay + UNI phiên sau; tính sẵn ngưỡng bán (TB 9/19 đóng cửa trước) + giá tham chiếu + sàn cho vị thế mở; cập nhật số phiên đã giữ | `ChiSoNgayV3` + `ViTheV3` (update) |

> Chi tiết từng nhịp + quy tắc danh mục: mục **“Luồng V3 — sự thật chuẩn duy nhất”** dưới (canon duy nhất).

API tiện: `POST .../jobs/daily` = Job 2 + phân tích. Header `X-Sync-Key`.

**OHLCV lưu kho = giá khớp thô.** Job 1 / Job 2 không nhân hệ số vào nến. `%` / RS / FOMO lúc chấm điểm dùng `LayLichSuChamDiem` (cổ tức + thưởng + **quyền mua trả tiền**). Nạp: chi tiết mã → **Sự kiện quyền**. Seed HCM 05/02 + 16/07 (4:1 giá 10.0), SSI 17/08.

**Cron tuần Job 1 (từ 2026-08):** Job 2 chỉ append giá cho mã `IsActive=1` (`GetActiveSymbolsAsync`), nên mã bị rescreen loại nhầm hoặc đủ điều kiện trở lại không tự khôi phục được (mã inactive không có nến mới để re-đánh giá). Job 1 chạy hàng tuần (chế độ đêm) để refetch full lịch sử mọi mã niêm yết + rescreen, phá vòng chết này. Cấu hình: `HistoryJobOptions.WeeklyRefreshEnabled/Day/Hour/Minute` (mặc định bật, Chủ Nhật 02:00 VN); tắt qua `MarketJobs:History:Enabled=false` hoặc `WeeklyRefreshEnabled=false`.

**Intraday Top refresh (15 phút, 9:00–11:30 & 13:00–14:45 VN):** cùng `DailyAnalysisJob` trigger `intraday` — chỉ chọn Top + ghi `DailyOpportunities` / Early Recovery (`runPostProcessing=false`, `includeStructureAndTracking=false`). Bỏ qua nghỉ trưa và ngoài khung. **Không** chạy Job 2, SetupTracks, shadow/criterion/T+2.5. Bản đầy đủ vẫn ở ~11:30 (Job 2 + Top) và ~15:05 (Job 2 + Top + structure + post-processing).

### Luồng bộ kịch bản V3 — **SỰ THẬT CHUẨN DUY NHẤT**

> Mục này là canon **duy nhất** cho luồng vận hành V3 as-is (hiệu lực từ spec 012, 2026-10-26 — V2 gỡ toàn bộ). Mọi chỗ khác (architecture, README, ai-context, [`specs/012-v3-scenario-engine/spec.md`](../../specs/012-v3-scenario-engine/spec.md)) chỉ tóm tắt/index và **trỏ về đây** — đổi hành vi V3 → sửa mục này trước tiên. Nguồn luật chuẩn (normative): [`../features/v3-scenario-engine/research/`](../features/v3-scenario-engine/research/) (01-phuong-phap · 02-so-phat-hien · 03-ket-qua) + Phụ lục A/C/D/H spec 012.

V3 chạy **song song V1**, 5 kịch bản **KB1** (hoảng loạn) · **KB2** (chỉ số thủng MA50) · **KB4** (ngành bán quá mức) · **N1** (giải chấp cạn kiệt) · **N3** (đón dòng tiền đầu tháng), trên bảng `TinHieuV3` (khóa tự nhiên **Symbol + Loai + NgayTinHieu** — mỗi mã × mỗi kịch bản × mỗi phiên một tín hiệu) + `ViTheV3` (vị thế) + `ChiSoNgayV3` (chỉ báo thị trường ngày, upsert một lần/phiên).

**Vòng đời**: tín hiệu `DaBao` → vào lệnh `DaVaoLenh` (vị thế CHỜ MUA → ĐANG GIỮ → ĐÃ THOÁT; hoặc `BiHuy` vì giá mở cửa đúng giá trần — FR-014) → thoát realtime/ATC → `V3ChamKetQua` chốt kết quả Thắng/Thua/Hoà vốn (lãi ròng so ngưỡng ±0,05%, phí khứ hồi 0,4%).

**Quy tắc danh mục (Phụ lục A/C):** mỗi kịch bản tối đa **top 5** ứng viên/phiên; ưu tiên KB1 > KB2 > KB4 > N1 > N3; dedup một mã chỉ thuộc kịch bản ưu tiên cao nhất; tối đa **5 vị thế**; mỗi lệnh = giá trị danh mục / 5, riêng **KB1/KB2/N1 ×1,5** (≈30% danh mục), KB4/N3 ×1,0; không vượt tiền mặt còn lại; không mua mã đang giữ.

**Chỉ báo toàn thị trường (FR-001/FR-003):** chỉ số bình quân đều cộng lợi nhuận ngày của các mã **thuộc UNI tính tại phiên liền trước** (research 01: `R.where(UNI.shift())` — TV20 ≥ 5 tỷ và close ≥ 5 cùng phiên đó), **không** dùng tập UNI của phiên tín hiệu cho cả lịch sử. Luật này nằm trong `V3ChiSoToanThiTruong.TinhDiemTheoTruc` — **một luật, hai cửa gọi**: `Tinh` (một phiên, cho bộ kịch bản) và `TinhChuoi` (mọi phiên trên trục, cho nhịp nạp chuỗi). **SECR20 = trung vị RET20 của ngành**; mã **chưa có ngành** → SECR20 = null (không lấy RET20 của chính nó thay thế) nên **rớt KB4 và N1**.

**Sắp xếp tải dữ liệu (owner chốt 26/10/2026 — thay các bàn tán về hiệu năng/lỗi chỉ số trước đó):** chỉ **một nhịp nạp đầy đủ** là 16:00; ba nhịp còn lại đọc kết quả đã lưu.

| Nhịp | Tập mã nạp | Chỉ số / MA50 | Bán realtime |
|------|-----------|---------------|--------------|
| 16:00 (`DayDu`) | `LayCacMaAsync` toàn universe (một lần duy nhất trong ngày) | Tính từ đầu chuỗi + **ghi `ChiSoNgayV3`**: giá trị hôm nay **và `DanhSachUni` cho phiên sau** | Tính sẵn `NguongChamMa` (TB 9 hoặc 19 đóng cửa trước) + `GiaThamChieu` + `Exchange` → ghi `ViTheV3` |
| 08:30 · 14:25 (`NhipNhe`) | `LayMaNhipNheAsync` — chỉ ~200 mã theo `DanhSachUni` đã lưu (+ mã đang giữ ở nhịp ATC) | **Đọc chuỗi đã lưu** (`ChiSoNgayV3`, 60 phiên gần nhất), không ghi lại lịch sử; riêng nhịp ATC **cộng điểm hôm nay** = mức lưu × (1 + `ChiSoLoiNhuanHomNay`) | Đọc `NguongChamMa` đã lưu |
| Mỗi phút (`NhipNhe`) | **KHÔNG nạp lịch sử, KHÔNG gọi `LayCacMaAsync`** — chỉ quote cho mã đang giữ | không cần | So giá khớp với `NguongChamMa` đã lưu (`V3LuatBanThucTe.XetTheoNguongDaLuu`) + trần/sàn từ `GiaThamChieu`, `Exchange` |

Nhịp 14:25 ghép nến hôm nay bằng **khối lượng khớp tích lũy** từ bảng giá (không truyền `0L`) → TV20/UNI của phiên ATC đúng. Mỗi nhịp **log thời gian chạy** (`Stopwatch` → "V3 …: nhịp chạy hết {Ms} ms"). Khi deploy chạy **một lần** `POST /api/v1/market/jobs/v3-nap-chi-so?tuNgay=` (mặc định `V3:TuNgayNapChiSo` = 01/01/2023) để dựng sẵn chuỗi chỉ số + UNI theo ngày vào `ChiSoNgayV3` — **không** tạo tín hiệu/vị thế cho các ngày đã qua.

**Luật bán realtime (FR-015..018, V3-B1):** bán khi **giá khớp ≥ max(ngưỡng chạm MAn, giá vào ×1,004)**; MAn theo kịch bản — KB1/KB2/KB4 MA10, N1 MA20, N3 không có MA (chỉ ATO theo lịch TDM=2 tháng sau); giữ tối đa KB1/KB2/N1 15 · KB4 10 · N3 12 phiên → hết hạn bán ATC; **T+2.5 (FR-017) = đúng phiên T+2 chỉ bán từ đầu buổi chiều (`V3:GioBanPhieuTplus2`, mặc định 13:00), từ phiên T+3 bán cả ngày**; **sàn trắng** (khối lượng phiên = 0) chặn mọi lệnh bán; không bao giờ bán theo "giá đóng cửa rồi phiên sau bán".

| Nhịp | Runner · lịch Quartz | Việc (as-is) | Output |
|------|----------------------|--------------|--------|
| **1 — Trước phiên** | `V3TruocPhienRunner` · **08:30** T2–T6 (`0 30 8 ? * MON-FRI` VN) | Nhịp **NHẸ**: nạp ~200 mã theo `DanhSachUni` đã lưu, chỉ số/MA50 đọc chuỗi đã lưu (phiên tín hiệu T = phiên qua, không cộng điểm hôm nay) → `V3BoKichBan.Cham` → chỉ lấy ứng viên `MuaMoCuaPhienSau` (KB1/KB2) → `V3DanhMucXepHang.Chon` (dedup + cắt theo slot/tiền mặt) → ghi tín hiệu + vị thế CHỜ MUA khớp ATO mở cửa hôm nay → Telegram `V3TelegramFormatter` mua. Đồng thời nhắc ATO bán N3 đúng phiên TDM=2 tháng sau | `TinHieuV3` / `ViTheV3` CHỜ MUA |
| **2 — Trong phiên** | `V3TrongPhienRunner` · **mỗi `V3:IntervalPhut` = 1 phút**, cron `9-14h` T2–T6; tựskip ngoài 09:00–14:45 / ngày nghỉ | Nhịp **NHẸ NHẤT**: chỉ quote cho mã đang giữ, **không** `LayCacMaAsync`/không nạp lịch sử. ① Khớp CHỜ MUA tại giá mở cửa KBS — rơi **đúng giá trần** → hủy (FR-014, trần tính từ `GiaThamChieu`+`Exchange` đã lưu) ② Xét BÁN mọi vị thế ĐANG GIỮ bằng `V3LuatBanThucTe.XetTheoNguongDaLuu` trên `NguongChamMa` đã lưu (chưa có ngưỡng → fallback giá vào ×1,004; ngưỡng = 0 → giữ, không bán oan): ChamMA; N3 theo lịch ATO; hết hạn nhường nhịp ATC; sàn trắng chặn bán; chưa đủ T+2.5 (gồm **sáng phiên T+2**) → chưa được bán | `ViTheV3` update + Telegram bán |
| **3 — ATC** | `V3AtcRunner` · **14:25** T2–T6 | Nhịp **NHẸ**: nạp ~200 mã UNI đã lưu + mã đang giữ; ghép nến hôm nay bằng **khối lượng khớp tích lũy** (không truyền `0L`); MA50 từ chuỗi đã lưu + **cộng điểm hôm nay**. Chấm KB4/N1/N3 bằng giá khớp hiện tại (tín hiệu = phiên hôm nay) → mua ATC ngay phiên tín hiệu (hủy nếu giá trần) + lưu sẵn ngưỡng bán; vị thế ĐANG GIỮ hết hạn giữ → bán ATC nếu đã qua T+2.5 (cũng theo ngưỡng đã lưu) | `TinHieuV3` / `ViTheV3` |
| **4 — Sau phiên** | `V3SauPhienRunner` · **16:00** T2–T6 | Nhịp **ĐẦY ĐỦ duy nhất**: quét toàn universe một lần → lưu `ChiSoNgayV3` (chỉ số hôm nay + **UNI cho phiên sau**); tính sẵn `NguongChamMa` (TB 9 hoặc 19 đóng cửa trước) + `GiaThamChieu` (đóng cửa hôm nay) + `Exchange` và cập nhật `SoPhienDaGi` cho mọi vị thế mở. Không bắn tin, không đổi trạng thái thoát. Kết quả đóng lệnh do `KhopBanAsync`/`V3ChamKetQua` chấm ngay lúc thoát (trong phiên/ATC) phục vụ màn Hiệu quả | `ChiSoNgayV3` + `ViTheV3` update |

**Kết nối:** Home Top vẫn **thuần V1** (`DailyOpportunities`); bắn tin VIP Master đã gỡ theo spec [`011-remove-v1-vip-alerting`](../../specs/011-remove-v1-vip-alerting/spec.md). V3 phục vụ: `GET /api/v1/v3/hieu-qua/tom-tat` · `lich-su` · `vi-the-dang-giu`, `GET /stocks/{sym}/v3` (màn chi tiết mã), `GET /nhat-ky/tin-hieu/v3` (nhật ký — FR-040, **không** còn đường dẫn tín hiệu V2). Trigger thủ công (header `X-Sync-Key`): `POST /api/v1/market/jobs/v3-truoc-phien` · `v3-trong-phien` · `v3-atc` · `v3-sau-phien` · `v3-nap-chi-so` (nạp chuỗi chỉ số + UNI một lần khi deploy). Config keys: section `V3` trong `appsettings.json` (`Enabled` · `IntervalPhut` · `TuNgayNapChiSo` · `GioBatDau/GioKetThuc` · ngưỡng từng kịch bản `Kb1*/Kb2*/Kb4*/N1*/N3*` · `BanPhanTramGiaVao` · `GiuToiDa*` · `Tplus25Sessions` · `GioBanPhieuTplus2` · `MaxViThe` · `TrongSoNhan` · `PhiKhuriHoi` · `NguongThang`).

> **Stop point (spec 012 Phụ lục G):** migration thêm 3 bảng V3 đã viết tay tại `Migrations/20261026130015_AddV3Tables.cs` (gồm các cột phục vụ split tải dữ liệu: `ChiSoNgayV3.DanhSachUni`, `ViTheV3.GiaThamChieu`, `ViTheV3.Exchange`) — **chưa apply trên production**; DB dev local đã tự apply khi chạy API (`DatabaseInitializer.MigrateAsync`). Bảng V2 cũ `KetQuaKichBan` vẫn còn trên cả hai DB; các dòng nhật ký gắn tín hiệu V2 cũ **giữ nguyên (phương án A — owner chốt 26/10/2026)**; migration xóa `KetQuaKichBan` **chưa viết**, chờ owner duyệt.

Deploy: `.\scripts\ship-all.ps1`. ML/HPO: xem architecture + stub lịch sử `pipeline-jobs` đã gộp Phase 2–3 vào đây (dataset/train/monitor-ranker).

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-PL-1 | Tên endpoint lịch sử `/jobs/daily-pipeline` không còn — dùng `/jobs/daily` | As-is |
| G-PL-2 | Chưa phát hiện thêm mâu thuẫn lịch vs code trong feature này | Cập nhật khi đổi Quartz |
| G-PL-3 | Job 2 chỉ OHLCV thô | Đúng thiết kế — điều chỉnh lúc tính % qua `su-kien-quyen.json` (màn Sự kiện quyền) |

## Tài liệu liên quan

- [`buy-decision.md`](./buy-decision.md)
- Điều chỉnh quyền: [`../../specs/005-ohlcv-corporate-adjust/spec.md`](../../specs/005-ohlcv-corporate-adjust/spec.md)
- [`../architecture.md`](../architecture.md), [`../build-and-deploy.md`](../build-and-deploy.md)
- Index: [`../README.md`](../README.md)

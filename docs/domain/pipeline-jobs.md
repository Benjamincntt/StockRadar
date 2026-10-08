# Pipeline jobs (dữ liệu & phân tích)

## Mục đích

Thứ tự và trách nhiệm Job 1 / Job 2 / phân tích daily / monitor VIP / ML·HPO — **as-is** vận hành.

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | Market job controllers / `POST /api/v1/market/jobs/*` | Trigger thủ công |
| 2 | `DailySessionSyncRunner` | Job 2 + Darvas alerts |
| 3 | `DailyAnalysisRunner` | Top V1 + criterion + sector wave + SetupTracks |
| 4 | `OpportunityIntradayMonitorRunner` | Job 3 / VIP ~60s |
| 5 | `Pha1TruocPhienRunner` / `Pha2TrongPhienRunner` / Pha 3 đo lường | Pipeline V2 Scenario Engine |
| 6 | `QuartzSchedulingExtensions.cs` | Lịch Quartz thật (VN timezone) |
| 7 | `docs/architecture.md` | Tổng quan |

> Khi docs lệch code → **tin code trên disk**.

## Luật as-is

| Job | Khi | Việc | DB / output |
|-----|-----|------|-------------|
| **Job 1** | Thủ công + **cron tuần** (CN 02:00 VN, chế độ đêm, `MarketJobs:History:WeeklyRefreshEnabled`) | Listing + backfill OHLCV + universe | `Stocks`, `IsActive` |
| **Job 2** | ~5 phút trong giờ GD (`IntervalMinutes`) + cron 15:00 | Append nến T **thô**; Darvas alert | History ngày T |
| **Phân tích V1** | ~11:30 + ~15:05 VN; **intraday 15'** (9:00–11:30 & 13:00–14:45, selection-only) | SmartMoney Top (gate chia chác FireAnt đầu loop → rank ML + bonus ngành → hygiene → sector cap → MaxResults=5); criterion T+2.5; sector wave; SetupTracks | `DailyOpportunities`, `DailyAnalysisRuns.GateStatsJson` (`sap-chot-quyen`) |
| **Monitor** | ~60s trong phiên T+1 | VIP trên Top | Telegram / SignalR / positions |
| **Pha 1 (V2)** | **08:30** T2–T6 | Sơ tuyển (~1500 → ~70) + đánh giá Bối cảnh/Hình thái → WATCHING/FORMING | `KetQuaKichBan` |
| **Pha 2 (V2)** | **mỗi 1 phút** 09:00–14:45 (`Pha2:IntervalPhut`) | Cò kích hoạt realtime → TRIGGERED + xếp hạng + **Telegram mua/sell** (mua chặn theo chia chác FireAnt) | `KetQuaKichBan` (update) |
| **Pha 3 (V2)** | **16:00** T2–T6 | Đo outcome T+3 → chốt state | `KetQuaKichBan` / hiệu quả |

> Chi tiết từng bước + vòng đời state V2: mục **“Luồng V2 Scenario Engine — sự thật chuẩn”** dưới (canon duy nhất).

API tiện: `POST .../jobs/daily` = Job 2 + phân tích. Header `X-Sync-Key`.

**OHLCV lưu kho = giá khớp thô.** Job 1 / Job 2 không nhân hệ số vào nến. `%` / RS / FOMO lúc chấm điểm dùng `LayLichSuChamDiem` (cổ tức + thưởng + **quyền mua trả tiền**). Nạp: chi tiết mã → **Sự kiện quyền**. Seed HCM 05/02 + 16/07 (4:1 giá 10.0), SSI 17/08.

**Cron tuần Job 1 (từ 2026-08):** Job 2 chỉ append giá cho mã `IsActive=1` (`GetActiveSymbolsAsync`), nên mã bị rescreen loại nhầm hoặc đủ điều kiện trở lại không tự khôi phục được (mã inactive không có nến mới để re-đánh giá). Job 1 chạy hàng tuần (chế độ đêm) để refetch full lịch sử mọi mã niêm yết + rescreen, phá vòng chết này. Cấu hình: `HistoryJobOptions.WeeklyRefreshEnabled/Day/Hour/Minute` (mặc định bật, Chủ Nhật 02:00 VN); tắt qua `MarketJobs:History:Enabled=false` hoặc `WeeklyRefreshEnabled=false`.

**Intraday Top refresh (15 phút, 9:00–11:30 & 13:00–14:45 VN):** cùng `DailyAnalysisJob` trigger `intraday` — chỉ chọn Top + ghi `DailyOpportunities` / Early Recovery (`runPostProcessing=false`, `includeStructureAndTracking=false`). Bỏ qua nghỉ trưa và ngoài khung. **Không** chạy Job 2, SetupTracks, shadow/criterion/T+2.5. Bản đầy đủ vẫn ở ~11:30 (Job 2 + Top) và ~15:05 (Job 2 + Top + structure + post-processing).

### Luồng V2 Scenario Engine — **SỰ THẬT CHUẨN DUY NHẤT**

> Mục này là canon **duy nhất** cho luồng vận hành V2 as-is (đối chiếu code 2026-10-06). Mọi chỗ khác (architecture, README, ai-context, [`features/v2-scenario-engine/spec.md`](../features/v2-scenario-engine/spec.md)) chỉ tóm tắt/index và **trỏ về đây** — đổi hành vi V2 → sửa mục này trước tiên.

V2 chạy **song song V1 từ 2026-09**, trên bảng `KetQuaKichBan` (khóa tự nhiên **Symbol + LoaiKichBan + NgayDanhGia** — mỗi mã × mỗi kịch bản × mỗi ngày một record upsert mới).

**Vòng đời trạng thái** (`TrangThaiKichBan`): `DangTheoDoi` (WATCHING) → `DangHinhThanh` (FORMING) → `DaKichHoat` (TRIGGERED, coi như HOLDING) → Pha 3 đo xong chốt `TakeProfit` / `Invalidated` / `Exit`.

| Pha | Runner · lịch Quartz | Việc (as-is) | Output |
|-----|----------------------|--------------|--------|
| **1 — Trước phiên** | `Pha1TruocPhienRunner` · **08:30** T2–T6 (`0 30 8 ? * MON-FRI` VN) | Sơ tuyển `SoTuyen` (~1500 → ~70: GTGD TB20 ≥ 10 tỷ, vốn hóa ≥ 500 tỷ, lịch sử ≥ 250 phiên) → mỗi mã đánh giá **Bối cảnh + Hình thái** của 5 kịch bản buy (`NoHuongLen` · `HoiHoTro` · `QuetThanhKhoan`) + 2 nền (`KietSuc` · `GayNen`) → ghi state | `KetQuaKichBan` WATCHING/FORMING |
| **2 — Trong phiên** | `Pha2TrongPhienRunner` · **mỗi `Pha2:IntervalPhut` = 1 phút**, cron `9-14h` T2–T6; job tự skip ngoài 09:00–14:45 / ngày nghỉ | ① Duyệt FORMING hôm nay, giá+vol realtime **KBS** (batch 50), khớp **Cò kích hoạt** đã tính sẵn → TRIGGERED (tính Entry/SL/TP, snapshot 13 chỉ báo) ② Xếp hạng 6 tiêu chí (`XepHangCoHoiService` — **không veto**, chỉ sort) ③ Telegram `V2TelegramFormatter.FormatMua` — **bỏ mã trong khoảng chia chác [chốt quyền → thực hiện quyền]** theo lịch quyền FireAnt (vẫn lưu TRIGGERED vào DB; fail-open) ④ Bán (2026-10-08, chi tiết [`v2-sell-fix`](../features/v2-sell-fix/spec.md) + [`v2-sell-plan-tracking`](../features/v2-sell-plan-tracking/spec.md)): mã `DaKichHoat` chưa đo (**không lọc theo ngày**, trừ `KietSuc`/`GayNen`, `KetQuaDoLuong == null`, `ThoiGianKichHoat != null`) → gom theo mã, giá vào lấy từ bản ghi mua mới nhất → vị thế đã thoát hết thì bỏ qua. **Trước tiên** so giá khớp với `GiaDungLo` / `GiaChotLoi1` / `GiaChotLoi2` của kế hoạch: chạm dừng lỗ → bán hết; chạm chốt lời 1 → bán nửa, dời dừng lỗ về giá vào; chạm chốt lời 2 → bán phần còn lại (`FormatChamMucGia`). **Sau đó** Kiệt sức (bán nửa, không dời dừng lỗ) / Gãy nền (bán hết) qua `DanhGiaBanAsync(symbol, history, giaVao)` — **truyền giá vào, không phải giá hiện tại** → `FormatBan`. Hai luồng ghi chung bộ cột `GiaBanNua`/`ThoiGianBanNua`/`DaDoiDungLo`/`GiaThoatHet`/`ThoiGianThoatHet`/`LyDoThoatHet`. Chưa đủ T+2.5 (`Pha2:MinTradingSessionsToSell`) → `FormatCanhBaoChuaBanDuoc` một lần mỗi sự kiện (ghi `CanhBaoDaGui`), không đổi trạng thái vị thế. `TelegramNotifier` nuốt lỗi nên gửi hỏng vẫn bị ghi là đã báo (rủi ro đã chấp nhận). **Không chặn chia chác** — bán là bảo vệ vị thế | `KetQuaKichBan` TRIGGERED + alert |
| **3 — Đo lường** | `Pha3DoLuongRunner` · **16:00** T2–T6 | Đo outcome kịch bản đã trigger sau T+3 phiên: **giá thoát ưu tiên từ Pha 2** (`GiaBanNua`/`GiaThoatHet` → TB, chỉ `GiaThoatHet` → dùng nó, chỉ `GiaBanNua` → TB với close, không có → close) — áp dụng từ **2026-10-08** (phương án B). Có giá thoát từ Pha 2 thì chỉ so % lãi với ngưỡng ±1% (không xét riêng lý do thoát); không có thì giữ cách cũ (so chốt lời 1 / dừng lỗ / ±1%). `TrangThai` theo kết quả: Thắng → `ChotLoi`, Thua → `HuyLenh`, Ngang → giữ nguyên. Lưu %LN + R:R thực tế | Nền tảng màn **Hiệu quả** |

**Kết nối:** Home Top + VIP alerts (Master L1/L2) vẫn **thuần V1** (`DailyOpportunities`) — V2 không cấp Top trang chủ. V2 phục vụ: `GET /api/v1/kich-ban/xep-hang` (Top `XepHang:SoLuongTop` = 5 mã TRIGGERED hôm nay), `GET /stocks/{sym}/kich-ban` (màn chi tiết mã), `GET /hieu-qua/tom-tat` · `lich-su` · `chi-tiet/{id}`. Trigger thủ công (header `X-Sync-Key`): `POST /api/v1/market/jobs/pha1-truoc-phien` · `pha2-trong-phien` · `pha3-do-luong`. Config keys: [`../ai-context.md`](../ai-context.md) (section `SoTuyen` · `KichBan` · `XepHang` · `Pha2`). Gate cleanup 2026-09 ở V1 (xóa 6 cổng, nới sóng ngành RS 2%→0%) bớt trùng việc kịch bản V2 đã phủ — xem [`buy-decision.md`](./buy-decision.md).


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

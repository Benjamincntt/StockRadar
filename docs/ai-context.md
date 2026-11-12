# StockRadar — AI Context (tham chiếu chính cho AI agent)

> **Mục đích:** tài liệu AI agent đọc trước khi làm việc trên StockRadar — pipeline flow, gates, config keys, API endpoints, UI structure.
> **Nguồn sự thật:** code trên disk (`backend/`, `frontend/`, `mobile/`). Khi doc lệch code → **tin code**, rồi cập nhật doc.
> **Cập nhật:** 2026-11-12 (spec 016 — V1 đã gỡ toàn bộ: Top, Buy Score, ML/HPO, radar, theo dõi lô lớn). Trước đó 2026-10-26 (spec 012 — V3 thay V2), 2026-10-06 (cổng chia chác FireAnt).
> Chi tiết luật đầy đủ nằm trong [`domain/`](./domain/) — file này là bản đối chiếu nhanh, không thay domain docs.

## 1. Bản đồ repo

| Thư mục | Nội dung |
|---------|----------|
| `backend/StockRadar.Domain` | Engines: `V3BoKichBan`, `V3LuatBanThucTe`, `V3DanhMucXepHang`, `V3ChamKetQua`, `V3ChiSoToanThiTruong`, `StockUniverseFilter`, `IndicatorMath` |
| `backend/StockRadar.Application` | `Options/MarketJobsOptions.cs` (config schema), DTOs, services |
| `backend/StockRadar.Infrastructure` | Runners: `V3TruocPhienRunner.cs`, `V3TrongPhienRunner.cs`, `V3AtcRunner.cs`, `V3SauPhienRunner.cs` (+ `V3Engine.cs` dùng chung), `Scheduling/Jobs/*`; Persistence; `Scheduling/QuartzSchedulingExtensions.cs` |
| `backend/StockRadar.Api` | Controllers, `appsettings.json` (config prod) |
| `frontend/` | Web Vite React |
| `mobile/` | Flutter (go_router) |
| `scripts/` | `ship-all.ps1` (deploy) |

Production: API `http://103.226.248.6/api/v1` · Web `https://stock.baobiantea.com/` · API local `http://localhost:5280`.

## 2. Pipeline hiện tại (sau spec 016)

| | Pipeline dữ liệu + V3 |
|--|----------------------|
| Job 1 | `HistoryBackfillJob` — universe + lịch sử (thủ công / startup / cron CN 02:00) |
| Job 2 | `DailySessionSyncJob` — 15:05 + thử lại 15:15/15:30, append nến ngày T (gồm ATC) |
| KBS sync | `KbsMarketSyncJob` — 60s, chỉ trong khung 08:59–11:31 · 12:59–15:10 |
| V3 | 4 nhịp: **08:30** · **mỗi phút 09:00–14:45** · **14:25** · **16:00** → `TinHieuV3` + `ViTheV3` + `ChiSoNgayV3` |
| Phục vụ | `GET /v3/hieu-qua/*`, `GET /stocks/{sym}/v3`, `GET /nhat-ky/*`, Home "Đang giữ" |

**V2 đã gỡ theo spec 012. V1 đã gỡ toàn bộ theo spec 016** — chi tiết giờ chạy và file nguồn: [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md).

## 3. V1 — Daily Analysis (ĐÃ GỠ toàn bộ, spec 016)

`DailyAnalysisRunner`, `BuyDecisionEngine`, `DarvasBreakoutAnalyzer`, `MarketPhaseClassifier`, ML ranker/HPO, radar trong phiên và theo dõi lô lớn đã xoá tháng 11/2026. Cách V1 lọc/chấm điểm (7 gate trong loop, 4 cổng mua, Buy Score 8 tiêu chí, ML ranking + sector bonus, hygiene, sector cap, Top 5, gateStats): [`features/v1-archive/`](./features/v1-archive/README.md).

> Đừng khôi phục các gate/Buy Score này. Luồng duy nhất còn chạy: mục 4 (V3) + Job 1 / Job 2 / KBS sync.

## 4. Pipeline V3 — Bộ kịch bản P1++ (spec 012)

| Nhịp | Runner | Lịch | Việc |
|-----|--------|------|------|
| 1 — Trước phiên | `V3TruocPhienRunner` | 08:30 T2–T6 (cron `0 30 8 ? * MON-FRI`) | Chỉ báo toàn thị trường trên nến hoàn thành tới hôm qua → `V3BoKichBan.Cham` (KB1/KB2/KB4/N1/N3, top 5 mỗi kịch bản) → ứng viên mua mở cửa phiên sau → `V3DanhMucXepHang.Chon` → ghi `TinHieuV3` + `ViTheV3` CHỜ MUA → Telegram mua |
| 2 — Trong phiên | `V3TrongPhienRunner` | mỗi 1 phút 09:00–14:45 (cron `0 0/1 9-14 ? * MON-FRI`) | Khớp CHỜ MUA tại giá mở cửa (hủy nếu đúng giá trần) · xét **bán realtime** trên giá khớp: `V3LuatBanThucTe.Xet` → giá khớp ≥ max(chạm MAn, giá vào ×1,004), cửa T+2.5 mở **từ 13:00 của phiên T+2** |
| 3 — ATC | `V3AtcRunner` | 14:25 T2–T6 (cron `0 25 14 ? * MON-FRI`) | Chấm KB4/N1/N3 bằng giá khớp hôm nay → mua ATC; vị thế hết hạn giữ → bán ATC (cần qua T+2.5) |
| 4 — Sau phiên | `V3SauPhienRunner` | 16:00 T2–T6 (cron `0 0 16 ? * MON-FRI`) | Đóng ngày: cập nhật số phiên đã giữ + ngưỡng chạm MAn cho vị thế mở |

**5 kịch bản (`LoaiKichBanV3`)**: `KB1` hoảng loạn · `KB2` chỉ số thủng MA50 · `KB4` ngành bán quá mức (nhóm mua ATC được gọi là “nền nến nhân” ×1,5 cùng KB1/KB2) · `N1` giải chấp cạn kiệt · `N3` đón dòng tiền đầu tháng.

**Kết quả** (`V3ChamKetQua`): lãi ròng = giá thoát/giá vào − 1 − phí khứ hồi 0,4%; xếp Thắng/Thua/Hoà vốn theo ngưỡng ±0,05%.

Canon duy nhất luồng V3 as-is: [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md#luồng-bộ-kịch-bản-v3--sự-thật-chuẩn-duy-nhất) · spec: [`specs/012-v3-scenario-engine/spec.md`](../specs/012-v3-scenario-engine/spec.md) · luật chuẩn (normative): [`features/v3-scenario-engine/research/`](./features/v3-scenario-engine/research/).

V2 (đã gỡ theo spec 012) từng chạy song song V1 từ 2026-09; toàn bộ code/config/docs V2 đã bị loại — **đừng khôi phục**.


## 5. Quartz jobs (`QuartzSchedulingExtensions.cs`, VN timezone)

| Job ID | Lịch |
|--------|------|
| `history-backfill` | thủ công / `RunOnStartup` (mặc định tắt) / cron CN 02:00 (`MarketJobs:History:WeeklyRefresh*`) |
| `daily-session-sync` | từ `DailySession.Hour:Minute` +5/+15/+30 phút → 15:05 · 15:15 · 15:30, T2–T6 |
| `kbs-market-sync` | ~60 giây; Job tự chặn ngoài khung 08:59–11:31 · 12:59–15:10 |
| `v3-truoc-phien` | `0 30 8 ? * MON-FRI` |
| `v3-trong-phien` | `0 0/{IntervalPhut} 9-14 ? * MON-FRI` |
| `v3-atc` | `0 25 14 ? * MON-FRI` |
| `v3-sau-phien` | `0 0 16 ? * MON-FRI` |

`GET /market/jobs` chỉ liệt kê 3 job pipeline trong `JobCatalog.All`; 4 job V3 không nằm trong catalog.

API trigger (header `X-Sync-Key`): `POST /api/v1/market/jobs/{history|history/night|session|universe-rescreen|kbs-sync|v3-truoc-phien|v3-trong-phien|v3-atc|v3-sau-phien|v3-nap-chi-so}`.

## 6. API endpoints (`/api/v1`)

| Nhóm | Endpoint | Ghi chú |
|------|----------|---------|
| Market | `GET /market` · `/market/vnindex/chart` · `/market/quotes` · `/market/sparklines` · `/market/stock-search` | Tổng quan · VNINDEX · bảng giá · sparkline · tìm mã |
| Market jobs | `GET /market/jobs` · `GET /market/jobs/history/status` | Trạng thái + lịch sử Job 1 |
| | `POST /market/jobs/{history|history/night|session|universe-rescreen|kbs-sync|v3-truoc-phien|v3-trong-phien|v3-atc|v3-sau-phien|v3-nap-chi-so}` | Chạy thủ công, header `X-Sync-Key` |
| Watchlists | `GET/POST /watchlists` · `GET/PATCH/DELETE /watchlists/{id}` | Tất cả danh sách của user — **lazy seed** (default + 30 ngành) lần GET đầu |
| | `GET /watchlists/{id}/items` | Items (enrich giá + `TrangThaiV3`); danh sách ngành = items động |
| | `PUT/DELETE /watchlists/{id}/items/{symbol}` | Thêm / xóa mã |
| | `GET /watchlist-items` · `PUT/POST /watchlist-items/{symbol}` · `DELETE /watchlist-items/{symbol}` | Backward compat — thao tác trên **danh sách mặc định** |
| Kịch bản V3 | `GET /v3/hieu-qua/tom-tat?tu=&den=` | Tóm tắt theo kịch bản + danh mục |
| | `GET /v3/hieu-qua/lich-su?page=&size=&status=mo|dong|huy` · `GET /v3/hieu-qua/vi-the-dang-giu` | Lịch sử lệnh phân trang · vị thế mở (Home "Đang giữ") |
| Nhật ký | `GET/POST /nhat-ky/lenh` · `GET/PUT/DELETE /nhat-ky/lenh/{id}` · `GET /nhat-ky/vi-the` · `GET /nhat-ky/thong-ke` | Sổ lệnh + đối chiếu vị thế |
| | `GET /nhat-ky/tin-hieu/v3` · `PUT/DELETE /nhat-ky/tin-hieu/{nguon}/{maTinHieu}/bo-qua` | Picker tín hiệu V3 (FR-040 — **không** còn đường dẫn V2) |
| Stocks | `GET /stocks/search` · `GET /stocks/{symbol}` · `/{symbol}/chart` | Tìm mã · chi tiết · nến (không còn Buy Score) |
| | `GET /stocks/{symbol}/v3` | Tín hiệu V3 mới nhất của 1 mã (mảng rỗng nếu chưa có) |
| | `PATCH /stocks/{symbol}/sector` · `GET|POST /stocks/{symbol}/rights-events` | Sửa ngành · sự kiện quyền |
| Khác | `GET /sectors/catalog` · `POST /thiet-bi/dang-ky|go|gui-thu` · `POST /auth/tokens` · `POST /users` | Ngành · thiết bị nhận push · đăng nhập · đăng ký |

## 7. UI structure

**Mobile (Flutter, go_router):** bottom nav **3 tab** — `Trang chủ` (`/`) · `Watchlist` (`/watchlist`) · `Hiệu quả` (`/performance`); màn push: `/stocks/:symbol` (Chi tiết — `GET /stocks/{symbol}` + `/stocks/{symbol}/v3`) và `/stocks/:symbol/su-kien-quyen`. Screens: `home_screen` · `watchlist_screen` · `hieu_qua_screen` · `stock_detail_screen` · `su_kien_quyen_screen` · `login_screen`.

**Web (Vite React):** `/` · `/stocks/:symbol` · `/stocks/:symbol/su-kien-quyen` · `/watchlist` · `/performance` · `/login`; `/radar`, `/heatmap` redirect về `/`.

**Đã dọn 2026-09:** mobile bỏ menu "Tác vụ", drawer, sidebar, tab "Khớp lệnh". Không còn màn Radar, Trades, Alerts, Phân tích chỉ báo trên UI.

## 8. Config reference

Nguồn: `backend/StockRadar.Api/appsettings.json` (mặc định trong repo). Production dùng `appsettings.Production.json` riêng nên có thể khác.

### `MarketJobs` (`MarketJobsOptions.cs`)

| Key | Repo | Ghi chú |
|-----|------|---------|
| `BypassApiCache` | true | Bỏ cache API khi đồng bộ |
| `History:RunOnStartup` | false | Job 1 chạy ngay khi API khởi động |
| `History:Groups` | HOSE, HNX, UPCOM | Universe = nhóm sàn |
| `History:MinClosePriceVnd` | 8000 | Lọc giá đóng cửa |
| `History:ExcludeIpoWithinDays` | 365 | Loại mã mới IPO |
| `History:WeeklyRefresh*` | CN 02:00 | Cron làm mới universe |
| `DailySession:Hour` / `Minute` | 15:00 | Mốc đóng cửa; Job 2 chạy +5/+15/+30 phút |
| `DailySession:BatchSize` | 40 | Số mã mỗi lô |

### `Push` · `TelegramNotify` · `RealizedPnl`

| Section | Key chính |
|---------|-----------|
| `Push` | `Enabled` true · `Firebase:CredentialPath` · `TimeoutGiay` 2 · `KhoiTaoLaiSauGiay` 300 |
| `TelegramNotify` | `Enabled` false · `BotToken` · `ChatId` — production bật bằng biến môi trường, không ghi vào file |
| `RealizedPnl` | `BuyFeePercent` 0,15 · `SellFeePercent` 0,25 · `SellTaxPercent` 0,1 · `WinThresholdPercent` 0 · `MeasureLookbackSessions` 500 — dùng cho phí trong Nhật ký (`NhatKyService.cs`) |

### V3 (section top-level trong appsettings — spec 012)

| Section | Key chính |
|---------|-----------|
| `V3` | `Enabled` true · `IntervalPhut` 1 · `GioBatDau` 09:00 · `GioKetThuc` 14:45 |
| Vũ trụ UNI | `Tv20MinUni` 5 · `CloseMinUni` 5 |
| Ngưỡng kịch bản | `Kb1Breadth50Max` 0,20 · `Kb1ChiSoDuoiMa50Min` 7 · `Kb1Tv20Min` 30 · `Kb2ChiSoDuoiMa50Min` 5 · `Kb2Tv20Min` 100 · `Kb4Secr20Max` −10 · `Kb4Tv20Min` 20 · `Kb4Breadth50Max` 0,35 · `N1Secr20Max` −12 · `N1Tv20Min` 10 · `N1LeftMax` 6 · `N3Left` 5 · `N3Tv20Min` 30 · `N3VungCamMa50` 1,5 |
| Bán + giữ | `BanPhanTramGiaVao` 1,004 · `GiuToiDaKb1/Kb2/N1` 15 · `GiuToiDaKb4` 10 · `GiuToiDaN3` 12 · `Tplus25Sessions` 2 · `GioBanPhieuTplus2` "13:00" (đúng phiên T+2 chỉ bán từ giờ này) |
| Danh mục | `MaxViThe` 5 · `TopMoiKichBan` 5 · `SoChiaViThe` 5 · `TrongSoNhan` 1,5 (KB1/KB2/N1) · `PhiKhuriHoi` 0,004 · `NguongThang` 0,0005 · `VonBanDau` 1.000.000 (chỉ hiển thị %) |

## 9. Doc map

| Chủ đề | File |
|--------|------|
| Mục lục | [`README.md`](./README.md) |
| Kiến trúc tổng quan | [`architecture.md`](./architecture.md) |
| Pipeline jobs hiện tại (Job 1 · Job 2 · KBS sync · 4 nhịp V3) | [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md) |
| Bộ kịch bản V3 (spec 012) — **luồng chuẩn** | [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md#luồng-bộ-kịch-bản-v3--sự-thật-chuẩn-duy-nhất) (spec normative: [`../specs/012-v3-scenario-engine/spec.md`](../specs/012-v3-scenario-engine/spec.md)) |
| Realized P&L | [`domain/realized-pnl.md`](./domain/realized-pnl.md) (tombstone) |
| Buy Score / cổng Top / VIP | **ĐÃ GỠ** (spec 016) — [`domain/buy-decision.md`](./domain/buy-decision.md) |
| MA stack & pha thị trường | **ĐÃ GỠ** (spec 016) — [`domain/ma-stack-and-market-phase.md`](./domain/ma-stack-and-market-phase.md) |
| Nền giá / flatBox | **ĐÃ GỠ** (spec 016) — [`domain/base-price-flatbox.md`](./domain/base-price-flatbox.md) |
| Cách V1 đã chạy (lưu trữ) | [`features/v1-archive/`](./features/v1-archive/README.md) |
| Deploy | [`build-and-deploy.md`](./build-and-deploy.md) |

## Phụ lục — AI tooling local (tiết kiệm token)

Setup một lần: `powershell -NoProfile -ExecutionPolicy Bypass -File "D:\Source\StockRadar\scripts\setup-ai-tools.ps1"`

| # | Công cụ | Vai trò |
|---|---------|---------|
| 1 | **Continue.dev** | Semantic index local (`%USERPROFILE%\.continue\index`); lỗi activating → `scripts\fix-continue-extension.ps1` + Reload Window |
| 2 | **CLAUDE.md + .cursor/rules** | Context cố định, mọi session agent |
| 3 | **Understand-Anything** | Knowledge graph (`/understand`); graph tại `.understand-anything/knowledge-graph.json` |
| 4 | **Repomix** | Pack repo → `repomix-output.xml` (chỉ attach khi cần): `scripts\repomix-pack.ps1` |

Reload Cursor: `Ctrl+Shift+P` → Developer: Reload Window.

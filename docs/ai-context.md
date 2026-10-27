# StockRadar — AI Context (tham chiếu chính cho AI agent)

> **Mục đích:** tài liệu AI agent đọc trước khi làm việc trên StockRadar — pipeline flow, gates, config keys, API endpoints, UI structure.
> **Nguồn sự thật:** code trên disk (`backend/`, `frontend/`, `mobile/`). Khi doc lệch code → **tin code**, rồi cập nhật doc.
> **Cập nhật:** 2026-10-06 (cổng chia chác lịch quyền FireAnt — chặn Top/VIP/noti Pha 2 trong khoảng chốt quyền → thực hiện quyền). Trước đó 2026-09-30 (gate cleanup Top V1 · Siết Top · gate GTGD + corporate action · multi-watchlist · V2 stock detail & menu cleanup).
> Chi tiết luật đầy đủ nằm trong [`domain/`](./domain/) — file này là bản đối chiếu nhanh, không thay domain docs.

## 1. Bản đồ repo

| Thư mục | Nội dung |
|---------|----------|
| `backend/StockRadar.Domain` | Engines: `BuyDecisionEngine` (Buy Score + 4 cổng Top), `SignalAnalyzer`, `SmartMoneyOpportunitySelector`, `GateFailureClassifier`, V3: `V3BoKichBan`, `V3LuatBanThucTe`, `V3DanhMucXepHang`, `V3ChamKetQua`, `V3ChiSoToanThiTruong` |
| `backend/StockRadar.Application` | `Options/MarketJobsOptions.cs` (config schema), DTOs, services |
| `backend/StockRadar.Infrastructure` | Runners: `MarketData/DailyAnalysisRunner.cs`, `V3TruocPhienRunner.cs`, `V3TrongPhienRunner.cs`, `V3AtcRunner.cs`, `V3SauPhienRunner.cs` (+ `V3Engine.cs` dùng chung); Persistence; `Scheduling/QuartzSchedulingExtensions.cs` |
| `backend/StockRadar.Api` | Controllers, `appsettings.json` (config prod) |
| `frontend/` | Web Vite React |
| `mobile/` | Flutter (go_router) |
| `scripts/` | `ship-all.ps1` (deploy), `tune-optuna.py` (HPO) |

Production: API `http://103.226.248.6/api/v1` · Web `https://stock.baobiantea.com/` · API local `http://localhost:5280`.

## 2. Hai pipeline song song

| | V1 — Daily Analysis | V3 — Bộ kịch bản P1++ (spec 012) |
|--|---------------------|----------------------|
| Runner | `DailyAnalysisRunner` | `V3TruocPhienRunner` · `V3TrongPhienRunner` · `V3AtcRunner` · `V3SauPhienRunner` |
| Lịch | 11:30 + ~15:05 + intraday 15' (T2–T6) | Trước phiên **08:30** · trong phiên **mỗi 1 phút** 09:00–14:45 · ATC **14:25** · sau phiên **16:00** |
| Output | `DailyOpportunities` + `SetupTracks` + `DailyAnalysisRuns.GateStatsJson` | `TinHieuV3` + `ViTheV3` + `ChiSoNgayV3` |
| Phục vụ | Home Top, `GET /opportunities` | `GET /v3/hieu-qua/*`, `GET /stocks/{sym}/v3`, `GET /nhat-ky/tin-hieu/v3`, màn chi tiết mã + Hiệu quả |

**V1 không bị thay thế** — hai pipeline chạy song song, output riêng. **V2 đã gỡ toàn bộ theo spec 012** (2026-10-26).

## 3. Pipeline V1 Top — `DailyAnalysisRunner.RunAsync`

Flow: **candidate loop (toàn universe, DB trực tiếp) → 7 gate trong loop → ML ranking + sector bonus → Top hygiene → sector cap → Take(MaxResults) → persist**.

### 3.1 Gates trong candidate loop (thứ tự — rớt ở đâu dừng ở đó)

| # | Gate | Điều kiện loại | Key gateStats | Config key |
|---|------|----------------|---------------|------------|
| 1 | Chia chác (FireAnt) | mã trong khoảng [ngày chốt quyền → ngày thực hiện quyền] theo lịch quyền FireAnt | `sap-chot-quyen` | `FireAnt:Enabled` · `FireAnt:LookaheadDays` (7) |
| 2 | Thiếu ngành | `Sector` rỗng (mã rác — vd ATA/DCT/DFF) | `thieu-nganh` | — |
| 3 | Không khối lượng | không có nến cuối hoặc `Volume ≤ 0` | `khong-khoi-luong` | — |
| 4 | 4 cổng BuyDecisionEngine | xem §3.2 | nhãn `GateFailureClassifier` | xem §3.2 |
| 5 | Volume ratio thấp | `VolumeRatio < MinVolumeRatioForTop` (**0.3**) | `volume-ratio-thap` | `MarketJobs:DailyAnalysis:MinVolumeRatioForTop` |
| 6 | GTGD thấp | `IndicatorMath.AverageTurnoverValue(history, 20) < MinGiaTriGiaoDichTB` (**10 tỷ VND**; Close nghìn VND × 1000 × Volume — cùng công thức sơ tuyển) | `gia-tri-gd-thap` | `MarketJobs:DailyAnalysis:MinGiaTriGiaoDichTB` |
| 7 | Lệch giá (corporate action) | giá hiện tại < `BaseLow × 0.5` **hoặc** > `BaseHigh × 2.0` khi nền hợp lệ (chia tách/phát hành chưa điều chỉnh) | `du-lieu-lech-gia` | hard-code 0.5× / 2.0× |

### 3.2 Bốn cổng BuyDecisionEngine (`ResolveTopGateFailure`) — đúng 4

| # | Cổng | Điều kiện fail | Message |
|---|------|----------------|---------|
| 1 | **Chia chác** | mã trong khoảng [chốt quyền → thực hiện quyền] theo `NextExDateBySymbol` (FireAnt, fail-open) | `Chờ chốt quyền dd/MM — <chia gì, tỷ lệ>` / `Đã chốt quyền dd/MM, chờ thực hiện dd/MM — …` |
| 2 | **FOMO** | gain so đáy thấp nhất 5 phiên > `MaxGainFromLow5SessionsPercent` (**7**) | `FOMO +x.x% so đáy 5 phiên` |
| 3 | **Thị trường khó (Unfavorable)** | pha `Unfavorable` **và** không phải RS Leader **và** (RS percentile < `MinRsPercentileForUnfavorable` (**80**) **hoặc** RS5 ≤ 0) | `Thị trường khó — chỉ mua mã dẫn dắt (RS top + khỏe hơn VNINDEX)` |
| 4 | **Sóng ngành** | không RS Leader **và** ngành không có sóng **và** không sector-wave regime active **và** RS5 < 0 (đã nới từ `< 2%` xuống `< 0%`) | `Ngành chưa có sóng + RS âm` |

**RS Leader bypass** (`IsRsLeader`): (có breakout entry **hoặc** nền xác nhận flatBox) **và** RS percentile ≥ `RsLeaderMinRsPercentile` (**85**) **và** RS5 ≥ 0 → miễn cổng 3 và 4 (cổng chia chác KHÔNG miễn).

### 3.3 Nhãn thống kê gate (`GateFailureClassifier` — khóa đếm ổn định, persist `DailyAnalysisRuns.GateStatsJson`, hiển thị `GET /opportunities → gateStats`)

| Nhãn | Gate tương ứng |
|------|----------------|
| `FOMO vượt ngưỡng so đáy 5 phiên` | cổng FOMO |
| `Thị trường khó — chỉ mua mã dẫn dắt` | cổng Unfavorable + RS |
| `Ngành chưa có sóng + RS âm` | cổng sóng ngành |
| `Khác` | không khớp nhãn nào |

> 6 key gạch-ngang (`sap-chot-quyen`, `thieu-nganh`, `khong-khoi-luong`, `volume-ratio-thap`, `gia-tri-gd-thap`, `du-lieu-lech-gia`) đếm trực tiếp từ runner, không qua classifier.

### 3.4 Sau loop: ranking → hygiene → sector cap → take

1. **ML ranking** — `RankedScore = MlProb + SectorPriorityBonus / 100` (`OpportunityRanker.PredictWinProbability`; model chưa active → heuristic `PredictedHitPercent`). Bonus ngành (khớp chuỗi `SectorCatalog`):
   - **Trọng yếu +10**: Ngân hàng · Dịch vụ tài chính · Thép · Bất động sản · Dầu khí
   - **Phụ trợ +5**: Xây dựng và Vật liệu · Vật liệu xây dựng · Bán lẻ · Thực phẩm và đồ uống
   - Sort: `RankedScore` desc → `Eval.Score` desc → sóng ngành desc → RS5 desc → Symbol asc
2. **Top hygiene (`ApplyTopHygiene`)**:
   - TradeState `Avoid` → **loại TRƯỚC mọi exemption** (kể cả RS Leader — Avoid = nền vỡ)
   - RS Leader → pass mọi chặn theo pha (giữ cả khi AwaitingTrigger / thị trường Unfavorable)
   - Setup breakout theo pha: `Favorable` được phép; `Neutral`/`Unfavorable` chỉ `Actionable`
3. **Sector cap (`ApplySectorCap`)**: tối đa `MaxPerSector` (**2**) mã mỗi ngành, áp sau sort — **RS Leader không miễn**. `MaxPerSector ≤ 0` = tắt.
4. **`Take(MaxResults)`** (prod **5**; 0 = không giới hạn).

Persist: `DailyOpportunities` + `SetupTracks` (đo T+2.5) + `DailyAnalysisRuns.GateStatsJson`.

### 3.5 Buy Score — 8 tiêu chí (adaptive 0–100)

| ID | Nhãn | Max thô |
|----|------|---------|
| `market` | Thị trường | 5 (Favorable 5 / Neutral 2 / Unfavorable 0) |
| `sector` | Sóng ngành | 18 (Strong 18 / Emerging 10 / None 0) |
| `rs` | Sức mạnh tương đối | 20 (≥ +3% → 20 / ≥ 0 → 12 / âm → 0) |
| `base` | Nền giá | 18 (phá nền 18 / test cạnh hộp 12) |
| `breakout` | Nổ hướng lên + khối lượng | 22 |
| `shakeout` | Shakeout / Phân kỳ | 10 (shakeout **hoặc** phân kỳ dương RSI) |
| `volume` | Khối lượng đột biến | 8 |
| `wyckoff` | Pha tăng giá | 5 |

Tổng thô 106 → `NormalizeAdaptiveScore` chuẩn hóa 0–100. `SmartMoneyOpportunitySelector.PassesFilter` = `eval.Passes` — **không MinPassScore, không relaxed fallback** (strict = 0 → Top rỗng, `analysisStatus = zero_matches` kèm `statusBullets` giải thích gate).

### 3.6 Gate đã xóa 2026-09 — đừng khôi phục

Thiếu lịch sử ≥ 250 · Thanh khoản volRatio · Base breakout / test cạnh hộp · Phân kỳ dương RSI · MinScore (Buy Score ≥ ngưỡng) · `ExcludeAwaitingTriggerFromTop`. Lý do (ghi trong code): V2 sơ tuyển lọc lịch sử/thanh khoản theo VND turnover, kịch bản V2 phủ phần nền giá/phân kỳ, ranker V2 lo sắp xếp chất lượng. MA stack **không còn là cổng Top** — chỉ checklist "Xếp lớp MA" + cờ `HasMaStack` (`SmartMoney:MinHistoryDays` vẫn đánh dấu entry Invalid nhưng không chặn Top).

**Dead config keys** (còn trong options nhưng không tác động Top): `MinScore`, `FallbackMinScore`, `FallbackMaxResults`, `ExcludeAwaitingTriggerFromTop`, `SmartMoney:RequireBaseBreakout`.

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


## 5. Quartz jobs (QuartzSchedulingExtensions, VN timezone)

| Job ID | Lịch |
|--------|------|
| `history-backfill` | thủ công / `RunOnStartup` / cron tuần CN 02:00 |
| `daily-session-sync` | 5 phút trong phiên + cron 15:00 |
| `daily-analysis` | 11:30 + ~15:05 T2–T6 + intraday 15' (9:00–11:30 / 13:00–14:45) |
| `v3-truoc-phien` | 08:30 T2–T6 (nhịp nhẹ: chỉ ~200 mã UNI đã lưu) |
| `v3-trong-phien` | mỗi 1 phút 09:00–14:45 (chỉ quote mã đang giữ, không `LayCacMaAsync`) |
| `v3-atc` | 14:25 T2–T6 (nhịp nhẹ + cộng điểm chỉ số hôm nay) |
| `v3-sau-phien` | 16:00 T2–T6 (nhịp nạp ĐẦY ĐỦ duy nhất: lưu chỉ số + UNI phiên sau + ngưỡng bán) |
| `kbs-market-sync` / `intraday-scanner` / `opportunity-monitor` | ~60s trong phiên |
| `weekly-opportunity-review` | T6 15:30 |

API trigger (header `X-Sync-Key`): `POST /api/v1/market/jobs/history|session|analysis|daily|opportunity-monitor|v3-truoc-phien|v3-trong-phien|v3-atc|v3-sau-phien|v3-nap-chi-so`; `POST /api/v1/opportunities/run-analysis` (cooldown 15').

## 6. API endpoints (`/api/v1`)

| Nhóm | Endpoint | Ghi chú |
|------|----------|---------|
| Top V1 | `GET /opportunities` | Top list + `gateStats` + `statusBullets` |
| | `POST /opportunities/run-analysis` | Chạy analysis thủ công |
| Watchlists (multi) | `GET /watchlists` | Tất cả danh sách của user — **lazy seed** (default + 30 ngành) lần GET đầu |
| | `POST /watchlists` | Tạo custom |
| | `GET /watchlists/{id}` · `PATCH /watchlists/{id}` · `DELETE /watchlists/{id}` | Xem / đổi tên / xóa |
| | `GET /watchlists/{id}/items` | Items (enrich giá + điểm); danh sách ngành = items động |
| | `PUT /watchlists/{id}/items/{symbol}` · `DELETE /watchlists/{id}/items/{symbol}` | Thêm / xóa mã |
| Kịch bản V3 | `GET /v3/hieu-qua/tom-tat?tu=&den=` | Tóm tắt theo kịch bản + danh mục |
| | `GET /v3/hieu-qua/lich-su?page=&size=&status=mo|dong|huy` · `GET /v3/hieu-qua/vi-the-dang-giu` | Lịch sử lệnh phân trang · vị thế mở |
| | `GET /stocks/{symbol}/v3` | Tín hiệu V3 mới nhất của 1 mã (mảng rỗng nếu chưa có) |
| Nhật ký | `GET /nhat-ky/tin-hieu/v3` | Picker tín hiệu cho lệnh nhật ký (FR-040 — **không** còn đường dẫn V2) |
| Stocks | `GET /stocks/{symbol}` · `/{symbol}/chart` · `/search` | Detail V1 (override Buy Score từ snapshot nếu trong Top) |
| | `PATCH /stocks/{symbol}/sector` · `GET|POST /stocks/{symbol}/rights-events` | Sửa ngành · sự kiện quyền |
| Performance | `GET /performance/*` | North Star, summary, realized (`/realized-trades`) |
| ML | `GET /ml/dataset/t25-ranking` · `POST /ml/train/t25-ranking` · `GET /ml/ranker/status` | Ranker V1 |

> API cũ vẫn tồn tại dù không còn màn UI: `GET /radar/live`, `GET /market/trades`, `GET /criteria/*`, `GET /alerts`, và nhóm backward-compat `api/v1/watchlist-items` (GET · PUT/POST `{symbol}` · DELETE `{symbol}` — thao tác trên **danh sách mặc định**).

## 7. UI structure

**Mobile (Flutter, go_router):** bottom nav **3 tab** — `Trang chủ` (`/`) · `Watchlist` (`/watchlist`) · `Hiệu quả` (`/performance`); màn push: `/stocks/:symbol` (Chi tiết — dùng V3 `stocks/{symbol}/v3` + V1 BuyDecision) và `/stocks/:symbol/su-kien-quyen`. Screens: `home_screen` · `watchlist_screen` · `hieu_qua_screen` · `stock_detail_screen` · `su_kien_quyen_screen` · `login_screen`.

**Web (Vite React):** `/` · `/stocks/:symbol` · `/stocks/:symbol/su-kien-quyen` · `/watchlist` · `/performance` · `/login`; `/radar`, `/heatmap` redirect về `/`.

**Đã dọn 2026-09:** mobile bỏ menu "Tác vụ", drawer, sidebar, tab "Khớp lệnh". Không còn màn Radar, Trades, Alerts, Phân tích chỉ báo trên UI.

## 8. Config reference

### `MarketJobs:DailyAnalysis` (toàn bộ key trong `DailyAnalysisJobOptions`)

| Key | Default (code) | Prod (appsettings) | Ghi chú |
|-----|----------------|--------------------|---------|
| `Enabled` | true | true | |
| `DelayAfterSessionMinutes` | 2 | 5 | Chạy sau sync phiên |
| `MinScore` | 60 | 55 | **Dead** — chỉ backtest/shadow |
| `MaxResults` | 30 | **5** | Top cuối; 0 = không giới hạn |
| `MinVolumeRatioForTop` | 0.3 | 0.3 | Gate `volume-ratio-thap` |
| `MinGiaTriGiaoDichTB` | 10 tỷ | 10 tỷ | Gate `gia-tri-gd-thap` (VND, TB 20 phiên) |
| `MaxPerSector` | 2 | 2 | Sector cap; ≤ 0 = tắt |
| `FallbackMinScore` | 45 | 45 | **Dead** — backtest only |
| `FallbackMaxResults` | 15 | 5 | **Dead** — backtest only |
| `ExcludeAwaitingTriggerFromTop` | true | true | **Dead** — gate đã xóa |
| `ManualAnalysisCooldownMinutes` | 15 | 15 | Cooldown nút phân tích |
| `MorningRunEnabled` / `Hour` / `Minute` | false / 11 / 30 | **true** / 11 / 30 | Phân tích sáng |
| `IntradayRefreshEnabled` | true | true | Refresh Top trong phiên (selection-only) |
| `IntradayRefreshIntervalMinutes` | 15 | 15 | |
| `IntradayMorning*` / `IntradayAfternoon*` | 9:00–11:30 / 13:00–14:45 | như default | Khung refresh |

### `SmartMoney` (liên quan cổng Top)

| Key | Prod | Vai trò |
|-----|------|---------|
| `MaxGainFromLow5SessionsPercent` | **7** | Cổng FOMO |
| `MinRsPercentileForUnfavorable` | **80** | Cổng thị trường khó |
| `RsLeaderMinRsPercentile` | **85** | RS Leader bypass |
| `MinHistoryDays` | 250 | Entry checklist (không chặn Top) |
| `RequireBaseBreakout` | false | **Dead** |
| `SectorWave:*` | MinAdvancerRatio .6 · MinMedianChangePercent 1.5 · MinVolumeRatio 1.3 · MinSectorRs5d 0 · MinStocksPerSector 3 | Định nghĩa sóng ngành (Strong/Emerging/None → điểm `sector`) |

> `SmartMoney:MinPassScore` **đã xóa** — `PassesFilter` chỉ trả `eval.Passes`.

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
| Buy Score / cổng Top / VIP | [`domain/buy-decision.md`](./domain/buy-decision.md) |
| Pipeline jobs V1 + V2 | [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md) |
| MA stack & pha thị trường | [`domain/ma-stack-and-market-phase.md`](./domain/ma-stack-and-market-phase.md) |
| Nền giá / flatBox | [`domain/base-price-flatbox.md`](./domain/base-price-flatbox.md) |
| Realized P&L | [`domain/realized-pnl.md`](./domain/realized-pnl.md) |
| Bộ kịch bản V3 (spec 012) — **luồng chuẩn** | [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md#luồng-bộ-kịch-bản-v3--sự-thật-chuẩn-duy-nhất) (canon duy nhất; spec normative: [`../specs/012-v3-scenario-engine/spec.md`](../specs/012-v3-scenario-engine/spec.md)) |
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

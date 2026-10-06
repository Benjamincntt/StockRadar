# StockRadar — AI Context (tham chiếu chính cho AI agent)

> **Mục đích:** tài liệu AI agent đọc trước khi làm việc trên StockRadar — pipeline flow, gates, config keys, API endpoints, UI structure.
> **Nguồn sự thật:** code trên disk (`backend/`, `frontend/`, `mobile/`). Khi doc lệch code → **tin code**, rồi cập nhật doc.
> **Cập nhật:** 2026-10-06 (cổng chia chác lịch quyền FireAnt — chặn Top/VIP/noti Pha 2 trong khoảng chốt quyền → thực hiện quyền). Trước đó 2026-09-30 (gate cleanup Top V1 · Siết Top · gate GTGD + corporate action · multi-watchlist · V2 stock detail & menu cleanup).
> Chi tiết luật đầy đủ nằm trong [`domain/`](./domain/) — file này là bản đối chiếu nhanh, không thay domain docs.

## 1. Bản đồ repo

| Thư mục | Nội dung |
|---------|----------|
| `backend/StockRadar.Domain` | Engines: `BuyDecisionEngine` (Buy Score + 4 cổng Top), `SignalAnalyzer`, `SmartMoneyOpportunitySelector`, `GateFailureClassifier`, V2: `MayNhanKichBan`, `XepHangCoHoi`, `SoTuyen` |
| `backend/StockRadar.Application` | `Options/MarketJobsOptions.cs` (config schema), DTOs, services |
| `backend/StockRadar.Infrastructure` | Runners: `MarketData/DailyAnalysisRunner.cs`, `Pha1TruocPhienRunner.cs`, `Pha2TrongPhienRunner.cs`, `Pha3DoLuongRunner.cs`; Persistence; `Scheduling/QuartzSchedulingExtensions.cs` |
| `backend/StockRadar.Api` | Controllers, `appsettings.json` (config prod) |
| `frontend/` | Web Vite React |
| `mobile/` | Flutter (go_router) |
| `scripts/` | `ship-all.ps1` (deploy), `tune-optuna.py` (HPO) |

Production: API `http://103.226.248.6/api/v1` · Web `https://stock.baobiantea.com/` · API local `http://localhost:5280`.

## 2. Hai pipeline song song (2026-09)

| | V1 — Daily Analysis | V2 — Scenario Engine |
|--|---------------------|----------------------|
| Runner | `DailyAnalysisRunner` | `Pha1TruocPhienRunner` · `Pha2TrongPhienRunner` · `Pha3DoLuongRunner` |
| Lịch | 11:30 + ~15:05 + intraday 15' (T2–T6) | Pha 1 **08:30** · Pha 2 **mỗi 1 phút** 09:00–14:45 · Pha 3 **16:00** |
| Output | `DailyOpportunities` + `SetupTracks` + `DailyAnalysisRuns.GateStatsJson` | `KetQuaKichBan` |
| Phục vụ | Home Top, VIP alerts, `GET /opportunities` | `GET /kich-ban/*`, `GET /hieu-qua/*`, `GET /stocks/{sym}/kich-ban`, màn chi tiết mã |

**V1 không bị thay thế** — hai pipeline chạy song song, output riêng.

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

## 4. Pipeline V2 — Scenario Engine

| Pha | Runner | Lịch | Việc |
|-----|--------|------|------|
| 1 — Trước phiên | `Pha1TruocPhienRunner` | 08:30 T2–T6 (cron `0 30 8 ? * MON-FRI`) | Sơ tuyển `SoTuyen` (~1500 → ~70) → `MayNhanKichBan` đánh giá Bối cảnh + Hình thái → `KetQuaKichBan` (WATCHING/FORMING) + điều kiện trigger |
| 2 — Trong phiên | `Pha2TrongPhienRunner` | mỗi 1 phút 09:00–14:45 (cron `0 0/1 9-14 ? * MON-FRI`) | So giá/volume realtime với trigger đã tính sẵn → TRIGGERED: tính Entry/SL/TP, bắn Telegram (**bỏ mã trong khoảng chia chác FireAnt**), check sell cho mã đã trigger, lưu snapshot |
| 3 — Đo lường | `Pha3DoLuongRunner` | 16:00 T2–T6 (cron `0 0 16 ? * MON-FRI`) | Cập nhật state cuối ngày, đo outcome T+1/T+2/T+3 |

**5 kịch bản (`LoaiKichBan`)**: `NoHuongLen` (Nổ hướng lên — BUY) · `HoiHoTro` (Hồi về hỗ trợ — BUY) · `QuetThanhKhoan` (Quét thanh khoản — BUY) · `KietSuc` (Kiệt sức — SELL, bán 50%) · `GayNen` (Gãy nền — EXIT, bán 100%).

**Xếp hạng (`XepHangCoHoi`)** — 6 tiêu chí, **không có quyền veto**: RS .30 · Sector .20 · Chất lượng trigger .20 · Regime .10 · R:R .10 · Confluence .10 → `SoLuongTop` = 5.

**Sơ tuyển (`SoTuyen`)**: GTGD TB ≥ 10 tỷ (`MinGiaTriGiaoDichTrungBinh`) · vốn hóa ≥ 500 tỷ (`MinVonHoa`) · lịch sử ≥ 250 phiên (`MinSoPhienLichSu`).

Canon duy nhất luồng V2 as-is: [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md#luồng-v2-scenario-engine--sự-thật-chuẩn-duy-nhất) (kịch bản gốc: [`features/v2-scenario-engine/spec.md`](./features/v2-scenario-engine/spec.md) — lịch sử thiết kế).

## 5. Quartz jobs (QuartzSchedulingExtensions, VN timezone)

| Job ID | Lịch |
|--------|------|
| `history-backfill` | thủ công / `RunOnStartup` / cron tuần CN 02:00 |
| `daily-session-sync` | 5 phút trong phiên + cron 15:00 |
| `daily-analysis` | 11:30 + ~15:05 T2–T6 + intraday 15' (9:00–11:30 / 13:00–14:45) |
| `pha1-truoc-phien` | 08:30 T2–T6 |
| `pha2-trong-phien` | mỗi 1 phút 09:00–14:45 |
| `pha3-do-luong` | 16:00 T2–T6 |
| `kbs-market-sync` / `intraday-scanner` / `opportunity-monitor` | ~60s trong phiên |
| `weekly-opportunity-review` | T6 15:30 |

API trigger (header `X-Sync-Key`): `POST /api/v1/market/jobs/history|session|analysis|daily|opportunity-monitor`; `POST /api/v1/opportunities/run-analysis` (cooldown 15').

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
| Kịch bản V2 | `GET /kich-ban/xep-hang` | Top kịch bản (chạy Pha 1 + xếp hạng) |
| | `GET /stocks/{symbol}/kich-ban` | Kịch bản mới nhất per loại của 1 mã (404 nếu chưa có) |
| Hiệu quả V2 | `GET /hieu-qua/tom-tat?period=` · `/lich-su` · `/chi-tiet/{id}` | Tổng hợp / lịch sử / chi tiết |
| Stocks | `GET /stocks/{symbol}` · `/{symbol}/chart` · `/search` | Detail V1 (override Buy Score từ snapshot nếu trong Top) |
| | `PATCH /stocks/{symbol}/sector` · `GET|POST /stocks/{symbol}/rights-events` | Sửa ngành · sự kiện quyền |
| Performance | `GET /performance/*` | North Star, summary, realized (`/realized-trades`) |
| ML | `GET /ml/dataset/t25-ranking` · `POST /ml/train/t25-ranking` · `GET /ml/ranker/status` | Ranker V1 |

> API cũ vẫn tồn tại dù không còn màn UI: `GET /radar/live`, `GET /market/trades`, `GET /criteria/*`, `GET /alerts`, và nhóm backward-compat `api/v1/watchlist-items` (GET · PUT/POST `{symbol}` · DELETE `{symbol}` — thao tác trên **danh sách mặc định**).

## 7. UI structure

**Mobile (Flutter, go_router):** bottom nav **3 tab** — `Trang chủ` (`/`) · `Watchlist` (`/watchlist`) · `Hiệu quả` (`/performance`); màn push: `/stocks/:symbol` (Chi tiết — dùng V2 `kich-ban` + V1 BuyDecision) và `/stocks/:symbol/su-kien-quyen`. Screens: `home_screen` · `watchlist_screen` · `hieu_qua_screen` · `stock_detail_screen` · `su_kien_quyen_screen` · `login_screen`.

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

### V2 (section top-level trong appsettings)

| Section | Key chính |
|---------|-----------|
| `SoTuyen` | `MinGiaTriGiaoDichTrungBinh` 10 tỷ · `MinVonHoa` 500 tỷ · `MinSoPhienLichSu` 250 |
| `KichBan` | Ngưỡng 5 kịch bản: `NoHuongLen` (MinAdx 20, MinVolumeRatio 1.5…) · `HoiHoTro` (RsiPullback 40–50…) · `QuetThanhKhoan` (MinVolumeSpike 2.0…) · `KietSuc` (MinRsi 78…) · `GayNen` (MinSellVolumeRatio 1.8…) |
| `XepHang` | `TrongSoRs` .30 · `TrongSoSector` .20 · `TrongSoChatLuongTrigger` .20 · `TrongSoRegime` .10 · `TrongSoTyLeLaiLo` .10 · `TrongSoConfluence` .10 · `SoLuongTop` 5 |
| `Pha2` | `IntervalPhut` 1 · `GioBatDau` 09:00 · `GioKetThuc` 14:45 |

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
| V2 Scenario Engine — **luồng chuẩn** | [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md#luồng-v2-scenario-engine--sự-thật-chuẩn-duy-nhất) (canon duy nhất; spec lịch sử: [`features/v2-scenario-engine/spec.md`](./features/v2-scenario-engine/spec.md)) |
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

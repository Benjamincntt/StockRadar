# Buy Score, cổng Top & hiển thị điểm

## Mục đích

Luật **tăng trưởng (pro-trend)**: tính Buy Score, cổng Top cơ hội (`PassesTopFilter` / `ResolveTopGateFailure`), hiển thị một điểm 0–100 Home↔detail. (Bắn tin VIP Master Alert gắn Top đã gỡ theo spec 011.)

Sóng hồi (ReversalBounce) **đã gỡ bỏ hoàn toàn** — spec [`008-remove-reversal-bounce`](../../specs/008-remove-reversal-bounce/spec.md). Buy Score giờ là thang điểm duy nhất của hệ thống.

> **V1 ↔ V2:** đây là luật của pipeline **V1** (`DailyAnalysisRunner` → `DailyOpportunities`), đang chạy **song song** với pipeline V2 Scenario Engine (`Pha1/2/3` → `KetQuaKichBan`, xem [`pipeline-jobs.md`](./pipeline-jobs.md) + [`../features/v2-scenario-engine/spec.md`](../features/v2-scenario-engine/spec.md)). Home Top hiện phụ thuộc V1 (bắn tin VIP đã gỡ — spec 011); kịch bản V2 phục vụ `GET /kich-ban/*`, `GET /hieu-qua/*` và màn chi tiết mã.

AIUP: [`UC-003`](../use_cases/UC-003-find-growth-opportunities.md) (Top), [`UC-005`](../use_cases/UC-005-manage-watchlist.md) / BR-019 (watchlist cùng Buy Score).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `BuyDecisionEngine.cs` | Score 8 tiêu chí + 4 cổng Top (`ResolveTopGateFailure`) |
| 2 | `DailyAnalysisRunner.cs` | Full Top pipeline: data-quality/GTGD/lệch-giá gates → ML ranking + sector bonus → hygiene → sector cap → MaxResults; persist `DailyOpportunities` + gateStats |
| 3 | `SmartMoneyOpportunitySelector.cs` | Wrapper Top (`PassesFilter` = `eval.Passes`) + sóng ngành (`BuildSectorSnapshots`) |
| 4 | `GateFailureClassifier.cs` | Nhãn đếm thống kê gate |
| 5 | `StockService.cs` | Override BuyScore từ snapshot trên detail |
| 6 | `WatchlistService.cs` | Watchlist ScorePill = Buy Score (snapshot Top / live) |

> Khi docs lệch code → **tin code trên disk**, rồi cập nhật doc này.

## Luật as-is

### Buy Score & Top

- Engine: `BuyDecisionEngine` — Buy Score (8 tiêu chí, chuẩn hóa adaptive 0–100) + cổng Top `ResolveTopGateFailure`. **Không còn `SmartMoney.MinPassScore`** — `SmartMoneyOpportunitySelector.PassesFilter` giờ chỉ trả `eval.Passes` (= `decision.PassesTopFilter`), xếp hạng chất lượng do bộ ranker ML + sector bonus lo.
- **Dọn cổng 2026-09 (6 gate đã xóa):** Thiếu lịch sử ≥250 · Thanh khoản volRatio · Base breakout / test cạnh hộp · Phân kỳ dương RSI · MinScore (Buy Score ≥ ngưỡng) · ExcludeAwaitingTriggerFromTop. Lý do ghi trong code: V2 sơ tuyển lọc lịch sử/Thanh khoản theo VND turnover, kịch bản V2 phủ phần nền giá/phân kỳ, ranker V2 lo phần sắp xếp chất lượng.
- **MA stack KHÔNG còn là cổng Top** — chỉ còn dòng checklist "Xếp lớp MA" + cờ `HasMaStack` expose (xem [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md)).
- **Cổng Top còn lại (`ResolveTopGateFailure`) — đúng 4 cổng, rớt ở đâu dừng ở đó:**
  1. **Chia chác** (thêm 10/2026): mã đang trong khoảng [ngày chốt quyền → ngày thực hiện quyền] theo lịch quyền FireAnt (xem mục "Cổng chia chác" dưới) → `"Chờ chốt quyền dd/MM — <chia gì, tỷ lệ>"` hoặc `"Đã chốt quyền dd/MM, chờ thực hiện dd/MM — …"`. RS Leader **không** được miễn cổng này.
  2. **FOMO**: gain so đáy thấp nhất 5 phiên > `SmartMoney:MaxGainFromLow5SessionsPercent` (**7%**) → `"FOMO +x.x% so đáy 5 phiên"`.
  3. **Thị trường khó (Unfavorable)**: pha `Unfavorable` **và** không phải RS Leader **và** (RS percentile < `SmartMoney:MinRsPercentileForUnfavorable` (**80**) **hoặc** RS5 ≤ 0) → `"Thị trường khó — chỉ mua mã dẫn dắt (RS top + khỏe hơn VNINDEX)"`.
  4. **Sóng ngành**: không phải RS Leader **và** ngành không có sóng **và** ngành không trong sector-wave regime active **và** RS5 < 0 (đã nới từ `< 2%` xuống `< 0%` — chỉ chặn RS âm) → `"Ngành chưa có sóng + RS âm"`.
  - **RS Leader bypass** (`IsRsLeader`): breakout/nền xác nhận + RS percentile ≥ `SmartMoney:RsLeaderMinRsPercentile` (**85**) + RS5 ≥ 0 → miễn cổng 3 và 4.
- **Kiểu điểm vào là lựa chọn thay thế nhau** (breakout thẳng **hoặc** shakeout đáy nền + hồi **hoặc** phân kỳ dương RSI) — đạt 1 trong 3 là kích hoạt, không cộng dồn và không trừ nhau. Xem [`features/sector-wave-entry-patterns/spec.md`](../features/sector-wave-entry-patterns/spec.md).
- **Sóng ngành thay xếp hạng ngành top N** — không còn `TopSectorCount` / composite rank. Xem mục "Sóng ngành" dưới.
- Early Recovery: Loose MA nhưng thiếu RS → `GET /api/v1/early-recovery` (không vào Top).
- **Không còn relaxed fallback** (bỏ từ feature [`004-remove-relaxed-fallback`](../../specs/004-remove-relaxed-fallback/spec.md)): khi strict = 0 mã, Top trả rỗng (`analysisStatus = zero_matches`) kèm `statusBullets` giải thích gate — không còn dựng danh sách thay thế từ rổ Buy Score nới. `GET /opportunities`: phiên mục tiêu đã quét và `OpportunitiesSaved = 0` thì **không** gắn list ngày cũ; fallback theo ngày (`reference_list`) chỉ khi chưa chạy phân tích.

### Luồng V1 Top pipeline (DailyAnalysisRunner.RunAsync)

Candidate loop trên toàn universe (DB trực tiếp) → mỗi mã qua chuỗi gate theo thứ tự, mã qua hết vào rổ ứng viên:

| # | Gate (trong loop) | Điều kiện loại | Key thống kê gateStats |
|---|-------------------|----------------|------------------------|
| 1 | Chia chác (FireAnt) | mã trong khoảng [chốt quyền → thực hiện quyền] — `context.CoChiaQuyenSapDen` | `sap-chot-quyen` |
| 2 | Thiếu ngành | `Sector` rỗng (mã rác — vd ATA/DCT/DFF) | `thieu-nganh` |
| 3 | Không khối lượng | không có nến cuối hoặc `Volume ≤ 0` | `khong-khoi-luong` |
| 4 | 4 cổng BuyDecisionEngine | Chia chác / FOMO / Unfavorable / sóng ngành (bảng trên) | nhãn `GateFailureClassifier` (dưới) |
| 5 | Volume ratio thấp | `VolumeRatio < MarketJobs:DailyAnalysis:MinVolumeRatioForTop` (**0.3**) | `volume-ratio-thap` |
| 6 | GTGD thấp | `IndicatorMath.AverageTurnoverValue(history, 20) < MarketJobs:DailyAnalysis:MinGiaTriGiaoDichTB` (**10 tỷ VND** — cùng công thức sơ tuyển: Close nghìn VND × 1000 × Volume) | `gia-tri-gd-thap` |
| 7 | Lệch giá (corporate action) | giá hiện tại < `BaseLow × 0.5` **hoặc** > `BaseHigh × 2.0` khi nền hợp lệ — chia tách/phát hành chưa điều chỉnh chấm trên thang giá cũ | `du-lieu-lech-gia` |

Sau đó: **xếp hạng ML + bonus ngành** → **Top hygiene** → **sector cap** → **Take(MaxResults)**.

1. **Xếp hạng**: `OpportunityRanker.PredictWinProbability` (ML P(hit) T+2.5; model chưa active → fallback heuristic `PredictedHitPercent`). `RankedScore = MlProb + BonusNganh / 100`:
   - Ngành trọng yếu **+10** (chuỗi khớp `SectorCatalog`): Ngân hàng · Dịch vụ tài chính · Thép · Bất động sản · Dầu khí.
   - Ngành phụ trợ **+5**: Xây dựng và Vật liệu · Vật liệu xây dựng · Bán lẻ · Thực phẩm và đồ uống.
   - Sort: `RankedScore` desc → `Eval.Score` desc → sóng ngành desc → RS5 desc → Symbol asc.
2. **Top hygiene (`ApplyTopHygiene`)**:
   - **TradeState `Avoid` → loại TRƯỚC mọi exemption** (kể cả RS Leader — Avoid = nền vỡ).
   - **RS Leader → pass** mọi chặn theo pha (giữ cả khi AwaitingTrigger / thị trường Unfavorable).
   - **Gate breakout theo pha**: setup dạng breakout (Entry type `Breakout` hoặc DNA bắt đầu `Breakout`/chứa "Phá vỡ") — pha `Favorable` được phép; `Neutral`/`Unfavorable` chỉ `Actionable`.
   - Gate 10 (ExcludeAwaitingTriggerFromTop) **đã bỏ** — AwaitingTrigger vẫn được vào Top, ranker tự sorts.
3. **Sector cap (`ApplySectorCap`)**: tối đa `MarketJobs:DailyAnalysis:MaxPerSector` (**2**) mã mỗi ngành, áp sau khi sort — **RS Leader cũng không miễn** (tránh 1 leader lấp đầy Top). `MaxPerSector ≤ 0` = tắt.
4. **`Take(MaxResults)`**: `MarketJobs:DailyAnalysis:MaxResults` (**5**; 0 = không giới hạn).

**Nhãn thống kê gate** (`GateFailureClassifier` — khóa đếm ổn định, không chứa số biến; persist vào `DailyAnalysisRuns.GateStatsJson`, hiển thị UI qua `GET /opportunities → gateStats`):

| Nhãn | Gate tương ứng |
|------|----------------|
| `FOMO vượt ngưỡng so đáy 5 phiên` | cổng FOMO |
| `Thị trường khó — chỉ mua mã dẫn dắt` | cổng Unfavorable + RS |
| `Ngành chưa có sóng + RS âm` | cổng sóng ngành |
| `Khác` | không khớp nhãn nào |

> Runner còn đếm 6 key tiền tố gạch-ngang (`sap-chot-quyen`, `thieu-nganh`, `khong-khoi-luong`, `volume-ratio-thap`, `gia-tri-gd-thap`, `du-lieu-lech-gia`) — các key này đến từ runner, không qua classifier.

### Cổng chia chác (lịch quyền FireAnt — 10/2026)

- **Luật:** mã đang trong khoảng **[ngày chốt quyền (recordDate) → ngày thực hiện quyền (executionDate)]** bị loại khỏi mọi khuyến nghị/noti **MUA**: không vào Top (đầu tiên trong loop — `sap-chot-quyen`), không noti Pha 2 trong phiên (`Pha2TrongPhienRunner` chặn trước vòng bắn Telegram; vẫn lưu TRIGGERED vào DB), và nhãn trên màn chi tiết đổi thành "Chờ chốt quyền…".
- **Không chặn noti BÁN** (cố ý): sell alert bảo vệ vị thế đang giữ qua giai đoạn giá điều chỉnh.
- **Nguồn data:** FireAnt `restv2.fireant.vn/events/search` (loại 1 tiền mặt · 2 cổ phiếu · 3 quyền mua), guest token Bearer scrape từ bundle Next.js của trang chủ (cache 12h). Query lùi 45 ngày + tới `LookaheadDays` (mặc định 7) để bắt cả sự kiện đã chốt nhưng chưa thực hiện. Map kết quả cache 6h theo ngày — **tự quay vòng mỗi ngày**: qua ngày thực hiện, mã tự vào lại (chỉ skip per-run, không đụng trạng thái universe).
- **Fail-open:** nguồn lỗi/token hết hạn/network hỏng → map rỗng → cổng mở, không chặn oan cả bảng Top.
- **Cấu trúc:** `INguonLichChotQuyen` (Application) → `FireAntLichChotQuyenClient` (Infrastructure) → `SmartMoneyMarketContext.NextExDateBySymbol` (`symbol → ThongTinChotQuyen(ExDate, MoTa, NgayThucHien, DaChot)`) → helper `CoChiaQuyenSapDen`; label do `ThongTinChotQuyen.Nhan()` dựng (title FireAnt cắt 80 ký tự).
- **Config:** `FireAnt:Enabled` (true) · `FireAnt:LookaheadDays` (7) · `FireAnt:AccessToken` (tùy chọn, thay scrape) trong appsettings.
- **Khác với `Data/su-kien-quyen.json`:** file đó (POST `/stocks/{sym}/rights-events`) chỉ dùng **điều chỉnh ngược nến quá khứ** (`BoDieuChinhGiaTheoQuyen`); feed FireAnt là lịch **tương lai** phục vụ cổng lọc này.

### Hiển thị một điểm 0–100

- List: `OpportunityDto.score` = snapshot `DailyOpportunity.BuyScore`.
- Detail Top ngày active: override `score` / `buyDecision.buyScore` từ snapshot; `buyScoreSource` = `snapshot` | `live`.
- Watchlist: cùng Buy Score — snapshot Top ngày active; mã ngoài Top → live `BuyDecisionEngine` (không dùng Criterion CompositeScore).
- Mobile: một `ScorePill`; không P% / ActionScore cạnh Buy Score; DNA không bucket `· Điểm`; nhãn mức giá **Giá vào**.
- Chỉ một thang điểm duy nhất (Buy Score) trên toàn app — không còn thang điểm hay tab song song.

### VIP / Master Alert — **ĐÃ GỠ (spec 011)**

Toàn bộ bắn tin VIP trong phiên — monitor ~60s, Master buy/sell, BuyPoint open/pullback, bull-trap deferral cho VIP, ML gate (`VipAlertFires`), anti-spam, LLM veto, logic bán vị thế Master, publisher/evaluator `TopOpportunityVipAlert*` — đã **gỡ** theo spec [`011-remove-v1-vip-alerting`](../../specs/011-remove-v1-vip-alerting/spec.md). Bộ lọc Top V1 (gate → ML rank + bonus ngành → hygiene → sector cap → Top 5) và Home Top **giữ nguyên**. `VnIndexPriorPeakAnalyzer` chỉ còn phục vụ gate bull-trap trên Home Top (V1), không còn đường VIP. Sell-side V2 (Pha 2/3) không bị ảnh hưởng — xem [`pipeline-jobs.md`](./pipeline-jobs.md).

### Sóng ngành (thay xếp hạng ngành)

- Nguồn: `SmartMoneyOpportunitySelector.BuildSectorSnapshots` + `SectorSnapshot` (`AnalysisResults.cs`). Ngưỡng: `SmartMoney:SectorWave` trong `appsettings.json`.
- Ngành cần ≥ `MinStocksPerSector` (3) mã đủ lịch sử; ngành thiếu mã / `Khác` / `N/A` → **không có sóng**.
- 4 điều kiện đo trong **phiên hiện tại**: độ rộng (≥60% mã tăng) · lực (trung vị ≥ +1.5% **hoặc** ≥25% mã tăng ≥ +4%) · tiền vào (tổng KL phiên ≥ 1.3× KL TB) · xác nhận (RS ngành 5 phiên > 0).
- **Hiện tại** % phiên / RS / FOMO hộp dùng dãy chấm điểm (`LayLichSuChamDiem` — nhân OHLC lùi về thang nến cuối theo sự kiện quyền, gồm quyền mua trả tiền). Giá last / nến chart vẫn thô. Nạp quyền: chi tiết mã → **Sự kiện quyền**. Spec: [`specs/006-paid-rights-adjust/spec.md`](../../specs/006-paid-rights-adjust/spec.md) (mở rộng [`005`](../../specs/005-ohlcv-corporate-adjust/spec.md)).
- **Sóng mạnh** = đủ 4 · **Chớm sóng** = đủ độ rộng + ≥1 điều kiện còn lại · **Không sóng** = còn lại.
- Dùng ở 3 chỗ: Buy Score component `sector` (18 / 10 / 0 điểm), cổng Top (`không sóng` + RS < 0% → loại — **đã nới từ 2% xuống 0%**, chỉ chặn RS âm; ngành có sector-wave regime active cũng được miễn), checklist điểm vào (`Sóng ngành` — hiển thị **số mã tăng / số mã giảm**).
- ML: `SetupDna` mang token sóng (`Sóng ngành mạnh` / `Chớm sóng ngành` / `Ngành chưa có sóng`); feature `sector_wave_inv` = `1/(1+rank)` với rank 1/2/3. `ParseSetupDna` vẫn đọc DNA cũ dạng `Ngành #n` để dataset lịch sử không vỡ.

### Mức giá điểm vào & cổng R:R

- Nguồn: `BuyDecisionEngine.EntryLevels` + `BuildEntry`. `range` = `max(đỉnh nền − đáy nền, đáy nền × 2%)`.
- **Một mã = một kiểu điểm vào = một bộ mức giá = một R:R.** `entryType` và `levels` tính đúng một lần ngay sau khi xác định được nền; mọi nhánh trả về (chờ phá nền / Late / R:R thấp / Ready / chờ kích hoạt) đều dùng lại cùng bộ số. Không nhánh nào được tính mức giá riêng.
- **Cắt lỗ** phụ thuộc vị trí điểm vào so với nền: đã phá nền (`entry > đỉnh nền`) → `max(đáy nền × 0.98, đỉnh nền × 0.97)` — đỉnh nền cũ thành hỗ trợ; chưa phá nền (shakeout / chờ) → `đáy nền × 0.98`.
- **Mục tiêu** = `đỉnh nền + range × 2` (đo chiều rộng nền phóng từ đỉnh nền); nếu giá đã vượt mức đó → `entry + range`.
- Ngưỡng chống FOMO (`MaxGainFromBasePercent`) **chỉ chặn điểm vào**, không dùng làm trần mục tiêu. Dùng làm trần thì giá chạy càng xa nền mục tiêu càng teo về sát giá hiện tại (R:R → 0).
- **Cổng R:R**: `RiskReward < 1.5` → hạ `Ready` xuống `Watch`, `IsActionable=false`, headline `R:R x.x < 1.5 — chưa đáng vào`.
- `TradeStateResolver`: `Watch` → trong list = `Watchlist`, ngoài list = `AwaitingTrigger` ("Chờ kích hoạt"). Một luật cho mọi nhánh Watch, không phân biệt nhánh nào sinh ra nó; `Avoid` chỉ dành cho `Late` / `Invalid` / gate nặng.

### Công thức chỉ số — nguồn duy nhất `IndicatorMath`

**Luật: một mã + một khung thời gian = một giá trị.** Khung thời gian là thứ *duy nhất* được phép khác nhau, và phải truyền qua tham số — không service nào được tự cài lại công thức.

- `IndicatorMath` (`TechnicalIndicatorAnalyzer.cs`) giữ: `TrueRange` · `AtrAt(history, index, period)` · `Atr(history, period)` · `Rsi(history, period)` · `Sma(history, period)` · `SmaAt(history, index, period)` · `AverageClose(history, start, end)` · `AverageVolume(history, period)` · `AverageVolume(history, start, end)` · `Ema` · `Macd` · `Stochastic`.
- SMA / EMA / KL trung bình: **thu hẹp cửa sổ** khi thiếu dữ liệu, trả 0 khi rỗng — không ném exception, không trả 0 giả.
- ATR: trung bình đơn giản của True Range (**không** làm mượt Wilder). Thiếu dữ liệu thì **thu hẹp cửa sổ**, chỉ trả 0 khi chưa đủ 2 phiên — không trả 0 giả.
- RSI: trung bình đơn giản, **không làm tròn** trong lõi; chỗ hiển thị tự định dạng.
- RS (`SignalAnalyzer.GetRelativeStrength`): `% giá N phiên − % index N phiên`. **Hai vế bắt buộc cùng N.** Mặc định N = 5 → phải truyền `MarketIndex.IndexChange5d`, không truyền `ChangePercent` (1 phiên). `% giá` lấy dãy chấm điểm (`LayLichSuChamDiem`); VNINDEX không seed quyền.
- RS percentile (`RsPercentileCalculator.Build`): xếp hạng RS trong rổ, **một công thức**, `days` là tham số — hiện chỉ Top dùng, khung 5 phiên. Rổ lọc lịch sử ≥ `max(minHistoryDays, days+1)` **và** thanh khoản, lọc **ngay khi xếp hạng** (lọc sau sẽ để mã thanh khoản thấp chiếm chỗ rồi bị loại, bóp hạng mã đủ điều kiện). Lưu ý: `% index` là hằng số chung toàn rổ nên trừ index **không** đổi thứ hạng — nó giữ đại lượng đúng nghĩa "RS"; thứ hạng chỉ đổi theo `days` và theo rổ đủ điều kiện.
- Tín hiệu phiên (`DetectSignals`) đo **1 phiên** → truyền `MarketIndex.ChangePercent`. Nơi nào cần cả hai thì nhận nguyên `MarketIndex` thay vì một con số `decimal`.

### MA stack

Xem [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md) — **không** nhân bản bảng Full/Medium/Loose ở đây.

### Hộp phẳng

Xem [`base-price-flatbox.md`](./base-price-flatbox.md).

### Chỉ báo kỹ thuật & Playbook — **không** vào Buy Score

> Nguồn: [`features/indicator-playbooks/spec.md`](../features/indicator-playbooks/spec.md) (đã land `004-indicator-playbooks`).

**Nguyên tắc bất biến (constitution §III):** chỉ báo kỹ thuật (RSI, EMA, MACD, VWAP…) và bundle VSA/POC+Delta/SMC **không tham gia tính Buy Score và không vào cổng Top**. Tháng 09/2026, toàn bộ 13 dòng kỹ thuật (10 single + 3 bundle chuyên biệt) đã được **gỡ hẳn** khỏi dây chấm điểm hậu kiểm, API chi tiết mã, màn Phân tích chỉ báo (web + mobile) và dossier LLM VIP (`TechnicalIndicatorAnalyzer`/`IndicatorBundleScorer` đã xóa). Màn hình chỉ còn 9 tiêu chí SmartMoney — phản chiếu đúng Buy Score. Giá trị enum cũ giữ `[Obsolete]` chỉ để đọc dữ liệu lịch sử trong DB. Vai trò *quyết định* của chỉ báo giờ nằm ở pipeline V2 — `MayNhanKichBan` (canon luồng: [`pipeline-jobs.md`](./pipeline-jobs.md#luồng-v2-scenario-engine--sự-thật-chuẩn-duy-nhất); kịch bản gốc: [`../features/v2-scenario-engine/spec.md`](../features/v2-scenario-engine/spec.md)); nguyên tắc "không vào Buy Score" này chỉ áp cho V1.

| Thực tế | Entry code |
|---------|-----------|
| `BuyDecisionEngine` chỉ nhận `ISignalAnalyzer`; không đọc điểm criterion | `BuyDecisionEngine.cs` |
| Hậu kiểm chấm duy nhất `ISmartMoneyCriterionScorer` (9 SmartMoney) | `DailyCriterionScoringRunner.cs` |
| Criterion scores không có trong 11 feature ML ranker | `OpportunityRankFeatures.cs:8` |
| `IndicatorMath` (ATR/RSI/EMA… thuần công thức) VẪN dùng thật — gate `ISignalAnalyzer`, feature ML, universe filter | `IndicatorMath.cs` |

**Playbook dimension** (`PlaybookId` — `breakout-darvas` / `pullback-ma20` / `unclassified` / `legacy`):

- Accuracy / edge / baseline đo theo `(criterion × playbook × marketPhase)` — áp cho 9 tiêu chí SmartMoney.
- Classifier (`PlaybookClassifier`) đọc cờ từ `BuyDecisionEvaluation` — không tính lại; cờ là kết quả của `BuyDecisionEngine` được expose thêm, **không ảnh hưởng điểm**.
- Cờ rollback: `CriterionAccuracyOptions.PlaybookDimensionEnabled` — tắt → ghi `unclassified`.
- 6 bundle + 10 single kỹ thuật đã gỡ khỏi dây chấm điểm (09/2026); timeline `PlaybookId` giữ nguyên cho dữ liệu lịch sử.

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-BD-1 | ~~Gap MA Favorable=Full khi index uptrend 1 phiên~~ | **Resolved** — xem `ma-stack-and-market-phase.md` (FTD+MA20+HL) |
| G-BD-2 | FE web ActionScore / PredictedHit chưa đồng bộ đợt hiển thị mobile | As-is; ưu tiên mobile đã làm |
| G-BD-3 | ~~`rsPercentile` có 2 định nghĩa khác bản chất~~ | **Resolved (phương án C)** — `RsPercentileCalculator.Build` là công thức duy nhất; `days` là tham số (Top 5). Cả hai đều trừ index cùng khung và lọc thanh khoản trong lúc xếp hạng.|
| G-BD-4 | ~~EMA có 2 cách mồi cho cùng `period`~~ | **Resolved** — thêm `IndicatorMath.EmaAt` (SMA mồi trên prefix); `BaseQualityEvaluator.EmaAt` gọi vào. Seed của `IndicatorMath.Ema` **giữ nguyên** |
| G-BD-5 | "Breakout" = **2 công thức, 3 tên** | `SignalAnalyzer.IsBreakout` = Donchian 20 phiên (Close > đỉnh High 20 phiên + KL > 2× TB + tăng > 3%). `FlatBoxProfile.IsBreakoutConfirmed` = 4 gate hộp phẳng. `IsDarvasBreakout` **là alias** của `IsBreakoutConfirmed`, không phải công thức thứ ba. `hasBreakoutEntry` OR hai tín hiệu là **phân loại kiểu điểm vào**, không phải trùng công thức — Top vẫn bắt hộp trước, Donchian chỉ thêm cửa kích hoạt. Siết hay không là quyết định sản phẩm, bàn riêng |
| G-BD-6 | ~~`%` / RS / FOMO dùng Close thô — gap GDKHQ bị hiểu là dump (SSI 17/08)~~ | **Resolved** — `LayLichSuChamDiem` + seed `su-kien-quyen.json`; last/chart vẫn thô |
| G-BD-7 | Key config **tồn tại nhưng không còn tác dụng** sau dọn cổng: `SmartMoney:RequireBaseBreakout` (cổng base breakout đã bỏ), `MarketJobs:DailyAnalysis:ExcludeAwaitingTriggerFromTop` (Gate 10 đã bỏ), `MarketJobs:DailyAnalysis:MinScore` (chỉ còn dùng cho backtest + shadow, không còn là gate Top) | As-is — key giữ lại tránh vỡ config prod; đừng ghi nhận key này như gate đang hoạt động |

## Tài liệu liên quan

- Domain: [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md), [`base-price-flatbox.md`](./base-price-flatbox.md), [`pipeline-jobs.md`](./pipeline-jobs.md)
- Sóng ngành: [`../features/sector-wave-entry-patterns/spec.md`](../features/sector-wave-entry-patterns/spec.md)
- Điều chỉnh quyền: [`../../specs/005-ohlcv-corporate-adjust/spec.md`](../../specs/005-ohlcv-corporate-adjust/spec.md)
- AIUP: UC-003
- Index: [`../README.md`](../README.md)
- Stub cũ: `../opportunity-scan-rules.md`, `../smartmoney-checklist.md`, `../buy-score-display.md`, `../telegram-vip-alerts-flow.md`

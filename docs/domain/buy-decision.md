# Buy Score, cổng Top & hiển thị điểm

## Mục đích

Luật **tăng trưởng (pro-trend)**: tính Buy Score, cổng Top cơ hội (`PassesTopFilter` / `ResolveTopGateFailure`), hiển thị một điểm 0–100 Home↔detail, và VIP Master Alert gắn Top.

**Không** mô tả sóng hồi — xem [`reversal-bounce.md`](./reversal-bounce.md). Điểm sóng hồi (`ReversalBounce.totalScore`) là thang **khác**; **cấm gộp** hai hệ chấm điểm trên cùng UI/logic.

AIUP: [`UC-003`](../use_cases/UC-003-find-growth-opportunities.md) (Top), [`UC-005`](../use_cases/UC-005-manage-watchlist.md) / BR-019 (watchlist cùng Buy Score).

## Nguồn đối chiếu (code entry)

| Ưu tiên | File / entry | Vai trò |
|---------|--------------|---------|
| 1 | `BuyDecisionEngine.cs` | Score 9 tiêu chí + gates |
| 2 | `SmartMoneyOpportunitySelector.cs` | Wrapper Top + `MinPassScore` + `ClassifyMarket` |
| 3 | `DailyAnalysisRunner.cs` | Persist `DailyOpportunities` |
| 4 | `StockService.cs` | Override BuyScore từ snapshot trên detail |
| 5 | `WatchlistService.cs` | Watchlist ScorePill = Buy Score (snapshot Top / live) |
| 6 | `TopOpportunityVipAlertPublisher.cs` / `TopOpportunityVipAlertEvaluator.cs` | VIP trong phiên |

> Khi docs lệch code → **tin code trên disk**, rồi cập nhật doc này.

## Luật as-is

### Buy Score & Top

- Engine: `BuyDecisionEngine` — gates + điểm; Top strict qua selector + `SmartMoney.MinPassScore` (prod thường **62**).
- Cổng Top (`ResolveTopGateFailure`) gồm (tóm tắt): đủ lịch sử, thanh khoản TB, không phân phối, Darvas breakout **hoặc** setup zone, FOMO ≤10% so đỉnh hộp, **MA stack theo pha**, Unfavorable+RS, **ngành chưa có sóng + RS <2%**, kích hoạt phiên / setup, RS âm, Buy Score ≥ MinPassScore.
- **Kiểu điểm vào là lựa chọn thay thế nhau** (breakout thẳng **hoặc** shakeout đáy nền + hồi **hoặc** phân kỳ dương RSI) — đạt 1 trong 3 là kích hoạt, không cộng dồn và không trừ nhau. Xem [`features/sector-wave-entry-patterns/spec.md`](../features/sector-wave-entry-patterns/spec.md).
- **Sóng ngành thay xếp hạng ngành top N** — không còn `TopSectorCount` / composite rank. Xem mục "Sóng ngành" dưới.
- Khi pha **không** Favorable (Nỗ lực hồi phục / Điều chỉnh), lý do fail MA trên list được rewrite thành **Chờ xác nhận thị trường chung** (không đổ lỗi MA Full giả Favorable).
- Early Recovery: Loose nhưng thiếu RS → `GET /api/v1/early-recovery` (không vào Top).
- **Top hygiene (DailyAnalysisRunner):** loại `AwaitingTrigger` khỏi Top (`ExcludeAwaitingTriggerFromTop`); gate breakout theo pha (Neutral chỉ Actionable; Unfavorable cần Actionable + BuyScore ≥ `UnfavorableMinBuyScore`).
- **Không còn relaxed fallback** (bỏ từ feature [`004-remove-relaxed-fallback`](../../specs/004-remove-relaxed-fallback/spec.md)): khi strict = 0 mã, Top trả rỗng (`analysisStatus = zero_matches`) kèm `statusBullets` giải thích gate (vd. bull-trap) — không còn dựng danh sách thay thế từ rổ Buy Score nới. `GET /opportunities`: phiên mục tiêu đã quét và `OpportunitiesSaved = 0` thì **không** gắn list ngày cũ; fallback theo ngày (`reference_list`) chỉ khi chưa chạy phân tích.

### Hiển thị một điểm 0–100

- List: `OpportunityDto.score` = snapshot `DailyOpportunity.BuyScore`.
- Detail Top ngày active: override `score` / `buyDecision.buyScore` từ snapshot; `buyScoreSource` = `snapshot` | `live`.
- Watchlist: cùng Buy Score — snapshot Top ngày active; mã ngoài Top → live `BuyDecisionEngine` (không dùng Criterion CompositeScore).
- Mobile: một `ScorePill`; không P% / ActionScore cạnh Buy Score; DNA không bucket `· Điểm`; nhãn mức giá **Giá vào**.
- Điểm sóng hồi giữ riêng (cần gạt Home / body chi tiết).

### VIP / Master Alert (tóm tắt)

- Monitor ~60s: chỉ mã trong Top ngày → Master buy/sell trong phiên. **Entry Ready Telegram tắt** (`EntryReadyEnabled=false`); vùng entry chỉ hiển thị UI.
- **BuyPoint:** `% từ Open phiên` (3%/6%) **hoặc** pullback sát MA10/MA20 khi uptrend dài hạn (chỉ Buy1). Prefetch MA từ history, fail-closed nếu thiếu. Spec: [`features/vip-buy-trigger-open-pullback/spec.md`](../features/vip-buy-trigger-open-pullback/spec.md).
- **Bull-trap env** (VNINDEX sát đỉnh ≤1.5% — đỉnh = swing overhead **gần nhất theo giá** trong lookback **~750 phiên / 3 năm** + pha ≠ Favorable): **không mua nổ**. Buy1 chỉ **dip-bounce** (uptrend dài hạn + ≥2 phiên đỏ/3 + phiên xanh đầu); Buy2 = **scale-in** khi lãi so entry Buy1 ≥ **10%** (không vol/ticks/ML). Ngoài env: Buy1/Buy2 cũ (breakout % Open / pullback MA). Helper: `VnIndexPriorPeakAnalyzer` + `VipVnIndexPeakCache`.
- **Deferral (chờ chiều xác nhận)** — `BullTrapDeferralEnabled`. Trong **trap-zone** (env bật *hoặc* index đã **xuyên** đỉnh đã ghim: `IsNearPriorPeak` trả false khi `live≥peak`, nên xuyên đỉnh mở cửa breakout đúng nhịp break hụt), Buy1 **và** Buy2-breakout bị **hoãn** tới **checkpoint chiều** (mặc định **14:00** — sau con sóng xả 13:00→14:00, không phải mép 13:00), chỉ bắn nếu mã **còn sát high phiên** (`(High−Close)/High ≤ 1.5%`). Trap-context ghim theo phiên (`_trapPeakPinned`) — `_peak` ephemeral ghi đè mỗi vòng nên phải pin để bắt xuyên. Pin không release trong phiên: deferral chỉ cấm *trước* checkpoint, tín hiệu chiều không bị cản. Scale-in +10% giữ nguyên (cửa thứ hai, slice sau).
  - **Hysteresis** (`BullTrapHysteresisEnabled`, mặc định bật): env bật ≤1.5%, chỉ tắt khi live lùi ra >3% — chống flicker khi index dao động quanh mép band. Độc lập với pin, chỉ áp dụng khi `live<peak`.
  - **Window-integrity** (`BullTrapDeferralRequireWindowIntegrity`, mặc định **tắt** — opt-in): thay đọc shape một lần tại checkpoint bằng "chưa từng thủng suốt 13:00→checkpoint" (per-mã + index-vs-pin). Dodge dead-cat bounce, đánh đổi bỏ lỡ vài reclaim thật.
  - **Foreign-hold** (`BullTrapDeferralRequireForeignHold`, mặc định **tắt** — opt-in, chưa backtest): khối ngoại chưa quay đầu bán từ 13:00 (snapshot trên `SessionFlowTracker`), fail-open nếu thiếu dữ liệu.
  - Spec: [`features/vip-bulltrap-deferral/spec.md`](../features/vip-bulltrap-deferral/spec.md).
- **ML gate + đo:** `MlGateEnabled` + `MinMlProbToFire` theo pha; log fire → `VipAlertFires`; KPI `GET /performance/vip-alert-accuracy`. Spec: [`features/vip-intraday-ml-accuracy/spec.md`](../features/vip-intraday-ml-accuracy/spec.md).
- **LLM veto (A / ShopAIKey Claude):** sau rule+ML, gửi hồ sơ đầy đủ mã → ALLOW/BLOCK; mặc định `ShadowMode=true`. Spec: [`features/vip-deepseek-veto/spec.md`](../features/vip-deepseek-veto/spec.md).
- Bán vị thế Master: chỉ từ **T+3** (`MinTradingSessionsToSell=3`); T+0…T+2 chỉ cảnh báo rủi ro (không chữ Bán).
- **Hai chế độ thoát** (chốt lúc mở vị thế / phân loại lười vị thế cũ):
  - **UnderBase** — còn hộp nền Darvas phía trên giá vào (biên độ ≤15%, ≥20 phiên): bán 1 nửa gần cạnh dưới nền; bán hết khi bị đẩy ngược; vượt cạnh trên → chuyển **BlueSky**.
  - **BlueSky** — mốc = `max(High)` 20 phiên gần nhất, không lùi xa hơn ngày mua; bán 1 nửa khi giảm ≥4% so mốc, bán hết ≥6% (nhân hệ số pha); thủng `EntryBarLow` → bán hết ngay. Không còn gate “phải lãi ≥3%”.
- Hệ số pha (chợ xấu bán sớm): Favorable **1.25** / Neutral **1.0** / Unfavorable **0.75**.
- Chi tiết ticks/vol: code `TopOpportunityVipAlert*`; kiến trúc [`architecture.md`](../architecture.md); Spec Kit `specs/003-regime-aware-sell-exits/`.

### Sóng ngành (thay xếp hạng ngành)

- Nguồn: `SmartMoneyOpportunitySelector.BuildSectorSnapshots` + `SectorSnapshot` (`AnalysisResults.cs`). Ngưỡng: `SmartMoney:SectorWave` trong `appsettings.json`.
- Ngành cần ≥ `MinStocksPerSector` (3) mã đủ lịch sử; ngành thiếu mã / `Khác` / `N/A` → **không có sóng**.
- 4 điều kiện đo trong **phiên hiện tại**: độ rộng (≥60% mã tăng) · lực (trung vị ≥ +1.5% **hoặc** ≥25% mã tăng ≥ +4%) · tiền vào (tổng KL phiên ≥ 1.3× KL TB) · xác nhận (RS ngành 5 phiên > 0).
- **Hiện tại** % phiên / RS / FOMO hộp dùng dãy chấm điểm (`LayLichSuChamDiem` — nhân OHLC lùi về thang nến cuối theo sự kiện quyền). Giá last / nến chart vẫn thô. Nạp quyền: chi tiết mã → **Sự kiện quyền** (`GET/POST /api/v1/stocks/{symbol}/rights-events`). Spec: [`specs/005-ohlcv-corporate-adjust/spec.md`](../../specs/005-ohlcv-corporate-adjust/spec.md).
- **Sóng mạnh** = đủ 4 · **Chớm sóng** = đủ độ rộng + ≥1 điều kiện còn lại · **Không sóng** = còn lại.
- Dùng ở 3 chỗ: Buy Score component `sector` (18 / 10 / 0 điểm), cổng Top (`không sóng` + RS < 2% → loại), checklist điểm vào (`Sóng ngành` — hiển thị **số mã tăng / số mã giảm**).
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
- RS percentile (`RsPercentileCalculator.Build`): xếp hạng RS trong rổ, **một công thức**, `days` là tham số — Top dùng 5 phiên, sóng hồi dùng 20 phiên. Rổ lọc lịch sử ≥ `max(minHistoryDays, days+1)` **và** thanh khoản, lọc **ngay khi xếp hạng** (lọc sau sẽ để mã thanh khoản thấp chiếm chỗ rồi bị loại, bóp hạng mã đủ điều kiện). Lưu ý: `% index` là hằng số chung toàn rổ nên trừ index **không** đổi thứ hạng — nó giữ đại lượng đúng nghĩa "RS"; thứ hạng chỉ đổi theo `days` và theo rổ đủ điều kiện.
- Tín hiệu phiên (`DetectSignals`) đo **1 phiên** → truyền `MarketIndex.ChangePercent`. Nơi nào cần cả hai thì nhận nguyên `MarketIndex` thay vì một con số `decimal`.

### MA stack

Xem [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md) — **không** nhân bản bảng Full/Medium/Loose ở đây.

### Hộp phẳng

Xem [`base-price-flatbox.md`](./base-price-flatbox.md).

### Chỉ báo kỹ thuật & Playbook — **không** vào Buy Score

> Nguồn: [`features/indicator-playbooks/spec.md`](../features/indicator-playbooks/spec.md) (đã land `004-indicator-playbooks`).

**Nguyên tắc bất biến (constitution §III):** 16 chỉ báo kỹ thuật (RSI, EMA, MACD, VWAP…) và các bundle VSA/POC+Delta/SMC **không tham gia tính Buy Score và không vào cổng Top**. Chúng chỉ là thước đo hậu kiểm độc lập.

| Thực tế | Entry code |
|---------|-----------|
| `BuyDecisionEngine` chỉ nhận `ISignalAnalyzer`; không đọc điểm criterion | `BuyDecisionEngine.cs:39` |
| `ScoreIndicators()` dùng cho hiển thị và hậu kiểm (`DailyCriterionScoringRunner`) | `DailyAnalysisRunner.cs:393` |
| Criterion scores không có trong 11 feature ML ranker | `OpportunityRankFeatures.cs:8` |

**Playbook dimension** (`PlaybookId` — `breakout-darvas` / `pullback-ma20` / `reversal-bounce` / `unclassified`):

- Accuracy / edge / baseline đo theo `(criterion × playbook × marketPhase)` — không còn thước chung cho mọi chỉ báo.
- Classifier (`PlaybookClassifier`) đọc cờ từ `BuyDecisionEvaluation` — không tính lại; cờ là kết quả của `BuyDecisionEngine` được expose thêm, **không ảnh hưởng điểm**.
- Cờ rollback: `CriterionAccuracyOptions.PlaybookDimensionEnabled` — tắt → ghi `unclassified`.
- 3 bundle trình độ (`BundleBeginner/Intermediate/Advanced`) đã **gỡ**; 3 bundle còn lại (VSA, POC+Delta, SMC) dùng gate/veto thay trung bình cộng.

## Khoảng trống / mâu thuẫn

| ID | Mô tả | Ghi chú |
|----|--------|---------|
| G-BD-1 | ~~Gap MA Favorable=Full khi index uptrend 1 phiên~~ | **Resolved** — xem `ma-stack-and-market-phase.md` (FTD+MA20+HL) |
| G-BD-2 | FE web ActionScore / PredictedHit chưa đồng bộ đợt hiển thị mobile | As-is; ưu tiên mobile đã làm |
| G-BD-3 | ~~`rsPercentile` có 2 định nghĩa khác bản chất~~ | **Resolved (phương án C)** — `RsPercentileCalculator.Build` là công thức duy nhất; `days` là tham số (Top 5, sóng hồi 20). Cả hai đều trừ index cùng khung và lọc thanh khoản trong lúc xếp hạng. Lệch spec `reversal-bounce/02-implementation-spec.md §5.4` đã hết |
| G-BD-4 | ~~EMA có 2 cách mồi cho cùng `period`~~ | **Resolved** — thêm `IndicatorMath.EmaAt` (SMA mồi trên prefix); `BaseQualityEvaluator.EmaAt` gọi vào. Seed của `IndicatorMath.Ema` **giữ nguyên** — không đụng criterion MA / EMA xác nhận sóng hồi |
| G-BD-5 | "Breakout" = **2 công thức, 3 tên** | `SignalAnalyzer.IsBreakout` = Donchian 20 phiên (Close > đỉnh High 20 phiên + KL > 2× TB + tăng > 3%). `FlatBoxProfile.IsBreakoutConfirmed` = 4 gate hộp phẳng. `IsDarvasBreakout` **là alias** của `IsBreakoutConfirmed`, không phải công thức thứ ba. `hasBreakoutEntry` OR hai tín hiệu là **phân loại kiểu điểm vào**, không phải trùng công thức — Top vẫn bắt hộp trước, Donchian chỉ thêm cửa kích hoạt. Siết hay không là quyết định sản phẩm, bàn riêng |
| G-BD-6 | ~~`%` / RS / FOMO dùng Close thô — gap GDKHQ bị hiểu là dump (SSI 17/08)~~ | **Resolved** — `LayLichSuChamDiem` + seed `su-kien-quyen.json`; last/chart vẫn thô |

## Tài liệu liên quan

- Domain: [`ma-stack-and-market-phase.md`](./ma-stack-and-market-phase.md), [`base-price-flatbox.md`](./base-price-flatbox.md), [`pipeline-jobs.md`](./pipeline-jobs.md)
- Sóng ngành: [`../features/sector-wave-entry-patterns/spec.md`](../features/sector-wave-entry-patterns/spec.md)
- Điều chỉnh quyền: [`../../specs/005-ohlcv-corporate-adjust/spec.md`](../../specs/005-ohlcv-corporate-adjust/spec.md)
- Rebound (tách): [`reversal-bounce.md`](./reversal-bounce.md)
- AIUP: UC-003
- Index: [`../README.md`](../README.md)
- Stub cũ: `../opportunity-scan-rules.md`, `../smartmoney-checklist.md`, `../buy-score-display.md`, `../telegram-vip-alerts-flow.md`

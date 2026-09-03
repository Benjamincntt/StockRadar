# Reversal Bounce — Đặc tả thiết kế (dài)

> **Living as-is:** [`docs/domain/reversal-bounce.md`](../../domain/reversal-bounce.md) — tin code trên disk khi lệch.
>
> File này giữ lịch sử thiết kế / phản biện. Header cũ (“chưa audit / chưa code”) **không** còn đúng với production hiện tại.
>
> Đối tượng đọc: maintainer cần ngữ cảnh thiết kế dài; vận hành hàng ngày dùng domain living.

---

## 1. Bối cảnh & vấn đề

### 1.1. Hiện trạng thị trường VN (2026)

- VN-Index 2 tuần gần nhất giảm sâu, nhiều mã chất sàn liên tục.
- Nhóm cổ phiếu chất lượng nền tảng tốt cũng bị kéo theo.
- Chiến lược hiện tại của StockRadar tập trung vào **vượt đỉnh (breakout), phá hộp Darvas, Spring/gãy nền rũ** — vốn là chiến lược **thuận xu hướng (pro-trend)**. Trong downtrend mạnh, các chiến lược này gần như không có tín hiệu hoặc tín hiệu sai.

### 1.2. Nhu cầu

Bổ sung **chiến lược ngược xu hướng (counter-trend)** chuyên tìm **đáy kỹ thuật → sóng hồi**, dành riêng cho môi trường thị trường xấu. Mục tiêu:

- Tận dụng các cú hồi kỹ thuật từ vùng bán quá mức.
- Không làm nhiễu / không làm hỏng chất lượng các chiến lược thuận xu hướng hiện tại.
- Có regime gate để **tự cấm tín hiệu** khi thị trường còn panic.

### 1.3. Ràng buộc thiết kế cứng

1. **Không sửa / nới gate** của `BuyDecisionEngine` hay `SmartMoneyOpportunitySelector` chỉ để "lọt" các mã bắt đáy.
2. Counter-trend **không đồng nghĩa** "đã tìm thấy đáy". UI phải tránh ngôn ngữ khẳng định tuyệt đối.
3. Backtest phải mô phỏng **T+2.5 (bán từ T+3)**, **trượt giá**, **khóa sàn**, **gap-entry cancellation** — không giả định lý tưởng.
4. Khi thị trường còn Panic, hệ thống **chỉ sinh watchlist**, không alert mua.
5. Triển khai theo **Phase 0 → 3**; mỗi phase có tiêu chí "pass" rõ ràng trước khi qua phase sau.

---

## 2. Mục tiêu sản phẩm

### 2.1. Mục tiêu kỹ thuật

- Detector **stateless**: stage (A/B/C/Invalidated) được suy ra từ lookback OHLCV, không phụ thuộc state machine mutable trong DB.
- Vẫn có **audit trail** thông qua snapshot bất biến (`ReversalCandidateSnapshot`).
- Có **market breadth/regime** làm prerequisite (Phase 0B).
- Có **floor-lock proxy** đủ tốt để backtest phản ánh đúng "không bán được khi kẹt sàn".

### 2.2. Mục tiêu kinh doanh

- Sinh **Watchlist** cho 3 trạng thái: Đang bán tháo / Đang cân bằng / Đã xác nhận hồi.
- Khi đủ điều kiện → sinh **trade plan** (entry, invalidation, target, time-stop, position factor).
- Không tự động chốt lời / cắt lỗ; chỉ gửi **alert** cho hệ thống Master Alert hiện hữu xử lý.

### 2.3. Phi mục tiêu (Non-goals)

- Không tìm đáy bằng phân tích cơ bản (P/E, ROE, v.v.) — đây là chiến lược kỹ thuật.
- Không thay thế các chiến lược breakout/Spring/Darvas — chỉ bổ sung.
- Không tự động thay đổi `MinTradingSessionsToSell` (luật T+3) của Master Alert.

---

## 3. Thuật ngữ & khái niệm

| Thuật ngữ | Định nghĩa |
|---|---|
| **Capitulation** | Pha bán tháo: giá giảm nhanh, thanh khoản cao, oscillator quá bán. Chưa có dấu hiệu cầu. |
| **Stabilization** | Pha cân bằng: ngừng tạo đáy mới, lực bán suy yếu, biên độ co lại. Chưa có xác nhận. |
| **Confirmation** | Pha xác nhận: xuất hiện cầu mua rõ ràng — đóng cửa vượt đỉnh ngắn hạn, dòng tiền mở rộng, close-location-value cao. |
| **Invalidated** | Setup bị vô hiệu: thủng đáy tạo setup, mất vùng xác nhận, regime quay lại Panic, RS tạo đáy mới. |
| **Market Regime** | Trạng thái thị trường tổng: `Panic` / `Stabilizing` / `ReboundConfirmed` / `Normal`. |
| **Floor Lock** | Tình trạng cổ phiếu đóng cửa tại giá sàn kèm thanh khoản cạn kiệt — bên mua thực tế không thể thoát lệnh. |
| **Setup** | Một lần đầy đủ A→B→C cho một mã, xác định bằng `SetupId`. |
| **Snapshot** | Bản ghi bất biến theo ngày cho mỗi mã × StrategyVersion × SetupId. |

---

## 4. Kiến trúc tổng quan

### 4.1. Sơ đồ luồng

```text
OHLCV (60–80 phiên) + Reference Price
            │
            ├── MarketBreadthAnalyzer ─────────► MarketBreadthSnapshot
            │                                      │
            │                                      ▼
            │                                  Market Regime
            │                                      │
            ▼                                      │
   ReversalBounceAnalyzer                          │
   (stateless, OHLCV-driven)                       │
            │                                      │
            ▼                                      ▼
   ReversalSetupResult                CounterTrendDecisionEngine
   (Stage, Score, Evidence)                      │
            │                                     │
            └──────────────►─────────────────────┤
                                                ▼
                                       SignalPriorityResolver
                                                │
                                                ▼
                                       AlertPublisher (Telegram/Mobile)
```

### 4.2. Các thành phần mới (chưa code)

| Thành phần | Trách nhiệm | Trạng thái |
|---|---|---|
| `MarketBreadthAnalyzer` | Tính breadth (%, MA20/MA50, drawdown, số mã sàn, median return) từ toàn universe → snapshot theo phiên. | Mới |
| `MarketRegimeClassifier` | Phân loại regime `Panic/Stabilizing/ReboundConfirmed/Normal` từ breadth snapshot với hysteresis stateless. | Mới |
| `ReversalBounceAnalyzer` | Stateless: nhận diện A/B/C/Invalidated từ 60–80 phiên OHLCV. Trả về stage + 6 component scores + reasons. | Mới |
| `CounterTrendDecisionEngine` | Áp dụng hard gate + regime gate + score threshold → quyết định Actionable. Sinh trade plan (entry, invalidation, target, time-stop, position factor). | Mới |
| `ReversalCandidateSnapshot` (entity) | Snapshot bất biến theo `(Symbol, TradingDate, StrategyVersion, SetupId)`. | Mới (DB) |
| `MarketBreadthSnapshot` (entity) | Snapshot breadth + regime theo `TradingDate`. | Mới (DB) |
| `SignalConfluence` (entity) | Ghi nhận cùng ngày 1 mã sinh nhiều loại tín hiệu (Reversal + Spring). | Mới (DB) |

### 4.3. Các thành phần tận dụng (kiểm chứng ở Phase 0A)

- `TechnicalIndicatorAnalyzer` (RSI/EMA/VWAP/volume bundles).
- `DailyAnalysisRunner` (điểm chạm pipeline; **không** sửa logic Top strict hiện tại).
- Backtester hiện tại (kiểm tra fill logic ở Phase 0A.3 trước khi tái sử dụng).
- `MasterAlertPositions` + luật `MinTradingSessionsToSell=3`.
- `RiskWarningIntraday` (cho defensive exit T+0…T+2).
- Mobile tab **Hiệu quả** `/performance` + Win/Flat/Lose tracking.
- North Star (`GET /performance/north-star`).
- Lịch sử alert (`GET /api/v1/performance/alert-history?kind=...`).

### 4.4. Các thành phần **không** sửa

- `BuyDecisionEngine.cs`
- `SmartMoneyOpportunitySelector.cs`
- `BaseQualityEvaluator.cs` (chỉ đọc, không gộp với counter-trend)
- `DarvasBreakoutAnalyzer.cs`

---

## 5. Logic nghiệp vụ

### 5.1. State machine suy ra từ OHLCV (stateless)

Mỗi phiên, với mỗi mã trong universe active:

```text
Input: 60–80 phiên OHLCV (unadjusted), ReferencePrice, ATR14, MA20, MA50, RSI14
Pipeline:
  1. Tính features (Drawdown, A-vs-MA, Down-Volume, CLV, Range, RS, Volume-vs-Avg)
  2. Tìm PeakGầnNhất (rolling high trong 60 phiên)
  3. Tìm CapitulationLow (low sau PeakGầnNhất)
  4. Tính HasCapitulation
  5. Tính HasStabilized (từ phiên sau CapitulationLow)
  6. Tính IsConfirmed (tại phiên hiện tại)
  7. Tính IsInvalidated
  8. Suy ra Stage ∈ {A, B, C, Invalidated}
```

#### 5.1.1. HasCapitulation (đợt bán tháo)

Phải thỏa **tất cả**:

```text
DrawdownFromPeak >= MinDrawdownPercent       (vd 18%)
DrawdownInAtr     >= MinDrawdownAtrMultiple   (vd 2.5 ATR)
```

**Và** thỏa ít nhất 1 trong 3:

```text
IsStatisticallyOversold   (RSI14 < OversoldThreshold, vd 25)
HasSellingClimax         (Volume-phiên-giảm-mạnh > ClimaxVolMultiple × AvgVol20)
HasMultipleWideDownBars  (≥ N phiên range/ATR > X trong 10 phiên gần CapitulationLow)
```

> **Lưu ý:** Volume climax không bắt buộc. Một số mã tạo đáy bằng giảm kéo dài + cạn thanh khoản, không có climax rõ.

#### 5.1.2. HasStabilized (lực bán suy yếu)

Phải thỏa **tất cả**:

```text
NoMaterialNewLow         (Low của [T-N..T] không thủng CapitulationLow quá tolerance × ATR)
RangeContraction         (ATR hiện tại < ATR giai đoạn Capitulation × ContractionRatio)
```

**Và** thỏa ít nhất 1 trong 3:

```text
DownVolumeDryUp          (avg down-volume của 5 phiên ổn định < avg down-volume 5 phiên trước)
RepeatedLowerWick        (≥ 2 phiên lower-wick-ratio > threshold)
RelativeStrengthImproving (RS-slope trong 5 phiên ổn định > 0)
```

> **Tolerance** cho "không tạo đáy mới" không nên là equality tuyệt đối. Dùng `CapitulationLow - tolerance × ATR` (vd `tolerance=0.2`).

#### 5.1.3. IsConfirmed (cầu mua xuất hiện)

Phải thỏa **đồng thời** cả 4:

```text
HasPriceBreak       (Close_T > HighestHigh(T-2..T-1))   HOẶC (Close_T > EMA5/EMA10)
                    HOẶC (Close_T > AnchoredVWAP từ phiên Capitulation nếu có dữ liệu)
HasStrongClose      (CloseLocationValue > threshold, vd 0.65)
HasDemandExpansion  (Volume_T > AvgVol_ỔnĐịnh × DemandMultiplier, vd 1.4)
NotOverextended     (Close_T không gap-up > GapCancelThreshold × ATR14, vd 0.5)
```

#### 5.1.4. IsInvalidated (mất hiệu lực)

Nếu **bất kỳ** điều nào xảy ra ở phiên hiện tại → Stage = Invalidated:

```text
Close_T < CapitulationLow - tolerance × ATR
Close_T < ConfirmationReference - ConfirmationBuffer × ATR
MarketRegime == Panic
RelativeStrength tạo đáy mới so với VN-Index
```

### 5.2. Stage suy ra

```text
if HasCapitulation == false: → không phải counter-trend (bỏ qua mã này)
elif IsInvalidated:          → Stage = Invalidated
elif IsConfirmed:            → Stage = C (Confirmed)
elif HasStabilized:          → Stage = B (Stabilizing)
else:                        → Stage = A (Capitulating)
```

> **Một mã đang ở Stage A có thể chuyển sang B, rồi C, rồi Invalidated.** Mỗi lần Stage thay đổi tạo snapshot mới với `SetupId` cố định (nếu chưa từng đạt C) hoặc `SetupId + ConfirmationDate` (nếu đã đạt C).

### 5.3. Market Regime (chạy mỗi phiên trước analyzer)

#### 5.3.1. Snapshot breadth (từ OHLCV toàn universe)

```text
Universe: Stock.IsActive
Exclude: NewListings (< 20 phiên), Restricted, ETF/Warrants (nếu filter)
Metrics:
  PctAboveMA20, PctAboveMA50, PctAboveEMA20
  PctNewLow20, PctUp, PctDown, PctFloorClose
  MedianReturn, MedianTurnover, FloorCount, CeilingCount
  VnIndexDrawdown, VnIndexDistanceToMA20
```

#### 5.3.2. Classifier với hysteresis stateless

```text
Panic:
  Enter khi: VnIndexDrawdown < -X% AND PctAboveMA20 < Y% AND FloorCount ≥ N
  Exit khi: breadth cải thiện liên tục 2 phiên (cả PctAboveMA20 tăng VÀ FloorCount giảm)

Stabilizing:
  Mặc định khi vừa thoát Panic hoặc vừa thoát ReboundConfirmed
  Hoặc: PctAboveMA20 tăng 2 phiên liên tiếp nhưng chưa tới ngưỡng Rebound

ReboundConfirmed:
  Enter khi: VnIndex lấy lại MA20 từ dưới lên AND PctAboveMA20 ≥ Z%
  Exit khi: breadth xấu trở lại (1 phiên xấu là đủ — hạ nhanh hơn nâng)

Normal:
  Khi không thỏa bất kỳ điều kiện nào ở trên
```

### 5.4. Score 6 trục (cố định weight ở MVP)

| Trục | Max | Mô tả |
|---|---:|---|
| CapitulationScore | 15 | Đã giảm đủ sâu và quá bán chưa |
| StabilizationScore | 20 | Đã ngừng rơi, cạn cung chưa |
| DemandScore | 15 | Có cầu mua xác nhận chưa |
| RelativeStrengthScore | 15 | Đang hồi tốt hơn thị trường/ngành không |
| LiquidityScore | 10 | Có khả năng vào/ra được không |
| RiskPenalty | −10 | Sàn liên tiếp, gap, biến động cực đoan |
| **Tổng tối đa** | **100** | |

> Regime **không đổi weight** ở MVP. Regime chỉ đổi **threshold**, **gate bắt buộc**, và **position factor** (xem 5.5).

### 5.5. Hard gate (luôn thắng score)

```text
Actionable =
    Stage == Confirmed
    AND MarketRegime != Panic
    AND HasDemandConfirmation (= DemandScore >= MinDemand)
    AND IsTradable (không bị Restricted, có đủ thanh khoản)
    AND HasAcceptableEntryRisk (R/R tới target ≥ MinRR)
    AND TotalScore >= RegimeThreshold
```

Bảng threshold/position factor theo regime:

| Regime | MinScore | MinDemand | PositionFactor | Notes |
|---|---:|---:|---:|---|
| Panic | — | — | 0 | Không Actionable |
| Stabilizing | 80 | 18/20 | 0.25 | Cho mua thăm dò với tỷ trọng nhỏ |
| ReboundConfirmed | 72 | 12/15 | 0.50 | Cho nhiều tín hiệu hơn |
| Normal | 75 | 14/15 | 0.40 | Không khuyến khích nhưng không cấm |

---

## 6. Dữ liệu

### 6.1. Nguồn sự thật cho detector

> **OHLCV unadjusted**, không phải adjusted. Lý do: tính giá sàn/giá trần và kiểm tra floor-lock đòi hỏi giá thực tế tại phiên đó.

### 6.2. Schema OHLCV đề xuất (cần audit Phase 0A.2)

```text
StocksHistory (entity)
  Symbol, TradingDate
  Open, High, Low, Close, Volume           -- unadjusted (raw)
  OpenAdj, HighAdj, LowAdj, CloseAdj       -- adjusted (cho chart/trend)
  ReferencePrice                            -- giá tham chiếu ngày T
  FloorPrice, CeilingPrice                  -- tính từ ReferencePrice + biên độ + tick size
  AdjustedFactor                            -- hệ số điều chỉnh
  CorporateActionFlag                       -- dividend/split/issue
```

```text
ExchangeRules (entity)
  EffectiveDate, Exchange, Board, PriceBand, TickSize, LotSize
```

```text
CorporateAction (entity)
  Symbol, ExDate, ActionType, Ratio, Amount
```

### 6.3. Snapshot bất biến

```text
ReversalCandidateSnapshot
  Id
  Symbol
  TradingDate
  Stage (A | B | C | Invalidated)
  SetupId
  CapitulationDate, CapitulationLow, CapitulationClose
  RecoveryAttemptCount
  ComponentScores (Capitulation, Stabilization, Demand, RelativeStrength, Liquidity, RiskPenalty)
  TotalScore
  MarketRegime
  BreadthSnapshotId
  StrategyVersion (semver, vd "reversal-bounce@1.0.0")
  AlgorithmParametersHash
  SchemaVersion
  RunBatchId (GUID)
  Reasons (mảng cấu trúc: tên metric + giá trị)
  CreatedAtUtc
```

```text
MarketBreadthSnapshot
  Id, TradingDate
  Metrics (cột riêng hoặc JSON)
  Regime (Panic | Stabilizing | ReboundConfirmed | Normal)
  HysteresisState
  Version
```

```text
SignalConfluence
  Symbol, TradingDate
  Signals[] (SignalType, StrategyVersion, SetupId)
```

### 6.4. Quy tắc idempotent

```text
(Symbol, TradingDate, StrategyVersion, SetupId)   -- unique cho ReversalCandidateSnapshot
(Symbol, TradingDate)                             -- unique cho MarketBreadthSnapshot
```

### 6.5. Floor-lock proxy

Hai mức (chỉ dùng unadjusted Close + FloorPrice):

```text
LikelyFloorLocked:
  Close_T == FloorPrice_T (sai số ≤ 1 tick)
  VÀ Close_T == Low_T
  VÀ Volume_T < FloorVolThreshold × AvgVol20 (vd 0.5)

FloorCloseWithExitRisk:
  Close_T == FloorPrice_T (sai số ≤ 1 tick)
  VÀ Close_T == Low_T
  (không yêu cầu volume — đã coi là rủi ro thoát)
```

---

## 7. Trade plan

Khi Actionable, sinh trade plan gắn trên signal:

```text
EntryReference        = Close_T (giá xác nhận)
MaxEntryPrice         = EntryReference × (1 + GapAcceptance × ATR14_Percent)  (vd 1.5%)
InvalidationPrice     = min(CapitulationLow - tolerance×ATR,
                           ConfirmationReference - ConfirmationBuffer×ATR)
NearestSupplyZone     = min gap-down-resistance, EMA20, prev-low, supply-cluster
FirstTarget           = NearestSupplyZone (ưu tiên vùng thực tế gần nhất)
TimeStopSessions      = MaxHoldSessions (vd 10 phiên giao dịch)
PositionFactor        = theo Regime (xem 5.5)
RiskWarnings[]        = ["near-floor", "low-liquidity", "against-trend", ...]
```

### 7.1. Entry trong T+1

- Mua tại **Open(T+1)**, không mua tại Close(T) — tránh look-ahead bias.
- Nếu Open(T+1) gap-up > GapCancelATRMultiple × ATR14 (vd 0.5) → **bỏ lệnh** (gap-entry cancellation).
- Nếu Open(T+1) gap-up nhẹ (≤ threshold) → cộng slippage mô phỏng (xem 7.4).

### 7.2. Exit

- **Hard exit**: chỉ thực thi từ phiên T+3 trở đi (tuân thủ `MinTradingSessionsToSell=3`).
- **Defensive exit**: T+0…T+2 chỉ phát **RiskWarning** (đồng bộ `RiskWarningIntraday`). Chưa tự bán.
- Nếu invalidation xảy ra nhưng phiên bị `LikelyFloorLocked`:
  - Không fill exit ở phiên đó.
  - Dời sang phiên giao dịch tiếp theo.
- Nếu `FloorCloseWithExitRisk` → giả định fill được nhưng áp slippage bảo thủ.

### 7.3. Time stop

Nếu sau `TimeStopSessions` mà chưa chạm target/invalidation → thoát theo close-of-day. Counter-trend **không** biến thành vị thế nắm giữ dài hạn.

### 7.4. Slippage mô phỏng (backtest)

Hai tầng:

```text
BaseSlippage           = theo Exchange × Board (config)
GapImpact              = max(0, Open(T+1)/Close(T) - 1 - 0.5%) × 0.5
FloorLockPenalty       = nếu phiên trước LikelyFloorLocked: +0.3%
OffHourPenalty         = nếu exit sau ATC: +x%
TotalSlippage = BaseSlippage + GapImpact + FloorLockPenalty + OffHourPenalty
```

### 7.5. Backtest chống look-ahead bias

- Không dùng High/Low phiên T+1 để xác nhận lại tín hiệu phiên T.
- Không dùng Close phiên T+1 để đánh giá exit đặt trước đó.
- Invalidation có thể dùng Low trong ngày, nhưng phải ghi rõ đây là proxy "chạm đáy", không phải "đóng cửa thủng đáy".

### 7.6. Backtest hai kịch bản

```text
Kịch bản A: Hard exit từ T+3 (tuân thủ Master Alert hiện tại)
Kịch bản B: Cho phép defensive exit sớm (upper-bound tiềm năng)
```

Nếu hiệu quả Kịch bản B vượt trội → đề xuất sửa `MinTradingSessionsToSell` ở review riêng.

---

## 8. Dedupe với chiến lược khác

Tại lớp `SignalPriorityResolver` (orchestration), sau khi tất cả engine sinh tín hiệu:

```text
Priority:
  1. DarvasBreakout
  2. VCP / Spring
  3. Top strict Buy Score (SmartMoneyOpportunitySelector)
  4. ReversalBounce (chỉ trong Stabilizing / ReboundConfirmed / Normal)
```

Quy tắc:

- Một mã một ngày chỉ **1 alert mua** theo priority cao nhất.
- Nếu mã đạt cả Spring lẫn ReversalBounce: giữ Spring, gắn tag `[Oversold Confluence]`.
- ReversalBounce **không** phát nếu cùng ngày Top strict đã đạt ngưỡng.
- Ghi nhận confluence vào `SignalConfluence` để phân tích về sau.

---

## 9. API & tích hợp (dự kiến)

### 9.1. Domain/API mới (chưa code)

```text
GET  /api/v1/reversal-bounce/candidates
GET  /api/v1/reversal-bounce/candidates/{symbol}
GET  /api/v1/reversal-bounce/market-regime
POST /api/v1/reversal-bounce/backtest/run          (admin)
GET  /api/v1/reversal-bounce/backtest/results/{id}
GET  /api/v1/reversal-bounce/performance?range=... (T+2.5, T+5, T+10)
```

### 9.2. Tận dụng API hiện có

```text
GET /api/v1/early-recovery                  -- kiểm tra trùng logic
GET /api/v1/backtest/smartmoney             -- framework backtest
GET /performance/north-star
GET /api/v1/performance/alert-history?kind=reversal-bounce (mở rộng kind)
```

### 9.3. Performance metrics mới

```text
WinRate, FlatRate, LoseRate theo regime
MaxFavorableExcursion (MFE), MaxAdverseExcursion (MAE)
SessionsToRecover, SessionsToFail
FloorLockCount, FloorLockExitDeferredCount
AvgSlippageBps
R_Multiple trung bình (R = risk tới invalidation)
```

---

## 10. UI / Mobile

### 10.1. Section mới trong tab Hiệu quả (hoặc tab mới)

```text
Tab "Sóng hồi" (đặt cạnh tab "Hiệu quả")
  ─ Đang bán tháo (Stage A)        -- watchlist
  ─ Đang cân bằng (Stage B)        -- watchlist
  ─ Đã xác nhận hồi (Stage C)      -- watchlist + trade plan
  ─ Mất hiệu lực (Invalidated)     -- lịch sử
```

> **Không hiển thị "Đã tạo đáy".** Ngôn ngữ phải trung lập: "Ứng viên cân bằng", "Đang xác nhận hồi", "Hồi kỹ thuật được xác nhận".

### 10.2. Card mỗi mã

```text
Symbol / Exchange / Board
Stage badge
Score / TotalScore + 6 trục (radial bar hoặc grid)
Evidence collapsible (Reasons[])
Trade plan (chỉ Stage C):
  EntryReference / MaxEntryPrice
  InvalidationPrice / R_R ratio
  FirstTarget / NearestSupplyZone
  TimeStopSessions / PositionFactor
  RiskWarnings[]
History: Mini-chart (Price + Volume + MA20 + MA50 + setup zone)
```

---

## 11. Lộ trình triển khai

### Phase 0A — Audit codebase (chưa code)

**Mục tiêu:** xác minh giả định trước khi viết code. Xem file hướng dẫn riêng `docs/_archive/reversal-bounce-phase0a-audit-instructions.md`.

Ticket:
- 0A.1 — Audit EarlyRecovery hiện tại
- 0A.2 — Audit Schema OHLCV (Adjusted/Unadjusted)
- 0A.3 — Audit Backtester fill logic + T+3
- 0A.4 — Audit Market Breadth / Regime engine

**Pass:** tất cả 4 ticket có báo cáo và đã ký "OK to proceed" hoặc "needs schema migration".

### Phase 0B — Market Breadth & Regime

- Implement `MarketBreadthAnalyzer` + `MarketRegimeClassifier`.
- Backfill lịch sử breadth cho toàn bộ dữ liệu hiện có.
- Kiểm tra regime trên 5–10 giai đoạn giảm mạnh nhất lịch sử VN.
- Unit test với các kịch bản biên: 1 phiên tăng đột biến, 1 phiên giảm đột biến, breadth cải thiện từ từ.

**Pass:** regime snapshot có sẵn cho mọi phiên lịch sử; chưa sinh tín hiệu cổ phiếu.

### Phase 0C — Stateless ReversalBounceAnalyzer

- Implement A/B/C/Invalidated từ 60–80 phiên OHLCV.
- Unit test bằng chuỗi OHLCV tổng hợp:
  - Đang rơi liên tục.
  - Bán tháo rồi cân bằng rồi xác nhận.
  - False confirmation.
  - Hai đáy.
  - Phiên sàn mất thanh khoản.
  - Spring hợp lệ nhưng không phải counter-trend.
  - Corporate-action-like gap.

**Pass:** pass tất cả unit test; stage được suy ra đúng trên ≥ 95% snapshot thủ công.

### Phase 0D — Backtest execution thực tế

- Tái sử dụng backtester (sau khi sửa fill logic ở 0A.3 nếu cần).
- Áp dụng:
  - Signal tại Close(T).
  - Fill tại Open(T+1).
  - Hard exit từ T+3.
  - Floor-lock defer.
  - Fees + slippage (2 tầng).
  - Gap-entry cancellation.
- Walk-forward validation: train/validation/test theo thời gian, không dùng cùng giai đoạn để tune và đo.

**Pass:** 2 kịch bản (T+3 strict vs defensive early) đều chạy được; kết quả có ý nghĩa thống kê (ít nhất 30 setups).

### Phase 1 — Shadow mode

- Chạy hằng ngày trong Daily Batch Runner (không alert user).
- Lưu snapshot, signal, trade plan, outcome T+2.5, T+5, T+10, MFE/MAE, số phiên không thoát được, hiệu quả theo regime.
- Daily report internal.

**Pass:** ≥ 30 ngày dữ liệu, ≥ 50 setups, WinRate ≥ 1% (theo khái niệm hiện tại), không có tail-loss > R × 2.

### Phase 2 — Watchlist

- Hiển thị 3 stage trong tab Sóng hồi.
- Chỉ Stage C mới có trade plan; A và B chỉ quan sát.
- Chưa gửi Telegram/Push.

**Pass:** UI ổn định, không crash, load time hợp lý.

### Phase 3 — Alert thử nghiệm

- Chỉ alert trong Stabilizing / ReboundConfirmed (không trong Panic / Normal).
- Ngưỡng cao hơn backtest +5 điểm.
- Giới hạn số tín hiệu mỗi ngày (vd ≤ 5).
- Tỷ trọng vị thế theo `PositionFactor`.
- **Không** dùng dynamic weight ở MVP.

**Pass:** ≥ 60 ngày chạy production, không có sự cố nghiêm trọng (alerts spam, false signal liên tục), hiệu quả theo dõi sát backtest trong biên ±10%.

---

## 12. Rủi ro & giảm thiểu

| Rủi ro | Giảm thiểu |
|---|---|
| Bắt dao rơi | Hard gate đòi hỏi Stage C + DemandScore ≥ threshold + Regime ≠ Panic |
| Look-ahead bias | Fill tại Open(T+1); không dùng High/Low T+1; unit test với lookback cố định |
| Kẹt sàn khi exit | Floor-lock proxy + defer exit + slippage 2 tầng |
| Overfit do data ngắn | Phase 0D walk-forward; Phase 1 shadow mode tối thiểu 30 ngày |
| Trùng alert với Spring/Top strict | SignalPriorityResolver + SignalConfluence |
| Nhiễu chất lượng Top strict | Tách hoàn toàn `CounterTrendDecisionEngine`; không sửa `BuyDecisionEngine` |
| Regime đổi liên tục | Hysteresis stateless: 2 phiên cải thiện mới nâng, 1 phiên xấu là hạ |
| Confusion UI: "đã tạo đáy" | Ngôn ngữ "hồi kỹ thuật được xác nhận", "ứng viên cân bằng" |

---

## 13. Tài liệu liên quan

- `docs/architecture.md` — kiến trúc tổng thể StockRadar.
- Related docs (cũ → living):
  - `docs/domain/buy-decision.md` — Top / BuyDecision
  - `docs/domain/base-price-flatbox.md` — flatBox / Darvas
  - `docs/domain/buy-decision.md` — VIP / Master Alert T+3
  - Stub path cũ: `opportunity-scan-rules.md`, `base-price-engine.md`, `telegram-vip-alerts-flow.md`
- `docs/_archive/reversal-bounce-phase0a-audit-instructions.md` — hướng dẫn cho model nhỏ hơn thực thi Phase 0A.
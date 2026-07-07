# Luồng quét Top cơ hội (Daily Analysis) — rule chi tiết

> **Mục đích doc:** Phân tích / chỉnh ngưỡng strict vs fallback.  
> **Nguồn sự thật:** code trên disk (`DailyAnalysisRunner`, `BuyDecisionEngine`, `SmartMoneyOpportunitySelector`, `appsettings.json`).  
> **Liên quan:** [base-price-engine.md](./base-price-engine.md) (hộp Darvas + 4 gate breakout).

---

## 1. Tổng quan luồng

```
Job 1 (History backfill)
  → Universe HOSE + HNX + UPCOM, lọc KL/giá/IPO
  → OHLCV trong DB (bảng Stocks, history)

Job 2 (Daily session sync)
  → Append nến phiên T cho mã active
  → (tách) DarvasBreakoutAlertPublisher — alert phá hộp toàn universe

Daily Analysis  ←── LUỒNG NÀY (Top cơ hội)
  → Quét TOÀN BỘ mã active trong DB
  → SmartMoney + Buy Score + gates strict
  → Xếp hạng → lưu DailyOpportunities (≤ MaxResults)
  → API GET /opportunities · VIP Telegram (subset Top trong phiên)

IntradayScanner → SessionRadar     (KHÁC — đột biến |±3%|, KL≥1M, không Buy Score)
OpportunityIntradayMonitor → VIP     (KHÁC — chỉ symbol đã có trong DailyOpportunities hôm nay)
```

### Khi nào chạy?

| Trigger | Entry | Ghi chú |
|---------|--------|---------|
| Quartz `daily-analysis-close-trigger` | `DailyAnalysisJob` | Sau Job 2, ~15:05 VN (sau đóng cửa) |
| Quartz `daily-analysis-morning-trigger` | `DailyAnalysisJob` | 11:30 VN nếu `MorningRunEnabled: true` |
| UI / API | `POST /api/v1/opportunities/run-analysis` | Cooldown 15 phút; bỏ post-processing nặng (shadow) để trả nhanh |
| Pipeline job | `POST /api/v1/market/jobs/analysis` | Cần `X-Sync-Key`; chạy full post-processing |

### Phiên mục tiêu (`ForTradingDate`)

| Hàm | Cutoff VN | Ý nghĩa |
|-----|-----------|---------|
| `GetPostSessionAnalysisDate()` | **15:00** | Ngày ghi vào DB khi chạy phân tích |
| `GetActiveOpportunityDate()` | **15:10** | Ngày UI/API hiển thị làm “phiên mục tiêu” |

Sau 15:10 VN trong ngày giao dịch → UI target **phiên mai** (ví dụ tối 07/07 → list cho 08/07).

---

## 2. Universe đầu vào

**Nguồn:** `IJobStockRepository.GetAllAsync()` — mọi mã `IsActive && !TradingRestricted`.

**Job 1** (`HistoryJobOptions`, `appsettings.json`):

| Config | Production hiện tại |
|--------|---------------------|
| `Universe` | `Groups` |
| `Groups` | `HOSE`, `HNX`, `UPCOM` |
| `MinAvgDailyVolume` (rescreen) | 500,000 CP/phiên (20 phiên) |
| `MinClosePriceVnd` | 8,000đ |
| `ExcludeIpoWithinDays` | 365 |

Mã không pass rescreen → `IsActive = false` → **không** vào quét phân tích.

---

## 3. Pipeline phân tích (từng bước)

File: `DailyAnalysisRunner.RunAsync`

### Bước A — Build context thị trường

`SmartMoneyOpportunitySelector.BuildContext(universe, VNINDEX, runup, smartMoney, adaptive, calibration)`

1. **Pha thị trường** (`ClassifyMarket`):
   - Uptrend → `Favorable`
   - Sideway → `Neutral`
   - Downtrend + VNINDEX < −1.5% → `Unfavorable`, còn lại `Neutral`

2. **Xếp hạng ngành** (`BuildSectorSnapshots`):
   - Bỏ sector `Khác` / `Other` / `N/A`
   - Ngành cần ≥ **3** mã và mỗi mã ≥ `MinHistoryDays` (21)
   - Điểm composite (RS, volume, cap proxy, số mã) — trọng số `SectorRankWeights`
   - Rank 1 = ngành mạnh nhất → dùng `TopSectorCount` (5) ở gate

3. **Adaptive scoring** + **Hit calibration** — điều chỉnh trọng số từng tiêu chí Buy Score theo lịch sử T+2.5

### Bước B — Lọc từng mã (STRICT)

Với mỗi `stock` trong universe:

```
eval = smartMoney.Evaluate(stock, context)
```

Trong `Evaluate`:
- `decision = buyDecision.Evaluate(stock, context)`
- Nếu `!decision.PassesTopFilter` → **loại** (lý do trong `GateFailure`)
- Ngược lại → `SmartMoneyEvaluation` với `Passes = true`, `Score = BuyScore`

Sau đó `DailyAnalysisRunner`:

```
if (!smartMoney.PassesFilter(eval, sm)) continue
  → PassesFilter = eval.Passes && eval.Score >= MinPassScore (62)

if (cfg.MinScore > 0 && eval.Score < cfg.MinScore) continue
  → MinScore DailyAnalysis = 60
```

**Hai lớp điểm:** `MinPassScore` (62) trong gate Buy + `MinScore` (60) job phân tích.

### Bước C — Xếp hạng & cắt Top

```csharp
ordered = candidates
  .Select(c => BuyDecision + ML P(hit) + ...)
  .OrderByDescending(MlProb)      // OpportunityRanker
  .ThenByDescending(SmartMoney Score)
  .ThenBy(SectorRank)
  .ThenByDescending(RS 5d)
  .ThenBy(Symbol)
  .Take(MaxResults)               // 10
```

- ML model có → sort theo `P(hit) T+2.5`
- Không model → fallback `PredictedHitPercent` heuristic

### Bước D — Fallback relaxed (**BẬT** trên dev/local)

`RelaxedFallbackEnabled: **true**` → nếu strict = 0 mã, tự kích hoạt rổ relaxed (3–5 mã tốt nhất trong lớp).

Khi **bật** (`BuildRelaxedCandidates`):

| Rule | Giá trị |
|------|---------|
| `FallbackMinScore` | Buy Score ≥ **45** (hạ xuống **35** nếu chưa đủ `FallbackMinResults`) |
| `FallbackMinResults` | Tối thiểu **3** mã |
| `FallbackMaxResults` | Tối đa **5** |
| Bỏ qua gate | Chỉ loại nếu gate chứa "phân phối" hoặc "FOMO" |
| **Không** yêu cầu `PassesTopFilter` | Mã vào list với `Passes = false` |
| API `analysisStatus` | `relaxed_fallback` khi lần quét dùng fallback |

### Bước E — Lưu DB

- `DailyOpportunities` — replace theo `ForTradingDate`
- `SetupTracks` — seed theo dõi T+2.5
- `DailyAnalysisRuns` — `StocksScored`, `OpportunitiesSaved`, `GeneratedAt`

### Bước F — Post-processing (chỉ job Quartz / full run)

- Shadow analysis (variant MinPassScore 58/60/62)
- Criterion scoring T-1
- Đo outcome T+2.5

**Chạy tay từ UI:** bỏ qua bước F (`runPostProcessing: false`).

---

## 4. Buy Score — thành phần điểm (tối đa ~100 sau chuẩn hóa adaptive)

File: `BuyDecisionEngine.BuildScore`

| ID | Nhãn | Base max | Điều kiện điểm đầy đủ |
|----|------|----------|------------------------|
| `market` | Thị trường | 12 | Pha thuận (12) / trung tính (6) / bất lợi (0) |
| `sector` | Ngành | 18 | Top 3 (18) / top ≤5 (10) / còn lại (0) |
| `rs` | Relative Strength | 20 | RS ≥3% (20) / RS ≥0 (12) / âm (0) |
| `base` | Nền giá / hộp | 18 | `hasFlatBoxBreakout` (Darvas confirmed + chưa FOMO) |
| `breakout` | Breakout + volume | 22 | `hasBreakoutEntry` |
| `shakeout` | Shakeout đáy nền | 10 | `hasShakeoutEntry` |
| `volume` | Volume spike | 8 | `VolumeSpike` signal |
| `wyckoff` | Pha tăng giá | 5 | Markup phase |
| `trend` | Xu hướng / MA | 5 | `hasMaStack` |

Điểm raw được **chuẩn hóa** theo adaptive profile (`NormalizeAdaptiveScore`).

### Các flag kích hoạt entry (dùng trong gate & điểm)

```csharp
meetsSessionBar = change phiên > MinSessionChangePercent (2%)
                  AND volume phiên >= MinSessionVolume (300,000)

hasBreakoutEntry = (Signal Breakout OR DarvasBreakout)
                   AND meetsSessionBar
                   AND (DarvasBreakout OR volRatio >= BreakoutMinVolumeRatio 1.5)

hasShakeoutEntry = flatBox hợp lệ
                   AND IsShakeoutFromBase (rũ đáy nền + hồi)
                   AND meetsSessionBar

hasMaStack = HasBullishMaStack (xem mục 6)

hasFlatBoxBreakout = flatBox.IsBreakoutConfirmed
                     AND gainFromBoxTop <= MaxGainFromBasePercent (10%)
```

**Breakout 20 phiên** (`SignalType.Breakout`): `Close > max High 20 phiên`, vol > 2× TB20, tăng > 3% — **khác** Darvas breakout.

---

## 5. Gate STRICT Top cơ hội (`PassesTopFilter`)

`gateFailure == null` mới pass. Thứ tự kiểm trong `ResolveTopGateFailure`:

| # | Gate | Điều kiện FAIL | Config |
|---|------|----------------|--------|
| 1 | Lịch sử | `history.Count < MinHistoryDays` | **21** phiên |
| 2 | Thanh khoản TB | `avgVol < MinAvgDailyVolume` | **800,000** CP/phiên |
| 3 | Phân phối | `IsDistribution(history)` | 5 phiên: vol tăng, giá đi ngang, ≥2 râu trên |
| 4 | Darvas breakout / setup | Không `IsBreakoutConfirmed` **và** không `IsSetupZone` | Breakout confirmed **hoặc** hộp hợp lệ + giá test đáy/đỉnh hộp (≤3% biên) |
| 5 | FOMO | `gainFromBoxTop > MaxGainFromBasePercent` | **> 10%** so đỉnh hộp |
| 6 | MA stack | `!hasMaStack` | MA20>50>100>200 (hoặc nới theo số phiên) |
| 7 | TT xấu + RS | `MarketPhase == Unfavorable && rs5 < 1%` | |
| 8 | Ngành yếu + RS | `sectorRank > 5 && rs5 < 2%` | `TopSectorCount = 5` |
| 9 | Chưa kích hoạt phiên | Không breakout/shakeout **và** không breakout confirmed **và** không setup zone | Phiên **> 2%**, KL **≥ 300k** (bỏ qua nếu setup zone) |
| 10 | RS âm | `rs5 < 0` và không breakout entry | |
| 11 | Buy Score | `score < MinPassScore` | **< 62** |

**Lưu ý gate 4 (Giải pháp 2):** Top strict chấp nhận mã **đang tích lũy trong hộp Darvas** nếu giá re-test cạnh dưới hoặc mấp mé cạnh trên (`DarvasBreakoutAnalyzer.IsSetupZone`) — không bắt buộc `IsBreakoutConfirmed`. Điểm nền giá: **12/18** (setup zone) vs **18/18** (breakout confirmed).

---

## 6. MA stack (`HasBullishMaStack`)

`MaStack.Enabled: true`

| Số phiên | Rule |
|----------|------|
| < 20 | Fail nếu enabled |
| < 50 (`MinSessionsForMa50`) | MA20 ≥ Close × 0.97 |
| < 200 (`MinSessionsForFullStack`) | MA20 > MA50 |
| ≥ 200 | MA20 > MA50 > MA100 > MA200 |

→ Nhiều mã VN **fail gate 6** vì chưa đủ 200 phiên hoặc MA chưa xếp chồng đủ 4 đường.

---

## 7. Hộp Darvas & 4 gate breakout

Chi tiết đầy đủ: [base-price-engine.md](./base-price-engine.md).

### Nhận diện hộp (`DarvasBreakoutAnalyzer.Analyze`)

- Quét `boxEnd` lùi từ phiên gần nhất; hộp **không** gồm phiên phá vỡ
- Cửa sổ `ConsolidationMinSessions` … `MaxBaseWindowSessions` (**10–45** phiên)
- `PassesDarvasBox` (biến thể breakout): biên hộp ≤ `BreakoutMaxBoxHeightPercent` (**10%**), không bắt KL cạn 1/3 cuối

### 4 gate trên phiên phá vỡ (`PassesBreakoutGates`)

| Gate | Rule | Config `PriceRunupFilter.Darvas` |
|------|------|----------------------------------|
| Vượt biên | `Close > Max(Close)` hộp | |
| Xung lực giá | `(Close − Close_prev) / Close_prev × 100 ≥` | **2.5%** (`BreakoutMinPriceGainPercent`) |
| KL bùng nổ | `Vol / AvgVol(hộp) ≥` | **1.5×** (`BreakoutMinVolumeMultiplier`) |
| Râu trên | `(High − Close) / (High − Low) ≤` | **0.25** (`BreakoutMaxUpperShadowRatio`) |

Pass trên **bất kỳ** phiên sau hộp → `IsBreakoutConfirmed = true` (kể cả pullback sau đó).

### FOMO filter (sau breakout)

`GainFromBoxTopPercent` từ **nến cuối** so đỉnh hộp — gate Top nếu **> 10%**.

---

## 8. Shakeout entry (thay breakout)

`IsShakeoutFromBase`:

- Có hộp hợp lệ (`HasValidBox`)
- Trong ~8 phiên gần nhất có nến `Low < baseLow` (rũ đáy)
- KL nến rũ < 1.2× TB volume
- Nến cuối `Close > baseLow`
- **Và** `meetsSessionBar` (phiên >2%, KL ≥300k)

---

## 9. Config production hiện tại (`appsettings.json`)

### `MarketJobs.DailyAnalysis`

```json
{
  "Enabled": true,
  "MorningRunEnabled": true,
  "MorningRunHour": 11,
  "MorningRunMinute": 30,
  "MinScore": 60,
  "MaxResults": 10,
  "RelaxedFallbackEnabled": true,
  "FallbackMinScore": 45,
  "FallbackMaxResults": 5,
  "FallbackMinResults": 3,
  "ManualAnalysisCooldownMinutes": 15
}
```

### `SmartMoney`

```json
{
  "MinHistoryDays": 21,
  "MinAvgDailyVolume": 800000,
  "MinSessionVolume": 300000,
  "MinSessionChangePercent": 2,
  "BreakoutMinVolumeRatio": 1.5,
  "TopSectorCount": 5,
  "MinPassScore": 62,
  "MaxGainInBasePercent": 5,
  "MaStack": { "Enabled": true, "MinSessionsForMa50": 50, "MinSessionsForFullStack": 200 }
}
```

### `PriceRunupFilter` (FOMO + Darvas)

```json
{
  "ConsolidationMinSessions": 10,
  "MaxScanSessions": 90,
  "MaxBaseWindowSessions": 45,
  "MaxGainFromBasePercent": 10,
  "Darvas": {
    "MaxBoxHeightPercent": 9,
    "ShadowTolerancePercent": 3,
    "VolDryUpRatio": 0.8,
    "TouchThresholdPercent": 1.5,
    "MinTopTouches": 2,
    "MinBottomTouches": 2,
    "MaxLast3AvgRangePercent": 3.5,
    "BreakoutMinPriceGainPercent": 4,
    "BreakoutMinVolumeMultiplier": 2,
    "BreakoutMaxUpperShadowRatio": 0.25,
    "BreakoutMaxBoxHeightPercent": 10
  }
}
```

---

## 10. API & trạng thái UI

`GET /api/v1/opportunities` trả metadata:

| Field | Ý nghĩa |
|-------|---------|
| `analysisStatus` | `not_run` · `zero_matches` · `has_results` · `reference_list` |
| `lastAnalysisAt` | Thời điểm quét thật (UTC) — **dùng cho “Lần quét cuối”** |
| `targetTradingDate` | Phiên mục tiêu strict |
| `forTradingDate` | Phiên của list đang hiển thị |
| `lastAnalysisStocksScored` | Số mã quét |
| `lastAnalysisOpportunitiesSaved` | Số mã strict lưu (0 = không ai pass) |
| `hasFreshData` | `forTradingDate == targetTradingDate` và có list strict |
| `needsAnalysis` | Chưa từng chạy phân tích cho target |
| `canRunAnalysis` | Hết cooldown 15 phút |

### Phân biệt cho user

| Tình huống | `analysisStatus` | UI |
|------------|------------------|-----|
| Chưa chạy phân tích | `not_run` | Banner vàng — “Chưa phân tích…” |
| Đã quét, 0 mã strict | `zero_matches` | Banner cam — “0 mã strict / N quét”; list cũ = tham khảo |
| Có Top strict | `has_results` | List chính thức |
| Strict = 0, có Top relaxed | `relaxed_fallback` | Banner xanh — list dự phòng |
| List cũ, chưa quét target | `reference_list` | Banner xám |
| API lỗi / 429 cooldown | — | Message đỏ / nút disabled (không nhầm với 0 mã) |

---

## 11. Vì sao thường ra 0 mã strict?

Với config hiện tại, mã phải **đồng thời**:

1. Đã **phá hộp Darvas** (4 gate) — không chỉ “có hộp”
2. **MA stack** đầy đủ (hoặc tối thiểu MA20>MA50)
3. Phiên kích hoạt **tăng >2%** + KL **≥300k** (hoặc shakeout hợp lệ)
4. Buy Score **≥ 62** sau adaptive
5. Không FOMO, không phân phối, RS/ngành/TT đạt

Ví dụ phiên giảm (ORS −5%, VDS −0.6%) → fail gate **phiên kích hoạt** dù có hộp đẹp.

---

## 12. Cách nới (khi product quyết định)

| Hướng | Config / code |
|-------|----------------|
| Luôn có list khi strict=0 | `RelaxedFallbackEnabled: true` (**đã bật**) |
| Cho mã “có hộp, chưa breakout” vào Top strict | **Gate 4** `IsSetupZone` + hạ Darvas breakout 2.5%/1.5× (**đã bật**) |
| Hạ ngưỡng điểm | `MinPassScore` 58–60 (shadow A/B — **chưa đổi**) |
| Bỏ / nới MA stack | `MaStack.Enabled: false` — **không áp dụng** |

---

## 13. File entry khi debug

| File | Vai trò |
|------|---------|
| `DailyAnalysisRunner.cs` | Orchestrator strict + fallback + lưu DB |
| `SmartMoneyOpportunitySelector.cs` | Context ngành + `PassesFilter` |
| `BuyDecisionEngine.cs` | Buy Score + **toàn bộ gate Top** |
| `DarvasBreakoutAnalyzer.cs` | Hộp + 4 gate breakout |
| `SignalAnalyzer.cs` | MA stack, session bar, signals |
| `MarketService.cs` | API list + `analysisStatus` |
| `OpportunityAnalysisStatuses.cs` | Hằng trạng thái API |
| `appsettings.json` | Ngưỡng production |

---

*Cập nhật: 2026-07-07 — Gate 4 setup zone + Darvas breakout 2.5%/1.5×; Relaxed Fallback.*

# Flat Box Engine — hướng dẫn cho AI

> **Đọc file này trước** khi mở `DarvasBreakoutAnalyzer.cs`, `BuyDecisionEngine.cs`, `SignalAnalyzer.cs`, hoặc sửa `PriceRunupFilter` / UI hộp tích lũy / tín hiệu breakout.
> Code trên disk là nguồn sự thật; nếu lệch doc → tin code, cập nhật doc sau.

## Mục đích

Nhận diện **hộp tích lũy phẳng Darvas** và **phá vỡ có xác nhận dòng tiền** trên OHLCV VN (giá lưu **nghìn đồng**: 25 = 25.000đ).

Kết quả chính (API/UI):

| Output | Dùng cho |
|--------|----------|
| `FlatBoxProfile` → `FlatBoxDto` (`flatBox`) | Card **Hộp tích lũy phẳng** trang CP, gate Buy Score, lọc FOMO (+10% so đỉnh hộp) |
| `SignalType.DarvasBreakout` | Tín hiệu + alert **Phá vỡ hộp tích lũy phẳng có xác nhận dòng tiền** |
| `SignalType.Breakout` | Phá đỉnh **20 phiên** + vol (logic cũ, song song) |

**Legacy:** `BaseQualityEvaluator` / `BasePriceProfile` vẫn tồn tại cho criterion scoring nội bộ; **không** còn expose `basePrice` qua `GET /api/v1/stocks/{symbol}`.

**Case ORS:** hộp Darvas hợp lệ + đủ 4 gate breakout → `flatBox.isBreakoutConfirmed: true` + `DarvasBreakout` trong `activeSignals` (không cần pipeline nền cũ `HasPriorUptrend`).

## Entry points (đọc theo thứ tự khi debug)

| Thứ tự | File | Vai trò |
|--------|------|---------|
| 1 | `docs/base-price-engine.md` | Bản đồ logic (file này) |
| 2 | `DarvasBreakoutAnalyzer.cs` | `AnalyzeFlatBox`, `Evaluate` — hộp + 4 gate breakout |
| 3 | `BuyDecisionEngine.cs` | Gate hộp phẳng, FOMO so đỉnh hộp, điểm vào |
| 4 | `SignalAnalyzer.cs` | `AnalyzeFlatBox`, `IsDarvasBreakout`, `DetectSignals` |
| 5 | `DarvasBreakoutAlertPublisher.cs` | Alert universe sau Job 2 |
| 6 | `SignalFormatter.cs` | Nhãn tiếng Việt `activeSignals` / alert |
| 7 | `StockService.cs` | `AnalyzeFlatBox` → `FlatBoxDto` |
| 8 | `PriceRunupFilterOptions.cs` | Config → `BasePriceFilterSettings` + `DarvasBoxSettings` |
| 9 | `BaseQualityEvaluator.cs` | *(legacy)* pipeline nền — criterion scoring nội bộ |

Test nhanh:

- `scripts/_test-darvas-breakout.ps1 -Symbol ORS` — `flatBox` + tín hiệu breakout hộp phẳng

## Kiến trúc: Parallel Gates (OR hình thái) — nhận diện **nền**

Thiết kế **đa mục tiêu** cho thị trường VN — không ép mọi mã vào một khuôn VCP Mỹ.

```
Cửa sổ nền: ConsolidationMinSessions … MaxBaseWindowSessions
(quét trong MaxScanSessions phiên gần nhất)

    │
    ├─ [GATE CHUNG — AND]
    │     • Đủ số phiên tối thiểu
    │     • HasPriorUptrend (impulse trước nền)
    │     • PassesRelaxedDistributionGate
    │
    ├─ [GATE HÌNH THÁI — OR, ít nhất 1 nhánh]
    │     ├─ PassesVcpShape        → VCP / Minervini
    │     ├─ PassesDarvasBox       → Hộp phẳng Darvas (Close-based)
    │     └─ PassesShakeoutSpringBase → Spring / quét đáy rồi hồi
    │
    └─ [GATE CUỐI — AND]
          • CheckNetDriftAndWidth (biên H/L, drift net)
          • ScoreWindow.TotalScore ≥ MinBaseQualityScore
```

**Quan trọng:** VCP và Darvas **loại trừ lẫn nhau về hình thái** — VCP **loại** ping-pong phẳng; Darvas **bắt buộc** ping-pong biên hẹp.

## Gate chung

### HasPriorUptrend

Trước `baseStart` (trong `PriorImpulseLookbackSessions`, mặc định 30 phiên):

- Impulse: `(Close tại baseStart − Low thấp nhất giai đoạn trước) / Low ≥ MinPriorImpulsePercent` (mặc định **15%**)
- EMA20 dốc lên; nửa sau giá trung bình > nửa đầu ~3%
- Giá neo ≥ EMA50 × 0.97

→ Nhiều mã VN tái tích lũy sau đà giảm **fail gate này** → `basePrice: null` dù hình hộp Darvas đẹp.

### PassesRelaxedDistributionGate

Trong **15 phiên cuối** của cửa sổ nền: tối đa **2** phiên giảm **>5%** với volume **>1.5×** TB.

## Nhánh 1 — VCP (`PassesVcpShape`)

Mẫu nén cổ điển (đỉnh thấp, đáy cao). Tất cả **AND**:

| Điều kiện | Mô tả |
|-----------|--------|
| `HasRelaxedAtrContraction` | ATR 3 phiên cuối < 80% ATR 3 phiên đầu nền |
| `HasVolumeDryUp` | Vol MA5 < MA20; 1/3 cuối KL < 85% 1/3 đầu |
| `HasSwingCompression` | Biên swing 4 đoạn co dần (≈60% so đầu) |
| `HasVolatilityContractionPattern` | Đỉnh thấp dần + đáy cao dần (hoặc wedge) |
| `!IsPingPongSideway` | **Không** phải hộp ping-pong phẳng |
| `HasTighteningTail` | 3–5 phiên cuối hẹp hơn ~85% biên cả nền |

## Nhánh 2 — Darvas (`PassesDarvasBox`)

Hộp phẳng VN: dùng **Close** làm khung xương, H/L chỉ kiểm **râu nến** (chống nhiễu ATC / quét đầu phiên).

### Công thức (nhận diện **nền** — đủ gate, gồm KL cạn)

**Khung xương (Close):**

```
CoreBoxHeight% = (Max(Close) − Min(Close)) / Min(Close) × 100 ≤ MaxBoxHeightPercent (mặc định 9%)
```

**Râu nến:**

```
Max(High) ≤ Max(Close) × (1 + ShadowTolerancePercent/100)   // mặc định +3%
Min(Low)  ≥ Min(Close) × (1 − ShadowTolerancePercent/100)   // mặc định −3%
```

**Touch (ping-pong bắt buộc):**

- Close tiệm cận biên trên (±`TouchThresholdPercent` của Min(Close), mặc định 1.5%): ≥ `MinTopTouches` (2)
- Close tiệm cận biên dưới: ≥ `MinBottomTouches` (2)

**Volume (VSA):**

```
AvgVol(Part3) < VolDryUpRatio × AvgVol(Part1)    // mặc định 0.8
AvgVol(Part3) < Vol MA20 tại end
```

**Pivot cuối hộp:** trung bình `(High−Low)/Low` của **3 phiên cuối** ≤ `MaxLast3AvgRangePercent` (mặc định **3.5%**)

`PassesDarvasBox` là **public static** — dùng chung cho pipeline nền và breakout analyzer.

## Breakout hộp phẳng (`DarvasBreakoutAnalyzer`) — **luồng riêng**

**Không** đi qua `PassesPipelineGates` / `HasPriorUptrend`. Chỉ cần hình hộp Darvas ngay **trước** phiên kích hoạt.

### Thuật toán

1. Quét `boxEnd` từ phiên gần nhất lùi về — hộp kết thúc tại `boxEnd`, **không** gồm phiên phá vỡ.
2. Quét `len` từ `MaxBaseWindowSessions` → `ConsolidationMinSessions`, lấy cửa sổ dài nhất pass `PassesDarvasBox` (biến thể breakout — xem dưới).
3. Kiểm tra 4 gate breakout trên **bất kỳ phiên nào** sau hộp (`boxEnd+1` … nến cuối). Phiên pullback sau breakout vẫn giữ `isBreakoutConfirmed`.
4. `gainFromBoxTopPercent` luôn tính từ **nến cuối** so đỉnh hộp (FOMO / pullback).

### 4 gate breakout

| Gate | Điều kiện |
|------|-----------|
| Vượt biên | `Close > Max(Close)` của hộp |
| Xung lực giá | Tăng so phiên trước ≥ `BreakoutMinPriceGainPercent` (mặc định **4%**) |
| KL bùng nổ | `Vol / AvgVol(hộp)` ≥ `BreakoutMinVolumeMultiplier` (mặc định **2×**) |
| Râu trên | `(High−Close)/(High−Low)` ≤ `BreakoutMaxUpperShadowRatio` (mặc định **0.25**) |

Pass → `SignalType.DarvasBreakout` · `DarvasBreakoutResult` (giá mua, stop loss = `Min(Close)` hộp, vol multiplier, kỳ hộp).

### Khác biệt `PassesDarvasBox` khi tìm hộp cho **breakout**

Gọi với `requireVolumeDryUp: false` và `maxBoxHeightPercent: BreakoutMaxBoxHeightPercent` (mặc định **10%**):

| Lý do | Chi tiết |
|-------|----------|
| Không bắt KL cạn 1/3 cuối | 1–2 phiên trước breakout KL thường tăng nhẹ (chuẩn bị phá vỡ) |
| Biên hộp 10% | Mã VN như ORS có biên Close ~9–10%; gate nền chuẩn 9% có thể quá chặt |

### Alert & pipeline

- **`DarvasBreakoutAlertPublisher`** — chạy cuối **Job 2** (`DailySessionSyncRunner`), quét **toàn universe**.
- Chỉ alert khi `DarvasBreakout` **mới** (so `DetectSignals` phiên hiện tại vs history bỏ nến cuối).
- Lưu `Alert` + push SignalR; `SourceTag` = `"Phá vỡ hộp tích lũy phẳng"`.
- DTO Job 2: `DailySessionSyncResultDto.DarvasBreakoutAlerts`.

### Tích hợp Buy Score

`BuyDecisionEngine.hasBreakoutEntry` = (`Breakout` **hoặc** `DarvasBreakout`) + `MeetsSessionEntryBar` + (DarvasBreakout **hoặc** `Vol ratio ≥ BreakoutMinVolumeRatio`).

`DarvasBreakout` đã kiểm vol ≥2× TB hộp trong analyzer — không cần lặp `BreakoutMinVolumeRatio` 1.5×.

## Hai loại “breakout” trên UI — đừng nhầm

| | `SignalType.Breakout` | `SignalType.DarvasBreakout` |
|--|----------------------|----------------------------|
| Code enum | `Breakout` | `DarvasBreakout` |
| **Nhãn UI (tiếng Việt)** | Vượt đỉnh | **Phá vỡ hộp tích lũy phẳng có xác nhận dòng tiền** |
| Logic | `Close > max High 20 phiên`, vol >2× TB20, tăng >3% | Hộp Darvas + 4 gate trên |
| `SignalFormatter` | `GetLabelVi` / `FormatTitle` / `FormatDescription` | Cùng file |
| Frontend | `frontend/src/lib/utils.ts` → `signalLabelVi` | Cùng map |
| Mobile | `home_screen.dart`, `buy_decision_card.dart` | Cùng map |

Emoji alert: `Breakout` 🚀 · `DarvasBreakout` 📦.

## Nhánh 3 — Spring / Shakeout (`PassesShakeoutSpringBase`)

Cho CP VN hay quét stop-loss:

- 1/3 đầu nền xác định `supportLow`
- Sau đó có phiên `Low` thủng support (~0.5%)
- Close hiện tại hồi trên vùng quét
- 1/3 cuối biên hẹp hơn 1/3 đầu; KL 1/3 cuối < 90% 1/3 đầu
- `HasTighteningTail` ở cuối

Drift cho phép **≥ −8%** (thay vì −4% cho VCP/Darvas).

## Gate cuối — `CheckNetDriftAndWidth`

- Biên H/L toàn nền: `(High−Low)/Low × 100 ≤ **18%**
- Drift: `(Close_end − Close_start) / Close_start` ≥ −4% (VCP/Darvas) hoặc ≥ −8% (Spring)

## Sau khi pass gate nền — chọn & chấm điểm

1. Quét mọi `(start, end)` trong cửa sổ scan/end hợp lệ.
2. `ScoreWindow` → `BaseQualityComponents` (7 thành phần).
3. Loại nếu `TotalScore < MinBaseQualityScore` (mặc định **50**).
4. `AnalyzeBasePrice`: tối đa 3 nền không overlap → chọn nền gần giá hiện tại nhất.
5. `AnalyzeBasePriceForFilter`: ưu tiên nền gần giá / breakout (FOMO filter).

## Config mặc định (`PriceRunupFilter`)

```json
{
  "ConsolidationMinSessions": 10,
  "MaxScanSessions": 90,
  "MaxBaseWindowSessions": 45,
  "MinBaseQualityScore": 50,
  "MinPriorImpulsePercent": 15,
  "PriorImpulseLookbackSessions": 30,
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

Mapping: `DarvasBoxOptions` → `DarvasBoxSettings` trong `PriceRunupFilterOptions.ToSettings()`.

## UI & API

- API: `GET /api/v1/stocks/{symbol}` → `flatBox` (có thể `null`), `activeSignals` (chuỗi đã format từ `SignalFormatter.FormatTitle`).
- Web: `StockDetailPage.tsx` — card **Hộp tích lũy phẳng** khi `flatBox != null`; chip tín hiệu từ `activeSignals`.
- Mobile: `stock_detail_screen.dart` — `_FlatBoxCard`.
- **Enum backend tiếng Anh** (`DarvasBreakout`); **nhãn user tiếng Việt** — không hiển thị “Breakout Darvas” trên UI.

## Liên kết SmartMoney

- `BuyDecisionEngine`: không có hộp → gate fail; chưa breakout → Watch; `isBreakoutConfirmed` → Ready path + `DarvasBreakout` trong `hasBreakoutEntry`.
- Chi tiết Top / điểm vào: `docs/smartmoney-checklist.md`.
- **Đừng nhầm** `TradeEventDetector` / Khớp lệnh (intraday KBS) — luồng khác.

## Khi sửa engine — checklist AI

1. Đọc lại **file này** + `PassesPipelineGates` + `DarvasBreakoutAnalyzer.Evaluate`.
2. Đổi gate/threshold → `appsettings.json` + `PriceRunupFilterOptions` + `DarvasBoxOptions` + **file này**.
3. Đổi nhãn UI → `SignalFormatter.cs` + `frontend/src/lib/utils.ts` + mobile maps.
4. Chạy `_test-base-price.ps1` và `_test-darvas-breakout.ps1`.
5. Cập nhật `docs/smartmoney-checklist.md` + `CLAUDE.md` nếu đổi pipeline/alert.

## Lịch sử thiết kế (ngắn)

| Giai đoạn | Mô tả |
|-----------|--------|
| Cũ | 8 gate AND tuần tự — gần như chỉ VCP; loại ping-pong → “trắng bảng” trên VN |
| Parallel gates | Gate chung + **VCP OR Darvas OR Spring**; Darvas Close-based |
| Jul 2026 | **`DarvasBreakoutAnalyzer`** + alert Job 2; UI nhãn tiếng Việt; breakout tách khỏi pipeline nền (không cần `HasPriorUptrend`) |

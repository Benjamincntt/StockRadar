# Base Price Engine — hướng dẫn cho AI

> **Đọc file này trước** khi mở `BaseQualityEvaluator.cs`, `SignalAnalyzer.cs`, hoặc sửa `PriceRunupFilter` / UI nền giá.
> Code trên disk là nguồn sự thật; nếu lệch doc → tin code, cập nhật doc sau.

## Mục đích

Nhận diện **nền giá tích lũy** trên OHLCV VN (giá lưu **nghìn đồng**: 25 = 25.000đ). Engine **không** dùng quy tắc “đi ngang X phiên = nền”.

Kết quả: `BasePriceProfile` → `BasePriceDto` trên trang chi tiết mã, `BuyDecisionEngine` (gate “Có nền giá”), lọc FOMO (+10% so đỉnh nền).

## Entry points (đọc theo thứ tự khi debug)

| Thứ tự | File | Vai trò |
|--------|------|---------|
| 1 | `docs/base-price-engine.md` | Bản đồ logic (file này) |
| 2 | `backend/StockRadar.Domain/Services/BaseQualityEvaluator.cs` | `PassesPipelineGates`, quét cửa sổ, chấm điểm |
| 3 | `backend/StockRadar.Domain/Services/SignalAnalyzer.cs` | `AnalyzeBasePrice`, `AnalyzeBasePriceForFilter` |
| 4 | `backend/StockRadar.Application/Services/StockService.cs` | Gọi analyzer → DTO API |
| 5 | `backend/StockRadar.Application/Options/PriceRunupFilterOptions.cs` | Config → `BasePriceFilterSettings` |
| 6 | `backend/StockRadar.Api/appsettings.json` → `PriceRunupFilter` | Giá trị runtime |

## Kiến trúc: Parallel Gates (OR hình thái)

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

### PassesRelaxedDistributionGate

Trong **15 phiên cuối** của cửa sổ nền: tối đa **2** phiên giảm **>5%** với volume **>1.5×** TB (thay vì zero-tolerance phiên giảm >6%).

## Nhánh 1 — VCP (`PassesVcpShape`)

Mẫu nén cổ điển (đỉnh thấp, đáy cao). Tất cả **AND**:

| Điều kiện | Mô tả |
|-----------|--------|
| `HasRelaxedAtrContraction` | ATR trung bình **3 phiên cuối** < **80%** ATR **3 phiên đầu** nền |
| `HasVolumeDryUp` | Vol MA5 < MA20; 1/3 cuối KL < 85% 1/3 đầu |
| `HasSwingCompression` | Biên swing 4 đoạn co dần (≈60% so đầu) |
| `HasVolatilityContractionPattern` | Đỉnh thấp dần + đáy cao dần (hoặc wedge) |
| `!IsPingPongSideway` | **Không** phải hộp ping-pong phẳng |
| `HasTighteningTail` | 3–5 phiên cuối hẹp hơn ~85% biên cả nền |

## Nhánh 2 — Darvas (`PassesDarvasBox`)

Hộp phẳng VN: dùng **Close** làm khung xương, H/L chỉ kiểm **râu nến** (chống nhiễu ATC / quét đầu phiên).

### Công thức

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

(Chia cửa sổ thành 3 phần bằng nhau.)

**Pivot cuối hộp:**

- Trung bình `(High−Low)/Low` của **3 phiên cuối** ≤ `MaxLast3AvgRangePercent` (mặc định **3.5%**)

Config: `appsettings.json` → `PriceRunupFilter:Darvas` / `DarvasBoxSettings` / `DarvasBoxOptions`.

## Nhánh 3 — Spring / Shakeout (`PassesShakeoutSpringBase`)

Cho CP VN hay quét stop-loss:

- 1/3 đầu nền xác định `supportLow`
- Sau đó có phiên `Low` thủng support (~0.5%)
- Close hiện tại hồi trên vùng quét
- 1/3 cuối biên hẹp hơn 1/3 đầu; KL 1/3 cuối < 90% 1/3 đầu
- `HasTighteningTail` ở cuối

Drift cho phép **≥ −8%** (thay vì −4% cho VCP/Darvas).

## Gate cuối — `CheckNetDriftAndWidth`

- Biên H/L toàn nền: `(High−Low)/Low × 100 ≤ **18%** (`MaxOverallBaseWidthPercent`)
- Drift: `(Close_end − Close_start) / Close_start` ≥ −4% (VCP/Darvas) hoặc ≥ −8% (Spring)

## Sau khi pass gate — chọn & chấm điểm

1. **Quét** mọi `(start, end)` trong cửa sổ scan/end hợp lệ.
2. **`ScoreWindow`** → `BaseQualityComponents` (7 thành phần, `TotalScore` weighted).
3. Loại nếu `TotalScore < MinBaseQualityScore` (mặc định **50**).
4. **`AnalyzeBasePrice`**: tối đa 3 nền không overlap → chọn nền **gần giá hiện tại nhất**.
5. **`AnalyzeBasePriceForFilter`**: ưu tiên nền gần giá / breakout (FOMO filter).

Thành phần điểm (`BaseQualityComponents.TotalScore`):

| Thành phần | Trọng số |
|------------|----------|
| PriorTrend | 15% |
| AtrContraction | 20% |
| Compression | 20% |
| VolumeDry | 20% |
| ContractionPattern | 15% |
| Distribution | 5% |
| Duration | 5% |

Nền đẹp: ≥ **StrongBaseQualityScore** (80).

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
    "MaxLast3AvgRangePercent": 3.5
  }
}
```

## UI & API

- API: `GET /api/v1/stocks/{symbol}` → `basePrice` (`BasePriceDto`), có thể `null` nếu không pass gate.
- Web: `StockDetailPage.tsx` — card “Nền giá” chỉ hiện khi `basePrice != null`.
- Mobile: `_BasePriceCard` trong `alerts_screen` / `stock_detail_screen`.
- Nhãn UI tiếng Việt; mã enum backend giữ tiếng Anh.

## Liên kết SmartMoney

- `BuyDecisionEngine`: không có nền → `EntryPoint.Invalid`, gate “Có nền giá” fail.
- Top cơ hội: cần nền + (breakout hoặc shakeout) + phiên kích hoạt — xem `docs/smartmoney-checklist.md`.
- **Đừng nhầm** với `TradeEventDetector` / Khớp lệnh (intraday KBS) — luồng khác hoàn toàn.

## Khi sửa engine — checklist AI

1. Đọc lại **file này** + `PassesPipelineGates` trong code.
2. Nếu đổi gate/threshold → cập nhật `appsettings.json` + `PriceRunupFilterOptions` + **file này**.
3. Chạy `scripts/_test-base-price.ps1` (local API `:5280`) để đếm % mã có `basePrice`.
4. Cập nhật dòng nền giá trong `docs/smartmoney-checklist.md` nếu đổi thiết kế.
5. Cập nhật `CLAUDE.md` mục pipeline nếu entry file đổi.

## Lịch sử thiết kế (ngắn)

| Giai đoạn | Mô tả |
|-----------|--------|
| Cũ | 8 gate AND tuần tự — gần như chỉ VCP; loại ping-pong → “trắng bảng” trên VN |
| Hiện tại | Gate chung + **VCP OR Darvas OR Spring**; Darvas Close-based; phân phối/ATR nới cho VN |

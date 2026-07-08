# Telegram VIP Alerts — luồng bắn tin nhắn

> Tài liệu vận hành/kỹ thuật cho StockRadar. Nguồn sự thật: code trên disk (`TopOpportunityVipAlertPublisher`, `TopOpportunityVipAlertEvaluator`, `MasterAlertOptions`).  
> Cập nhật: 2026-07-08.

## Mục tiêu

Trong phiên giao dịch, hệ thống gửi Telegram (HTML) cho mã nằm trong **Top cơ hội** (`DailyOpportunities`), gồm:

1. **Entry Ready** — early warning: giá lọt vùng mua AI (đồng bộ phân tích T-1 qua `IsActionable`).
2. **Master alerts** — xác nhận momentum/cắt lỗ **trong phiên** (độc lập `IsActionable`).

Bot có thể mang tên “StockRadar HPO” — mọi VIP alert vẫn đi qua notifier Telegram chung, không phải job HPO tuần.

---

## Pipeline tổng quan

```mermaid
flowchart TD
  A[Quartz OpportunityMonitor ~60s] --> B[OpportunityIntradayMonitorRunner]
  B --> C[KBS bảng giá toàn universe]
  C --> D{Mã có trong TopMap<br/>DailyOpportunities hôm nay?}
  D -->|Không| Z[Bỏ qua VIP]
  D -->|Có| E[TopOpportunityVipAlertPublisher.ProcessQuote]

  E --> F{Telegram + VipAlerts bật?}
  F -->|Không| Z
  F -->|Có| G[State phiên: GetOrReset symbol]

  G --> H{Entry Ready?<br/>IsActionable<br/>chưa EntryReadyFired<br/>chưa BuyPoint1<br/>giá trong vùng entry}
  H -->|Có| I[Format + lý do Headline/Action]
  I --> J[🎯 Entry Ready → Telegram]
  J --> K[EntryReadyFired = true]
  H -->|Không| L
  K --> L

  L{MasterAlerts Enabled?}
  L -->|Không| Z
  L -->|Có| M[pacedVol = project KL / ADV × elapsed phiên]

  M --> N[EvaluateMasterSignal]
  N --> O{gain so đỉnh nền BaseHigh}

  O --> P[BuyPoint1: dải [3%, 6%)<br/>ticks++ / reset nếu dưới 3%<br/>≥3 ticks + vol ≥1.5x]
  P -->|Pass| Q[🟢 Mua 1 nửa]
  P -->|Fail| R[UpdateHigh sau Buy1]

  R --> S[BuyPoint2: ≥6%<br/>ticks++ / reset nếu dưới 6%<br/>≥3 ticks + vol ≥1.8x]
  S -->|Pass| T[🔥 Mua hết]
  S -->|Fail| U{Đã BuyPoint1?}

  U -->|Không| Z
  U -->|Có| V{Trailing stop?<br/>peak≥3% + drawdown≥ ngưỡng×phase}
  V -->|CutAll| W[🔴 Bán hết]
  V -->|Cut1| X[🟡 Bán 1 nửa]
  V -->|Không| Y{Distribution scan?}
  Y -->|Có + peak đủ| WX[🟡/🔴 theo peak]
  Y -->|Không| Z

  Q --> AA[Build reasoning + cooldown]
  T --> AA
  W --> AA
  X --> AA
  WX --> AA
  AA --> BB[Dispatch: Alert DB + SignalR + Telegram HTML]
  BB --> CC{Là buy kind?}
  CC -->|Có| DD[SetupTrack nếu chưa có]
  CC -->|Không| Z
```

---

## Nguồn dữ liệu

| Thành phần | Thời điểm | Vai trò |
|------------|-----------|---------|
| `DailyAnalysisRunner` | Sau phiên T-1 (~15:05 / job analysis) | Top list + `EntryPointJson`, `AverageDailyVolume`, `MarketPhase` |
| `OpportunityIntradayMonitor` | Mỗi ~60s trong phiên | Quote live KBS → VIP evaluator |
| `MasterAlertSessionTracker` | In-memory / phiên | Cờ đã bắn + confirmation ticks + đỉnh sau Buy1 |
| `IntradayAlertTracker` | In-memory | Cooldown theo symbol + signal key |

**Chỉ mã trong TopMap hôm nay** được xét VIP. Mã ngoài list Top → không spam Telegram.

Config: `TelegramNotify.Enabled`, `TelegramNotify.VipAlertsEnabled`, section `MasterAlerts`.

---

## Hai lớp tín hiệu (phải tách trong đầu)

| | Entry Ready | Master (Mua/Bán) |
|--|-------------|------------------|
| Mục đích | “Mã actionable, giá đang trong vùng” | “Đang bứt / rút / phân phối thật trong phiên” |
| Đồng bộ `IsActionable` | **Có** — không gửi nếu Watch/fail gate | **Không** — momentum độc lập |
| Ngưỡng chính | Vùng `BaseLow`…`Trigger` (+tolerance) | `% so đỉnh nền` (`BaseHigh`) + paced vol + ticks |
| Lặp | **1 lần/phiên**; tắt sau khi đã BuyPoint1 | Mỗi loại **1 lần/phiên** (cờ Fired) |

Ví dụ: phân tích gán Watch → **không** Entry Ready; nếu mã vẫn trong Top và giá + vol đủ → vẫn có thể **Mua 1/2 / Mua hết**.

---

## Công thức quan trọng

### Gain so đỉnh nền (Master buy)

```
gainFromBase% = (liveClose − entry.BaseHigh) / entry.BaseHigh × 100
```

**Không** dùng `ChangePercent` phiên (KBS `CHP`) cho trigger Master buy. Nhãn Telegram “từ đỉnh nền” phải khớp công thức này.

### Paced volume ratio

```
elapsedFraction = % phút phiên đã trôi / 255
effectiveElapsed = max(elapsedFraction, MinElapsedFractionForPacing)  // mặc định 0.2 — chống FOMO ATO đầu phiên

projectedVol = sessionVolume / effectiveElapsed
pacedVolumeRatio = projectedVol / AverageDailyVolume
```

Gate volume:

- Có `AverageDailyVolume > 0`:  
  `sessionVolume ≥ MinSessionVolumeFloor` (50K) **và** `pacedVolumeRatio ≥ ngưỡng`
- Chưa có ADV (record cũ / quên re-analysis): fallback `sessionVolume ≥ MinSessionVolume` (800K)

Ngưỡng paced:

- BuyPoint1: `MinVolumeRatioPaced` = **1.5**
- BuyPoint2: `BuyPoint2MinVolumeRatio` = **1.8**

### Confirmation ticks (chống bull trap)

- Mỗi chu kỳ quét (~60s): nếu giá **còn trên ngưỡng** → `ConfirmTicks++`
- Bắn khi `ConfirmTicks ≥ RequiredConfirmationTicks` (**3**) **và** pass volume
- Nếu giá **rớt dưới ngưỡng** → **reset counter về 0** (không decay)
- BuyPoint1: trong band **[3%, 6%)**; rớt `< 3%` → reset; `≥ 6%` → nhường BP2
- BuyPoint2: `≥ 6%`; rớt `< 6%` → reset

Case KLB: spike +7% một tick rồi rớt dưới 6% → không đủ 3 ticks → **không** Mua hết.

---

## Chi tiết từng loại tin

### 🎯 Entry Ready

Điều kiện (AND):

- `TelegramNotify` bật + VipAlerts
- `entry.IsActionable == true`
- Chưa `EntryReadyFired`, chưa `BuyPoint1Fired`
- `IsPriceInEntryZone(entry, close)`

Sau gửi: `EntryReadyFired = true`. Lý do gắn `Headline` / `Action` từ entry.

### 🟢 Mua 1 nửa (BuyPoint1)

- `gainFromBase` trong **[3%, 6%)**
- `ConfirmTicks ≥ 3`
- Volume gate với ratio **1.5×**
- Một lần / phiên (`BuyPoint1Fired`)
- Telegram kèm **slippage buffer** từ `BaseHigh` (`SlippageBufferPercent`, mặc định 1.5%): giá đuổi tối đa

### 🔥 Mua hết (BuyPoint2)

- `gainFromBase ≥ 6%`
- `ConfirmTicks ≥ 3`
- Volume gate với ratio **1.8×**
- Có thể “gap” thẳng BP2 (ép gán BuyPoint1 state nếu chưa có)

### 🟡 Bán 1 nửa / 🔴 Bán hết

Chỉ xét sau khi đã có **BuyPoint1** (trực tiếp hoặc qua gap BP2).

1. **Trailing stop động** (ưu tiên trước distribution):  
   - Peak gain từ giá Buy1 ≥ `TrailingStopMinPeak` (3%)  
   - Drawdown lãi từ peak (`peakGain − currentGain`) ≥ `BaseTrailingStopPercent × MarketPhaseMultipliers[phase]`  
   - CutAll ngưỡng lớn hơn CutLoss1
2. **Distribution scan** (fallback): scan xả / ngoại+prop bán + peak đạt `CutLoss*MinPeakGainPercent`

---

## Nội dung tin nhắn

Format HTML nhiều dòng: tiêu đề + 1 dòng số chính + **reasoning** + Vol session.

Reasoning mua: phá nền (BaseHigh → Close), paced vol ×TB, MarketPhase.  
Reasoning bán: trailing (drawdown, peak→hiện, stop×phase) hoặc nhãn phân phối.

Dispatch mỗi lần gửi còn: lưu `Alert`, SignalR; buy kind → `SetupTrack` nếu chưa tồn tại.

---

## Config tham chiếu (`MasterAlerts`)

```json
{
  "Enabled": true,
  "BuyPoint1MinChangePercent": 3,
  "BuyPoint2MinChangePercent": 6,
  "MinSessionVolume": 800000,
  "MinVolumeRatioPaced": 1.5,
  "BuyPoint2MinVolumeRatio": 1.8,
  "MinElapsedFractionForPacing": 0.2,
  "RequiredConfirmationTicks": 3,
  "MinSessionVolumeFloor": 50000,
  "CutLoss1MinPeakGainPercent": 4,
  "CutAllMinPeakGainPercent": 6.5,
  "TrailingStopMinPeak": 3,
  "BaseTrailingStopPercent1": 2.5,
  "BaseTrailingStopPercent2": 4.0,
  "MarketPhaseMultipliers": {
    "Favorable": 0.8,
    "Neutral": 1.0,
    "Unfavorable": 2.25
  },
  "CooldownMinutes": 15,
  "SlippageBufferPercent": 1.5
}
```

Sau deploy có thay đổi schema Top (`AverageDailyVolume`, `MarketPhase`): **chạy lại analysis** để fill; record 0 ADV → dùng fallback 800K.

---

## File code chính

| File | Vai trò |
|------|---------|
| `OpportunityIntradayMonitorRunner.cs` | Quét KBS → gọi VIP per Top symbol |
| `TopOpportunityVipAlertPublisher.cs` | Entry Ready + paced vol + reasoning + Dispatch/Telegram |
| `TopOpportunityVipAlertEvaluator.cs` | Zone, Master signal, ticks, volume, trailing, distribution |
| `MasterAlertSessionTracker.cs` | State phiên / ticks / peak / drawdown |
| `VipTelegramMessageFormatter.cs` | HTML tin nhắn |
| `VietnamMarketCalendar.cs` | `SessionElapsedFraction` |
| `MasterAlertOptions.cs` | Config |
| `DailyAnalysisRunner.cs` | Gán ADV + MarketPhase vào Top record |

---

## Checklist khi đọc / sửa tiếp

1. Không gắn `IsActionable` vào Master buy/sell — đó là confirmation độc lập.
2. Không thay gain nền bằng % phiên chỉ vì wording Telegram.
3. Confirmation: **full reset** khi rớt dưới ngưỡng (chưa dùng decay).
4. BP2 volume chặt hơn BP1 (1.8 vs 1.5).
5. Entry Ready: actionable + 1 lần/phiên; không spam cooldown 15 phút kiểu cũ.
6. Restart API giữa phiên → state in-memory mất → ticks về 0 (conservative).

---

## Ví dụ timeline (bull trap bị lọc)

```
13:00  gain +7.2%  → BP2 ticks = 1  (chưa đủ 3, chưa bắn)
13:01  gain +2%    → BP2 ticks = 0  (reset)
13:02  gain +1%    → ticks = 0
→ Không Mua hết.
```

Breakout thật: giữ ≥6% qua ≥3 chu kỳ + paced ≥1.8× → 🔥 Mua hết + reasoning.

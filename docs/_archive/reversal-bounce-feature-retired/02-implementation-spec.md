# Reversal Bounce — Implementation Spec v2 (Phase 0C + 0D)

> **Phiên bản 2** — viết lại sau khi đối chiếu code 0B đã chạy và phản hồi của model nhỏ.
>
> **Đối tượng đọc:** model implementer (Sonnet / Codex / dev). File này đủ chi tiết để code chính xác.
>
> **Đọc trước khi code:**
> 1. `docs/features/reversal-bounce/reversal-bounce.md` — spec tổng quan (state machine, scoring tổng thể, hard gate, regime rule).
> 2. `docs/_archive/phase0a-audit-reports/0A.1-0A.4 + 00-summary.md` — audit codebase đã verify.
> 3. File này.
>
> **Quy tắc cứng:**
> 1. **KHÔNG đụng** các file/component đã có từ 0B (xem §0.2).
> 2. Snapshot ReversalBounce **idempotent** theo `(Symbol, TradingDate, StrategyVersion, SetupId)` với **SetupId deterministic** (§4.2).
> 3. **KHÔNG** dùng High/Low phiên T+1 để xác nhận tín hiệu phiên T.
> 4. `asOfDate` = phiên hiện tại **inclusive** (§7.1).
> 5. `OhlcvBar` chưa mở rộng — dùng runtime computation cho `ReferencePrice`/`FloorPrice` (§3.3).

---

## 0. Trạng thái Phase 0B đã có (KHÔNG đụng)

### 0.1. Code thật (đã build xanh, migration đã apply DB dev)

| File đã có | Vai trò |
|---|---|
| `backend/StockRadar.Domain/Services/ReversalBounce/MarketRegime.cs` | enum `MarketRegime {Normal, Stabilizing, ReboundConfirmed, Panic}` + `MarketRegimeThresholds` record + `Default` static |
| `backend/StockRadar.Domain/Services/ReversalBounce/MarketBreadth.cs` | record `MarketBreadthSnapshot(...)` 17 trường (TradingDate là PK) |
| `backend/StockRadar.Domain/Services/ReversalBounce/MarketBreadthAnalyzer.cs` | `IMarketBreadthAnalyzer.Analyze(universe, indexHistory, tradingDate)` → `MarketBreadthSnapshot` (pure/sync, Reg=Normal default) |
| `backend/StockRadar.Domain/Services/ReversalBounce/MarketRegimeClassifier.cs` | `IMarketRegimeClassifier.Classify(current, previous, thresholds)` → updated snapshot (stateless, hysteresis qua `ImproveStreak`) |
| `backend/StockRadar.Infrastructure/MarketData/MarketBreadthRunner.cs` | `internal sealed`, async wrapper, load `VNINDEX` history từ `db.MarketIndices.HistoryJson` (qua `EntityMapper.JsonOptions`) |
| `backend/StockRadar.Infrastructure/Migrations/20260721043541_AddMarketBreadthSnapshot.cs` | Bảng `MarketBreadthSnapshots` PK=TradingDate |
| `backend/StockRadar.Application/Options/ReversalBounceOptions.cs` (đã có) | có `Enabled` + `.ToRegimeThresholds()` extension |
| `backend/StockRadar.Application/Abstractions/IMarketBreadthSnapshotRepository.cs` (đã có) | `GetPreviousAsync` + `UpsertAsync` |
| `backend/StockRadar.Api/Controllers/ReversalBounceController.cs` (đã có) | đã có `GET /api/v1/reversal-bounce/market-regime` |
| `backend/StockRadar.Application/Services/ReversalBounceQueryService.cs` (đã có) | query layer cho controller |

### 0.2. Danh sách cấm — KHÔNG sửa

- Bất kỳ file nào trong `backend/StockRadar.Domain/Services/ReversalBounce/` (0B).
- `MarketBreadthRunner.cs` (0B Infrastructure).
- Migration `20260721043541_AddMarketBreadthSnapshot.cs` (0B).
- `IMarketBreadthSnapshotRepository` và implementation.
- `ReversalBounceController.cs` hiện tại (sẽ **mở rộng** chứ không tạo mới).
- `ReversalBounceQueryService.cs` hiện tại.
- `BuyDecisionEngine.cs`, `SmartMoneyOpportunitySelector.cs`, `DarvasBreakoutAnalyzer.cs`, `BaseQualityEvaluator.cs` (cũng cấm từ spec tổng).

### 0.3. Stack & convention

| Mục | Giá trị |
|---|---|
| Ngôn ngữ | C# / .NET |
| ORM | EF Core (SqlServer provider) |
| DB | SQL Server |
| DI | `Microsoft.Extensions.DependencyInjection` |
| Logging | `ILogger<T>` |
| Testing | xUnit (project **chưa có**, phải tạo — xem §12) |
| Tiền tệ/giá | `decimal`, precision **18 scale 2** (xem `ApplicationDbContext.cs:41-42` + migration 0B đã dùng `decimal(18,2)`) |
| Date | `DateOnly` cho phiên, `DateTime` (UTC) cho audit |
| Cancellation | Mọi async nhận `CancellationToken cancellationToken = default` |
| JSON | `System.Text.Json` qua `EntityMapper.JsonOptions` (xem `MarketBreadthRunner.cs:61`) |

### 0.4. Folder placement (file MỚI)

```
backend/
├── StockRadar.Domain/
│   ├── Services/ReversalBounce/
│   │   ├── ReversalBounceStage.cs           [mới]
│   │   ├── ReversalBounceAnalyzer.cs        [mới]
│   │   └── ReversalBounceTradePlan.cs       [mới]
│   └── MarketData/
│       └── ExchangePriceBand.cs             [mới — runtime floor/ceiling]
├── StockRadar.Application/
│   ├── Abstractions/
│   │   ├── IReversalBounceTypes.cs          [mới — Setup/Signal/Records]
│   │   ├── IReversalBounceAnalyzer.cs       [mới]
│   │   ├── ICounterTrendDecisionEngine.cs   [mới]
│   │   ├── IReversalCandidateSnapshotRepository.cs [mới]
│   │   ├── IReversalBounceAnalysisService.cs        [mới]
│   │   ├── IReversalBounceBacktestService.cs        [mới]
│   │   └── ISignalConfluenceRepository.cs           [mới — optional, cho Phase 2+]
│   ├── Options/
│   │   ├── ReversalBounceOptions.cs         [mở rộng — thêm analyzer/decision params]
│   │   └── ReversalBounceBacktestOptions.cs [mới]
│   └── DTOs/
│       └── ReversalBounceDtos.cs            [mở rộng]
├── StockRadar.Infrastructure/
│   ├── MarketData/
│   │   ├── ReversalBounceAnalysisRunner.cs  [mới — wrapper async cho analyzer]
│   │   ├── CounterTrendDecisionEngine.cs    [mới]
│   │   └── Backtest/
│   │       └── ReversalBounceBacktestRunner.cs [mới]
│   ├── Persistence/
│   │   ├── Entities/
│   │   │   ├── ReversalCandidateSnapshotEntity.cs [mới]
│   │   │   └── SignalConfluenceEntity.cs          [mới — Phase 2+]
│   │   └── Repositories/
│   │       ├── EfReversalCandidateSnapshotRepository.cs [mới]
│   │       └── EfSignalConfluenceRepository.cs           [mới — Phase 2+]
│   └── Migrations/
│       └── <timestamp>_AddReversalBounceSnapshots.cs    [mới — generated bằng dotnet ef]
└── StockRadar.Tests/                                [MỚI — khởi tạo project]
    ├── StockRadar.Tests.csproj
    └── ReversalBounce/
        ├── ReversalBounceAnalyzerTests.cs
        ├── CounterTrendDecisionEngineTests.cs
        ├── MarketRegimeClassifierTests.cs
        ├── ReversalBounceBacktestRunnerTests.cs
        └── OhlcvFixtures.cs
```

---

## 1. Enums (Domain/Services/ReversalBounce/)

### 1.1. `ReversalBounceStage.cs`

```csharp
namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>Stage suy ra từ OHLCV (stateless). Dùng chung cho cả snapshot và signal.</summary>
public enum ReversalBounceStage
{
    /// <summary>Chưa từng có đợt bán tháo trong lookback → không phải ứng viên.</summary>
    None = 0,

    /// <summary>Capitulating — đang bán tháo, chưa cân bằng.</summary>
    Capitulating = 1,

    /// <summary>Stabilizing — ngừng rơi, lực bán suy yếu.</summary>
    Stabilizing = 2,

    /// <summary>Confirmed — xuất hiện cầu mua xác nhận.</summary>
    Confirmed = 3,

    /// <summary>Invalidated — mất hiệu lực (thủng đáy / mất vùng xác nhận / regime xấu).</summary>
    Invalidated = 4,
}
```

> Lưu ý: `MarketRegime` đã có sẵn ở `MarketRegime.cs` — **dùng enum đó**, không tạo mới.

---

## 2. Domain entities & records (Application/Abstractions)

### 2.1. `IReversalBounceTypes.cs`

```csharp
namespace StockRadar.Application.Abstractions;

// ──── Kết quả analyzer (stateless, pure) ──────────────────────────

/// <summary>6 component scores theo công thức §5.</summary>
public sealed record ReversalBounceComponentScores(
    decimal Capitulation,        // 0..15
    decimal Stabilization,       // 0..20
    decimal Demand,              // 0..15
    decimal RelativeStrength,    // 0..15
    decimal Liquidity,           // 0..10
    decimal RiskPenalty);        // -10..0

public sealed record ReversalBounceReason(
    string Code,                 // "DRAWDOWN_FROM_PEAK", "RSI_OVERSOLD", ...
    string Label,                // Tiếng Việt cho UI
    decimal NumericValue,        // giá trị đo
    decimal? Threshold,          // ngưỡng so sánh
    bool Pass);                  // true = đạt điều kiện

/// <summary>Output của analyzer cho 1 mã tại 1 phiên.</summary>
public sealed record ReversalBounceSetup(
    string Symbol,
    DateOnly TradingDate,
    ReversalBounceStage Stage,
    Guid SetupId,                                    // deterministic (xem §4.2)
    DateOnly? CapitulationDate,
    decimal? CapitulationLow,
    decimal? CapitulationClose,
    int RecoveryAttemptCount,
    ReversalBounceComponentScores ComponentScores,
    decimal TotalScore,
    MarketRegime MarketRegime,                        // ← dùng enum 0B đã có
    Guid? BreadthSnapshotId,
    string StrategyVersion,                          // "reversal-bounce@1.0.0"
    string AlgorithmParametersHash,
    int SchemaVersion,
    Guid RunBatchId,
    IReadOnlyList<ReversalBounceReason> Reasons);

// ──── Trade plan (sau decision engine) ─────────────────────────────

public sealed record ReversalBounceTradePlan(
    decimal EntryReference,             // Close_T
    decimal MaxEntryPrice,              // EntryReference × (1 + GapAcceptance × ATR14%)
    decimal InvalidationPrice,
    decimal FirstTarget,
    decimal RewardToRisk,               // |Target-Entry| / |Entry-Invalidation|  (as-built: đổi tên từ StopToTargetRMultiple)
    int TimeStopSessions,
    decimal PositionFactor,
    IReadOnlyList<string> RiskWarnings);

public sealed record ReversalBounceSignal(
    ReversalBounceSetup Setup,
    ReversalBounceTradePlan? TradePlan);     // null nếu Stage != Confirmed hoặc fail hard gate
```

### 2.2. `ReversalBounceTradePlan.cs` (Domain)

Đặt file tại `Domain/Services/ReversalBounce/ReversalBounceTradePlan.cs` (không phải Application — vì trade plan là khái niệm domain, không có dependency):

```csharp
namespace StockRadar.Domain.Services.ReversalBounce;

// Chỉ chứa các const / helper domain. Record `ReversalBounceTradePlan` ở Application để tránh
// duplicate (xem §2.1).

public static class ReversalBounceTradePlanDefaults
{
    public const int DefaultTimeStopSessions = 10;
    public const decimal DefaultPositionFactor = 0.25m;
    public const decimal MinRewardToRisk = 1.5m;
}
```

---

## 3. Options & helpers

### 3.1. Mở rộng `ReversalBounceOptions` (đã có ở 0B)

**KHÔNG** thay đổi các field đã có (`Enabled`, `.ToRegimeThresholds()`). Chỉ **thêm** các section sau:

```csharp
public sealed class ReversalBounceOptions
{
    // ── Field đã có từ 0B — KHÔNG sửa ─────────────────────────────
    public const string SectionName = "ReversalBounce";
    public bool Enabled { get; set; } = true;
    public MarketRegimeThresholds Regime { get; set; } = MarketRegimeThresholds.Default;

    // ── Thêm mới cho Phase 0C/0D ──────────────────────────────────
    public string StrategyVersion { get; set; } = "reversal-bounce@1.0.0";
    public int SchemaVersion { get; set; } = 1;

    // --- Phase A: Capitulation ---
    public decimal MinDrawdownPercent { get; set; } = 18m;
    public decimal MinDrawdownInAtr { get; set; } = 2.5m;
    public decimal OversoldRsiThreshold { get; set; } = 25m;
    public decimal SellingClimaxVolMultiple { get; set; } = 2.5m;
    public int WideDownBarsMinCount { get; set; } = 3;
    public int WideDownBarsWindow { get; set; } = 10;
    public decimal WideDownBarsRangeToAtr { get; set; } = 1.2m;

    // --- Phase B: Stabilization ---
    public int StabilizationNoNewLowToleranceAtr { get; set; } = 1;
    public decimal RangeContractionRatio { get; set; } = 0.7m;
    public int StabilizationMinSessions { get; set; } = 2;
    public decimal LowerWickRatioThreshold { get; set; } = 0.55m;
    public int LowerWickMinCount { get; set; } = 2;

    // --- Phase C: Confirmation ---
    public int ConfirmationLookbackHigh { get; set; } = 2;
    public decimal StrongCloseClvThreshold { get; set; } = 0.65m;
    public decimal DemandExpansionVolMultiple { get; set; } = 1.4m;
    public decimal GapCancelAtrMultiple { get; set; } = 0.5m;
    public decimal GapAcceptanceAtrMultiple { get; set; } = 0.15m;
    public int ConfirmationEmaShort { get; set; } = 5;
    public int ConfirmationEmaLong { get; set; } = 10;

    // --- Phase invalidation ---
    public int InvalidConfirmationBufferAtr { get; set; } = 1;

    // --- Lookback ---
    public int LookbackSessions { get; set; } = 80;
    public int MaShortWindow { get; set; } = 20;
    public int MaLongWindow { get; set; } = 50;
    public int AtrWindow { get; set; } = 14;
    public int RsiWindow { get; set; } = 14;

    // --- Universe filter ---
    public int MinHistoryDays { get; set; } = 60;
    public decimal MinAvgDailyVolume { get; set; } = 100_000m;

    // --- Hard gate per regime ---
    public ReversalBounceRegimeThresholds RegimeThresholds { get; set; } = new();
    public ReversalBounceTradeOptions Trade { get; set; } = new();
}

public sealed class ReversalBounceRegimeThresholds
{
    public decimal PanicMinScore { get; set; } = 0m;             // không actionable
    public decimal StabilizingMinScore { get; set; } = 80m;
    public decimal StabilizingMinDemand { get; set; } = 18m;
    public decimal ReboundConfirmedMinScore { get; set; } = 72m;
    public decimal ReboundConfirmedMinDemand { get; set; } = 12m;
    public decimal NormalMinScore { get; set; } = 75m;
    public decimal NormalMinDemand { get; set; } = 14m;
    public decimal MinLiquidityScore { get; set; } = 5m;
    public decimal MaxRiskPenalty { get; set; } = -5m;            // RiskPenalty phải >= -5

    public decimal StabilizingPositionFactor { get; set; } = 0.25m;
    public decimal ReboundConfirmedPositionFactor { get; set; } = 0.50m;
    public decimal NormalPositionFactor { get; set; } = 0.40m;
}

public sealed class ReversalBounceTradeOptions
{
    public int TimeStopSessions { get; set; } = 10;
    public int MaxHoldSessions { get; set; } = 20;
    public decimal MinRewardToRisk { get; set; } = 1.5m;
    public decimal SlippageBaseBps { get; set; } = 10m;
    public decimal SlippageGapImpactCoeff { get; set; } = 0.5m;
    public decimal SlippageFloorLockPenaltyBps { get; set; } = 30m;
    public decimal FeeBuyPercent { get; set; } = 0.15m;
    public decimal FeeSellPercent { get; set; } = 0.15m;
    public decimal TaxSellPercent { get; set; } = 0.10m;
    public int MinTradingSessionsToSell { get; set; } = 3;
    public int MaxSignalsPerDay { get; set; } = 5;
}
```

### 3.2. `ReversalBounceBacktestOptions.cs`

```csharp
namespace StockRadar.Application.Options;

public sealed class ReversalBounceBacktestOptions
{
    public const string SectionName = "ReversalBounceBacktest";
    public int MaxSetupsToSimulate { get; set; } = 10_000;
    public int MaxSignalsPerDay { get; set; } = 5;
    public int DefaultMinTradingSessionsToSell { get; set; } = 3;
    public int DefaultTimeStopSessions { get; set; } = 10;
    public int DefaultMaxHoldSessions { get; set; } = 20;
}
```

### 3.3. `ExchangePriceBand.cs` (Domain/MarketData)

Dùng runtime — không sửa `OhlcvBar` (đợt 0D mới sửa):

```csharp
namespace StockRadar.Domain.MarketData;

/// <summary>Tính ReferencePrice/Floor/Ceiling runtime từ OHLCV + Exchange (chưa lưu DB).</summary>
public static class ExchangePriceBand
{
    public const decimal Hose = 0.07m;
    public const decimal Hnx  = 0.10m;
    public const decimal Upcom = 0.15m;

    public static decimal GetReferencePrice(IReadOnlyList<OhlcvBar> history, DateOnly asOfDate)
    {
        var prev = history.LastOrDefault(b => b.Date < asOfDate);
        return prev?.Close ?? 0m;
    }

    public static (decimal Floor, decimal Ceiling) Calculate(decimal referencePrice, string? exchange)
    {
        var band = exchange?.Trim().ToUpperInvariant() switch
        {
            var s when s.Contains("HNX")              => Hnx,
            var s when s.Contains("UPCOM") || s.Contains("UPCM") => Upcom,
            _                                          => Hose
        };
        var rawFloor   = referencePrice * (1m - band);
        var rawCeiling = referencePrice * (1m + band);
        return (RoundToTick(rawFloor, referencePrice), RoundToTick(rawCeiling, referencePrice));
    }

    private static decimal RoundToTick(decimal price, decimal reference)
    {
        var tick = reference switch
        {
            <  50_000m => 100m,
            < 100_000m => 1_000m,
            _          => 10_000m
        };
        return Math.Round(price / tick, MidpointRounding.AwayFromZero) * tick;
    }

    /// <summary>Proxy floor-lock đơn giản (chấp nhận cho MVP — sẽ tinh chỉnh ở 0D khi có data thật).</summary>
    public static bool IsLikelyFloorLocked(OhlcvBar bar, decimal? floorPrice)
    {
        if (floorPrice is null) return false;
        return Math.Abs(bar.Close - floorPrice.Value) <= 100m && bar.Close == bar.Low;
    }
}
```

---

## 4. SetupId deterministic & idempotency

### 4.1. Quy tắc

Mỗi "đợt tìm đáy" (đáy mới → setup mới). Cùng `(Symbol, CapitulationDate, StrategyVersion)` → cùng SetupId.

### 4.2. Helper

```csharp
namespace StockRadar.Domain.Services.ReversalBounce;

public static class ReversalBounceSetupId
{
    public static Guid Compute(string symbol, DateOnly? capitulationDate, string strategyVersion)
    {
        var key = $"{symbol}|{capitulationDate?.ToString("yyyy-MM-dd") ?? "0001-01-01"}|{strategyVersion}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}
```

### 4.3. Hệ quả

- Snapshot insert `(TradingDate, Symbol, StrategyVersion, SetupId)` là idempotent qua mọi lần chạy lại cùng phiên.
- Khi `CapitulationDate` đổi (đáy mới sâu hơn) → SetupId đổi → coi là setup mới, viết 1 snapshot mới với `RecoveryAttemptCount` reset về 1.
- Khi `CapitulationDate` giữ nguyên qua các phiên → cùng SetupId → có thể query lịch sử setup bằng `(Symbol, SetupId)` và đếm phiên đã trôi qua để tính `RecoveryAttemptCount`.

### 4.4. Tính `RecoveryAttemptCount`

Đây là **một chỗ duy nhất** analyzer cần đọc DB (qua `IReversalCandidateSnapshotRepository.GetHistoryAsync(symbol, from, to)`):

```csharp
var previousSnapshots = await snapshotRepo.GetHistoryAsync(
    symbol,
    asOfDate.AddDays(-LookbackSessions),
    asOfDate.AddDays(-1),
    ct);
var sameSetupPrior = previousSnapshots
    .Where(s => s.SetupId == setupId && s.StrategyVersion == StrategyVersion)
    .Count();
var recoveryAttemptCount = sameSetupPrior + 1;   // +1 cho hôm nay
```

> Stateless vẫn đúng vì không có state machine mutable; chỉ là query idempotent.

---

## 5. Công thức tính điểm 6 trục (đã chốt)

### 5.1. `CapitulationScore` (0..15)

```text
DrawdownScore     = clamp(|DrawdownPct| / 25, 0, 1) × 8       // đạt max ở -25%
OversoldScore     = clamp((25 - RSI14) / 15, 0, 1) × 4         // đạt max ở RSI=10
IntensityScore    = clamp(DrawdownInAtr / 4, 0, 1) × 3        // đạt max ở 4 ATR
CapitulationScore = min(15, tổng 3 mục trên)
```

### 5.2. `StabilizationScore` (0..20)

```text
RangeContraction  = clamp(1 - ATR_now / ATR_capit, 0, 1) × 8
NoNewLowScore     = clamp(NoNewLowSessions / 4, 0, 1) × 6
DownVolDryUp      = clamp(1 - DownVol_5 / AvgDownVol_20, 0, 1) × 3
WicksOrRsImproving= 3 nếu (LowerWicks ≥ 2 HOẶC RsSlope5 > 0), 0 nếu không
StabilizationScore = min(20, tổng)
```

### 5.3. `DemandScore` (0..15)

```text
PriceBreak       = 5 nếu Close > HighestHigh(T-2..T-1) HOẶC Close > EMA5/10; 0 nếu không
StrongClose      = clamp(CLV / 0.85, 0, 1) × 4                 // CLV = (C-L) / max(H-L, ε)
VolumeExpansion  = clamp(Volume_T / AvgVol_Stab - 1, 0, 1) × 4 // đạt max ở +100%
NotOverextended  = 2 nếu gap ≤ GapAcceptance × ATR14%, 0 nếu gap > GapCancel × ATR14%
DemandScore      = min(15, tổng)
```

### 5.4. `RelativeStrengthScore` (0..15)

```text
RsImproving      = clamp(RsSlope5 × 10, 0, 1) × 8             // slope dương
RsPercentileNow  = clamp((RsPct - 50) / 30, 0, 1) × 4         // Pct > 80 đạt max
VsSector         = 3 nếu SectorMedianChange5d > IndexChange5d; 0 nếu không
RelativeStrengthScore = min(15, tổng)
```

> `RsPct` lấy từ `RsPercentile` đã có (xem `BuildRsPercentile` ở `SmartMoneyOpportunitySelector.cs:87-114` — inject qua `SmartMoneyMarketContext` nếu có, hoặc tính tương tự).

### 5.5. `LiquidityScore` (0..10)

```text
AvgVolume20      = clamp(log10(AvgVolume20 / MinAvgDailyVolume) / 2, 0, 1) × 6
TurnoverAdequate = 4 nếu MedianTurnover ≥ 1e9, 2 nếu ≥ 5e8, 0 nếu < 5e8
LiquidityScore   = min(10, tổng)
```

### 5.6. `RiskPenalty` (-10..0)

```text
FloorLockRecent       = -3 nếu có ≥ 1 phiên sàn (IsLikelyFloorLocked) trong 5 phiên gần nhất
ConsecutiveDownBars   = -clamp(ConsecutiveDownBars / 4, 0, 1) × 3
GapVolatility         = -clamp(AvgTrueRangePct / 5, 0, 1) × 2
NearSupplyCluster     = -2 nếu lệch bán dày đặc trong 5% trên (gặp cung gần entry)
RiskPenalty           = max(-10, tổng)
```

> `RiskPenalty` âm càng nhiều = rủi ro càng cao. Hard gate yêu cầu `RiskPenalty >= -5`.

### 5.7. `TotalScore`

```text
TotalScore = max(0, min(100,
    CapitulationScore
    + StabilizationScore
    + DemandScore
    + RelativeStrengthScore
    + LiquidityScore
    + RiskPenalty))
```

---

## 6. Algorithm chi tiết (Domain/Services/ReversalBounce/ReversalBounceAnalyzer.cs)

### 6.1. `ComputeFeatures` — đã sửa O(n²) → O(n)

```csharp
private static ReversalBounceFeatures ComputeFeatures(
    IReadOnlyList<OhlcvBar> bars,
    IReadOnlyList<OhlcvBar> indexHistory,
    DateOnly asOfDate,
    int maShort, int maLong, int atrWindow, int rsiWindow)
{
    // asOfDate = phiên hiện tại (inclusive) — analyzer xét Close(asOfDate).
    var past = bars.Where(b => b.Date <= asOfDate).OrderBy(b => b.Date).ToList();
    if (past.Count < maLong) throw new InsufficientHistoryException();

    var ma20     = past.TakeLast(maShort).Average(b => b.Close);
    var ma20prev = past.SkipLast(maShort).TakeLast(maShort).Average(b => b.Close);
    var ma50     = past.TakeLast(maLong).Average(b => b.Close);
    var atr      = ComputeAtr(past, atrWindow);
    var rsi      = ComputeRsi(past, rsiWindow);

    // Peak trong 60 phiên gần nhất → CapitulationLow SAU peak
    var window60 = past.TakeLast(60).ToList();
    var peakIdxLocal = 0;
    for (var i = 0; i < window60.Count; i++)
        if (window60[i].High > window60[peakIdxLocal].High) peakIdxLocal = i;

    // Tìm index của peak trong `past`
    var globalPeakStart = past.Count - window60.Count;
    var peakIdx = globalPeakStart + peakIdxLocal;

    var afterPeak = past.Skip(peakIdx + 1).ToList();
    if (afterPeak.Count == 0)
        return new ReversalBounceFeatures(past, ma20, ma20prev, ma50, atr, rsi, null, null, null, 0m, 0m);

    var capitIdxLocal = 0;
    for (var i = 0; i < afterPeak.Count; i++)
        if (afterPeak[i].Low < afterPeak[capitIdxLocal].Low) capitIdxLocal = i;

    var capitIdxGlobal = peakIdx + 1 + capitIdxLocal;
    var capitulationLow = afterPeak[capitIdxLocal].Low;
    var capitulationClose = afterPeak[capitIdxLocal].Close;
    var peakHigh = window60[peakIdxLocal].High;
    var drawdownPct = peakHigh > 0 ? (capitulationLow - peakHigh) / peakHigh * 100m : 0m;
    var drawdownInAtr = atr > 0 ? (peakHigh - capitulationLow) / atr : 0m;

    return new ReversalBounceFeatures(
        past, ma20, ma20prev, ma50, atr, rsi,
        PeakHigh: peakHigh,
        CapitulationLow: capitulationLow,
        CapitulationClose: capitulationClose,
        CapitulationDate: afterPeak[capitIdxLocal].Date,
        CapitulationIndex: capitIdxGlobal,
        DrawdownPercent: drawdownPct,
        DrawdownInAtr: drawdownInAtr,
        IndexHistory: indexHistory);
}
```

### 6.2. `ComputeAtr` — sửa O(n²) → O(n)

```csharp
private static decimal ComputeAtr(IReadOnlyList<OhlcvBar> history, int window)
{
    if (history.Count < 2) return 0m;
    var trs = new List<decimal>(window);
    var start = Math.Max(1, history.Count - window);
    for (var i = start; i < history.Count; i++)
    {
        var high = history[i].High;
        var low  = history[i].Low;
        var prevClose = history[i - 1].Close;
        var tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
        trs.Add(tr);
    }
    return trs.Count == 0 ? 0m : trs.Average();
}
```

### 6.3. `HasCapitulation`

Đã chốt ở spec tổng §5.1. Implementation mẫu:

```csharp
private static (bool, List<ReversalBounceReason>) HasCapitulation(
    ReversalBounceFeatures f, ReversalBounceOptions opt)
{
    var reasons = new List<ReversalBounceReason>();
    var ddOk = Math.Abs(f.DrawdownPercent) >= opt.MinDrawdownPercent;
    var ddaOk = f.DrawdownInAtr >= opt.MinDrawdownInAtr;
    reasons.Add(new("DRAWDOWN_FROM_PEAK", "Drawdown từ đỉnh", f.DrawdownPercent, -opt.MinDrawdownPercent, ddOk));
    reasons.Add(new("DRAWDOWN_IN_ATR", "Drawdown theo ATR", f.DrawdownInAtr, opt.MinDrawdownInAtr, ddaOk));

    var oversold = f.Rsi <= opt.OversoldRsiThreshold;
    var climax = SellingClimax(f, opt.SellingClimaxVolMultiple);
    var wideDown = CountWideDownBars(f.History, f.CapitulationIndex, opt);
    var wideDownOk = wideDown >= opt.WideDownBarsMinCount;

    reasons.Add(new("RSI_OVERSOLD", "RSI14", f.Rsi, opt.OversoldRsiThreshold, oversold));
    reasons.Add(new("SELLING_CLIMAX", "Volume climax", climax, opt.SellingClimaxVolMultiple, climax > opt.SellingClimaxVolMultiple));
    reasons.Add(new("WIDE_DOWN_BARS", "Số phiên giảm mạnh", wideDown, opt.WideDownBarsMinCount, wideDownOk));

    var capit = ddOk && ddaOk && (oversold || climax > opt.SellingClimaxVolMultiple || wideDownOk);
    return (capit, reasons);
}
```

### 6.4. `HasStabilized`

Lấy slice **sau `CapitulationIndex`** (sau đáy):

```csharp
private static (bool, List<ReversalBounceReason>) HasStabilized(
    ReversalBounceFeatures f, ReversalBounceOptions opt)
{
    var afterCapit = f.History.Skip(f.CapitulationIndex + 1).ToList();
    if (afterCapit.Count < opt.StabilizationMinSessions)
        return (false, []);

    var atrCapit = ComputeAtr(f.History.Take(f.CapitulationIndex + 1).ToList(), opt.AtrWindow);
    var rangeContracted = f.Atr <= atrCapit * opt.RangeContractionRatio;

    var tol = f.Atr * opt.StabilizationNoNewLowToleranceAtr;
    var noNewLow = afterCapit.Min(b => b.Low) >= f.CapitulationLow!.Value - tol;

    var noNewLowSessions = CountConsecutiveNoNewLowSessions(afterCapit, f.CapitulationLow!.Value, tol);
    var downVolDryUp = ComputeDownVolumeDryUp(afterCapit);
    var lowerWicks = CountLowerWicks(afterCapit, opt.LowerWickRatioThreshold);
    var rsImproving = ComputeRsImproving(f.IndexHistory, afterCapit);

    var ok = noNewLow && rangeContracted
        && (downVolDryUp || lowerWicks >= opt.LowerWickMinCount || rsImproving);

    // reasons...
    return (ok, reasons);
}
```

### 6.5. `IsConfirmed`

```csharp
private static (bool, List<ReversalBounceReason>) IsConfirmed(
    ReversalBounceFeatures f, ReversalBounceOptions opt)
{
    var today = f.History[^1];
    var prior = f.History.TakeLast(opt.ConfirmationLookbackHigh + 1).Take(opt.ConfirmationLookbackHigh).ToList();

    var priceBreak = today.Close > prior.Max(b => b.High);
    var emaShort = ComputeEma(f.History, opt.ConfirmationEmaShort);
    var emaLong  = ComputeEma(f.History, opt.ConfirmationEmaLong);
    var emaBreak = today.Close > emaShort || today.Close > emaLong;

    var clv = today.High > today.Low
        ? (today.Close - today.Low) / (today.High - today.Low)
        : 0m;
    var strongClose = clv >= opt.StrongCloseClvThreshold;

    var volAvgStab = ComputeAvgVolume(f.History.TakeLast(10));
    var demandExpansion = today.Volume >= volAvgStab * opt.DemandExpansionVolMultiple;

    var gap = (today.Open / f.History[^2].Close) - 1m;
    var notOverextended = gap <= opt.GapCancelAtrMultiple * (f.Atr / today.Close);

    var confirmed = (priceBreak || emaBreak) && strongClose && demandExpansion && notOverextended;
    return (confirmed, reasons);
}
```

### 6.6. `IsInvalidated`

```csharp
private static bool IsInvalidated(
    ReversalBounceFeatures f, MarketRegime regime, ReversalBounceOptions opt)
{
    var tol = f.Atr * opt.StabilizationNoNewLowToleranceAtr;
    var brokeCapitLow = f.CapitulationLow is not null
        && f.History[^1].Close < f.CapitulationLow.Value - tol;

    var brokeConfirmation = f.History[^1].Close < f.History[^1].Open - opt.InvalidConfirmationBufferAtr * f.Atr;

    var panicRegime = regime == MarketRegime.Panic;

    return brokeCapitLow || brokeConfirmation || panicRegime;
}
```

### 6.7. `Stage` suy ra

```csharp
public static ReversalBounceStage DeriveStage(
    bool hasCapitulation, bool isInvalidated, bool isConfirmed, bool hasStabilized) =>
    !hasCapitulation ? ReversalBounceStage.None
    : isInvalidated    ? ReversalBounceStage.Invalidated
    : isConfirmed      ? ReversalBounceStage.Confirmed
    : hasStabilized    ? ReversalBounceStage.Stabilizing
    :                    ReversalBounceStage.Capitulating;
```

### 6.8. Tính `TotalScore`

```csharp
public static decimal ComputeTotalScore(ReversalBounceComponentScores s) =>
    Math.Clamp(
        s.Capitulation + s.Stabilization + s.Demand + s.RelativeStrength + s.Liquidity + s.RiskPenalty,
        0m, 100m);
```

### 6.9. `NearestSupplyZone`

```csharp
public static decimal NearestSupplyZone(IReadOnlyList<OhlcvBar> past, decimal entryRef, decimal atr)
{
    var ema20 = ComputeEma(past, 20);
    var candidates = new List<decimal> { ema20 };

    // Gap-down resistance
    for (var i = 1; i < past.Count; i++)
        if (past[i].Low > past[i - 1].High)
            candidates.Add(past[i - 1].High);

    // Swing high 40 phiên
    candidates.Add(past.TakeLast(40).Max(b => b.High));
    candidates.Add(entryRef + 2m * atr);

    return candidates.Where(z => z > entryRef).DefaultIfEmpty(entryRef + 2m * atr).Min();
}
```

---

## 7. CounterTrendDecisionEngine (Application)

### 7.1. Hard gate (luôn thắng score)

```csharp
public ReversalBounceSignal Decide(ReversalBounceSignal signal, ReversalBounceOptions opt)
{
    var s = signal.Setup;
    var t = opt.RegimeThresholds;

    // Stage != Confirmed → không có plan
    if (s.Stage != ReversalBounceStage.Confirmed)
        return signal with { TradePlan = null };

    // Regime = Panic → không có plan (kể cả khi stage = Confirmed)
    if (s.MarketRegime == MarketRegime.Panic)
        return signal with { TradePlan = null };

    // Lấy threshold theo regime
    var (minScore, minDemand, positionFactor) = s.MarketRegime switch
    {
        MarketRegime.Stabilizing      => (t.StabilizingMinScore, t.StabilizingMinDemand, t.StabilizingPositionFactor),
        MarketRegime.ReboundConfirmed => (t.ReboundConfirmedMinScore, t.ReboundConfirmedMinDemand, t.ReboundConfirmedPositionFactor),
        _                              => (t.NormalMinScore, t.NormalMinDemand, t.NormalPositionFactor)
    };

    // Hard gate
    if (s.TotalScore < minScore) return signal with { TradePlan = null };
    if (s.ComponentScores.Demand < minDemand) return signal with { TradePlan = null };
    if (s.ComponentScores.Liquidity < t.MinLiquidityScore) return signal with { TradePlan = null };
    if (s.ComponentScores.RiskPenalty < t.MaxRiskPenalty) return signal with { TradePlan = null };

    // Build plan
    var today = s.TradingDate;
    var stockHistory = signal.Setup; // placeholder — pass history riêng nếu cần
    var features = /* reconstruct từ Reasons hoặc truyền qua signal */ default;
    // (Decision engine nhận input đầy đủ — xem §7.2)

    var entryRef = features.Close(asOfDate);
    var maxEntry = entryRef * (1m + opt.GapAcceptanceAtrMultiple * features.AtrPercent);
    var invalidation = ComputeInvalidationPrice(features);
    var target = NearestSupplyZone(features.History, entryRef, features.Atr);
    var rAbs = Math.Abs(entryRef - invalidation);
    var rr = rAbs > 0 ? Math.Abs(target - entryRef) / rAbs : 0m;

    if (rr < opt.Trade.MinRewardToRisk) return signal with { TradePlan = null };

    var plan = new ReversalBounceTradePlan(
        EntryReference: entryRef,
        MaxEntryPrice: maxEntry,
        InvalidationPrice: invalidation,
        FirstTarget: target,
        RewardToRisk: Math.Round(rr, 2),
        TimeStopSessions: opt.Trade.TimeStopSessions,
        PositionFactor: positionFactor,
        RiskWarnings: BuildRiskWarnings(features, opt));

    return signal with { TradePlan = plan };
}
```

### 7.2. Đề xuất refactor signature

Để tránh "reconstruct features", đổi signature thành:

```csharp
public interface ICounterTrendDecisionEngine
{
    ReversalBounceSignal Decide(
        ReversalBounceSetup setup,
        ReversalBounceFeatures features,
        ReversalBounceOptions opt);
}
```

→ Tách `Setup` (DB-friendly) khỏi `Features` (in-memory). Caller (runner) giữ cả hai.

---

## 8. ReversalBounceAnalysisRunner (Infrastructure)

### 8.1. Async wrapper cho analyzer

```csharp
namespace StockRadar.Infrastructure.MarketData;

internal sealed class ReversalBounceAnalysisRunner(
    ApplicationDbContext db,
    IJobStockRepository stockRepo,
    IMarketBreadthSnapshotRepository breadthRepo,
    IReversalBounceAnalyzer analyzer,
    ICounterTrendDecisionEngine decision,
    IReversalCandidateSnapshotRepository snapshotRepo,
    IOptions<ReversalBounceOptions> options,
    ILogger<ReversalBounceAnalysisRunner> logger)
{
    public async Task<ReversalBounceAnalysisResult> RunAsync(
        DateOnly forTradingDate,
        Guid runBatchId,
        CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        var universe = (await stockRepo.GetAllAsync(cancellationToken))
            .Where(s => s.IsActive && !s.TradingRestricted)
            .ToList();

        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);
        var breadth = await breadthRepo.GetPreviousAsync(forTradingDate, cancellationToken);
        var regime = breadth?.Regime ?? MarketRegime.Normal;
        var breadthSnapshotId = breadth is null ? Guid.Empty : Guid.Empty; // PK là DateOnly, không có Id riêng

        var parametersHash = ComputeHash(opt);
        int snapshotsWritten = 0, signalsEmitted = 0;
        var eligibleSignals = new List<ReversalBounceSignal>();

        foreach (var stock in universe)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stock.History.Count < opt.MinHistoryDays) continue;
            if (AverageVolume(stock.History) < opt.MinAvgDailyVolume) continue;

            var setup = analyzer.Analyze(
                stock, indexHistory, regime, forTradingDate, opt,
                strategyVersion: opt.StrategyVersion,
                parametersHash: parametersHash,
                runBatchId: runBatchId);

            if (setup.Stage == ReversalBounceStage.None) continue;

            var features = analyzer.LastFeatures;  // hoặc return tuple (Setup, Features)
            var decided = decision.Decide(setup, features, opt);
            await snapshotRepo.UpsertAsync(BuildEntity(decided), cancellationToken);
            snapshotsWritten++;

            if (decided.TradePlan is not null) eligibleSignals.Add(decided);
        }

        // Limit per day → đánh dấu Actionable=false thay vì xóa row (giữ audit trail)
        var maxSignals = opt.Trade.MaxSignalsPerDay;
        var ranked = eligibleSignals
            .OrderByDescending(s => s.Setup.TotalScore)
            .ThenBy(s => s.Setup.Symbol)
            .ToList();
        var actionable = ranked.Take(maxSignals).ToHashSet();
        var demoted = ranked.Skip(maxSignals).Select(s => s.Setup.SetupId).ToHashSet();
        await snapshotRepo.DemoteNonActionableAsync(forTradingDate, opt.StrategyVersion, demoted, cancellationToken);
        signalsEmitted = actionable.Count;

        return new ReversalBounceAnalysisResult(
            runBatchId, forTradingDate, universe.Count, snapshotsWritten, signalsEmitted);
    }

    private async Task<IReadOnlyList<OhlcvBar>> LoadIndexHistoryAsync(CancellationToken cancellationToken)
    {
        var entity = await db.MarketIndices.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Symbol == "VNINDEX", cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.HistoryJson)) return [];
        return JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, EntityMapper.JsonOptions) ?? [];
    }
}
```

### 8.2. Canonical hash cho `AlgorithmParametersHash`

```csharp
private static string ComputeHash(ReversalBounceOptions opt)
{
    var canonical = new
    {
        opt.MinDrawdownPercent, opt.MinDrawdownInAtr,
        opt.OversoldRsiThreshold, opt.SellingClimaxVolMultiple,
        opt.WideDownBarsMinCount, opt.WideDownBarsWindow, opt.WideDownBarsRangeToAtr,
        opt.StabilizationNoNewLowToleranceAtr, opt.RangeContractionRatio,
        opt.StabilizationMinSessions, opt.LowerWickRatioThreshold, opt.LowerWickMinCount,
        opt.ConfirmationLookbackHigh, opt.StrongCloseClvThreshold,
        opt.DemandExpansionVolMultiple, opt.GapCancelAtrMultiple, opt.GapAcceptanceAtrMultiple,
        opt.ConfirmationEmaShort, opt.ConfirmationEmaLong,
        opt.InvalidConfirmationBufferAtr,
        opt.LookbackSessions, opt.MaShortWindow, opt.MaLongWindow,
        opt.AtrWindow, opt.RsiWindow,
        opt.MinHistoryDays, opt.MinAvgDailyVolume,
        opt.RegimeThresholds, opt.Trade
    };
    var json = JsonSerializer.Serialize(canonical, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
    return Convert.ToHexString(bytes).ToLowerInvariant();
}
```

> **KHÔNG** gồm `Enabled` hay `StrategyVersion` trong hash (chúng nằm trong snapshot PK).

### 8.3. Wiring vào `DailyAnalysisRunner`

`DailyAnalysisRunner.RunAsync(CancellationToken, bool runPostProcessing)` đã có. Inject trực tiếp `IReversalBounceAnalysisService` (qua `InternalDependencyInjection`) vào constructor; **cuối** hàm (sau khi `MarketBreadthRunner` chạy xong) gọi:

```csharp
// Trong DailyAnalysisRunner.RunAsync, SAU khi breadth runner chạy
if (opt.ReversalBounceEnabled && runPostProcessing)
{
    var analysisRun = await reversalBounce.RunAsync(asOfDate, runBatchId, cancellationToken);
    logger.LogInformation(
        "ReversalBounce: scanned={Scanned}, snapshots={Snaps}, actionable={Sig}",
        analysisRun.UniverseScanned, analysisRun.SnapshotsWritten, analysisRun.SignalsEmitted);
}
```

`ReversalBounceEnabled` lấy từ `ReversalBounceOptions.Enabled` (đã có ở 0B).

### 8.4. Đăng ký DI trong `DependencyInjection.cs`

Thêm sau các dòng 0B hiện có:

```csharp
services.AddScoped<ReversalBounceAnalyzer>();
services.AddScoped<IReversalBounceAnalyzer>(sp => sp.GetRequiredService<ReversalBounceAnalyzer>());
services.AddScoped<CounterTrendDecisionEngine>();
services.AddScoped<ICounterTrendDecisionEngine>(sp => sp.GetRequiredService<CounterTrendDecisionEngine>());
services.AddScoped<EfReversalCandidateSnapshotRepository>();
services.AddScoped<IReversalCandidateSnapshotRepository>(sp => sp.GetRequiredService<EfReversalCandidateSnapshotRepository>());
services.AddScoped<ReversalBounceAnalysisRunner>();
services.AddScoped<IReversalBounceAnalysisService>(sp => sp.GetRequiredService<ReversalBounceAnalysisRunner>());
services.AddScoped<ReversalBounceBacktestRunner>();
services.AddScoped<IReversalBounceBacktestService>(sp => sp.GetRequiredService<ReversalBounceBacktestRunner>());
services.Configure<ReversalBounceBacktestOptions>(configuration.GetSection(ReversalBounceBacktestOptions.SectionName));
```

---

## 9. EF entities + Migration (BẢNG MỚI)

### 9.1. `ReversalCandidateSnapshotEntity`

```csharp
namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class ReversalCandidateSnapshotEntity
{
    public Guid Id { get; set; }
    public DateOnly TradingDate { get; set; }
    public string Symbol { get; set; } = "";
    public string Stage { get; set; } = "";
    public Guid SetupId { get; set; }
    public DateOnly? CapitulationDate { get; set; }
    public decimal? CapitulationLow { get; set; }
    public decimal? CapitulationClose { get; set; }
    public int RecoveryAttemptCount { get; set; }
    public decimal ScoreCapitulation { get; set; }
    public decimal ScoreStabilization { get; set; }
    public decimal ScoreDemand { get; set; }
    public decimal ScoreRelativeStrength { get; set; }
    public decimal ScoreLiquidity { get; set; }
    public decimal ScoreRiskPenalty { get; set; }
    public decimal TotalScore { get; set; }
    public string MarketRegime { get; set; } = "Normal";
    public bool IsActionable { get; set; } = false;             // đánh dấu top-5 trong ngày
    public string StrategyVersion { get; set; } = "";
    public string AlgorithmParametersHash { get; set; } = "";
    public int SchemaVersion { get; set; }
    public Guid RunBatchId { get; set; }
    public string ReasonsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
}
```

### 9.2. Migration — **TẠO BẰNG CLI** (không viết tay)

```bash
cd backend
dotnet ef migrations add AddReversalBounceSnapshots \
  --project StockRadar.Infrastructure \
  --startup-project StockRadar.Api \
  --context ApplicationDbContext
```

Sau đó **chỉnh tay** 2 chỗ nếu EF generate sai:
- `ReasonsJson` phải là `nvarchar(max)`, không default `nvarchar(4000)`.
- `decimal(18,2)` cho tất cả cột tiền (xem pattern migration 0B).

### 9.3. Đăng ký DbSet

```csharp
public DbSet<ReversalCandidateSnapshotEntity> ReversalCandidateSnapshots
    => Set<ReversalCandidateSnapshotEntity>();
```

### 9.4. `OnModelCreating` block

```csharp
modelBuilder.Entity<ReversalCandidateSnapshotEntity>(e =>
{
    e.HasKey(x => x.Id);
    e.Property(x => x.Symbol).HasMaxLength(16);
    e.Property(x => x.Stage).HasMaxLength(32);
    e.Property(x => x.MarketRegime).HasMaxLength(32);
    e.Property(x => x.StrategyVersion).HasMaxLength(32);
    e.Property(x => x.AlgorithmParametersHash).HasMaxLength(64);
    e.Property(x => x.ReasonsJson).HasColumnType("nvarchar(max)");
    e.HasIndex(x => new { x.TradingDate, x.Symbol, x.StrategyVersion, x.SetupId }).IsUnique();
    e.HasIndex(x => x.TradingDate);
    e.HasIndex(x => x.Symbol);
    e.HasIndex(x => x.SetupId);
});
```

---

## 10. Repository

### 10.1. `IReversalCandidateSnapshotRepository`

```csharp
public interface IReversalCandidateSnapshotRepository
{
    Task UpsertAsync(ReversalCandidateSnapshotEntity entity, CancellationToken ct = default);
    Task<IReadOnlyList<ReversalCandidateSnapshotEntity>> GetForDateAsync(
        DateOnly tradingDate, bool? actionableOnly = null, CancellationToken ct = default);
    Task<IReadOnlyList<ReversalCandidateSnapshotEntity>> GetHistoryAsync(
        string symbol, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<ReversalCandidateSnapshotEntity?> GetBySetupAsync(Guid setupId, CancellationToken ct = default);
    Task<int> DeleteByRunBatchAsync(Guid runBatchId, CancellationToken ct = default);
    Task DemoteNonActionableAsync(
        DateOnly tradingDate, string strategyVersion,
        IReadOnlyCollection<Guid> setupIdsToExclude,
        CancellationToken ct = default);
}
```

> `Upsert` dựa trên conflict unique index `(TradingDate, Symbol, StrategyVersion, SetupId)`.

### 10.2. `EfReversalCandidateSnapshotRepository` — skeleton

```csharp
internal sealed class EfReversalCandidateSnapshotRepository(ApplicationDbContext db)
    : IReversalCandidateSnapshotRepository
{
    public async Task UpsertAsync(ReversalCandidateSnapshotEntity entity, CancellationToken ct = default)
    {
        var existing = await db.ReversalCandidateSnapshots
            .FirstOrDefaultAsync(s =>
                s.TradingDate == entity.TradingDate
                && s.Symbol == entity.Symbol
                && s.StrategyVersion == entity.StrategyVersion
                && s.SetupId == entity.SetupId, ct);

        if (existing is null)
        {
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.CreatedAtUtc = DateTime.UtcNow;
            db.ReversalCandidateSnapshots.Add(entity);
        }
        else
        {
            existing.Stage = entity.Stage;
            existing.CapitulationDate = entity.CapitulationDate;
            existing.CapitulationLow = entity.CapitulationLow;
            existing.CapitulationClose = entity.CapitulationClose;
            existing.RecoveryAttemptCount = entity.RecoveryAttemptCount;
            existing.ScoreCapitulation = entity.ScoreCapitulation;
            existing.ScoreStabilization = entity.ScoreStabilization;
            existing.ScoreDemand = entity.ScoreDemand;
            existing.ScoreRelativeStrength = entity.ScoreRelativeStrength;
            existing.ScoreLiquidity = entity.ScoreLiquidity;
            existing.ScoreRiskPenalty = entity.ScoreRiskPenalty;
            existing.TotalScore = entity.TotalScore;
            existing.MarketRegime = entity.MarketRegime;
            existing.IsActionable = entity.IsActionable;
            existing.ReasonsJson = entity.ReasonsJson;
            existing.RunBatchId = entity.RunBatchId;
            // KHÔNG đổi: Id, SetupId, StrategyVersion, TradingDate, Symbol, CreatedAtUtc
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReversalCandidateSnapshotEntity>> GetForDateAsync(
        DateOnly tradingDate, bool? actionableOnly = null, CancellationToken ct = default)
    {
        var q = db.ReversalCandidateSnapshots.AsNoTracking()
            .Where(s => s.TradingDate == tradingDate);
        if (actionableOnly == true) q = q.Where(s => s.IsActionable);
        if (actionableOnly == false) q = q.Where(s => !s.IsActionable);
        return await q.OrderByDescending(s => s.TotalScore).ThenBy(s => s.Symbol).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReversalCandidateSnapshotEntity>> GetHistoryAsync(
        string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await db.ReversalCandidateSnapshots.AsNoTracking()
            .Where(s => s.Symbol == symbol && s.TradingDate >= from && s.TradingDate <= to)
            .OrderBy(s => s.TradingDate).ToListAsync(ct);

    public Task<ReversalCandidateSnapshotEntity?> GetBySetupAsync(Guid setupId, CancellationToken ct = default)
        => db.ReversalCandidateSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SetupId == setupId, ct);

    public async Task<int> DeleteByRunBatchAsync(Guid runBatchId, CancellationToken ct = default)
    {
        var rows = await db.ReversalCandidateSnapshots.Where(s => s.RunBatchId == runBatchId).ToListAsync(ct);
        db.ReversalCandidateSnapshots.RemoveRange(rows);
        return await db.SaveChangesAsync(ct);
    }

    public async Task DemoteNonActionableAsync(
        DateOnly tradingDate, string strategyVersion,
        IReadOnlyCollection<Guid> setupIdsToExclude,
        CancellationToken ct = default)
    {
        var candidates = await db.ReversalCandidateSnapshots
            .Where(s => s.TradingDate == tradingDate
                     && s.StrategyVersion == strategyVersion
                     && s.IsActionable)
            .ToListAsync(ct);
        foreach (var s in candidates)
            if (setupIdsToExclude.Contains(s.SetupId))
                s.IsActionable = false;
        await db.SaveChangesAsync(ct);
    }
}
```

---

## 11. BacktestRunner (Phase 0D)

### 11.1. Service interface

```csharp
public interface IReversalBounceBacktestService
{
    Task<ReversalBounceBacktestReport> RunAsync(
        ReversalBounceBacktestRequest req, CancellationToken ct = default);
}

public sealed record ReversalBounceBacktestRequest(
    DateOnly From,
    DateOnly To,
    decimal? MinScoreOverride,
    bool AllowDefensiveEarlyExit = false);

public sealed record ReversalBounceBacktestReport(
    int TotalSetups,
    int EnteredTrades,
    int ExitedTrades,
    int FloorLockDeferredCount,
    int GapCancelledCount,
    int WinCount,
    int FlatCount,
    int LoseCount,
    decimal WinRatePercent,
    decimal AvgReturnPercentGross,
    decimal AvgReturnPercentNet,
    decimal AvgMfePercent,
    decimal AvgMaePercent,
    IReadOnlyList<ReversalBounceBacktestTradeRecord> Trades);

public sealed record ReversalBounceBacktestTradeRecord(
    string Symbol, DateOnly SignalDate, DateOnly EntryDate,
    decimal EntryPrice, decimal? ExitPrice, DateOnly? ExitDate,
    int SessionsToExit,
    string ExitReason,
    decimal ReturnPercentGross, decimal ReturnPercentNet,
    decimal MaxFavorablePercent, decimal MaxAdversePercent,
    string Regime);
```

### 11.2. Fill rules (xem spec tổng §7)

```text
1. Signal at Close(T) — asOfDate=T.
2. Entry at Open(T+1):
   - If Open(T+1)/Close(T) - 1 > GapCancelAtrMultiple × ATR14%  → GapCancelled (no entry).
   - Else fill at Open(T+1) + gap-impact slippage.
3. Hard exit only from T+3 (MinTradingSessionsToSell=3) — default.
4. If AllowDefensiveEarlyExit=true → defensive exit T+1/T+2 khi invalidation xảy ra.
5. Floor-lock defer:
   - If next session Close at LikelyFloorLocked → defer exit to following session.
   - If 2 consecutive LikelyFloorLocked → force exit at Open của session +2 (mô phỏng bán được cuối cùng).
6. Exit priority:
   - Stop hit (Close ≤ InvalidationPrice)
   - Target hit (Close ≥ FirstTarget)
   - TimeStop (Sessions ≥ TimeStopSessions)
   - MaxHold (Sessions ≥ MaxHoldSessions)
7. Slippage: BaseBps + GapImpact×max(0, gap-0.5%) + FloorLockPenalty nếu phiên trước LikelyFloorLocked.
8. Fees: buy 0.15% + sell 0.15% + tax 0.10% (sell).
```

### 11.3. Win/Flat/Lose buckets (theo T+2.5)

```text
Win   : Net return ≥ +1%
Flat  : -0.5% < Net return < +1%
Lose  : Net return ≤ -0.5%
```

---

## 12. Test project (BẮT BUỘC tạo mới)

### 12.1. Khởi tạo

```bash
cd backend
dotnet new xunit -n StockRadar.Tests -f net9.0
dotnet sln StockRadar.slnx add StockRadar.Tests/StockRadar.Tests.csproj
dotnet add StockRadar.Tests/StockRadar.Tests.csproj reference StockRadar.Domain/StockRadar.Domain.csproj
dotnet add StockRadar.Tests/StockRadar.Tests.csproj reference StockRadar.Application/StockRadar.Application.csproj
dotnet add StockRadar.Tests/StockRadar.Tests.csproj reference StockRadar.Infrastructure/StockRadar.Infrastructure.csproj
```

### 12.2. Tests bắt buộc cho đợt 0C

File: `StockRadar.Tests/ReversalBounce/ReversalBounceAnalyzerTests.cs`

```csharp
public sealed class ReversalBounceAnalyzerTests
{
    [Fact] public void Stage_None_When_NoCapitulation_In_Lookback() { }
    [Fact] public void Stage_Capitulating_When_Drawdown_But_No_Stabilization() { }
    [Fact] public void Stage_Stabilizing_When_NoNewLow_And_RangeContracted() { }
    [Fact] public void Stage_Confirmed_When_PriceBreak_And_Demand_And_StrongClose() { }
    [Fact] public void Stage_Invalidated_When_Breaks_CapitulationLow() { }
    [Fact] public void Stage_Invalidated_When_Regime_Panic() { }

    [Fact] public void CapitulationScore_Reaches_Max_When_Drawdown_25pct_And_Oversold() { }
    [Fact] public void DemandScore_Zero_When_Only_PriceBreak_No_Volume() { }
    [Fact] public void DemandScore_Zero_When_GapUp_Above_Threshold() { }
    [Fact] public void RiskPenalty_Negative_When_Consecutive_FloorLocks() { }

    [Fact] public void SetupId_Stable_For_Same_Capitulation_Date() { }
    [Fact] public void SetupId_Differs_When_Capitulation_Date_Changes() { }

    [Fact] public void Atr_Loop_Is_Linear_Not_Quadratic_On_Long_History() { }
    [Fact] public void Peak_Detection_Prefers_Later_Peak_With_Same_High() { }
    [Fact] public void CapitulationLow_Is_After_Peak_Not_Before() { }

    [Fact] public void DecisionEngine_NullPlan_In_Panic_Regime() { }
    [Fact] public void DecisionEngine_NullPlan_When_Score_Below_Threshold() { }
    [Fact] public void DecisionEngine_NullPlan_When_RR_Below_Min() { }
    [Fact] public void DecisionEngine_Plan_When_All_Hard_Gates_Pass() { }
}
```

### 12.3. Tests bắt buộc cho đợt 0D

File: `StockRadar.Tests/ReversalBounce/ReversalBounceBacktestRunnerTests.cs`

```csharp
[Fact] public void FloorLock_Defers_Exit_To_Next_Session() { }
[Fact] public void FloorLock_Two_Consecutive_Forces_Exit_At_Session_2() { }
[Fact] public void GapCancelled_When_Open_Gap_Above_Threshold() { }
[Fact] public void T3_Gate_Blocks_Exit_Before_T3() { }
[Fact] public void Defensive_Exit_Allowed_When_AllowDefensiveEarlyExit_True() { }
[Fact] public void Win_Bucket_Requires_Net_Return_At_Least_One_Percent() { }
```

### 12.4. Helper `OhlcvFixtures.cs`

```csharp
internal static class OhlcvFixtures
{
    public static IReadOnlyList<OhlcvBar> SteadyUptrend(int n, decimal start = 20_000m) { ... }
    public static IReadOnlyList<OhlcvBar> CapitulationThenStabilization(int n) { ... }
    public static IReadOnlyList<OhlcvBar> CapitulationThenStabilizationThenConfirmed(int n) { ... }
    public static IReadOnlyList<OhlcvBar> CapitulationThenSpringButNotCounterTrend(int n) { ... }
    public static IReadOnlyList<OhlcvBar> SelloffThenFloorCloseThenStabilize(int n) { ... }
    public static IReadOnlyList<OhlcvBar> CorporateActionLikeGap(int n) { ... }
    public static IReadOnlyList<OhlcvBar> LongHistoryForAtrLoopTest(int n = 5000) { ... }
}
```

---

## 13. API & DTO (mở rộng controller đã có)

### 13.1. Mở rộng `ReversalBounceController.cs` (đã có ở 0B)

**KHÔNG** tạo controller mới. Mở rộng controller hiện tại với các endpoint:

```csharp
[HttpGet("candidates")]
public async Task<ActionResult<ReversalBounceListDto>> GetCandidates(
    [FromQuery] DateOnly? date,
    [FromQuery] string? stage,
    [FromQuery] bool? actionableOnly,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
    => Ok(await query.GetCandidatesAsync(date, stage, actionableOnly, page, pageSize, ct));

[HttpGet("candidates/{symbol}")]
public async Task<ActionResult<ReversalBounceDetailDto>> GetBySymbol(
    string symbol, [FromQuery] int lookback = 30, CancellationToken ct = default)
    => Ok(await query.GetBySymbolAsync(symbol, lookback, ct));

[HttpPost("backtest/run")]
public async Task<ActionResult<ReversalBounceBacktestReport>> RunBacktest(
    ReversalBounceBacktestRequest req, CancellationToken ct = default)
    => Ok(await backtest.RunAsync(req, ct));
```

> `GET market-regime` đã có từ 0B — không thêm.

### 13.2. Mở rộng `ReversalBounceQueryService.cs`

Thêm method (KHÔNG override method đã có):

```csharp
public sealed record ReversalBounceListDto(
    IReadOnlyList<ReversalBounceItemDto> Items,
    int Page, int PageSize, int Total,
    DateOnly TradingDate,
    string? MarketRegime,
    string? StatusMessage);

public sealed record ReversalBounceItemDto(
    string Symbol, string Name, string Sector,
    string Stage, bool IsActionable, decimal TotalScore,
    ReversalBounceComponentScoreDto ComponentScores,
    decimal? EntryReference, decimal? InvalidationPrice,
    decimal? FirstTarget, decimal? RMultiple,
    decimal? PositionFactor,
    IReadOnlyList<string> RiskWarnings, string Reason);

public sealed record ReversalBounceComponentScoreDto(
    decimal Capitulation, decimal Stabilization, decimal Demand,
    decimal RelativeStrength, decimal Liquidity, decimal RiskPenalty);

public sealed record ReversalBounceDetailDto(
    ReversalBounceItemDto Current,
    IReadOnlyList<ReversalBounceHistoryItemDto> History);

public sealed record ReversalBounceHistoryItemDto(
    DateOnly TradingDate, string Stage, decimal TotalScore,
    IReadOnlyList<ReversalBounceReasonDto> Reasons);

public sealed record ReversalBounceReasonDto(
    string Code, string Label, decimal NumericValue, decimal? Threshold, bool Pass);
```

---

## 14. appsettings.json bổ sung

```json
{
  "ReversalBounce": {
    "Enabled": true,
    "StrategyVersion": "reversal-bounce@1.0.0",
    "SchemaVersion": 1,
    "MinDrawdownPercent": 18,
    "MinDrawdownInAtr": 2.5,
    "OversoldRsiThreshold": 25,
    "SellingClimaxVolMultiple": 2.5,
    "WideDownBarsMinCount": 3,
    "WideDownBarsWindow": 10,
    "WideDownBarsRangeToAtr": 1.2,
    "StabilizationNoNewLowToleranceAtr": 1,
    "RangeContractionRatio": 0.7,
    "StabilizationMinSessions": 2,
    "LowerWickRatioThreshold": 0.55,
    "LowerWickMinCount": 2,
    "ConfirmationLookbackHigh": 2,
    "StrongCloseClvThreshold": 0.65,
    "DemandExpansionVolMultiple": 1.4,
    "GapCancelAtrMultiple": 0.5,
    "GapAcceptanceAtrMultiple": 0.15,
    "ConfirmationEmaShort": 5,
    "ConfirmationEmaLong": 10,
    "InvalidConfirmationBufferAtr": 1,
    "LookbackSessions": 80,
    "MaShortWindow": 20,
    "MaLongWindow": 50,
    "AtrWindow": 14,
    "RsiWindow": 14,
    "MinHistoryDays": 60,
    "MinAvgDailyVolume": 100000,
    "RegimeThresholds": {
      "PanicMinScore": 0,
      "StabilizingMinScore": 80,
      "StabilizingMinDemand": 18,
      "ReboundConfirmedMinScore": 72,
      "ReboundConfirmedMinDemand": 12,
      "NormalMinScore": 75,
      "NormalMinDemand": 14,
      "MinLiquidityScore": 5,
      "MaxRiskPenalty": -5,
      "StabilizingPositionFactor": 0.25,
      "ReboundConfirmedPositionFactor": 0.50,
      "NormalPositionFactor": 0.40
    },
    "Trade": {
      "TimeStopSessions": 10,
      "MaxHoldSessions": 20,
      "MinRewardToRisk": 1.5,
      "SlippageBaseBps": 10,
      "SlippageGapImpactCoeff": 0.5,
      "SlippageFloorLockPenaltyBps": 30,
      "FeeBuyPercent": 0.15,
      "FeeSellPercent": 0.15,
      "TaxSellPercent": 0.10,
      "MinTradingSessionsToSell": 3,
      "MaxSignalsPerDay": 5
    }
  },
  "ReversalBounceBacktest": {
    "MaxSetupsToSimulate": 10000,
    "MaxSignalsPerDay": 5,
    "DefaultMinTradingSessionsToSell": 3,
    "DefaultTimeStopSessions": 10,
    "DefaultMaxHoldSessions": 20
  }
}
```

> Field `Regime` (singular) từ 0B vẫn giữ — dùng cho breadth snapshot; field `RegimeThresholds` (plural) mới thêm cho decision engine.

---

## 15. Phase plan code theo tợt

### Đợt 0C — Analyzer + Decision + Runner + Snapshot

**File cần tạo/sửa:**
1. `Domain/Services/ReversalBounce/ReversalBounceStage.cs`
2. `Domain/Services/ReversalBounce/ReversalBounceAnalyzer.cs` (logic §6)
3. `Domain/Services/ReversalBounce/ReversalBounceTradePlan.cs` (const defaults)
4. `Domain/MarketData/ExchangePriceBand.cs`
5. `Application/Abstractions/IReversalBounceTypes.cs`
6. `Application/Abstractions/IReversalBounceAnalyzer.cs`
7. `Application/Abstractions/ICounterTrendDecisionEngine.cs`
8. `Application/Abstractions/IReversalCandidateSnapshotRepository.cs`
9. `Application/Abstractions/IReversalBounceAnalysisService.cs`
10. **Mở rộng** `Application/Options/ReversalBounceOptions.cs` (thêm field §3.1)
11. `Application/Services/ReversalBounceQueryService.cs` — **mở rộng** method
12. `Application/DTOs/ReversalBounceDtos.cs` — **mở rộng**
13. `Infrastructure/Persistence/Entities/ReversalCandidateSnapshotEntity.cs`
14. `Infrastructure/Persistence/Repositories/EfReversalCandidateSnapshotRepository.cs`
15. `Infrastructure/MarketData/CounterTrendDecisionEngine.cs`
16. `Infrastructure/MarketData/ReversalBounceAnalysisRunner.cs`
17. `Infrastructure/Persistence/ApplicationDbContext.cs` — thêm 1 DbSet + 1 entity block
18. `Infrastructure/DependencyInjection.cs` — thêm 6 dòng theo §8.4
19. `Api/Controllers/ReversalBounceController.cs` — **mở rộng** 2 endpoint (§13.1)
20. `Infrastructure/MarketData/DailyAnalysisRunner.cs` — thêm 4 dòng cuối hàm (§8.3)
21. Migration: `dotnet ef migrations add AddReversalBounceSnapshots` (chỉnh tay 2 dòng nếu cần)
22. **Tạo mới** test project (xem §12)
23. Test files (xem §12.2) — **17 test bắt buộc**

**Pass:**
- `dotnet build` xanh, không warning mới.
- 17 test xanh.
- `dotnet ef database update` thành công.
- Chạy `POST /api/v1/market/jobs/daily` → log "ReversalBounce: scanned=…, snapshots=…, actionable=…".
- `GET /api/v1/reversal-bounce/candidates?actionableOnly=true` trả JSON đúng schema.

### Đợt 0D — BacktestRunner

24. `Application/Options/ReversalBounceBacktestOptions.cs`
25. `Application/Abstractions/IReversalBounceBacktestService.cs`
26. `Infrastructure/MarketData/Backtest/ReversalBounceBacktestRunner.cs`
27. **Sửa** `Api/Controllers/ReversalBounceController.cs` — thêm 1 endpoint `POST /api/v1/reversal-bounce/backtest/run`
28. Test file backtest (xem §12.3) — **6 test bắt buộc**

**Pass:**
- `dotnet build` xanh.
- 6 test xanh (đặc biệt FloorLock defer, GapCancel, T+3 gate).
- `POST /api/v1/reversal-bounce/backtest/run` với `From=2026-01-01`, `To=2026-07-01` trả report có `Trades.Count > 0`; ít nhất 1 trade có `FloorLockDeferredCount > 0` (nếu test data có).

### Đợt 1 — Shadow mode UI (Phase 2)

Không code cho tới khi 0C + 0D pass và chạy ≥ 30 ngày.

---

## 16. Definition of Done cho mỗi đợt

Mỗi đợt chỉ tính "xong" khi **đủ 6 tiêu chí**:

1. ✅ `dotnet build` xanh, không warning mới.
2. ✅ Unit test mới pass (≥ 1 test/file mới).
3. ✅ `dotnet ef database update` thành công (nếu có migration mới).
4. ✅ Không sửa file thuộc **danh sách cấm** §0.2.
5. ✅ Không tăng đáng kể thời gian `DailyAnalysisRunner` (< 30 giây thêm).
6. ✅ Log đầy đủ: mỗi setup có log line `Symbol/Stage/Score/Regime/IsActionable` ở mức Information.

---

## 17. File index tổng hợp

Khi đợt 0D xong, cấu trúc file mới là:

```
backend/StockRadar.Domain/
├── MarketData/
│   └── ExchangePriceBand.cs                                    [mới]
└── Services/ReversalBounce/
    ├── MarketRegime.cs                                          [0B — KHÔNG đụng]
    ├── MarketBreadth.cs                                         [0B — KHÔNG đụng]
    ├── MarketBreadthAnalyzer.cs                                 [0B — KHÔNG đụng]
    ├── MarketRegimeClassifier.cs                                [0B — KHÔNG đụng]
    ├── ReversalBounceStage.cs                                   [mới]
    ├── ReversalBounceAnalyzer.cs                                [mới]
    └── ReversalBounceTradePlan.cs                               [mới]

backend/StockRadar.Application/
├── Abstractions/
│   ├── IMarketBreadthSnapshotRepository.cs                      [0B — KHÔNG đụng]
│   ├── IReversalBounceTypes.cs                                  [mới]
│   ├── IReversalBounceAnalyzer.cs                               [mới]
│   ├── ICounterTrendDecisionEngine.cs                           [mới]
│   ├── IReversalCandidateSnapshotRepository.cs                  [mới]
│   ├── IReversalBounceAnalysisService.cs                        [mới]
│   └── IReversalBounceBacktestService.cs                        [mới]
├── Options/
│   ├── ReversalBounceOptions.cs                                 [MỞ RỘNG]
│   └── ReversalBounceBacktestOptions.cs                         [mới]
├── DTOs/
│   └── ReversalBounceDtos.cs                                    [MỞ RỘNG]
└── Services/
    └── ReversalBounceQueryService.cs                            [MỞ RỘNG]

backend/StockRadar.Infrastructure/
├── MarketData/
│   ├── MarketBreadthRunner.cs                                   [0B — KHÔNG đụng]
│   ├── ReversalBounceAnalysisRunner.cs                          [mới]
│   ├── CounterTrendDecisionEngine.cs                            [mới]
│   └── Backtest/
│       └── ReversalBounceBacktestRunner.cs                      [mới]
├── Persistence/
│   ├── ApplicationDbContext.cs                                  [MỞ RỘNG — 1 DbSet + 1 block]
│   ├── Entities/
│   │   └── ReversalCandidateSnapshotEntity.cs                   [mới]
│   └── Repositories/
│       └── EfReversalCandidateSnapshotRepository.cs             [mới]
├── Migrations/
│   └── <timestamp>_AddReversalBounceSnapshots.cs                [mới — generated by dotnet ef]
├── DependencyInjection.cs                                       [MỞ RỘNG — ~10 dòng]
└── MarketData/DailyAnalysisRunner.cs                            [MỞ RỘNG — ~4 dòng cuối hàm]

backend/StockRadar.Api/
└── Controllers/
    └── ReversalBounceController.cs                              [MỞ RỘNG — +2 endpoint]

backend/StockRadar.Tests/                                        [MỚI — khởi tạo project]
├── StockRadar.Tests.csproj
└── ReversalBounce/
    ├── OhlcvFixtures.cs
    ├── ReversalBounceAnalyzerTests.cs                           [17 test]
    ├── CounterTrendDecisionEngineTests.cs                       [4 test]
    ├── MarketRegimeClassifierTests.cs                           [3 test]
    └── ReversalBounceBacktestRunnerTests.cs                     [6 test]
```

**Tổng:** ~14 file mới + ~6 file mở rộng. Tất cả đều phụ thuộc nhẹ vào 0B, không phá vỡ component nào đã chạy.

---

## Deviations / As-built (0C–0D)

> Spec này là tài liệu **thiết kế**; **code trên disk là nguồn sự thật** (xem `CLAUDE.md`). Các điểm dưới đây là chỗ implement thực tế khác thiết kế — ghi nhận để tra cứu, phần thiết kế còn lại giữ nguyên làm lịch sử. `CLAUDE.md` đã đồng bộ theo as-built.

1. **Fill logic tách khỏi runner** — thay vì nằm trong `ReversalBounceBacktestRunner`, phần mô phỏng fill/exit là một simulator thuần ở Domain: `Domain/Services/ReversalBounce/ReversalBounceFillSimulator.cs` (+ `ReversalBounceExitReasons`, `ReversalBounceFillResult`). Runner chỉ dựng regime timeline, thu tín hiệu, gọi simulator, tổng hợp. Test file tên **`ReversalBounceFillSimulatorTests.cs`** (không phải `ReversalBounceBacktestRunnerTests.cs`).
2. **`ReversalBounceBacktestReport`/`TradeRecord` là superset của §11.1** — Report thêm `From`, `To`; `TradeRecord` thêm `TotalScore` (để report tự mô tả + debug). Toàn bộ field trong §11.1 giữ nguyên.
3. **Regime lịch sử trong backtest tính on-the-fly** — runner dựng lại regime từng phiên bằng `MarketBreadthAnalyzer` + `MarketRegimeClassifier` (warmup ~40 phiên trước `From`), vì `MarketBreadthSnapshots` (0B) chưa đủ dữ liệu quá khứ. §11 không quy định nguồn regime.
4. **RS percentile = 50 (trung tính) trong backtest** — chưa tính `RsPercentileNow` trên universe theo từng phiên (MVP). Chỉ ảnh hưởng ≤4/15 điểm RS, không phải hard gate. (RS `VsSector` + NearSupplyCluster cũng để 0 như MVP tổng.)
5. **DoD 0D** — đã sửa in-place: điều kiện chuyển từ `ExitReason=FloorLockDefer` → `FloorLockDeferredCount > 0`. Implement không có exit reason `FloorLockDefer`: defer giữ nguyên reason gốc (`Stop`/`Target`…) và đếm qua `FloorLockDeferredCount`; chỉ có reason **`FloorLockForced`** khi 2 phiên sàn liên tiếp.
6. **Interface gộp 1 file** — `Application/Abstractions/IReversalBounceBacktest.cs` chứa `ReversalBounceBacktestRequest` + `ReversalBounceBacktestReport` + `ReversalBounceBacktestTradeRecord` + `IReversalBounceBacktestService` (không tách `IReversalBounceBacktestService.cs` riêng).
7. **Test project TFM = `net10.0`** (theo `StockRadar.slnx`), không phải `net9.0` như lệnh mẫu §12.1.
8. **`ReversalBounceTradePlan.StopToTargetRMultiple` → `RewardToRisk`** — đã sửa in-place §2.1 + §7. Tên field as-built là `RewardToRisk` (`|Target-Entry| / |Entry-Invalidation|`).
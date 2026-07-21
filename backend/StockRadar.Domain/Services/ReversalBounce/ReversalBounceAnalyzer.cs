using StockRadar.Domain.Entities;
using StockRadar.Domain.MarketData;

namespace StockRadar.Domain.Services.ReversalBounce;

public interface IReversalBounceAnalyzer
{
    /// <summary>
    /// Phân tích 1 mã tại 1 phiên (stateless, thuần). <paramref name="rsPercentile"/> do caller tính
    /// trên toàn universe. <c>RecoveryAttemptCount</c> mặc định 1 — caller (runner) chỉnh lại qua DB.
    /// </summary>
    ReversalBounceAnalysis Analyze(
        Stock stock,
        IReadOnlyList<OhlcvBar> indexHistory,
        MarketRegime regime,
        decimal rsPercentile,
        DateOnly asOfDate,
        ReversalBounceSettings settings);
}

/// <summary>
/// Analyzer counter-trend "sóng hồi": suy Stage (Capitulating→Stabilizing→Confirmed / None / Invalidated)
/// và 6 trục điểm (spec §5, §6). Không đọc DB, không dùng High/Low phiên T+1.
/// </summary>
public sealed class ReversalBounceAnalyzer : IReversalBounceAnalyzer
{
    public ReversalBounceAnalysis Analyze(
        Stock stock,
        IReadOnlyList<OhlcvBar> indexHistory,
        MarketRegime regime,
        decimal rsPercentile,
        DateOnly asOfDate,
        ReversalBounceSettings settings)
    {
        var past = stock.History
            .Where(b => b.Date <= asOfDate)
            .OrderBy(b => b.Date)
            .ToList();

        var features = ComputeFeatures(stock, past, indexHistory, asOfDate, settings);

        if (past.Count < settings.MaLongWindow || features.CapitulationDate is null)
            return BuildNone(stock, asOfDate, settings, features);

        var (hasCapit, capitReasons) = HasCapitulation(features, settings);
        if (!hasCapit)
            return BuildNone(stock, asOfDate, settings, features, capitReasons);

        var (hasStab, stabReasons) = HasStabilized(features, settings);
        var (confirmed, confirmReasons) = IsConfirmed(features, settings);
        var invalid = IsInvalidated(features, regime, settings);
        var stage = DeriveStage(hasCapit, invalid, confirmed, hasStab);

        var scores = ComputeScores(features, rsPercentile, settings);
        var total = ComputeTotalScore(scores);

        var reasons = new List<ReversalBounceReason>();
        reasons.AddRange(capitReasons);
        reasons.AddRange(stabReasons);
        reasons.AddRange(confirmReasons);

        var setupId = ReversalBounceSetupId.Compute(stock.Symbol, features.CapitulationDate, settings.StrategyVersion);

        var setup = new ReversalBounceSetup(
            Symbol: stock.Symbol,
            TradingDate: asOfDate,
            Stage: stage,
            SetupId: setupId,
            CapitulationDate: features.CapitulationDate,
            CapitulationLow: features.CapitulationLow,
            CapitulationClose: features.CapitulationClose,
            RecoveryAttemptCount: 1,
            ComponentScores: scores,
            TotalScore: total,
            MarketRegime: regime,
            StrategyVersion: settings.StrategyVersion,
            AlgorithmParametersHash: "",
            SchemaVersion: settings.SchemaVersion,
            Reasons: reasons);

        return new ReversalBounceAnalysis(setup, features);
    }

    private static ReversalBounceAnalysis BuildNone(
        Stock stock,
        DateOnly asOfDate,
        ReversalBounceSettings settings,
        ReversalBounceFeatures features,
        IReadOnlyList<ReversalBounceReason>? reasons = null)
    {
        var setup = new ReversalBounceSetup(
            Symbol: stock.Symbol,
            TradingDate: asOfDate,
            Stage: ReversalBounceStage.None,
            SetupId: ReversalBounceSetupId.Compute(stock.Symbol, features.CapitulationDate, settings.StrategyVersion),
            CapitulationDate: features.CapitulationDate,
            CapitulationLow: features.CapitulationLow,
            CapitulationClose: features.CapitulationClose,
            RecoveryAttemptCount: 0,
            ComponentScores: ReversalBounceComponentScores.Empty,
            TotalScore: 0m,
            MarketRegime: MarketRegime.Normal,
            StrategyVersion: settings.StrategyVersion,
            AlgorithmParametersHash: "",
            SchemaVersion: settings.SchemaVersion,
            Reasons: reasons ?? []);
        return new ReversalBounceAnalysis(setup, features);
    }

    // ─────────────────────────── Features (O(n)) ───────────────────────────

    private static ReversalBounceFeatures ComputeFeatures(
        Stock stock,
        IReadOnlyList<OhlcvBar> past,
        IReadOnlyList<OhlcvBar> indexHistory,
        DateOnly asOfDate,
        ReversalBounceSettings settings)
    {
        if (past.Count == 0)
        {
            return new ReversalBounceFeatures(
                stock.Symbol, stock.Exchange, asOfDate, past, indexHistory,
                0m, 0m, 0m, 0m, 0m, null, null, null, null, -1, 0m, 0m);
        }

        var ma20 = AverageClose(past, settings.MaShortWindow);
        var ma20Prev = past.Count > settings.MaShortWindow
            ? AverageClose(past.SkipLast(settings.MaShortWindow).ToList(), settings.MaShortWindow)
            : ma20;
        var ma50 = AverageClose(past, settings.MaLongWindow);
        var atr = ComputeAtr(past, settings.AtrWindow);
        var rsi = ComputeRsi(past, settings.RsiWindow);

        if (past.Count < settings.MaLongWindow)
        {
            return new ReversalBounceFeatures(
                stock.Symbol, stock.Exchange, asOfDate, past, indexHistory,
                ma20, ma20Prev, ma50, atr, rsi, null, null, null, null, -1, 0m, 0m);
        }

        // Peak (High cao nhất) trong LookbackSessions gần nhất → đáy SAU peak.
        var lookback = Math.Min(settings.LookbackSessions, past.Count);
        var windowStart = past.Count - lookback;
        var peakIdx = windowStart;
        for (var i = windowStart; i < past.Count; i++)
            if (past[i].High >= past[peakIdx].High)   // >= → ưu tiên peak muộn hơn khi bằng
                peakIdx = i;

        var peakHigh = past[peakIdx].High;

        if (peakIdx >= past.Count - 1)
        {
            // Peak là phiên cuối → chưa có drawdown.
            return new ReversalBounceFeatures(
                stock.Symbol, stock.Exchange, asOfDate, past, indexHistory,
                ma20, ma20Prev, ma50, atr, rsi, peakHigh, null, null, null, -1, 0m, 0m);
        }

        var capitIdx = peakIdx + 1;
        for (var i = peakIdx + 1; i < past.Count; i++)
            if (past[i].Low < past[capitIdx].Low)
                capitIdx = i;

        var capitLow = past[capitIdx].Low;
        var capitClose = past[capitIdx].Close;
        var capitDate = past[capitIdx].Date;
        var drawdownPct = peakHigh > 0 ? (capitLow - peakHigh) / peakHigh * 100m : 0m;
        var drawdownInAtr = atr > 0 ? (peakHigh - capitLow) / atr : 0m;

        return new ReversalBounceFeatures(
            Symbol: stock.Symbol,
            Exchange: stock.Exchange,
            AsOfDate: asOfDate,
            History: past,
            IndexHistory: indexHistory,
            Ma20: ma20,
            Ma20Prev: ma20Prev,
            Ma50: ma50,
            Atr: atr,
            Rsi: rsi,
            PeakHigh: peakHigh,
            CapitulationLow: capitLow,
            CapitulationClose: capitClose,
            CapitulationDate: capitDate,
            CapitulationIndex: capitIdx,
            DrawdownPercent: drawdownPct,
            DrawdownInAtr: drawdownInAtr);
    }

    // ─────────────────────────── Stage detectors ───────────────────────────

    private static (bool, List<ReversalBounceReason>) HasCapitulation(
        ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        var reasons = new List<ReversalBounceReason>();
        var ddOk = Math.Abs(f.DrawdownPercent) >= opt.MinDrawdownPercent;
        var ddaOk = f.DrawdownInAtr >= opt.MinDrawdownInAtr;
        reasons.Add(new("DRAWDOWN_FROM_PEAK", "Drawdown từ đỉnh (%)", Math.Round(f.DrawdownPercent, 2), -opt.MinDrawdownPercent, ddOk));
        reasons.Add(new("DRAWDOWN_IN_ATR", "Drawdown theo ATR", Math.Round(f.DrawdownInAtr, 2), opt.MinDrawdownInAtr, ddaOk));

        var oversold = f.Rsi <= opt.OversoldRsiThreshold;
        var climax = SellingClimaxRatio(f, opt);
        var wideDown = CountWideDownBars(f, opt);
        var wideDownOk = wideDown >= opt.WideDownBarsMinCount;

        reasons.Add(new("RSI_OVERSOLD", "RSI14", Math.Round(f.Rsi, 2), opt.OversoldRsiThreshold, oversold));
        reasons.Add(new("SELLING_CLIMAX", "Volume climax (x TB)", Math.Round(climax, 2), opt.SellingClimaxVolMultiple, climax > opt.SellingClimaxVolMultiple));
        reasons.Add(new("WIDE_DOWN_BARS", "Số phiên giảm biên rộng", wideDown, opt.WideDownBarsMinCount, wideDownOk));

        var capit = ddOk && ddaOk && (oversold || climax > opt.SellingClimaxVolMultiple || wideDownOk);
        return (capit, reasons);
    }

    private static (bool, List<ReversalBounceReason>) HasStabilized(
        ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        var reasons = new List<ReversalBounceReason>();
        var afterCapit = f.CapitulationIndex >= 0
            ? f.History.Skip(f.CapitulationIndex + 1).ToList()
            : [];
        if (afterCapit.Count < opt.StabilizationMinSessions || f.CapitulationLow is null)
            return (false, reasons);

        var atrCapit = ComputeAtr(f.History.Take(f.CapitulationIndex + 1).ToList(), opt.AtrWindow);
        var rangeContracted = atrCapit > 0 && f.Atr <= atrCapit * opt.RangeContractionRatio;

        var tol = f.Atr * opt.StabilizationNoNewLowToleranceAtr;
        var noNewLow = afterCapit.Min(b => b.Low) >= f.CapitulationLow.Value - tol;
        var noNewLowSessions = CountConsecutiveNoNewLow(afterCapit, f.CapitulationLow.Value, tol);
        var downVolDryUp = DownVolumeDryUp(f.History);
        var lowerWicks = CountLowerWicks(afterCapit, opt.LowerWickRatioThreshold);
        var rsSlope = RsSlope5(f);
        var rsImproving = rsSlope > 0m;

        var ok = noNewLow && rangeContracted
            && (downVolDryUp || lowerWicks >= opt.LowerWickMinCount || rsImproving);

        reasons.Add(new("RANGE_CONTRACTION", "Biên độ co lại", Math.Round(f.Atr, 0), Math.Round(atrCapit * opt.RangeContractionRatio, 0), rangeContracted));
        reasons.Add(new("NO_NEW_LOW", "Không thủng đáy mới", noNewLowSessions, opt.StabilizationMinSessions, noNewLow));
        reasons.Add(new("LOWER_WICKS", "Số nến rút chân", lowerWicks, opt.LowerWickMinCount, lowerWicks >= opt.LowerWickMinCount));

        return (ok, reasons);
    }

    private static (bool, List<ReversalBounceReason>) IsConfirmed(
        ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        var reasons = new List<ReversalBounceReason>();
        if (f.History.Count < opt.ConfirmationLookbackHigh + 2 || f.CapitulationIndex < 0)
            return (false, reasons);

        var today = f.History[^1];
        var prior = f.History
            .SkipLast(1)
            .TakeLast(opt.ConfirmationLookbackHigh)
            .ToList();

        var priceBreak = prior.Count > 0 && today.Close > prior.Max(b => b.High);
        var emaShort = ComputeEma(f.History, opt.ConfirmationEmaShort);
        var emaLong = ComputeEma(f.History, opt.ConfirmationEmaLong);
        var emaBreak = today.Close > emaShort || today.Close > emaLong;

        var clv = today.High > today.Low
            ? (today.Close - today.Low) / (today.High - today.Low)
            : 0m;
        var strongClose = clv >= opt.StrongCloseClvThreshold;

        var volAvgStab = AverageVolume(AfterCapitSlice(f, 10));
        var demandExpansion = volAvgStab > 0 && today.Volume >= volAvgStab * opt.DemandExpansionVolMultiple;

        var prevClose = f.History[^2].Close;
        var gap = prevClose > 0 ? today.Open / prevClose - 1m : 0m;
        var notOverextended = gap <= opt.GapCancelAtrMultiple * f.AtrPercent;

        reasons.Add(new("PRICE_BREAK", "Vượt đỉnh gần / EMA", today.Close, prior.Count > 0 ? prior.Max(b => b.High) : 0m, priceBreak || emaBreak));
        reasons.Add(new("STRONG_CLOSE", "Đóng cửa mạnh (CLV)", Math.Round(clv, 2), opt.StrongCloseClvThreshold, strongClose));
        reasons.Add(new("DEMAND_EXPANSION", "Cầu mua bùng nổ (x TB)", volAvgStab > 0 ? Math.Round(today.Volume / volAvgStab, 2) : 0m, opt.DemandExpansionVolMultiple, demandExpansion));
        reasons.Add(new("NOT_OVEREXTENDED", "Không gap quá đà", Math.Round(gap * 100m, 2), Math.Round(opt.GapCancelAtrMultiple * f.AtrPercent * 100m, 2), notOverextended));

        var confirmed = (priceBreak || emaBreak) && strongClose && demandExpansion && notOverextended;
        return (confirmed, reasons);
    }

    private static bool IsInvalidated(
        ReversalBounceFeatures f, MarketRegime regime, ReversalBounceSettings opt)
    {
        if (regime == MarketRegime.Panic)
            return true;
        if (f.CapitulationLow is null)
            return false;

        var tol = f.Atr * opt.StabilizationNoNewLowToleranceAtr;
        var today = f.History[^1];
        var brokeCapitLow = today.Close < f.CapitulationLow.Value - tol;
        var brokeConfirmation = today.Close < today.Open - opt.InvalidConfirmationBufferAtr * f.Atr;
        return brokeCapitLow || brokeConfirmation;
    }

    public static ReversalBounceStage DeriveStage(
        bool hasCapitulation, bool isInvalidated, bool isConfirmed, bool hasStabilized) =>
        !hasCapitulation ? ReversalBounceStage.None
        : isInvalidated ? ReversalBounceStage.Invalidated
        : isConfirmed ? ReversalBounceStage.Confirmed
        : hasStabilized ? ReversalBounceStage.Stabilizing
        : ReversalBounceStage.Capitulating;

    // ─────────────────────────── Scoring (spec §5) ───────────────────────────

    private static ReversalBounceComponentScores ComputeScores(
        ReversalBounceFeatures f, decimal rsPercentile, ReversalBounceSettings opt)
    {
        var capit = CapitulationScore(f);
        var stab = StabilizationScore(f, opt);
        var demand = DemandScore(f, opt);
        var rs = RelativeStrengthScore(f, rsPercentile);
        var liq = LiquidityScore(f, opt);
        var risk = RiskPenaltyScore(f);
        return new ReversalBounceComponentScores(capit, stab, demand, rs, liq, risk);
    }

    private static decimal CapitulationScore(ReversalBounceFeatures f)
    {
        var drawdown = Clamp01(Math.Abs(f.DrawdownPercent) / 25m) * 8m;
        var oversold = Clamp01((25m - f.Rsi) / 15m) * 4m;
        var intensity = Clamp01(f.DrawdownInAtr / 4m) * 3m;
        return Math.Min(15m, drawdown + oversold + intensity);
    }

    private static decimal StabilizationScore(ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        if (f.CapitulationIndex < 0)
            return 0m;
        var afterCapit = AfterCapitSlice(f, 40);
        if (afterCapit.Count == 0)
            return 0m;

        var atrCapit = ComputeAtr(f.History.Take(f.CapitulationIndex + 1).ToList(), opt.AtrWindow);
        var rangeContraction = atrCapit > 0 ? Clamp01(1m - f.Atr / atrCapit) * 8m : 0m;

        var tol = f.Atr * opt.StabilizationNoNewLowToleranceAtr;
        var noNewLowSessions = CountConsecutiveNoNewLow(afterCapit, f.CapitulationLow ?? 0m, tol);
        var noNewLow = Clamp01(noNewLowSessions / 4m) * 6m;

        var downVolDry = DownVolumeDryUpScore(f.History) * 3m;

        var lowerWicks = CountLowerWicks(afterCapit, opt.LowerWickRatioThreshold);
        var rsSlope = RsSlope5(f);
        var wicksOrRs = (lowerWicks >= opt.LowerWickMinCount || rsSlope > 0m) ? 3m : 0m;

        return Math.Min(20m, rangeContraction + noNewLow + downVolDry + wicksOrRs);
    }

    private static decimal DemandScore(ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        if (f.History.Count < 3)
            return 0m;
        var today = f.History[^1];
        var prior = f.History.SkipLast(1).TakeLast(opt.ConfirmationLookbackHigh).ToList();
        var emaShort = ComputeEma(f.History, opt.ConfirmationEmaShort);
        var emaLong = ComputeEma(f.History, opt.ConfirmationEmaLong);
        var priceBreak = (prior.Count > 0 && today.Close > prior.Max(b => b.High))
            || today.Close > emaShort || today.Close > emaLong;
        var priceBreakScore = priceBreak ? 5m : 0m;

        var clv = today.High > today.Low ? (today.Close - today.Low) / (today.High - today.Low) : 0m;
        var strongClose = Clamp01(clv / 0.85m) * 4m;

        var volAvgStab = AverageVolume(AfterCapitSlice(f, 10));
        var volumeExpansion = volAvgStab > 0 ? Clamp01(today.Volume / volAvgStab - 1m) * 4m : 0m;

        var prevClose = f.History[^2].Close;
        var gap = prevClose > 0 ? today.Open / prevClose - 1m : 0m;
        var notOverextended = gap <= opt.GapAcceptanceAtrMultiple * f.AtrPercent ? 2m
            : gap > opt.GapCancelAtrMultiple * f.AtrPercent ? 0m
            : 1m;

        return Math.Min(15m, priceBreakScore + strongClose + volumeExpansion + notOverextended);
    }

    private static decimal RelativeStrengthScore(ReversalBounceFeatures f, decimal rsPercentile)
    {
        var rsSlope = RsSlope5(f);
        var rsImproving = Clamp01(rsSlope * 10m) * 8m;
        var rsPct = Clamp01((rsPercentile - 50m) / 30m) * 4m;
        // VsSector: chưa có sector median tại Domain → tạm 0 (MVP; sẽ bổ sung khi runner cấp sector context).
        return Math.Min(15m, rsImproving + rsPct);
    }

    private static decimal LiquidityScore(ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        var avgVol = AverageVolume(f.History.TakeLast(20).ToList());
        var volScore = 0m;
        if (avgVol > 0 && opt.MinAvgDailyVolume > 0)
        {
            var ratio = (double)(avgVol / opt.MinAvgDailyVolume);
            volScore = ratio > 0 ? (decimal)Clamp01Double(Math.Log10(ratio) / 2d) * 6m : 0m;
        }

        var turnover = MedianTurnover(f.History.TakeLast(20).ToList());
        var turnoverScore = turnover >= 1_000_000_000m ? 4m : turnover >= 500_000_000m ? 2m : 0m;

        return Math.Min(10m, volScore + turnoverScore);
    }

    private static decimal RiskPenaltyScore(ReversalBounceFeatures f)
    {
        var floorLock = FloorLockRecent(f, 5) ? -3m : 0m;
        var consecutiveDown = -Clamp01(ConsecutiveDownBars(f.History) / 4m) * 3m;
        var gapVol = -Clamp01(f.AtrPercent * 100m / 5m) * 2m;
        // NearSupplyCluster: chưa có order-book → tạm 0 (MVP).
        return Math.Max(-10m, floorLock + consecutiveDown + gapVol);
    }

    public static decimal ComputeTotalScore(ReversalBounceComponentScores s) =>
        Math.Clamp(
            s.Capitulation + s.Stabilization + s.Demand + s.RelativeStrength + s.Liquidity + s.RiskPenalty,
            0m, 100m);

    // ─────────────────────────── Supply zone / target ───────────────────────────

    public static decimal NearestSupplyZone(IReadOnlyList<OhlcvBar> past, decimal entryRef, decimal atr)
    {
        var ema20 = ComputeEma(past, 20);
        var candidates = new List<decimal> { ema20 };

        for (var i = 1; i < past.Count; i++)
            if (past[i].Low > past[i - 1].High)
                candidates.Add(past[i - 1].High);

        if (past.Count > 0)
            candidates.Add(past.TakeLast(Math.Min(40, past.Count)).Max(b => b.High));
        candidates.Add(entryRef + 2m * atr);

        return candidates.Where(z => z > entryRef).DefaultIfEmpty(entryRef + 2m * atr).Min();
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static List<OhlcvBar> AfterCapitSlice(ReversalBounceFeatures f, int maxCount)
    {
        if (f.CapitulationIndex < 0)
            return [];
        var after = f.History.Skip(f.CapitulationIndex + 1).ToList();
        return after.Count <= maxCount ? after : after.TakeLast(maxCount).ToList();
    }

    private static decimal SellingClimaxRatio(ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        if (f.CapitulationIndex < 1)
            return 0m;
        var capitVol = f.History[f.CapitulationIndex].Volume;
        var before = f.History.Take(f.CapitulationIndex).TakeLast(20).ToList();
        var avg = AverageVolume(before);
        return avg > 0 ? capitVol / avg : 0m;
    }

    private static int CountWideDownBars(ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        if (f.Atr <= 0)
            return 0;
        var end = f.CapitulationIndex >= 0 ? f.CapitulationIndex : f.History.Count - 1;
        var start = Math.Max(0, end - opt.WideDownBarsWindow + 1);
        var count = 0;
        for (var i = start; i <= end && i < f.History.Count; i++)
        {
            var bar = f.History[i];
            var range = bar.High - bar.Low;
            if (bar.Close < bar.Open && range >= opt.WideDownBarsRangeToAtr * f.Atr)
                count++;
        }
        return count;
    }

    private static int CountConsecutiveNoNewLow(IReadOnlyList<OhlcvBar> afterCapit, decimal capitLow, decimal tol)
    {
        var count = 0;
        for (var i = afterCapit.Count - 1; i >= 0; i--)
        {
            if (afterCapit[i].Low >= capitLow - tol)
                count++;
            else
                break;
        }
        return count;
    }

    private static bool DownVolumeDryUp(IReadOnlyList<OhlcvBar> history) => DownVolumeDryUpScore(history) > 0.3m;

    private static decimal DownVolumeDryUpScore(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 6)
            return 0m;
        var last5 = history.TakeLast(5).ToList();
        var last20 = history.TakeLast(20).ToList();
        var downVol5 = AvgDownVolume(last5);
        var avgDownVol20 = AvgDownVolume(last20);
        if (avgDownVol20 <= 0)
            return 0m;
        return Clamp01(1m - downVol5 / avgDownVol20);
    }

    private static decimal AvgDownVolume(IReadOnlyList<OhlcvBar> bars)
    {
        var downs = new List<decimal>();
        for (var i = 1; i < bars.Count; i++)
            if (bars[i].Close < bars[i - 1].Close)
                downs.Add(bars[i].Volume);
        return downs.Count == 0 ? 0m : downs.Average();
    }

    private static int CountLowerWicks(IReadOnlyList<OhlcvBar> bars, decimal ratioThreshold)
    {
        var count = 0;
        foreach (var b in bars)
        {
            var range = b.High - b.Low;
            if (range <= 0)
                continue;
            var lowerWick = Math.Min(b.Open, b.Close) - b.Low;
            if (lowerWick / range >= ratioThreshold)
                count++;
        }
        return count;
    }

    /// <summary>RS slope 5 phiên: thay đổi tỉ số Close(stock)/Close(index) so 5 phiên trước.</summary>
    private static decimal RsSlope5(ReversalBounceFeatures f)
    {
        if (f.IndexHistory.Count == 0 || f.History.Count < 6)
            return 0m;

        var idx = new Dictionary<DateOnly, decimal>();
        foreach (var b in f.IndexHistory)
            idx[b.Date] = b.Close;

        var recent = f.History.TakeLast(6).ToList();
        var ratios = new List<decimal>();
        foreach (var bar in recent)
            if (idx.TryGetValue(bar.Date, out var ic) && ic > 0)
                ratios.Add(bar.Close / ic);

        if (ratios.Count < 2 || ratios[0] <= 0)
            return 0m;
        return (ratios[^1] - ratios[0]) / ratios[0];
    }

    private static bool FloorLockRecent(ReversalBounceFeatures f, int lookback)
    {
        var start = Math.Max(0, f.History.Count - lookback);
        for (var i = start; i < f.History.Count; i++)
        {
            var refPrice = i > 0 ? f.History[i - 1].Close : 0m;
            var (floor, _) = ExchangePriceBand.Calculate(refPrice, f.Exchange);
            if (ExchangePriceBand.IsLikelyFloorLocked(f.History[i], floor))
                return true;
        }
        return false;
    }

    private static int ConsecutiveDownBars(IReadOnlyList<OhlcvBar> history)
    {
        var count = 0;
        for (var i = history.Count - 1; i >= 1; i--)
        {
            if (history[i].Close < history[i - 1].Close)
                count++;
            else
                break;
        }
        return count;
    }

    private static decimal MedianTurnover(IReadOnlyList<OhlcvBar> bars)
    {
        if (bars.Count == 0)
            return 0m;
        var vals = bars.Select(b => b.Close * b.Volume).OrderBy(v => v).ToList();
        var mid = vals.Count / 2;
        return vals.Count % 2 == 1 ? vals[mid] : (vals[mid - 1] + vals[mid]) / 2m;
    }

    private static decimal AverageClose(IReadOnlyList<OhlcvBar> history, int window)
    {
        var count = Math.Min(window, history.Count);
        return count == 0 ? 0m : history.TakeLast(count).Average(b => b.Close);
    }

    private static decimal AverageVolume(IReadOnlyList<OhlcvBar> bars) =>
        bars.Count == 0 ? 0m : bars.Average(b => (decimal)b.Volume);

    /// <summary>ATR trung bình đơn giản trên <paramref name="window"/> phiên cuối (O(n)).</summary>
    private static decimal ComputeAtr(IReadOnlyList<OhlcvBar> history, int window)
    {
        if (history.Count < 2)
            return 0m;
        var start = Math.Max(1, history.Count - window);
        var sum = 0m;
        var n = 0;
        for (var i = start; i < history.Count; i++)
        {
            var high = history[i].High;
            var low = history[i].Low;
            var prevClose = history[i - 1].Close;
            var tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
            sum += tr;
            n++;
        }
        return n == 0 ? 0m : sum / n;
    }

    private static decimal ComputeRsi(IReadOnlyList<OhlcvBar> history, int window)
    {
        if (history.Count < window + 1)
            return 50m;
        var gains = 0m;
        var losses = 0m;
        var start = history.Count - window;
        for (var i = start; i < history.Count; i++)
        {
            var change = history[i].Close - history[i - 1].Close;
            if (change >= 0) gains += change;
            else losses -= change;
        }
        if (losses == 0m)
            return 100m;
        var rs = gains / losses;
        return 100m - 100m / (1m + rs);
    }

    private static decimal ComputeEma(IReadOnlyList<OhlcvBar> history, int period)
    {
        if (history.Count == 0)
            return 0m;
        if (history.Count < period)
            return history.Average(b => b.Close);
        var k = 2m / (period + 1);
        var ema = history.Take(period).Average(b => b.Close);
        for (var i = period; i < history.Count; i++)
            ema = history[i].Close * k + ema * (1m - k);
        return ema;
    }

    private static decimal Clamp01(decimal v) => v < 0m ? 0m : v > 1m ? 1m : v;

    private static double Clamp01Double(double v) => v < 0d ? 0d : v > 1d ? 1d : v;
}

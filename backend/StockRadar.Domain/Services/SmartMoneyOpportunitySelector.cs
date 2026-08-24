using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Bộ lọc SmartMoney: pha TT, ngành, RS 5 phiên, nền giá, MA stack, breakout/shakeout.
/// </summary>
public interface ISmartMoneyOpportunitySelector
{
    SmartMoneyMarketContext BuildContext(
        IReadOnlyList<Stock> universe,
        MarketIndex index,
        BasePriceFilterSettings runupFilter,
        SmartMoneySettings settings,
        AdaptiveScoringProfile? adaptive = null,
        HitCalibrationProfile? calibration = null);

    SmartMoneyEvaluation Evaluate(Stock stock, SmartMoneyMarketContext context);

    bool PassesFilter(SmartMoneyEvaluation eval, SmartMoneySettings settings);
}

public sealed record SmartMoneyMarketContext(
    MarketIndex Index,
    decimal IndexChangePercent5d,
    MarketWyckoffPhase MarketPhase,
    IReadOnlyDictionary<string, SectorSnapshot> SectorSnapshots,
    BasePriceFilterSettings RunupFilter,
    SmartMoneySettings Settings,
    AdaptiveScoringProfile Adaptive,
    HitCalibrationProfile Calibration,
    IReadOnlyDictionary<string, decimal> RsPercentile,
    MarketPhaseClassification? PhaseDetail = null,
    /// <summary>Ngành đang trong chu kỳ Sóng ngành Active (spec 007) — kế thừa từ phiên trước,
    /// khác <see cref="SectorSnapshot.HasWave"/> (chỉ đúng phiên hiện tại). Rỗng nếu quy trình gọi
    /// không tính regime (ví dụ shadow/backtest) — an toàn, gate rơi về hành vi cũ.</summary>
    IReadOnlySet<string>? ActiveSectorRegimes = null)
{
    /// <summary>Sóng ngành của một mã — ngành thiếu dữ liệu coi như không có sóng.</summary>
    public SectorSnapshot SectorWaveFor(string? sector) =>
        !string.IsNullOrWhiteSpace(sector)
        && SectorSnapshots.TryGetValue(sector.Trim(), out var snapshot)
            ? snapshot
            : SectorSnapshot.Unknown(string.IsNullOrWhiteSpace(sector) ? "N/A" : sector.Trim());

    /// <summary>Ngành đang trong chu kỳ Sóng ngành Active (spec 007), bất kể breadth đúng-phiên hiện tại.</summary>
    public bool IsSectorRegimeActive(string? sector) =>
        !string.IsNullOrWhiteSpace(sector)
        && ActiveSectorRegimes is not null
        && ActiveSectorRegimes.Contains(sector.Trim());
}

public sealed record SmartMoneyEvaluation(
    string Symbol,
    int Score,
    bool Passes,
    WyckoffPhase StockPhase,
    SectorSnapshot SectorWave,
    decimal RelativeStrength5d,
    decimal VolumeRatio,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<SignalType> Signals,
    decimal PredictedHitPercent = 0,
    int PredictedSampleCount = 0,
    string? SetupDna = null,
    IReadOnlyList<BuyScoreComponent> Breakdown = null!);

public sealed class SmartMoneyOpportunitySelector(
    ISignalAnalyzer signals,
    IBuyDecisionEngine buyDecision) : ISmartMoneyOpportunitySelector
{
    public SmartMoneyMarketContext BuildContext(
        IReadOnlyList<Stock> universe,
        MarketIndex index,
        BasePriceFilterSettings runupFilter,
        SmartMoneySettings settings,
        AdaptiveScoringProfile? adaptive = null,
        HitCalibrationProfile? calibration = null)
    {
        var index5d = index.IndexChange5d;
        var phaseResult = MarketPhaseClassifier.Classify(index.Bars, settings.PhaseThresholds);
        var marketPhase = phaseResult.Phase;
        var snapshots = BuildSectorSnapshots(universe, index5d, settings);
        var rsPercentile = BuildRsPercentile(universe, index5d, settings);

        return new SmartMoneyMarketContext(
            index,
            index5d,
            marketPhase,
            snapshots,
            runupFilter,
            settings,
            adaptive ?? AdaptiveScoringProfile.Default,
            calibration ?? HitCalibrationProfile.Default,
            rsPercentile,
            phaseResult);
    }

    /// <summary>RS percentile khung 5 phiên — công thức dùng chung ở <see cref="RsPercentileCalculator"/>.</summary>
    private IReadOnlyDictionary<string, decimal> BuildRsPercentile(
        IReadOnlyList<Stock> universe,
        decimal indexChange5d,
        SmartMoneySettings settings) =>
        RsPercentileCalculator.Build(
            universe,
            signals,
            indexChange5d,
            days: 5,
            settings.MinHistoryDays,
            settings.MinAvgDailyVolume);

    public SmartMoneyEvaluation Evaluate(Stock stock, SmartMoneyMarketContext context)
    {
        var decision = buyDecision.Evaluate(stock, context);
        if (!decision.PassesTopFilter)
        {
            var reason = decision.GateFailure ?? "Chưa đạt điều kiện Top cơ hội";
            return Fail(stock.Symbol, reason);
        }

        return new SmartMoneyEvaluation(
            stock.Symbol,
            decision.BuyScore,
            true,
            decision.StockPhase,
            decision.SectorWave,
            decision.RelativeStrength5d,
            decision.VolumeRatio,
            decision.Reasons,
            decision.Signals,
            decision.PredictedHitPercent,
            decision.PredictedSampleCount,
            decision.SetupDna,
            decision.Breakdown);
    }

    public bool PassesFilter(SmartMoneyEvaluation eval, SmartMoneySettings settings) =>
        eval.Passes && eval.Score >= settings.MinPassScore;

    private static SmartMoneyEvaluation Fail(string symbol, string reason) =>
        new(symbol, 0, false, WyckoffPhase.Unknown, SectorSnapshot.Unknown("N/A"), 0, 0, [reason], [], 0, 0, null, []);

    private static bool IsExcludedSector(string? sector)
    {
        if (string.IsNullOrWhiteSpace(sector))
            return true;

        var s = sector.Trim();
        return s.Equals("Khác", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Other", StringComparison.OrdinalIgnoreCase)
            || s.Equals("N/A", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Chấm "sóng ngành" cho từng ngành trong phiên: độ rộng tăng/giảm, lực, tiền vào, RS.
    /// Không còn xếp hạng ngành top N — chỉ Sóng mạnh / Chớm sóng / Không sóng.
    /// </summary>
    private Dictionary<string, SectorSnapshot> BuildSectorSnapshots(
        IReadOnlyList<Stock> universe,
        decimal indexChange5d,
        SmartMoneySettings settings)
    {
        var wave = settings.SectorWaveThresholds;

        var groups = universe
            .Where(s => !IsExcludedSector(s.Sector) && s.History.Count >= settings.MinHistoryDays)
            .GroupBy(s => s.Sector.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= wave.MinStocksPerSector)
            .ToList();

        var result = new Dictionary<string, SectorSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            var stocks = g.ToList();
            var sessionChanges = stocks
                .Select(s => signals.GetChangePercent(s, 1))
                .Where(c => c is > -95m and < 500m)
                .OrderBy(c => c)
                .ToList();

            if (sessionChanges.Count == 0)
                continue;

            var advancers = sessionChanges.Count(c => c > 0);
            var decliners = sessionChanges.Count(c => c < 0);
            var advancerRatio = (decimal)advancers / sessionChanges.Count;
            var median = Median(sessionChanges);
            var nearCeiling = sessionChanges.Count(c => c >= wave.NearCeilingChangePercent);
            var nearCeilingRatio = (decimal)nearCeiling / sessionChanges.Count;

            var sessionVol = stocks.Sum(s => s.History.Count > 0 ? s.History[^1].Volume : 0m);
            var avgVol = stocks.Sum(s => signals.GetAverageVolume(s.History));
            var volumeRatio = avgVol > 0 ? Math.Round(sessionVol / avgVol, 2) : 0m;
            var avgRs5d = Math.Round(
                stocks.Average(s => signals.GetRelativeStrength(s, indexChange5d, 5)), 2);

            var state = ClassifyWave(wave, advancerRatio, median, nearCeilingRatio, volumeRatio, avgRs5d);

            result[g.Key] = new SectorSnapshot(
                g.Key,
                sessionChanges.Count,
                advancers,
                decliners,
                Math.Round(advancerRatio, 3),
                median,
                Math.Round(nearCeilingRatio, 3),
                volumeRatio,
                avgRs5d,
                MedianChange5d(stocks),
                avgVol,
                state);
        }

        return result;
    }

    /// <summary>
    /// Sóng mạnh = đủ 4 điều kiện (độ rộng + lực + tiền vào + RS ngành).
    /// Chớm sóng = đủ độ rộng và ít nhất 1 trong 3 điều kiện còn lại.
    /// </summary>
    private static SectorWaveState ClassifyWave(
        SectorWaveSettings wave,
        decimal advancerRatio,
        decimal medianChange,
        decimal nearCeilingRatio,
        decimal volumeRatio,
        decimal sectorRs5d)
    {
        var breadthOk = advancerRatio >= wave.MinAdvancerRatio;
        if (!breadthOk)
            return SectorWaveState.None;

        var strengthOk = medianChange >= wave.MinMedianChangePercent
            || nearCeilingRatio >= wave.MinNearCeilingRatio;
        var moneyOk = volumeRatio >= wave.MinVolumeRatio;
        var rsOk = sectorRs5d > wave.MinSectorRs5d;

        if (strengthOk && moneyOk && rsOk)
            return SectorWaveState.Strong;

        return strengthOk || moneyOk || rsOk
            ? SectorWaveState.Emerging
            : SectorWaveState.None;
    }

    private static decimal Median(IReadOnlyList<decimal> sortedValues)
    {
        if (sortedValues.Count == 0)
            return 0;

        var mid = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 0
            ? Math.Round((sortedValues[mid - 1] + sortedValues[mid]) / 2, 2)
            : Math.Round(sortedValues[mid], 2);
    }

    private decimal MedianChange5d(IReadOnlyList<Stock> stocks)
    {
        var values = stocks
            .Select(s => signals.GetChangePercent(s, 5))
            .Where(c => c is > -95m and < 500m)
            .OrderBy(c => c)
            .ToList();

        if (values.Count == 0)
            return 0;

        var mid = values.Count / 2;
        return values.Count % 2 == 0
            ? Math.Round((values[mid - 1] + values[mid]) / 2, 2)
            : values[mid];
    }
}

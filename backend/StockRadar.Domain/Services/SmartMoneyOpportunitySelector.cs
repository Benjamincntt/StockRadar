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
    IReadOnlyDictionary<string, int> SectorRank,
    IReadOnlyDictionary<string, SectorSnapshot> SectorSnapshots,
    int SectorCount,
    BasePriceFilterSettings RunupFilter,
    SmartMoneySettings Settings,
    AdaptiveScoringProfile Adaptive,
    HitCalibrationProfile Calibration,
    IReadOnlyDictionary<string, decimal> RsPercentile);

public sealed record SmartMoneyEvaluation(
    string Symbol,
    int Score,
    bool Passes,
    WyckoffPhase StockPhase,
    int SectorRank,
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
        var marketPhase = ClassifyMarket(index);
        var snapshots = BuildSectorSnapshots(universe, index5d, settings);
        var sectorRank = snapshots
            .OrderBy(kv => kv.Value.Rank)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Rank, StringComparer.OrdinalIgnoreCase);
        var rsPercentile = BuildRsPercentile(universe, index5d, settings);

        return new SmartMoneyMarketContext(
            index,
            index5d,
            marketPhase,
            sectorRank,
            snapshots,
            sectorRank.Count,
            runupFilter,
            settings,
            adaptive ?? AdaptiveScoringProfile.Default,
            calibration ?? HitCalibrationProfile.Default,
            rsPercentile);
    }

    private Dictionary<string, decimal> BuildRsPercentile(
        IReadOnlyList<Stock> universe,
        decimal indexChange5d,
        SmartMoneySettings settings)
    {
        var eligible = universe
            .Where(s =>
                s.History.Count >= settings.MinHistoryDays
                && signals.GetAverageVolume(s.History) >= settings.MinAvgDailyVolume)
            .Select(s => (s.Symbol, Rs5: signals.GetRelativeStrength(s, indexChange5d, 5)))
            .OrderBy(x => x.Rs5)
            .ToList();

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (eligible.Count == 0)
            return result;

        if (eligible.Count == 1)
        {
            result[eligible[0].Symbol] = 100m;
            return result;
        }

        var denom = eligible.Count - 1;
        for (var i = 0; i < eligible.Count; i++)
            result[eligible[i].Symbol] = Math.Round(i / (decimal)denom * 100m, 2);

        return result;
    }

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
            decision.SectorRank,
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
        new(symbol, 0, false, WyckoffPhase.Unknown, 999, 0, 0, [reason], [], 0, 0, null, []);

    private static MarketWyckoffPhase ClassifyMarket(MarketIndex index) =>
        index.Trend switch
        {
            MarketTrend.Uptrend => MarketWyckoffPhase.Favorable,
            MarketTrend.Sideway => MarketWyckoffPhase.Neutral,
            _ => index.ChangePercent < -1.5m
                ? MarketWyckoffPhase.Unfavorable
                : MarketWyckoffPhase.Neutral
        };

    private static bool IsExcludedSector(string? sector)
    {
        if (string.IsNullOrWhiteSpace(sector))
            return true;

        var s = sector.Trim();
        return s.Equals("Khác", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Other", StringComparison.OrdinalIgnoreCase)
            || s.Equals("N/A", StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<string, SectorSnapshot> BuildSectorSnapshots(
        IReadOnlyList<Stock> universe,
        decimal indexChange5d,
        SmartMoneySettings settings)
    {
        const int minStocksPerSector = 3;

        var groups = universe
            .Where(s => !IsExcludedSector(s.Sector) && s.History.Count >= settings.MinHistoryDays)
            .GroupBy(s => s.Sector.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= minStocksPerSector)
            .ToList();

        if (groups.Count == 0)
            return new Dictionary<string, SectorSnapshot>(StringComparer.OrdinalIgnoreCase);

        var raw = groups.Select(g =>
        {
            var stocks = g.ToList();
            var avgRs = stocks.Average(s => (double)signals.GetRelativeStrength(s, indexChange5d, 5));
            var totalVol = stocks.Sum(s => (double)signals.GetAverageVolume(s.History));
            var capProxy = stocks.Sum(s => (double)(s.LatestPrice * signals.GetAverageVolume(s.History)));
            var avgChange5d = MedianChange5d(stocks);
            return new
            {
                Sector = g.Key,
                StockCount = stocks.Count,
                AvgRs = avgRs,
                TotalVol = totalVol,
                CapProxy = capProxy,
                AvgChange5d = avgChange5d
            };
        }).ToList();

        var maxVol = raw.Max(x => x.TotalVol);
        var maxCap = raw.Max(x => x.CapProxy);
        var maxCount = raw.Max(x => x.StockCount);
        var maxRs = raw.Max(x => Math.Abs(x.AvgRs));
        if (maxRs < 0.01) maxRs = 1;
        if (maxVol < 1) maxVol = 1;
        if (maxCap < 1) maxCap = 1;
        if (maxCount < 1) maxCount = 1;

        var w = settings;
        var scored = raw
            .Select(x =>
            {
                var normRs = (x.AvgRs + maxRs) / (2 * maxRs);
                var normVol = x.TotalVol / maxVol;
                var normCap = x.CapProxy / maxCap;
                var normCount = x.StockCount / (double)maxCount;
                var composite = normRs * w.SectorWeightRs
                    + normVol * w.SectorWeightVolume
                    + normCap * w.SectorWeightCap
                    + normCount * w.SectorWeightCount;
                return new
                {
                    x.Sector,
                    x.StockCount,
                    AvgChange5d = (decimal)x.AvgChange5d,
                    TotalVol = (decimal)x.TotalVol,
                    CapProxy = (decimal)x.CapProxy,
                    Composite = composite
                };
            })
            .OrderByDescending(x => x.Composite)
            .ToList();

        var result = new Dictionary<string, SectorSnapshot>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < scored.Count; i++)
        {
            var x = scored[i];
            result[x.Sector] = new SectorSnapshot(
                x.Sector,
                i + 1,
                x.StockCount,
                x.AvgChange5d,
                x.TotalVol,
                x.CapProxy,
                x.Composite);
        }

        return result;
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

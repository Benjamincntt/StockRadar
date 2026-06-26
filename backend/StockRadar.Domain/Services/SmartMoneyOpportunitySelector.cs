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
        SmartMoneySettings settings);

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
    SmartMoneySettings Settings);

public sealed record SmartMoneyEvaluation(
    string Symbol,
    int Score,
    bool Passes,
    WyckoffPhase StockPhase,
    int SectorRank,
    decimal RelativeStrength5d,
    decimal VolumeRatio,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<SignalType> Signals);

public sealed class SmartMoneyOpportunitySelector(ISignalAnalyzer signals) : ISmartMoneyOpportunitySelector
{
    public SmartMoneyMarketContext BuildContext(
        IReadOnlyList<Stock> universe,
        MarketIndex index,
        BasePriceFilterSettings runupFilter,
        SmartMoneySettings settings)
    {
        var index5d = index.IndexChange5d;
        var marketPhase = ClassifyMarket(index);
        var snapshots = BuildSectorSnapshots(universe, index5d, settings);
        var sectorRank = snapshots
            .OrderBy(kv => kv.Value.Rank)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Rank, StringComparer.OrdinalIgnoreCase);

        return new SmartMoneyMarketContext(
            index,
            index5d,
            marketPhase,
            sectorRank,
            snapshots,
            sectorRank.Count,
            runupFilter,
            settings);
    }

    public SmartMoneyEvaluation Evaluate(Stock stock, SmartMoneyMarketContext context)
    {
        var settings = context.Settings;
        var runup = context.RunupFilter;
        var history = stock.History;
        var index5d = context.IndexChangePercent5d;
        var detected = signals.DetectSignals(stock, context.Index.ChangePercent);
        var reasons = new List<string>();

        if (history.Count < settings.MinHistoryDays)
            return Fail(stock.Symbol, $"Thiếu lịch sử (<{settings.MinHistoryDays} phiên)");

        if (signals.GetAverageVolume(history) < settings.MinAvgDailyVolume)
            return Fail(stock.Symbol, "Thanh khoản thấp");

        if (signals.IsDistribution(history))
            return Fail(stock.Symbol, "Pha phân phối — không mua");

        if (signals.HasExceededMaxGainFromBase(history, runup))
        {
            var filterProfile = signals.AnalyzeBasePriceForFilter(history, runup);
            var gainFromBase = filterProfile?.GainFromBasePercent ?? 0;
            var zoneDesc = filterProfile is not null
                ? $"đỉnh nền {filterProfile.BaseHigh:N1}"
                : "vùng nền";
            return Fail(
                stock.Symbol,
                $"Đã tăng {gainFromBase:0.#}% so với {zoneDesc} (>{runup.MaxGainFromBasePercent:0.#}%) — tránh FOMO");
        }

        if (!signals.HasBullishMaStack(
                history,
                settings.RequireMaStack,
                settings.MinSessionsForMa50,
                settings.MinSessionsForFullStack))
            return Fail(stock.Symbol, "Chưa đạt MA stack / xu hướng dài hạn");

        var volRatio = signals.GetVolumeRatio(history);
        var rs5 = signals.GetRelativeStrength(stock, index5d, 5);
        var change20d = signals.GetChangePercent(stock, 20);
        var stockPhase = ClassifyStock(stock, detected, volRatio, settings.BreakoutMinVolumeRatio);
        var sectorRank = context.SectorRank.GetValueOrDefault(stock.Sector, context.SectorCount + 1);

        if (context.MarketPhase == MarketWyckoffPhase.Unfavorable && rs5 < 1m)
            return Fail(stock.Symbol, "Thị trường xấu + CP không khỏe hơn VNINDEX");

        if (change20d < -15m && !signals.HasValidBaseSetup(history, runup, settings.MaxGainInBasePercent)
            && !detected.Contains(SignalType.Breakout) && !detected.Contains(SignalType.Shakeout))
            return Fail(stock.Symbol, "Giảm sâu chưa có nền/tích lũy");

        if (sectorRank > settings.TopSectorCount && rs5 < 2m)
            return Fail(stock.Symbol, "Ngành yếu + RS không đủ");

        var hasBaseSetup = signals.HasValidBaseSetup(history, runup, settings.MaxGainInBasePercent);
        var hasBreakoutVol = detected.Contains(SignalType.Breakout)
            && volRatio >= settings.BreakoutMinVolumeRatio;
        var hasShakeoutRecovery = detected.Contains(SignalType.Shakeout);

        if (!hasBaseSetup && !hasBreakoutVol && !hasShakeoutRecovery)
            return Fail(stock.Symbol, "Chưa có nền giá / breakout + volume");

        if (rs5 < 0 && !hasBreakoutVol)
            return Fail(stock.Symbol, "Yếu hơn VNINDEX (RS âm)");

        var score = 0;

        if (context.MarketPhase == MarketWyckoffPhase.Favorable)
        {
            score += 12;
            reasons.Add("Pha thị trường thuận");
        }
        else if (context.MarketPhase == MarketWyckoffPhase.Neutral)
        {
            score += 6;
            reasons.Add("Thị trường trung tính");
        }

        if (sectorRank <= 3)
        {
            score += 18;
            reasons.Add($"Ngành top #{sectorRank}");
        }
        else if (sectorRank <= settings.TopSectorCount)
        {
            score += 10;
            reasons.Add($"Ngành mạnh #{sectorRank}");
        }

        if (rs5 >= 3m)
        {
            score += 20;
            reasons.Add($"RS +{rs5:0.#}% vs VN (5 phiên)");
        }
        else if (rs5 >= 0)
        {
            score += 12;
            reasons.Add("Khỏe hơn VNINDEX (5 phiên)");
        }

        if (hasBaseSetup)
        {
            score += 18;
            reasons.Add("Nền giá / tích lũy");
        }

        if (hasBreakoutVol)
        {
            score += 22;
            reasons.Add($"Breakout Vol×{volRatio:0.0}");
        }

        if (hasShakeoutRecovery)
        {
            score += 10;
            reasons.Add("Đáy trước thị trường");
        }

        if (detected.Contains(SignalType.VolumeSpike))
        {
            score += 8;
            reasons.Add("KL bất thường");
        }

        if (stockPhase == WyckoffPhase.Markup)
        {
            score += 5;
            reasons.Add("Pha tăng giá");
        }

        if (signals.HasBullishMaStack(
                history,
                settings.RequireMaStack,
                settings.MinSessionsForMa50,
                settings.MinSessionsForFullStack))
        {
            score += 5;
            reasons.Add("MA stack tăng");
        }

        score = Math.Clamp(score, 0, 100);
        var passes = score >= settings.MinPassScore
            && (hasBaseSetup || hasBreakoutVol || hasShakeoutRecovery)
            && rs5 >= -1m;

        return new SmartMoneyEvaluation(
            stock.Symbol,
            score,
            passes,
            stockPhase,
            sectorRank,
            rs5,
            volRatio,
            reasons,
            detected);
    }

    public bool PassesFilter(SmartMoneyEvaluation eval, SmartMoneySettings settings) =>
        eval.Passes && eval.Score >= settings.MinPassScore;

    private static SmartMoneyEvaluation Fail(string symbol, string reason) =>
        new(symbol, 0, false, WyckoffPhase.Unknown, 999, 0, 0, [reason], []);

    private static MarketWyckoffPhase ClassifyMarket(MarketIndex index) =>
        index.Trend switch
        {
            MarketTrend.Uptrend => MarketWyckoffPhase.Favorable,
            MarketTrend.Sideway => MarketWyckoffPhase.Neutral,
            _ => index.ChangePercent < -1.5m
                ? MarketWyckoffPhase.Unfavorable
                : MarketWyckoffPhase.Neutral
        };

    private static WyckoffPhase ClassifyStock(
        Stock stock,
        IReadOnlyList<SignalType> detected,
        decimal volRatio,
        decimal breakoutMinVolumeRatio)
    {
        if (detected.Contains(SignalType.Distribution))
            return WyckoffPhase.Distribution;

        if (detected.Contains(SignalType.Shakeout))
            return WyckoffPhase.Accumulation;

        if (detected.Contains(SignalType.Breakout) && volRatio >= breakoutMinVolumeRatio)
            return WyckoffPhase.Markup;

        var change20 = stock.History.Count >= 21
            ? (stock.History[^1].Close - stock.History[^21].Close) / stock.History[^21].Close * 100m
            : 0m;

        if (change20 < -10m)
            return WyckoffPhase.Markdown;

        return WyckoffPhase.Unknown;
    }

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

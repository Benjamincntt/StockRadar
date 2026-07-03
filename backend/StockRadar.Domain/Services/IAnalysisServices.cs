using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public interface ISignalAnalyzer
{
    decimal GetChangePercent(IReadOnlyList<OhlcvBar> history, int days = 1);
    decimal GetChangePercent(Stock stock, int days = 1);
    decimal GetVolumeRatio(IReadOnlyList<OhlcvBar> history);
    decimal GetAverageVolume(IReadOnlyList<OhlcvBar> history, int period = 20);
    public decimal GetRelativeStrength(Stock stock, decimal indexChangePercent, int days = 5);
    bool HasBullishMaStack(
        IReadOnlyList<OhlcvBar> history,
        bool enabled = true,
        int minSessionsForMa50 = 50,
        int minSessionsForFullStack = 200);
    bool HasValidBaseSetup(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings runup,
        decimal maxGainInBasePercent);
    ConsolidationZone? FindNearestConsolidationZone(
        IReadOnlyList<OhlcvBar> history,
        decimal consolidationMaxRangePercent = 15m,
        int consolidationMinSessions = 5,
        int maxScanSessions = 90,
        decimal maxCloseDriftPercent = 8m);
    decimal GetGainFromBasePercent(
        IReadOnlyList<OhlcvBar> history,
        decimal consolidationMaxRangePercent = 15m,
        int consolidationMinSessions = 5,
        int maxScanSessions = 90,
        decimal? currentPrice = null);
    bool HasExceededMaxGainFromBase(
        IReadOnlyList<OhlcvBar> history,
        decimal maxGainPercent,
        decimal consolidationMaxRangePercent = 15m,
        int consolidationMinSessions = 5,
        int maxScanSessions = 90,
        decimal? currentPrice = null);
    bool HasExceededMaxGainFromBase(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null);
    BasePriceProfile? AnalyzeBasePrice(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null);
    BasePriceProfile? AnalyzeBasePriceForFilter(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null);
    bool IsBreakout(IReadOnlyList<OhlcvBar> history);
    DarvasBreakoutResult EvaluateDarvasBreakout(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter);
    FlatBoxProfile AnalyzeFlatBox(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter);
    bool IsDarvasBreakout(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter);
    bool IsAccumulation(IReadOnlyList<OhlcvBar> history);
    bool IsVolumeSpike(IReadOnlyList<OhlcvBar> history);
    bool IsDistribution(IReadOnlyList<OhlcvBar> history);
    bool IsShakeout(IReadOnlyList<OhlcvBar> history);
    bool IsShakeoutFromBase(IReadOnlyList<OhlcvBar> history, BasePriceFilterSettings filter);
    bool MeetsSessionEntryBar(
        IReadOnlyList<OhlcvBar> history,
        decimal minChangePercent,
        decimal minSessionVolume);
    IReadOnlyList<SignalType> DetectSignals(
        Stock stock,
        decimal indexChangePercent = 0,
        BasePriceFilterSettings? runup = null);
    PriceLevels CalculatePriceLevels(IReadOnlyList<OhlcvBar> history);
}

namespace StockRadar.Application.DTOs;

public sealed record SmartMoneyBacktestRequestDto(
    int Days = 90,
    int MaxPicksPerDay = 10,
    int HoldSessions = 5,
    bool RelaxedFallback = true,
    int? MinScore = null,
    int? MinPassScore = null,
    SmartMoneyBacktestMode Mode = SmartMoneyBacktestMode.StrictThenRelaxed);

public enum SmartMoneyBacktestMode
{
    Strict,
    Relaxed,
    StrictThenRelaxed
}

public sealed record SmartMoneyBacktestTradeDto(
    string Symbol,
    DateOnly EntryDate,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal ReturnPercent,
    int BuyScore,
    string Outcome,
    bool UsedRelaxedFallback);

public sealed record SmartMoneyBacktestSummaryDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int TradingDaysScanned,
    int DaysWithPicks,
    int TotalTrades,
    int WinCount,
    int LossCount,
    int FlatCount,
    decimal WinRatePercent,
    decimal AvgReturnPercent,
    decimal MedianReturnPercent,
    decimal MaxDrawdownPercent,
    decimal SuccessThresholdPercent,
    int UniverseSize,
    bool RelaxedFallbackEnabled);

public sealed record SmartMoneyBacktestResultDto(
    SmartMoneyBacktestSummaryDto Summary,
    IReadOnlyList<SmartMoneyBacktestTradeDto> Trades);

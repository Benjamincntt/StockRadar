namespace StockRadar.Application.DTOs;

public sealed record TuneEvaluateRequest(
    int MinPassScore,
    int MaxResults,
    int? Days = null,
    int? HoldSessions = null,
    /// <summary>Lùi cửa sổ backtest thêm N phiên (walk-forward).</summary>
    int? EndOffsetSessions = null);

public sealed record TuneEvaluateResponse(
    decimal FitnessScore,
    decimal HitRateTopK,
    decimal AvgMfe,
    decimal MaxDrawdown,
    int TotalTrades,
    int TradingDaysScanned,
    int DaysWithPicks,
    string Message);

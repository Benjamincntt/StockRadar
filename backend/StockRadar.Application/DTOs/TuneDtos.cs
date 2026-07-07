namespace StockRadar.Application.DTOs;

public sealed record TuneEvaluateRequest(
    int MinPassScore,
    int MaxResults,
    int? Days = null,
    int? HoldSessions = null);

public sealed record TuneEvaluateResponse(
    decimal FitnessScore,
    decimal HitRateTopK,
    decimal AvgMfe,
    decimal MaxDrawdown,
    int TotalTrades,
    int TradingDaysScanned,
    int DaysWithPicks,
    string Message);

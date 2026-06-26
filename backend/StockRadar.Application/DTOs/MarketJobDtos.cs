namespace StockRadar.Application.DTOs;

public sealed record DailySessionSyncResultDto(
    int SymbolsSynced,
    bool IndexUpdated,
    DateOnly SessionDate,
    DateTime CompletedAt);

public sealed record DailyAnalysisResultDto(
    DateOnly ForTradingDate,
    int StocksScored,
    int OpportunitiesSaved,
    DateTime CompletedAt,
    int PatternAlertsPublished = 0);

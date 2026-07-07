namespace StockRadar.Application.DTOs;

public sealed record DailySessionSyncResultDto(
    int SymbolsSynced,
    bool IndexUpdated,
    DateOnly SessionDate,
    DateTime CompletedAt,
    int UniverseDeactivated = 0,
    int DarvasBreakoutAlerts = 0);

public sealed record UniverseRescreenResultDto(
    int ActiveBefore,
    int Deactivated,
    int Reactivated,
    DateTime CompletedAt);

public sealed record DailyAnalysisResultDto(
    DateOnly ForTradingDate,
    int StocksScored,
    int OpportunitiesSaved,
    DateTime CompletedAt,
    int PatternAlertsPublished = 0,
    bool UsedRelaxedFallback = false);

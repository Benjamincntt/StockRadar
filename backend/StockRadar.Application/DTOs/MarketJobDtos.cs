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
    int PatternAlertsPublished = 0);

/// <summary>Kết quả backfill Sóng ngành (spec 007) — không đụng Buy Score/Top/DailyOpportunities.</summary>
public sealed record SectorWaveRegimeBackfillResultDto(
    DateOnly FromDate,
    IReadOnlyList<DateOnly> ProcessedDates,
    int SectorDayRowsWritten,
    DateTime CompletedAt);

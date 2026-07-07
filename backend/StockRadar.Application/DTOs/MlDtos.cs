namespace StockRadar.Application.DTOs;

public record OpportunityRankingDatasetDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int RowCount,
    int PositiveLabels,
    decimal PositiveRatePercent,
    IReadOnlyList<string> FeatureNames,
    IReadOnlyList<OpportunityRankingRowDto> Rows,
    string LabelNote);

public record OpportunityRankingRowDto(
    string Symbol,
    DateOnly EntryDate,
    int? Rank,
    int BuyScore,
    decimal PredictedHitPercent,
    int SectorRank,
    decimal RelativeStrength5d,
    decimal VolumeRatio,
    bool IsActionable,
    bool DnaBreakout,
    bool DnaShakeout,
    bool MarketFavorable,
    bool LabelHit,
    string LabelSource,
    decimal? ForwardReturnT25,
    decimal? MaxFavorableExcursionPercent,
    decimal? MaxAdverseExcursionPercent,
    string? TradeState,
    string? SetupDna);

public record OpportunityRankerTrainingResultDto(
    bool Success,
    int Samples,
    decimal TrainingAccuracy,
    decimal PositiveRatePercent,
    DateTime? TrainedAtUtc,
    string? ModelPath,
    string Message);

public record OpportunityRankerStatusDto(
    bool Enabled,
    bool ModelActive,
    int TrainingSamples,
    decimal? TrainingAccuracy,
    DateTime? TrainedAtUtc,
    IReadOnlyList<string> FeatureNames,
    IReadOnlyList<decimal>? Weights,
    bool AutoRetrainEnabled);

public record OpportunityRankerModelVersionDto(
    string FileName,
    DateTime? TrainedAtUtc,
    int TrainingSamples,
    decimal TrainingAccuracy,
    bool IsActive);

public record SetupTrackBackfillResultDto(
    int DaysRequested,
    DateOnly FromDate,
    int OpportunityRowsScanned,
    int DatesProcessed,
    int TracksRegistered,
    int OutcomesMeasured,
    string Message);

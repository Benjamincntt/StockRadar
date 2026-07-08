using Microsoft.EntityFrameworkCore;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<StockEntity> Stocks => Set<StockEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<MarketIndexEntity> MarketIndices => Set<MarketIndexEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<WatchlistItemEntity> WatchlistItems => Set<WatchlistItemEntity>();
    public DbSet<SectorDefinitionEntity> SectorDefinitions => Set<SectorDefinitionEntity>();
    public DbSet<DailyOpportunityEntity> DailyOpportunities => Set<DailyOpportunityEntity>();
    public DbSet<DailyAnalysisRunEntity> DailyAnalysisRuns => Set<DailyAnalysisRunEntity>();
    public DbSet<SessionRadarHitEntity> SessionRadarHits => Set<SessionRadarHitEntity>();
    public DbSet<CriterionWeightEntity> CriterionWeights => Set<CriterionWeightEntity>();
    public DbSet<DailyCriterionAccuracyEntity> DailyCriterionAccuracies => Set<DailyCriterionAccuracyEntity>();
    public DbSet<StockCriterionScoreEntity> StockCriterionScores => Set<StockCriterionScoreEntity>();
    public DbSet<StockCriterionDetailEntity> StockCriterionDetails => Set<StockCriterionDetailEntity>();
    public DbSet<CriterionGroupDailyAccuracyEntity> CriterionGroupDailyAccuracies => Set<CriterionGroupDailyAccuracyEntity>();
    public DbSet<WeeklyCriterionReviewEntity> WeeklyCriterionReviews => Set<WeeklyCriterionReviewEntity>();
    public DbSet<CriterionGroupWeeklyReviewEntity> CriterionGroupWeeklyReviews => Set<CriterionGroupWeeklyReviewEntity>();
    public DbSet<SetupTrackEntity> SetupTracks => Set<SetupTrackEntity>();
    public DbSet<WeeklyOpportunityReviewEntity> WeeklyOpportunityReviews => Set<WeeklyOpportunityReviewEntity>();
    public DbSet<HitCalibrationBucketEntity> HitCalibrationBuckets => Set<HitCalibrationBucketEntity>();
    public DbSet<HitCalibrationStateEntity> HitCalibrationStates => Set<HitCalibrationStateEntity>();
    public DbSet<FalsePositiveMiningStateEntity> FalsePositiveMiningStates => Set<FalsePositiveMiningStateEntity>();
    public DbSet<ShadowPickEntity> ShadowPicks => Set<ShadowPickEntity>();
    public DbSet<ShadowVariantSummaryEntity> ShadowVariantSummaries => Set<ShadowVariantSummaryEntity>();
    public DbSet<ShadowWeightPickEntity> ShadowWeightPicks => Set<ShadowWeightPickEntity>();
    public DbSet<ShadowWeightSummaryEntity> ShadowWeightSummaries => Set<ShadowWeightSummaryEntity>();
    public DbSet<EntryTimingStateEntity> EntryTimingStates => Set<EntryTimingStateEntity>();
    public DbSet<TradeJournalEntryEntity> TradeJournalEntries => Set<TradeJournalEntryEntity>();
    public DbSet<PersonalCalibrationStateEntity> PersonalCalibrationStates => Set<PersonalCalibrationStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const int moneyPrecision = 18;
        const int moneyScale = 2;

        modelBuilder.Entity<StockEntity>(e =>
        {
            e.HasKey(x => x.Symbol);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Sector).HasMaxLength(64);
            e.Property(x => x.SectorLocked).HasDefaultValue(false);
            e.Property(x => x.HistoryJson).IsRequired();
            e.Property(x => x.LastChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Exchange).HasMaxLength(16);
            e.Property(x => x.AvgVolume30d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.TradingStatus).HasMaxLength(128);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<DailyAnalysisRunEntity>(e =>
        {
            e.HasKey(x => x.ForTradingDate);
        });

        modelBuilder.Entity<AlertEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(256);
            e.Property(x => x.Message).HasMaxLength(1024);
            e.Property(x => x.VolumeRatio).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.RelativeStrength).HasPrecision(moneyPrecision, moneyScale);
        });

        modelBuilder.Entity<MarketIndexEntity>(e =>
        {
            e.HasKey(x => x.Symbol);
            e.Property(x => x.Symbol).HasMaxLength(32);
            e.Property(x => x.Price).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.HistoryJson).IsRequired();
        });

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<WatchlistItemEntity>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Symbol });
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SectorDefinitionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<DailyOpportunityEntity>(e =>
        {
            e.HasKey(x => new { x.ForTradingDate, x.Symbol });
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Sector).HasMaxLength(64);
            e.Property(x => x.Price).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.VolumeRatio).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PredictedHitPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.SetupDna).HasMaxLength(512);
            e.Property(x => x.Recommendation).HasMaxLength(32);
            e.Property(x => x.TradeState).HasMaxLength(32);
            e.Property(x => x.TradeStateReason).HasMaxLength(256);
            e.Property(x => x.EntryPointJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.ExplainJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.MarketPhase).HasMaxLength(32);
            e.HasIndex(x => x.ForTradingDate);
        });

        modelBuilder.Entity<SessionRadarHitEntity>(e =>
        {
            e.HasKey(x => new { x.SessionDate, x.Exchange, x.Symbol });
            e.Property(x => x.Exchange).HasMaxLength(16);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Sector).HasMaxLength(64);
            e.Property(x => x.SignalsJson).IsRequired();
            e.Property(x => x.Price).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.VolumeRatio).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.RelativeStrength).HasPrecision(moneyPrecision, moneyScale);
            e.HasIndex(x => new { x.SessionDate, x.Exchange });
        });

        modelBuilder.Entity<CriterionWeightEntity>(e =>
        {
            e.HasKey(x => x.CriterionId);
            e.Property(x => x.CriterionId).HasMaxLength(32);
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.Weight).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Accuracy7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Accuracy30d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Reliability7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Edge7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.RecommendedAction).HasMaxLength(16);
        });

        modelBuilder.Entity<DailyCriterionAccuracyEntity>(e =>
        {
            e.HasKey(x => new { x.AsOfDate, x.Horizon, x.CriterionId });
            e.Property(x => x.CriterionId).HasMaxLength(32);
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.AccuracyPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgScore).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgMfePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgMaePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.InvalidationRatePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.BaselinePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.EdgePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ReliabilityScore).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.BreakdownJson).IsRequired();
            e.HasIndex(x => x.AsOfDate);
        });

        modelBuilder.Entity<CriterionGroupDailyAccuracyEntity>(e =>
        {
            e.HasKey(x => new { x.AsOfDate, x.Horizon, x.GroupId });
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.AccuracyPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgScore).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ReliabilityScore).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.EdgePercent).HasPrecision(moneyPrecision, moneyScale);
            e.HasIndex(x => x.AsOfDate);
        });

        modelBuilder.Entity<StockCriterionScoreEntity>(e =>
        {
            e.HasKey(x => new { x.AsOfDate, x.Symbol });
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.ScoresJson).IsRequired();
            e.Property(x => x.NextDayChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.HasIndex(x => x.AsOfDate);
        });

        modelBuilder.Entity<StockCriterionDetailEntity>(e =>
        {
            e.HasKey(x => new { x.AsOfDate, x.Horizon, x.Symbol, x.CriterionId });
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.CriterionId).HasMaxLength(32);
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.Bias).HasMaxLength(16);
            e.Property(x => x.Summary).HasMaxLength(256);
            e.Property(x => x.NextDayChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.MaxFavorablePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.MaxAdversePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.RelativeStrengthForward).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ScoreBucket).HasMaxLength(8);
            e.Property(x => x.MarketPhase).HasMaxLength(16);
            e.HasIndex(x => new { x.AsOfDate, x.CriterionId });
            e.HasIndex(x => new { x.AsOfDate, x.GroupId });
        });

        modelBuilder.Entity<WeeklyCriterionReviewEntity>(e =>
        {
            e.HasKey(x => new { x.WeekStartDate, x.CriterionId });
            e.Property(x => x.CriterionId).HasMaxLength(32);
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.Label).HasMaxLength(64);
            e.Property(x => x.Accuracy7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgScore7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Weight).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Edge7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.Reliability7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgMfe7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.InvalidationRate7d).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.BreakdownJson).IsRequired();
            e.Property(x => x.RecommendedAction).HasMaxLength(16);
            e.HasIndex(x => x.WeekStartDate);
        });

        modelBuilder.Entity<CriterionGroupWeeklyReviewEntity>(e =>
        {
            e.HasKey(x => new { x.WeekStartDate, x.GroupId });
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.AccuracyPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgScore).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.RecommendedAction).HasMaxLength(16);
            e.HasIndex(x => x.WeekStartDate);
        });

        modelBuilder.Entity<SetupTrackEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.SourceType).HasMaxLength(24);
            e.Property(x => x.EntryPrice).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.SessionChangePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PeakGainPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ForwardPriceT25).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ForwardReturnPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.OutcomeBucket).HasMaxLength(16);
            e.Property(x => x.OutcomeBucketT5).HasMaxLength(16);
            e.Property(x => x.OutcomeBucketT10).HasMaxLength(16);
            e.Property(x => x.ForwardReturnT5).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ForwardReturnT10).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.MaxFavorableExcursionPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.MaxAdverseExcursionPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PredictedHitPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.SetupDna).HasMaxLength(256);
            e.Property(x => x.ScoreBreakdownJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.TradeState).HasMaxLength(32);
            e.Property(x => x.TradeStateReason).HasMaxLength(256);
            e.HasIndex(x => new { x.Symbol, x.SourceType, x.EntryDate }).IsUnique();
            e.HasIndex(x => x.OutcomeMeasured);
            e.HasIndex(x => x.SwingMetricsMeasured);
            e.HasIndex(x => x.WeekStartDate);
        });

        modelBuilder.Entity<WeeklyOpportunityReviewEntity>(e =>
        {
            e.HasKey(x => x.WeekStartDate);
            e.Property(x => x.SuccessRatePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.FailedRatePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.OpportunitySuccessRate).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.BuyPoint1SuccessRate).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.BuyPoint2SuccessRate).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.RecommendedAction).HasMaxLength(16);
            e.Property(x => x.Summary).HasMaxLength(512);
        });

        modelBuilder.Entity<HitCalibrationBucketEntity>(e =>
        {
            e.HasKey(x => x.BucketId);
            e.Property(x => x.BucketId).HasMaxLength(16);
            e.Property(x => x.PredictedMidPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ActualHitRatePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.CalibrationFactor).HasPrecision(moneyPrecision, moneyScale);
        });

        modelBuilder.Entity<HitCalibrationStateEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.GlobalFactor).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PredictionBiasPercent).HasPrecision(moneyPrecision, moneyScale);
        });

        modelBuilder.Entity<FalsePositiveMiningStateEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ResultsJson).IsRequired();
        });

        modelBuilder.Entity<ShadowPickEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.EntryPrice).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PredictedHitPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ForwardReturnPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.OutcomeBucket).HasMaxLength(16);
            e.HasIndex(x => new { x.ForTradingDate, x.VariantMinPassScore, x.Symbol }).IsUnique();
            e.HasIndex(x => x.OutcomeMeasured);
        });

        modelBuilder.Entity<ShadowWeightPickEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.WeightMultiplier).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.EntryPrice).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PredictedHitPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.ForwardReturnPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.OutcomeBucket).HasMaxLength(16);
            e.HasIndex(x => new { x.ForTradingDate, x.WeightMultiplier, x.Symbol }).IsUnique();
        });

        modelBuilder.Entity<ShadowWeightSummaryEntity>(e =>
        {
            e.HasKey(x => x.WeightMultiplier);
            e.Property(x => x.WeightMultiplier).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.SuccessRatePercent).HasPrecision(moneyPrecision, moneyScale);
        });

        modelBuilder.Entity<EntryTimingStateEntity>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<TradeJournalEntryEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Action).HasMaxLength(16);
            e.Property(x => x.EngineVerdict).HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(512);
            e.Property(x => x.SetupDna).HasMaxLength(256);
            e.Property(x => x.SizePercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.PredictedHit).HasPrecision(moneyPrecision, moneyScale);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        modelBuilder.Entity<PersonalCalibrationStateEntity>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.Factor).HasPrecision(moneyPrecision, moneyScale);
        });

        modelBuilder.Entity<ShadowVariantSummaryEntity>(e =>
        {
            e.HasKey(x => x.VariantMinPassScore);
            e.Property(x => x.SuccessRatePercent).HasPrecision(moneyPrecision, moneyScale);
        });
    }
}

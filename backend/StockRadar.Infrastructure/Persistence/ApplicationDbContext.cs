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
            e.Property(x => x.RecommendedAction).HasMaxLength(16);
        });

        modelBuilder.Entity<DailyCriterionAccuracyEntity>(e =>
        {
            e.HasKey(x => new { x.AsOfDate, x.CriterionId });
            e.Property(x => x.CriterionId).HasMaxLength(32);
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.AccuracyPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgScore).HasPrecision(moneyPrecision, moneyScale);
            e.HasIndex(x => x.AsOfDate);
        });

        modelBuilder.Entity<CriterionGroupDailyAccuracyEntity>(e =>
        {
            e.HasKey(x => new { x.AsOfDate, x.GroupId });
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.AccuracyPercent).HasPrecision(moneyPrecision, moneyScale);
            e.Property(x => x.AvgScore).HasPrecision(moneyPrecision, moneyScale);
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
            e.HasKey(x => new { x.AsOfDate, x.Symbol, x.CriterionId });
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.CriterionId).HasMaxLength(32);
            e.Property(x => x.GroupId).HasMaxLength(32);
            e.Property(x => x.Bias).HasMaxLength(16);
            e.Property(x => x.Summary).HasMaxLength(256);
            e.Property(x => x.NextDayChangePercent).HasPrecision(moneyPrecision, moneyScale);
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
    }
}

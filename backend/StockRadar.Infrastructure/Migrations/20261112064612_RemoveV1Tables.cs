using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveV1Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "CriterionGroupDailyAccuracies");

            migrationBuilder.DropTable(
                name: "CriterionGroupWeeklyReviews");

            migrationBuilder.DropTable(
                name: "CriterionWeights");

            migrationBuilder.DropTable(
                name: "DailyAnalysisRuns");

            migrationBuilder.DropTable(
                name: "DailyCriterionAccuracies");

            migrationBuilder.DropTable(
                name: "DailyOpportunities");

            migrationBuilder.DropTable(
                name: "EarlyRecoveryRadar");

            migrationBuilder.DropTable(
                name: "EntryTimingStates");

            migrationBuilder.DropTable(
                name: "FalsePositiveMiningStates");

            migrationBuilder.DropTable(
                name: "HitCalibrationBuckets");

            migrationBuilder.DropTable(
                name: "HitCalibrationStates");

            migrationBuilder.DropTable(
                name: "PersonalCalibrationStates");

            migrationBuilder.DropTable(
                name: "PositionSellLegs");

            migrationBuilder.DropTable(
                name: "SectorWaveRegimes");

            migrationBuilder.DropTable(
                name: "SessionRadarHits");

            migrationBuilder.DropTable(
                name: "SetupTracks");

            migrationBuilder.DropTable(
                name: "ShadowWeightPicks");

            migrationBuilder.DropTable(
                name: "ShadowWeightSummaries");

            migrationBuilder.DropTable(
                name: "StockCriterionDetails");

            migrationBuilder.DropTable(
                name: "StockCriterionScores");

            migrationBuilder.DropTable(
                name: "TradeJournalEntries");

            migrationBuilder.DropTable(
                name: "VipAlertFires");

            migrationBuilder.DropTable(
                name: "WeeklyCriterionReviews");

            migrationBuilder.DropTable(
                name: "WeeklyOpportunityReviews");

            migrationBuilder.DropTable(
                name: "MasterAlertPositions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    RelativeStrength = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SectorRank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    VolumeRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CriterionGroupDailyAccuracies",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Horizon = table.Column<int>(type: "int", nullable: false),
                    PlaybookId = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccuracyPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CriterionCount = table.Column<int>(type: "int", nullable: false),
                    EdgePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HitCount = table.Column<int>(type: "int", nullable: false),
                    ReliabilityScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionGroupDailyAccuracies", x => new { x.AsOfDate, x.Horizon, x.PlaybookId, x.GroupId });
                });

            migrationBuilder.CreateTable(
                name: "CriterionGroupWeeklyReviews",
                columns: table => new
                {
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccuracyPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HitCount = table.Column<int>(type: "int", nullable: false),
                    KeepCount = table.Column<int>(type: "int", nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RemoveCount = table.Column<int>(type: "int", nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    WatchCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionGroupWeeklyReviews", x => new { x.WeekStartDate, x.GroupId });
                });

            migrationBuilder.CreateTable(
                name: "CriterionWeights",
                columns: table => new
                {
                    CriterionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Accuracy30d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Accuracy7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Edge7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reliability7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SampleCount7d = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionWeights", x => x.CriterionId);
                });

            migrationBuilder.CreateTable(
                name: "DailyAnalysisRuns",
                columns: table => new
                {
                    ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GateStatsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpportunitiesSaved = table.Column<int>(type: "int", nullable: false),
                    StocksScored = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyAnalysisRuns", x => x.ForTradingDate);
                });

            migrationBuilder.CreateTable(
                name: "DailyCriterionAccuracies",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Horizon = table.Column<int>(type: "int", nullable: false),
                    PlaybookId = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CriterionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccuracyPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgMaePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgMfePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaselinePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EdgePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HitCount = table.Column<int>(type: "int", nullable: false),
                    InvalidationRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    ReliabilityScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCriterionAccuracies", x => new { x.AsOfDate, x.Horizon, x.PlaybookId, x.CriterionId });
                });

            migrationBuilder.CreateTable(
                name: "DailyOpportunities",
                columns: table => new
                {
                    ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AverageDailyVolume = table.Column<long>(type: "bigint", nullable: false),
                    BuyScore = table.Column<int>(type: "int", nullable: true),
                    ChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EntryPointJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExplainJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MarketPhase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PredictedHitPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PredictedSampleCount = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SetupDna = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TradeState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TradeStateReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    VolumeRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyOpportunities", x => new { x.ForTradingDate, x.Symbol });
                });

            migrationBuilder.CreateTable(
                name: "EarlyRecoveryRadar",
                columns: table => new
                {
                    ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MarketPhase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Rs5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RsPercentile = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VolumeRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarlyRecoveryRadar", x => new { x.ForTradingDate, x.Symbol });
                });

            migrationBuilder.CreateTable(
                name: "EntryTimingStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ConfirmGood = table.Column<int>(type: "int", nullable: false),
                    ConfirmMeasured = table.Column<int>(type: "int", nullable: false),
                    PreferMasterConfirm = table.Column<bool>(type: "bit", nullable: false),
                    TopOnlyGood = table.Column<int>(type: "int", nullable: false),
                    TopOnlyMeasured = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryTimingStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FalsePositiveMiningStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    FalsePositiveSetups = table.Column<int>(type: "int", nullable: false),
                    GoodSetups = table.Column<int>(type: "int", nullable: false),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FalsePositiveMiningStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HitCalibrationBuckets",
                columns: table => new
                {
                    BucketId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ActualHitRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CalibrationFactor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    PredictedMax = table.Column<int>(type: "int", nullable: false),
                    PredictedMidPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PredictedMin = table.Column<int>(type: "int", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HitCalibrationBuckets", x => x.BucketId);
                });

            migrationBuilder.CreateTable(
                name: "HitCalibrationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    GlobalFactor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PredictionBiasPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSamples = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HitCalibrationStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterAlertPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnchorWindowStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentPositionSize = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EntryBarLow = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExitRegime = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FiredAlertKindsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HoldingSessions = table.Column<int>(type: "int", nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    MarketPhaseAtEntry = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MaxPositionSize = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OverheadBaseHigh = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OverheadBaseLow = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PeakPriceSinceEntry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RealizedFeeProfile = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true),
                    RealizedGrossReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RealizedMeasured = table.Column<bool>(type: "bit", nullable: false),
                    RealizedMeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RealizedOutcomeBucket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    RealizedReturnOnDeployedPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RealizedStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    RealizedWeightedReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterAlertPositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalCalibrationStates",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalCalibrationStates", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "SectorWaveRegimes",
                columns: table => new
                {
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ConsecutiveLowVolumeSessions = table.Column<int>(type: "int", nullable: false),
                    FailedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SessionsSinceActivation = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorWaveRegimes", x => new { x.Sector, x.TradingDate });
                });

            migrationBuilder.CreateTable(
                name: "SessionRadarHits",
                columns: table => new
                {
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RelativeStrength = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SessionVolume = table.Column<long>(type: "bigint", nullable: false),
                    SignalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VolumeRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRadarHits", x => new { x.SessionDate, x.Exchange, x.Symbol });
                });

            migrationBuilder.CreateTable(
                name: "SetupTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ForwardPriceT25 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForwardReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForwardReturnT10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForwardReturnT5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    HadMasterConfirm = table.Column<bool>(type: "bit", nullable: true),
                    MaxAdverseExcursionPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxFavorableExcursionPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpportunityForDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OpportunityRank = table.Column<int>(type: "int", nullable: true),
                    OpportunityScore = table.Column<int>(type: "int", nullable: true),
                    OutcomeBucket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    OutcomeBucketT10 = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    OutcomeBucketT5 = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    OutcomeMeasured = table.Column<bool>(type: "bit", nullable: false),
                    PeakGainPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PredictedHitPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ScoreBreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SessionVolume = table.Column<long>(type: "bigint", nullable: true),
                    SetupDna = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SwingMetricsMeasured = table.Column<bool>(type: "bit", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TradeState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TradeStateReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupTracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShadowWeightPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ForwardReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutcomeBucket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    OutcomeMeasured = table.Column<bool>(type: "bit", nullable: false),
                    PredictedHitPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    WeightMultiplier = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShadowWeightPicks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShadowWeightSummaries",
                columns: table => new
                {
                    WeightMultiplier = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    FlatCount = table.Column<int>(type: "int", nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    IsLeader = table.Column<bool>(type: "bit", nullable: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false),
                    MeasuredCount = table.Column<int>(type: "int", nullable: false),
                    SuccessRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShadowWeightSummaries", x => x.WeightMultiplier);
                });

            migrationBuilder.CreateTable(
                name: "StockCriterionDetails",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Horizon = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CriterionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Bias = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InvalidatedBase = table.Column<bool>(type: "bit", nullable: false),
                    MarketPhase = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MatchedOutcome = table.Column<bool>(type: "bit", nullable: false),
                    MaxAdversePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxFavorablePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NextDayChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PlaybookId = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    RelativeStrengthForward = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    ScoreBucket = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCriterionDetails", x => new { x.AsOfDate, x.Horizon, x.Symbol, x.CriterionId });
                });

            migrationBuilder.CreateTable(
                name: "StockCriterionScores",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CompositeScore = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextDayChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCriterionScores", x => new { x.AsOfDate, x.Symbol });
                });

            migrationBuilder.CreateTable(
                name: "TradeJournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BuyScore = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EngineVerdict = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PredictedHit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SetupDna = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SizePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeJournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VipAlertFires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtrPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    BuyScore = table.Column<int>(type: "int", nullable: true),
                    DistMa20Percent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FeaturesComplete = table.Column<bool>(type: "bit", nullable: false),
                    FirePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ForeignNet = table.Column<long>(type: "bigint", nullable: true),
                    GainFromOpenPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IntradayMaePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IntradayMeasured = table.Column<bool>(type: "bit", nullable: false),
                    IntradayMeasuredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IntradayMfePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IntradayReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LlmDecision = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LlmLatencyMs = table.Column<int>(type: "int", nullable: true),
                    LlmModel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LlmReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LlmShadowMode = table.Column<bool>(type: "bit", nullable: false),
                    Ma10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Ma20 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Ma50 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MarketPhase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MlModelActive = table.Column<bool>(type: "bit", nullable: false),
                    MlProbAtFire = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OpenPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PacedVolumeRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PredictedHitPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PropNet = table.Column<long>(type: "bigint", nullable: true),
                    Rs5dPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SellContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionHighSinceFire = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SessionLowSinceFire = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SessionPressure = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Signal = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UptrendLong = table.Column<bool>(type: "bit", nullable: true),
                    VsaLabel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VipAlertFires", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyCriterionReviews",
                columns: table => new
                {
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CriterionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Accuracy7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgMfe7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgScore7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Edge7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HitCount7d = table.Column<int>(type: "int", nullable: false),
                    InvalidationRate7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reliability7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCount7d = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyCriterionReviews", x => new { x.WeekStartDate, x.CriterionId });
                });

            migrationBuilder.CreateTable(
                name: "WeeklyOpportunityReviews",
                columns: table => new
                {
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BuyPoint1Count = table.Column<int>(type: "int", nullable: false),
                    BuyPoint1SuccessRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BuyPoint2Count = table.Column<int>(type: "int", nullable: false),
                    BuyPoint2SuccessRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CutAllCount = table.Column<int>(type: "int", nullable: false),
                    CutLoss1Count = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    FailedRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FlatCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    MeasuredCount = table.Column<int>(type: "int", nullable: false),
                    OpportunityCount = table.Column<int>(type: "int", nullable: false),
                    OpportunitySuccessRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SuccessRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TotalTracked = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyOpportunityReviews", x => x.WeekStartDate);
                });

            migrationBuilder.CreateTable(
                name: "PositionSellLegs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceSource = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RemainingSizeAfter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SellDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Signal = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SoldSize = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionSellLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionSellLegs_MasterAlertPositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "MasterAlertPositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriterionGroupDailyAccuracies_AsOfDate",
                table: "CriterionGroupDailyAccuracies",
                column: "AsOfDate");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionGroupDailyAccuracies_AsOfDate_PlaybookId",
                table: "CriterionGroupDailyAccuracies",
                columns: new[] { "AsOfDate", "PlaybookId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriterionGroupWeeklyReviews_WeekStartDate",
                table: "CriterionGroupWeeklyReviews",
                column: "WeekStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCriterionAccuracies_AsOfDate",
                table: "DailyCriterionAccuracies",
                column: "AsOfDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCriterionAccuracies_AsOfDate_PlaybookId",
                table: "DailyCriterionAccuracies",
                columns: new[] { "AsOfDate", "PlaybookId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyOpportunities_ForTradingDate",
                table: "DailyOpportunities",
                column: "ForTradingDate");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyRecoveryRadar_ForTradingDate",
                table: "EarlyRecoveryRadar",
                column: "ForTradingDate");

            migrationBuilder.CreateIndex(
                name: "IX_MasterAlertPositions_IsClosed",
                table: "MasterAlertPositions",
                column: "IsClosed");

            migrationBuilder.CreateIndex(
                name: "IX_MasterAlertPositions_IsClosed_RealizedMeasured",
                table: "MasterAlertPositions",
                columns: new[] { "IsClosed", "RealizedMeasured" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterAlertPositions_Symbol_IsClosed",
                table: "MasterAlertPositions",
                columns: new[] { "Symbol", "IsClosed" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionSellLegs_PositionId_Signal",
                table: "PositionSellLegs",
                columns: new[] { "PositionId", "Signal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PositionSellLegs_Symbol_SellDate",
                table: "PositionSellLegs",
                columns: new[] { "Symbol", "SellDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SectorWaveRegimes_TradingDate",
                table: "SectorWaveRegimes",
                column: "TradingDate");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRadarHits_SessionDate_Exchange",
                table: "SessionRadarHits",
                columns: new[] { "SessionDate", "Exchange" });

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_OutcomeMeasured",
                table: "SetupTracks",
                column: "OutcomeMeasured");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_PositionId",
                table: "SetupTracks",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_SwingMetricsMeasured",
                table: "SetupTracks",
                column: "SwingMetricsMeasured");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_Symbol_SourceType_EntryDate",
                table: "SetupTracks",
                columns: new[] { "Symbol", "SourceType", "EntryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_WeekStartDate",
                table: "SetupTracks",
                column: "WeekStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ShadowWeightPicks_ForTradingDate_WeightMultiplier_Symbol",
                table: "ShadowWeightPicks",
                columns: new[] { "ForTradingDate", "WeightMultiplier", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCriterionDetails_AsOfDate_CriterionId",
                table: "StockCriterionDetails",
                columns: new[] { "AsOfDate", "CriterionId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCriterionDetails_AsOfDate_GroupId",
                table: "StockCriterionDetails",
                columns: new[] { "AsOfDate", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCriterionScores_AsOfDate",
                table: "StockCriterionScores",
                column: "AsOfDate");

            migrationBuilder.CreateIndex(
                name: "IX_TradeJournalEntries_UserId_CreatedAt",
                table: "TradeJournalEntries",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VipAlertFires_IntradayMeasured_SessionDate",
                table: "VipAlertFires",
                columns: new[] { "IntradayMeasured", "SessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VipAlertFires_SessionDate_Symbol",
                table: "VipAlertFires",
                columns: new[] { "SessionDate", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyCriterionReviews_WeekStartDate",
                table: "WeeklyCriterionReviews",
                column: "WeekStartDate");
        }
    }
}

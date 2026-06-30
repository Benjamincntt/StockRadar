using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSwingTradingLearning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ForwardReturnT5",
                table: "SetupTracks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForwardReturnT10",
                table: "SetupTracks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeBucketT5",
                table: "SetupTracks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeBucketT10",
                table: "SetupTracks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxFavorableExcursionPercent",
                table: "SetupTracks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxAdverseExcursionPercent",
                table: "SetupTracks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SwingMetricsMeasured",
                table: "SetupTracks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HadMasterConfirm",
                table: "SetupTracks",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShadowWeightPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeightMultiplier = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PredictedHitPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OutcomeMeasured = table.Column<bool>(type: "bit", nullable: false),
                    ForwardReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OutcomeBucket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_ShadowWeightPicks", x => x.Id));

            migrationBuilder.CreateTable(
                name: "ShadowWeightSummaries",
                columns: table => new
                {
                    WeightMultiplier = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MeasuredCount = table.Column<int>(type: "int", nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    FlatCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    SuccessRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false),
                    IsLeader = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ShadowWeightSummaries", x => x.WeightMultiplier));

            migrationBuilder.CreateTable(
                name: "EntryTimingStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TopOnlyMeasured = table.Column<int>(type: "int", nullable: false),
                    TopOnlyGood = table.Column<int>(type: "int", nullable: false),
                    ConfirmMeasured = table.Column<int>(type: "int", nullable: false),
                    ConfirmGood = table.Column<int>(type: "int", nullable: false),
                    PreferMasterConfirm = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_EntryTimingStates", x => x.Id));

            migrationBuilder.CreateTable(
                name: "TradeJournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SizePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EngineVerdict = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BuyScore = table.Column<int>(type: "int", nullable: true),
                    PredictedHit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SetupDna = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_TradeJournalEntries", x => x.Id));

            migrationBuilder.CreateTable(
                name: "PersonalCalibrationStates",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_PersonalCalibrationStates", x => x.UserId));

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_SwingMetricsMeasured",
                table: "SetupTracks",
                column: "SwingMetricsMeasured");

            migrationBuilder.CreateIndex(
                name: "IX_ShadowWeightPicks_ForTradingDate_WeightMultiplier_Symbol",
                table: "ShadowWeightPicks",
                columns: new[] { "ForTradingDate", "WeightMultiplier", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeJournalEntries_UserId_CreatedAt",
                table: "TradeJournalEntries",
                columns: new[] { "UserId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PersonalCalibrationStates");
            migrationBuilder.DropTable(name: "TradeJournalEntries");
            migrationBuilder.DropTable(name: "EntryTimingStates");
            migrationBuilder.DropTable(name: "ShadowWeightSummaries");
            migrationBuilder.DropTable(name: "ShadowWeightPicks");

            migrationBuilder.DropIndex(name: "IX_SetupTracks_SwingMetricsMeasured", table: "SetupTracks");

            migrationBuilder.DropColumn(name: "ForwardReturnT5", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "ForwardReturnT10", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "OutcomeBucketT5", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "OutcomeBucketT10", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "MaxFavorableExcursionPercent", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "MaxAdverseExcursionPercent", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "SwingMetricsMeasured", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "HadMasterConfirm", table: "SetupTracks");
        }
    }
}

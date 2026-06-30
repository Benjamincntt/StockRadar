using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCriterionAnalystTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CriterionGroupDailyAccuracies",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccuracyPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CriterionCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HitCount = table.Column<int>(type: "int", nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionGroupDailyAccuracies", x => new { x.AsOfDate, x.GroupId });
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
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SampleCount7d = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionWeights", x => x.CriterionId);
                });

            migrationBuilder.CreateTable(
                name: "DailyCriterionAccuracies",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CriterionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccuracyPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HitCount = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCriterionAccuracies", x => new { x.AsOfDate, x.CriterionId });
                });

            migrationBuilder.CreateTable(
                name: "StockCriterionDetails",
                columns: table => new
                {
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CriterionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Bias = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MatchedOutcome = table.Column<bool>(type: "bit", nullable: false),
                    NextDayChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCriterionDetails", x => new { x.AsOfDate, x.Symbol, x.CriterionId });
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
                name: "WeeklyCriterionReviews",
                columns: table => new
                {
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CriterionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Accuracy7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgScore7d = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HitCount7d = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TotalCount7d = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyCriterionReviews", x => new { x.WeekStartDate, x.CriterionId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriterionGroupDailyAccuracies_AsOfDate",
                table: "CriterionGroupDailyAccuracies",
                column: "AsOfDate");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionGroupWeeklyReviews_WeekStartDate",
                table: "CriterionGroupWeeklyReviews",
                column: "WeekStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCriterionAccuracies_AsOfDate",
                table: "DailyCriterionAccuracies",
                column: "AsOfDate");

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
                name: "IX_WeeklyCriterionReviews_WeekStartDate",
                table: "WeeklyCriterionReviews",
                column: "WeekStartDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CriterionGroupDailyAccuracies");
            migrationBuilder.DropTable(name: "CriterionGroupWeeklyReviews");
            migrationBuilder.DropTable(name: "CriterionWeights");
            migrationBuilder.DropTable(name: "DailyCriterionAccuracies");
            migrationBuilder.DropTable(name: "StockCriterionDetails");
            migrationBuilder.DropTable(name: "StockCriterionScores");
            migrationBuilder.DropTable(name: "WeeklyCriterionReviews");
        }
    }
}

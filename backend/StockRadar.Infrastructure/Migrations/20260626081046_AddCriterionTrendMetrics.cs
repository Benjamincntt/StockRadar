using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCriterionTrendMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvgMfe7d",
                table: "WeeklyCriterionReviews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BreakdownJson",
                table: "WeeklyCriterionReviews",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Edge7d",
                table: "WeeklyCriterionReviews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InvalidationRate7d",
                table: "WeeklyCriterionReviews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Reliability7d",
                table: "WeeklyCriterionReviews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "InvalidatedBase",
                table: "StockCriterionDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MarketPhase",
                table: "StockCriterionDetails",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxAdversePercent",
                table: "StockCriterionDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxFavorablePercent",
                table: "StockCriterionDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RelativeStrengthForward",
                table: "StockCriterionDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ScoreBucket",
                table: "StockCriterionDetails",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "AvgMaePercent",
                table: "DailyCriterionAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AvgMfePercent",
                table: "DailyCriterionAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaselinePercent",
                table: "DailyCriterionAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BreakdownJson",
                table: "DailyCriterionAccuracies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "EdgePercent",
                table: "DailyCriterionAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InvalidationRatePercent",
                table: "DailyCriterionAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReliabilityScore",
                table: "DailyCriterionAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Edge7d",
                table: "CriterionWeights",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Reliability7d",
                table: "CriterionWeights",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EdgePercent",
                table: "CriterionGroupDailyAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReliabilityScore",
                table: "CriterionGroupDailyAccuracies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvgMfe7d",
                table: "WeeklyCriterionReviews");

            migrationBuilder.DropColumn(
                name: "BreakdownJson",
                table: "WeeklyCriterionReviews");

            migrationBuilder.DropColumn(
                name: "Edge7d",
                table: "WeeklyCriterionReviews");

            migrationBuilder.DropColumn(
                name: "InvalidationRate7d",
                table: "WeeklyCriterionReviews");

            migrationBuilder.DropColumn(
                name: "Reliability7d",
                table: "WeeklyCriterionReviews");

            migrationBuilder.DropColumn(
                name: "InvalidatedBase",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "MarketPhase",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "MaxAdversePercent",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "MaxFavorablePercent",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "RelativeStrengthForward",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "ScoreBucket",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "AvgMaePercent",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "AvgMfePercent",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "BaselinePercent",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "BreakdownJson",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "EdgePercent",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "InvalidationRatePercent",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "ReliabilityScore",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "Edge7d",
                table: "CriterionWeights");

            migrationBuilder.DropColumn(
                name: "Reliability7d",
                table: "CriterionWeights");

            migrationBuilder.DropColumn(
                name: "EdgePercent",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.DropColumn(
                name: "ReliabilityScore",
                table: "CriterionGroupDailyAccuracies");
        }
    }
}

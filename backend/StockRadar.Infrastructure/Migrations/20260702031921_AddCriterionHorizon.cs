using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCriterionHorizon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StockCriterionDetails",
                table: "StockCriterionDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.AddColumn<int>(
                name: "Horizon",
                table: "StockCriterionDetails",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "Horizon",
                table: "DailyCriterionAccuracies",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "Horizon",
                table: "CriterionGroupDailyAccuracies",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockCriterionDetails",
                table: "StockCriterionDetails",
                columns: new[] { "AsOfDate", "Horizon", "Symbol", "CriterionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies",
                columns: new[] { "AsOfDate", "Horizon", "CriterionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies",
                columns: new[] { "AsOfDate", "Horizon", "GroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StockCriterionDetails",
                table: "StockCriterionDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.DropColumn(
                name: "Horizon",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "Horizon",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "Horizon",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockCriterionDetails",
                table: "StockCriterionDetails",
                columns: new[] { "AsOfDate", "Symbol", "CriterionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies",
                columns: new[] { "AsOfDate", "CriterionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies",
                columns: new[] { "AsOfDate", "GroupId" });
        }
    }
}

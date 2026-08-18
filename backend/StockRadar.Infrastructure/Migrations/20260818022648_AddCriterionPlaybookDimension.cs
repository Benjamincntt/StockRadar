using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCriterionPlaybookDimension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.AddColumn<string>(
                name: "PlaybookId",
                table: "StockCriterionDetails",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<string>(
                name: "PlaybookId",
                table: "DailyCriterionAccuracies",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<string>(
                name: "PlaybookId",
                table: "CriterionGroupDailyAccuracies",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies",
                columns: new[] { "AsOfDate", "Horizon", "PlaybookId", "CriterionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies",
                columns: new[] { "AsOfDate", "Horizon", "PlaybookId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCriterionAccuracies_AsOfDate_PlaybookId",
                table: "DailyCriterionAccuracies",
                columns: new[] { "AsOfDate", "PlaybookId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriterionGroupDailyAccuracies_AsOfDate_PlaybookId",
                table: "CriterionGroupDailyAccuracies",
                columns: new[] { "AsOfDate", "PlaybookId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropIndex(
                name: "IX_DailyCriterionAccuracies_AsOfDate_PlaybookId",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.DropIndex(
                name: "IX_CriterionGroupDailyAccuracies_AsOfDate_PlaybookId",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.DropColumn(
                name: "PlaybookId",
                table: "StockCriterionDetails");

            migrationBuilder.DropColumn(
                name: "PlaybookId",
                table: "DailyCriterionAccuracies");

            migrationBuilder.DropColumn(
                name: "PlaybookId",
                table: "CriterionGroupDailyAccuracies");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyCriterionAccuracies",
                table: "DailyCriterionAccuracies",
                columns: new[] { "AsOfDate", "Horizon", "CriterionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CriterionGroupDailyAccuracies",
                table: "CriterionGroupDailyAccuracies",
                columns: new[] { "AsOfDate", "Horizon", "GroupId" });
        }
    }
}

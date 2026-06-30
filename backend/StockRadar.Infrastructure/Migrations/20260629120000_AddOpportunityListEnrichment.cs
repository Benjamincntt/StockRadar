using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddOpportunityListEnrichment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BuyScore",
            table: "DailyOpportunities",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EntryPointJson",
            table: "DailyOpportunities",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExplainJson",
            table: "DailyOpportunities",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PredictedHitPercent",
            table: "DailyOpportunities",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PredictedSampleCount",
            table: "DailyOpportunities",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Recommendation",
            table: "DailyOpportunities",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SetupDna",
            table: "DailyOpportunities",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BuyScore", table: "DailyOpportunities");
        migrationBuilder.DropColumn(name: "EntryPointJson", table: "DailyOpportunities");
        migrationBuilder.DropColumn(name: "ExplainJson", table: "DailyOpportunities");
        migrationBuilder.DropColumn(name: "PredictedHitPercent", table: "DailyOpportunities");
        migrationBuilder.DropColumn(name: "PredictedSampleCount", table: "DailyOpportunities");
        migrationBuilder.DropColumn(name: "Recommendation", table: "DailyOpportunities");
        migrationBuilder.DropColumn(name: "SetupDna", table: "DailyOpportunities");
    }
}

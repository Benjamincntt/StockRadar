using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations;

public partial class AddStockUniverseAndAnalysisRuns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "AvgVolume30d",
            table: "Stocks",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "Exchange",
            table: "Stocks",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateOnly>(
            name: "FirstTradeDate",
            table: "Stocks",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "Stocks",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "TradingRestricted",
            table: "Stocks",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "TradingStatus",
            table: "Stocks",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UniverseUpdatedAt",
            table: "Stocks",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DailyAnalysisRuns",
            columns: table => new
            {
                ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                StocksScored = table.Column<int>(type: "int", nullable: false),
                OpportunitiesSaved = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyAnalysisRuns", x => x.ForTradingDate);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Stocks_IsActive",
            table: "Stocks",
            column: "IsActive");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DailyAnalysisRuns");

        migrationBuilder.DropIndex(
            name: "IX_Stocks_IsActive",
            table: "Stocks");

        migrationBuilder.DropColumn(
            name: "AvgVolume30d",
            table: "Stocks");

        migrationBuilder.DropColumn(
            name: "Exchange",
            table: "Stocks");

        migrationBuilder.DropColumn(
            name: "FirstTradeDate",
            table: "Stocks");

        migrationBuilder.DropColumn(
            name: "IsActive",
            table: "Stocks");

        migrationBuilder.DropColumn(
            name: "TradingRestricted",
            table: "Stocks");

        migrationBuilder.DropColumn(
            name: "TradingStatus",
            table: "Stocks");

        migrationBuilder.DropColumn(
            name: "UniverseUpdatedAt",
            table: "Stocks");
    }
}

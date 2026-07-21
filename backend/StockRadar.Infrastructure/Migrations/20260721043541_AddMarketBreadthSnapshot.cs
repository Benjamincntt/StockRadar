using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketBreadthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketBreadthSnapshots",
                columns: table => new
                {
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UniverseCount = table.Column<int>(type: "int", nullable: false),
                    PctAboveMa20 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctAboveMa50 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctNewLow20 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctUp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctDown = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FloorCount = table.Column<int>(type: "int", nullable: false),
                    CeilingCount = table.Column<int>(type: "int", nullable: false),
                    MedianReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MedianTurnover = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VnIndexDrawdownPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VnIndexDistanceToMa20Percent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VnIndexAboveMa20 = table.Column<bool>(type: "bit", nullable: false),
                    VnIndexReclaimedMa20 = table.Column<bool>(type: "bit", nullable: false),
                    Regime = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ImproveStreak = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketBreadthSnapshots", x => x.TradingDate);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketBreadthSnapshots");
        }
    }
}

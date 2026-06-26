using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyOpportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyOpportunities",
                columns: table => new
                {
                    ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChangePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VolumeRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyOpportunities", x => new { x.ForTradingDate, x.Symbol });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyOpportunities_ForTradingDate",
                table: "DailyOpportunities",
                column: "ForTradingDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyOpportunities");
        }
    }
}

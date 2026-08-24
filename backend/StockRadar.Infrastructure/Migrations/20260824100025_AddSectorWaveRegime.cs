using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectorWaveRegime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectorWaveRegimes",
                columns: table => new
                {
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionsSinceActivation = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveLowVolumeSessions = table.Column<int>(type: "int", nullable: false),
                    FailedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorWaveRegimes", x => new { x.Sector, x.TradingDate });
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectorWaveRegimes_TradingDate",
                table: "SectorWaveRegimes",
                column: "TradingDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorWaveRegimes");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRadarHits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionRadarHits",
                columns: table => new
                {
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChangePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SessionVolume = table.Column<long>(type: "bigint", nullable: false),
                    VolumeRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RelativeStrength = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SignalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRadarHits", x => new { x.SessionDate, x.Exchange, x.Symbol });
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionRadarHits_SessionDate_Exchange",
                table: "SessionRadarHits",
                columns: new[] { "SessionDate", "Exchange" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionRadarHits");
        }
    }
}

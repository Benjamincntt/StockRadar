using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockRadar.Infrastructure.Persistence;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260714120000_AddMasterAlertPositions")]
    public partial class AddMasterAlertPositions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MasterAlertPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PeakPriceSinceEntry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentPositionSize = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FiredAlertKindsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketPhaseAtEntry = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_MasterAlertPositions", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_MasterAlertPositions_Symbol_IsClosed",
                table: "MasterAlertPositions",
                columns: new[] { "Symbol", "IsClosed" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterAlertPositions_IsClosed",
                table: "MasterAlertPositions",
                column: "IsClosed");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "MasterAlertPositions");
    }
}

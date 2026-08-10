using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockRadar.Infrastructure.Persistence;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260810140000_AddVipAlertFires")]
    public partial class AddVipAlertFires : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VipAlertFires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Signal = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FirePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GainFromOpenPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PacedVolumeRatio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MlProbAtFire = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MlModelActive = table.Column<bool>(type: "bit", nullable: false),
                    BuyScore = table.Column<int>(type: "int", nullable: true),
                    PredictedHitPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MarketPhase = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Rs5dPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AtrPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DistMa20Percent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Ma10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Ma20 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Ma50 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UptrendLong = table.Column<bool>(type: "bit", nullable: true),
                    ForeignNet = table.Column<long>(type: "bigint", nullable: true),
                    PropNet = table.Column<long>(type: "bigint", nullable: true),
                    SessionPressure = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    VsaLabel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FeaturesComplete = table.Column<bool>(type: "bit", nullable: false),
                    IntradayMeasured = table.Column<bool>(type: "bit", nullable: false),
                    IntradayReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IntradayMfePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IntradayMaePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SessionHighSinceFire = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SessionLowSinceFire = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IntradayMeasuredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_VipAlertFires", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_VipAlertFires_SessionDate_Symbol",
                table: "VipAlertFires",
                columns: new[] { "SessionDate", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_VipAlertFires_IntradayMeasured_SessionDate",
                table: "VipAlertFires",
                columns: new[] { "IntradayMeasured", "SessionDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "VipAlertFires");
    }
}

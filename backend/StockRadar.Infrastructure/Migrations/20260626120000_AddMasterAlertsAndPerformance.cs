using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockRadar.Infrastructure.Persistence;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260626120000_AddMasterAlertsAndPerformance")]
    public partial class AddMasterAlertsAndPerformance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SetupTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpportunityForDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OpportunityRank = table.Column<int>(type: "int", nullable: true),
                    OpportunityScore = table.Column<int>(type: "int", nullable: true),
                    SessionChangePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SessionVolume = table.Column<long>(type: "bigint", nullable: true),
                    PeakGainPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OutcomeMeasured = table.Column<bool>(type: "bit", nullable: false),
                    ForwardPriceT25 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForwardReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OutcomeBucket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupTracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyOpportunityReviews",
                columns: table => new
                {
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalTracked = table.Column<int>(type: "int", nullable: false),
                    MeasuredCount = table.Column<int>(type: "int", nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    FlatCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    SuccessRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FailedRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpportunityCount = table.Column<int>(type: "int", nullable: false),
                    BuyPoint1Count = table.Column<int>(type: "int", nullable: false),
                    BuyPoint2Count = table.Column<int>(type: "int", nullable: false),
                    CutLoss1Count = table.Column<int>(type: "int", nullable: false),
                    CutAllCount = table.Column<int>(type: "int", nullable: false),
                    OpportunitySuccessRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BuyPoint1SuccessRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BuyPoint2SuccessRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyOpportunityReviews", x => x.WeekStartDate);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_OutcomeMeasured",
                table: "SetupTracks",
                column: "OutcomeMeasured");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_Symbol_SourceType_EntryDate",
                table: "SetupTracks",
                columns: new[] { "Symbol", "SourceType", "EntryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_WeekStartDate",
                table: "SetupTracks",
                column: "WeekStartDate");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SetupTracks");
            migrationBuilder.DropTable(name: "WeeklyOpportunityReviews");
        }
    }
}

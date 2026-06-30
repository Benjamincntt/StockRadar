using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShadowAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShadowPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForTradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VariantMinPassScore = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PredictedHitPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OutcomeMeasured = table.Column<bool>(type: "bit", nullable: false),
                    ForwardReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OutcomeBucket = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShadowPicks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShadowVariantSummaries",
                columns: table => new
                {
                    VariantMinPassScore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeasuredCount = table.Column<int>(type: "int", nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    FlatCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    SuccessRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false),
                    IsLeader = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShadowVariantSummaries", x => x.VariantMinPassScore);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShadowPicks_ForTradingDate_VariantMinPassScore_Symbol",
                table: "ShadowPicks",
                columns: new[] { "ForTradingDate", "VariantMinPassScore", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShadowPicks_OutcomeMeasured",
                table: "ShadowPicks",
                column: "OutcomeMeasured");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShadowPicks");

            migrationBuilder.DropTable(
                name: "ShadowVariantSummaries");
        }
    }
}

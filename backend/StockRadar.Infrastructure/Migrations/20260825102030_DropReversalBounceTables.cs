using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropReversalBounceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketBreadthSnapshots");

            migrationBuilder.DropTable(
                name: "ReversalCandidateSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketBreadthSnapshots",
                columns: table => new
                {
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CeilingCount = table.Column<int>(type: "int", nullable: false),
                    FloorCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImproveStreak = table.Column<int>(type: "int", nullable: false),
                    MedianReturnPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MedianTurnover = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctAboveMa20 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctAboveMa50 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctDown = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctNewLow20 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PctUp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Regime = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UniverseCount = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    VnIndexAboveMa20 = table.Column<bool>(type: "bit", nullable: false),
                    VnIndexDistanceToMa20Percent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VnIndexDrawdownPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VnIndexReclaimedMa20 = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketBreadthSnapshots", x => x.TradingDate);
                });

            migrationBuilder.CreateTable(
                name: "ReversalCandidateSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlgorithmParametersHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CapitulationClose = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CapitulationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CapitulationLow = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntryReference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FirstTarget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InvalidationPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActionable = table.Column<bool>(type: "bit", nullable: false),
                    MarketRegime = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MaxEntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PositionFactor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecoveryAttemptCount = table.Column<int>(type: "int", nullable: false),
                    RewardToRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RiskWarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RunBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ScoreCapitulation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreDemand = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreLiquidity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreRelativeStrength = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreRiskPenalty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreStabilization = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SetupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StrategyVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TimeStopSessions = table.Column<int>(type: "int", nullable: true),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReversalCandidateSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReversalCandidateSnapshots_SetupId",
                table: "ReversalCandidateSnapshots",
                column: "SetupId");

            migrationBuilder.CreateIndex(
                name: "IX_ReversalCandidateSnapshots_Symbol",
                table: "ReversalCandidateSnapshots",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_ReversalCandidateSnapshots_TradingDate",
                table: "ReversalCandidateSnapshots",
                column: "TradingDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReversalCandidateSnapshots_TradingDate_Symbol_StrategyVersion_SetupId",
                table: "ReversalCandidateSnapshots",
                columns: new[] { "TradingDate", "Symbol", "StrategyVersion", "SetupId" },
                unique: true);
        }
    }
}

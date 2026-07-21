using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReversalBounceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReversalCandidateSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SetupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapitulationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CapitulationLow = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CapitulationClose = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RecoveryAttemptCount = table.Column<int>(type: "int", nullable: false),
                    ScoreCapitulation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreStabilization = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreDemand = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreRelativeStrength = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreLiquidity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreRiskPenalty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarketRegime = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActionable = table.Column<bool>(type: "bit", nullable: false),
                    EntryReference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxEntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InvalidationPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FirstTarget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RewardToRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PositionFactor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TimeStopSessions = table.Column<int>(type: "int", nullable: true),
                    RiskWarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StrategyVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AlgorithmParametersHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    RunBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReversalCandidateSnapshots");
        }
    }
}

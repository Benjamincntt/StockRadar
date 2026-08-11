using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRealizedPnl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PositionId",
                table: "SetupTracks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HoldingSessions",
                table: "MasterAlertPositions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPositionSize",
                table: "MasterAlertPositions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RealizedFeeProfile",
                table: "MasterAlertPositions",
                type: "nvarchar(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RealizedGrossReturnPercent",
                table: "MasterAlertPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RealizedMeasured",
                table: "MasterAlertPositions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RealizedMeasuredAt",
                table: "MasterAlertPositions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealizedOutcomeBucket",
                table: "MasterAlertPositions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RealizedReturnOnDeployedPercent",
                table: "MasterAlertPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealizedStatus",
                table: "MasterAlertPositions",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RealizedWeightedReturnPercent",
                table: "MasterAlertPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PositionSellLegs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Signal = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SellDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SoldSize = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RemainingSizeAfter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PriceSource = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionSellLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionSellLegs_MasterAlertPositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "MasterAlertPositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetupTracks_PositionId",
                table: "SetupTracks",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterAlertPositions_IsClosed_RealizedMeasured",
                table: "MasterAlertPositions",
                columns: new[] { "IsClosed", "RealizedMeasured" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionSellLegs_PositionId_Signal",
                table: "PositionSellLegs",
                columns: new[] { "PositionId", "Signal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PositionSellLegs_Symbol_SellDate",
                table: "PositionSellLegs",
                columns: new[] { "Symbol", "SellDate" });

            // Data-fix: các vị thế cũ chưa có MaxPositionSize (cột mới, default 0) — suy ra từ dấu vết đã có.
            // Deterministic + idempotent theo WHERE MaxPositionSize = 0 nên chạy lại an toàn.
            migrationBuilder.Sql(@"
UPDATE MasterAlertPositions
SET MaxPositionSize = CASE
      WHEN FiredAlertKindsJson LIKE '%MuaDiem2%' THEN 1.0
      WHEN CurrentPositionSize > 0 THEN CurrentPositionSize
      ELSE 0.5 END
WHERE MaxPositionSize = 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PositionSellLegs");

            migrationBuilder.DropIndex(
                name: "IX_SetupTracks_PositionId",
                table: "SetupTracks");

            migrationBuilder.DropIndex(
                name: "IX_MasterAlertPositions_IsClosed_RealizedMeasured",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "PositionId",
                table: "SetupTracks");

            migrationBuilder.DropColumn(
                name: "HoldingSessions",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "MaxPositionSize",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedFeeProfile",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedGrossReturnPercent",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedMeasured",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedMeasuredAt",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedOutcomeBucket",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedReturnOnDeployedPercent",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedStatus",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "RealizedWeightedReturnPercent",
                table: "MasterAlertPositions");
        }
    }
}

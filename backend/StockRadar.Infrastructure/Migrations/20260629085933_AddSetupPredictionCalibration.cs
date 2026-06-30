using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSetupPredictionCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PredictedHitPercent",
                table: "SetupTracks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetupDna",
                table: "SetupTracks",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HitCalibrationBuckets",
                columns: table => new
                {
                    BucketId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PredictedMin = table.Column<int>(type: "int", nullable: false),
                    PredictedMax = table.Column<int>(type: "int", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    PredictedMidPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualHitRatePercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CalibrationFactor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HitCalibrationBuckets", x => x.BucketId);
                });

            migrationBuilder.CreateTable(
                name: "HitCalibrationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GlobalFactor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSamples = table.Column<int>(type: "int", nullable: false),
                    PredictionBiasPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HitCalibrationStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HitCalibrationBuckets");

            migrationBuilder.DropTable(
                name: "HitCalibrationStates");

            migrationBuilder.DropColumn(
                name: "PredictedHitPercent",
                table: "SetupTracks");

            migrationBuilder.DropColumn(
                name: "SetupDna",
                table: "SetupTracks");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSellRegimeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AnchorWindowStart",
                table: "MasterAlertPositions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EntryBarLow",
                table: "MasterAlertPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitRegime",
                table: "MasterAlertPositions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverheadBaseHigh",
                table: "MasterAlertPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverheadBaseLow",
                table: "MasterAlertPositions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellContextJson",
                table: "VipAlertFires",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellContextJson",
                table: "VipAlertFires");

            migrationBuilder.DropColumn(
                name: "AnchorWindowStart",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "EntryBarLow",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "ExitRegime",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "OverheadBaseHigh",
                table: "MasterAlertPositions");

            migrationBuilder.DropColumn(
                name: "OverheadBaseLow",
                table: "MasterAlertPositions");
        }
    }
}

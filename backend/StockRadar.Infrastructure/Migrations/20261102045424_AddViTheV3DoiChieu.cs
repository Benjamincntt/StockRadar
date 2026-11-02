using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddViTheV3DoiChieu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DoiChieuBan",
                table: "ViTheV3",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DoiChieuLuc",
                table: "ViTheV3",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DoiChieuMua",
                table: "ViTheV3",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoiChieuBan",
                table: "ViTheV3");

            migrationBuilder.DropColumn(
                name: "DoiChieuLuc",
                table: "ViTheV3");

            migrationBuilder.DropColumn(
                name: "DoiChieuMua",
                table: "ViTheV3");
        }
    }
}

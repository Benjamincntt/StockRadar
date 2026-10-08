using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSellNotificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoaiBaoBan",
                table: "KetQuaKichBan",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianBaoBan",
                table: "KetQuaKichBan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianCanhBaoBan",
                table: "KetQuaKichBan",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoaiBaoBan",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "ThoiGianBaoBan",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "ThoiGianCanhBaoBan",
                table: "KetQuaKichBan");
        }
    }
}

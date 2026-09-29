using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemFieldOutcomePha3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiaThoat",
                table: "KetQuaKichBan",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KetQuaDoLuong",
                table: "KetQuaKichBan",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NgayThoat",
                table: "KetQuaKichBan",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhanTramLoiNhuan",
                table: "KetQuaKichBan",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TyLeLaiLoThucTe",
                table: "KetQuaKichBan",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GiaThoat",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "KetQuaDoLuong",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "NgayThoat",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "PhanTramLoiNhuan",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "TyLeLaiLoThucTe",
                table: "KetQuaKichBan");
        }
    }
}

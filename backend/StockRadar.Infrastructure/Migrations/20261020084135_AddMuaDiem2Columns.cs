using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMuaDiem2Columns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiaMuaDiem2ThucTe",
                table: "KetQuaKichBan",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianMuaDiem2",
                table: "KetQuaKichBan",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GiaMuaDiem2ThucTe",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "ThoiGianMuaDiem2",
                table: "KetQuaKichBan");
        }
    }
}

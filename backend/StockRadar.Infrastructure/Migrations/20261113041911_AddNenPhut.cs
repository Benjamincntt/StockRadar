using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNenPhut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NenPhutTheoPhien",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NgayPhien = table.Column<DateOnly>(type: "date", nullable: false),
                    SoNen = table.Column<int>(type: "int", nullable: false),
                    Nguon = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    DuLieuNen = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ThoiDiemGhi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NenPhutTheoPhien", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoChatLuongNenPhut",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NgayPhien = table.Column<DateOnly>(type: "date", nullable: false),
                    SoNenNhanDuoc = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    GioNenAtc = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    SoLanThu = table.Column<int>(type: "int", nullable: false),
                    ThongDiepLoi = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LanGhi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ThoiDiemGhi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoChatLuongNenPhut", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NenPhutTheoPhien_NgayPhien",
                table: "NenPhutTheoPhien",
                column: "NgayPhien");

            migrationBuilder.CreateIndex(
                name: "IX_NenPhutTheoPhien_Symbol_NgayPhien",
                table: "NenPhutTheoPhien",
                columns: new[] { "Symbol", "NgayPhien" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoChatLuongNenPhut_NgayPhien_TrangThai",
                table: "SoChatLuongNenPhut",
                columns: new[] { "NgayPhien", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_SoChatLuongNenPhut_Symbol_NgayPhien",
                table: "SoChatLuongNenPhut",
                columns: new[] { "Symbol", "NgayPhien" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NenPhutTheoPhien");

            migrationBuilder.DropTable(
                name: "SoChatLuongNenPhut");
        }
    }
}

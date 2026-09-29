using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemBangKetQuaKichBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KetQuaKichBan",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LoaiKichBan = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    DatBoiCanh = table.Column<bool>(type: "bit", nullable: false),
                    DatHinhThai = table.Column<bool>(type: "bit", nullable: false),
                    DatCoKichHoat = table.Column<bool>(type: "bit", nullable: false),
                    MucHoanThien = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    NgayDanhGia = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThoiGianKichHoat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KeHoachGiaoDichJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BangChupChiBaoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DanhSachBangChungJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiemXepHang = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LoiNhuanT1 = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    LoiNhuanT2 = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    LoiNhuanT3 = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Mfe = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Mae = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KetQuaKichBan", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KetQuaKichBan_NgayDanhGia",
                table: "KetQuaKichBan",
                column: "NgayDanhGia");

            migrationBuilder.CreateIndex(
                name: "IX_KetQuaKichBan_Symbol_LoaiKichBan_NgayDanhGia",
                table: "KetQuaKichBan",
                columns: new[] { "Symbol", "LoaiKichBan", "NgayDanhGia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KetQuaKichBan_TrangThai_NgayDanhGia",
                table: "KetQuaKichBan",
                columns: new[] { "TrangThai", "NgayDanhGia" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KetQuaKichBan");
        }
    }
}

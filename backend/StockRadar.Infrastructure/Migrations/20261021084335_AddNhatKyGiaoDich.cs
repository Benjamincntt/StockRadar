using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNhatKyGiaoDich : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NhatKyGiaoDich",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaChungKhoan = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Chieu = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ThoiDiemKhop = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GiaKhop = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KhoiLuong = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Phi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NguonTinHieu = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    MaTinHieu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GiaTinHieuChup = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LyDo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    KLieuChinh = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    VonDieuChinh = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TaoNgay = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapNhatNgay = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyGiaoDich", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NhatKyGiaoDich_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NhatKyTinHieuBoQua",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nguon = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    MaTinHieu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BoQuaNgay = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyTinHieuBoQua", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NhatKyTinHieuBoQua_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyGiaoDich_UserId_MaChungKhoan",
                table: "NhatKyGiaoDich",
                columns: new[] { "UserId", "MaChungKhoan" });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyGiaoDich_UserId_NguonTinHieu_MaTinHieu",
                table: "NhatKyGiaoDich",
                columns: new[] { "UserId", "NguonTinHieu", "MaTinHieu" });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyGiaoDich_UserId_ThoiDiemKhop",
                table: "NhatKyGiaoDich",
                columns: new[] { "UserId", "ThoiDiemKhop" });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyTinHieuBoQua_UserId_Nguon_MaTinHieu",
                table: "NhatKyTinHieuBoQua",
                columns: new[] { "UserId", "Nguon", "MaTinHieu" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhatKyGiaoDich");

            migrationBuilder.DropTable(
                name: "NhatKyTinHieuBoQua");
        }
    }
}

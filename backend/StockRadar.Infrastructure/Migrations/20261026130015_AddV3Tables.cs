using Microsoft.EntityFrameworkCore.Migrations;
using StockRadar.Infrastructure.Persistence;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// BUSINESS-RULE: Migration viết TAY theo chỉ dẫn spec 012 Phụ lục G — `dotnet ef migrations add`
    /// tự sinh sẽ tạo Up() SAI (recreate ~35 bảng vì snapshot chưa có thực thể V3).
    /// Up() chỉ thêm 3 bảng V3 (TinHieuV3 / ViTheV3 / ChiSoNgayV3);
    /// KHÔNG DROP KetQuaKichBan — việc xoá bảng V2 và xử lý hơn 4.200 dòng Nhật ký gắn
    /// tín hiệu V2 cũ (NguonTinHieu="V2") là quyết định RIÊNG chờ owner duyệt (Phụ lục G + FR-040).
    /// Snapshot/.Designer.cs: không có Designer cho migration viết tay này (ID đặt bằng attribute
    /// [Migration] ở trên); `ApplicationDbContextModelSnapshot.cs` đã được dựng lại 26/10/2026
    /// (tool sinh rồi bỏ migration tạm, chỉ giữ snapshot) nên snapshot ĐÃ gồm 3 thực thể V3
    /// với đúng các cột dưới đây — `migrations add` lần sau không còn recreate lại bảng V3.
    /// Hệ quả: thực thể V2 `KetQuaKichBan` không còn trong mô hình, nên migration xóa bảng đó
    /// (nếu owner duyệt) PHẢI viết tay `DropTable` — tool sẽ không tự sinh ra diff.
    /// Bổ sung (owner duyệt tải dữ liệu V3 kiểu mới): ViTheV3.GiaThamChieu+Exchange và
    /// ChiSoNgayV3.DanhSachUni — sửa trực tiếp trong file này vì migration CHƯA apply trên production.
    /// DB local đã apply bản cũ → đã ALTER ADD 3 cột (additive, không mất dữ liệu).
    /// </remarks>
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261026130015_AddV3Tables")]
    public partial class AddV3Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TinHieuV3",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Loai = table.Column<int>(type: "int", nullable: false),
                    NgayTinHieu = table.Column<DateOnly>(type: "date", nullable: false),
                    GiaTinHieu = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Hang = table.Column<int>(type: "int", nullable: false),
                    GiaTriXepHang = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MuaMoCuaPhienSau = table.Column<bool>(type: "bit", nullable: false),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GiaVao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GiaThoat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PhanTramLoiNhuan = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    KetQua = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinHieuV3", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ViTheV3",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TinHieuV3Id = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Loai = table.Column<int>(type: "int", nullable: false),
                    NgayTinHieu = table.Column<DateOnly>(type: "date", nullable: false),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    NgayVao = table.Column<DateOnly>(type: "date", nullable: true),
                    GiaVao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    KhoiLuong = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    GiaTriLenh = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SoPhienDaGi = table.Column<int>(type: "int", nullable: false),
                    NguongChamMa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GiaThamChieu = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Exchange = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    NgayThoat = table.Column<DateOnly>(type: "date", nullable: true),
                    GiaThoat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LyDoThoat = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    PhanTramLoiNhuan = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    KetQua = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Mfe = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Mae = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    DaBanTinThoat = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViTheV3", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChiSoNgayV3",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ngay = table.Column<DateOnly>(type: "date", nullable: false),
                    SoMaUni = table.Column<int>(type: "int", nullable: false),
                    Breadth50 = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    ChiSo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ChiSoMa50 = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ChiSoDoLechMa50PhanTram = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Left = table.Column<int>(type: "int", nullable: false),
                    Tdm = table.Column<int>(type: "int", nullable: false),
                    DanhSachUni = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiSoNgayV3", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TinHieuV3_Loai_NgayTinHieu",
                table: "TinHieuV3",
                columns: new[] { "Loai", "NgayTinHieu" });

            migrationBuilder.CreateIndex(
                name: "IX_TinHieuV3_Symbol_Loai_NgayTinHieu",
                table: "TinHieuV3",
                columns: new[] { "Symbol", "Loai", "NgayTinHieu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViTheV3_NgayThoat",
                table: "ViTheV3",
                column: "NgayThoat");

            migrationBuilder.CreateIndex(
                name: "IX_ViTheV3_TinHieuV3Id",
                table: "ViTheV3",
                column: "TinHieuV3Id");

            migrationBuilder.CreateIndex(
                name: "IX_ViTheV3_TrangThai_Symbol",
                table: "ViTheV3",
                columns: new[] { "TrangThai", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_ChiSoNgayV3_Ngay",
                table: "ChiSoNgayV3",
                column: "Ngay",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chỉ lùi đúng phần Up() thêm — không đụng KetQuaKichBan (chưa bao giờ bị migration này xoá).
            migrationBuilder.DropTable(
                name: "ChiSoNgayV3");

            migrationBuilder.DropTable(
                name: "ViTheV3");

            migrationBuilder.DropTable(
                name: "TinHieuV3");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSellPositionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bước 1: Thêm 7 cột mới
            migrationBuilder.AddColumn<decimal>(
                name: "GiaBanNua",
                table: "KetQuaKichBan",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianBanNua",
                table: "KetQuaKichBan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DaDoiDungLo",
                table: "KetQuaKichBan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "GiaThoatHet",
                table: "KetQuaKichBan",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianThoatHet",
                table: "KetQuaKichBan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LyDoThoatHet",
                table: "KetQuaKichBan",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanhBaoDaGui",
                table: "KetQuaKichBan",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // Bước 2: Chuyển dữ liệu cũ (LoaiBaoBan=5 GayNen, LoaiBaoBan=4 KietSuc)
            // Gãy nền → ThoiGianThoatHet + LyDoThoatHet
            migrationBuilder.Sql(
                "UPDATE KetQuaKichBan SET ThoiGianThoatHet = ThoiGianBaoBan, LyDoThoatHet = 'GayNen'"
                + " WHERE LoaiBaoBan = 5 AND ThoiGianBaoBan IS NOT NULL;");

            // Kiệt sức → ThoiGianBanNua
            migrationBuilder.Sql(
                "UPDATE KetQuaKichBan SET ThoiGianBanNua = ThoiGianBaoBan"
                + " WHERE LoaiBaoBan = 4 AND ThoiGianBaoBan IS NOT NULL;");

            // Cảnh báo đã gửi → đặt cả hai sự kiện để chặn lặp
            migrationBuilder.Sql(
                "UPDATE KetQuaKichBan SET CanhBaoDaGui = 'KietSuc,GayNen'"
                + " WHERE ThoiGianCanhBaoBan IS NOT NULL;");

            // Bước 3: Xóa 3 cột cũ của phương án A
            migrationBuilder.DropColumn(name: "ThoiGianBaoBan", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "LoaiBaoBan", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "ThoiGianCanhBaoBan", table: "KetQuaKichBan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bước 1: Thêm lại 3 cột cũ
            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianBaoBan",
                table: "KetQuaKichBan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoaiBaoBan",
                table: "KetQuaKichBan",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianCanhBaoBan",
                table: "KetQuaKichBan",
                type: "datetime2",
                nullable: true);

            // Bước 2: Chuyển dữ liệu ngược
            // Bất kỳ ThoatHet nào → GayNen (LoaiBaoBan=5)
            migrationBuilder.Sql(
                "UPDATE KetQuaKichBan SET ThoiGianBaoBan = ThoiGianThoatHet, LoaiBaoBan = 5"
                + " WHERE ThoiGianThoatHet IS NOT NULL;");

            // BanNua (chưa có GayNen) → KietSuc (LoaiBaoBan=4)
            migrationBuilder.Sql(
                "UPDATE KetQuaKichBan SET ThoiGianBaoBan = ThoiGianBanNua, LoaiBaoBan = 4"
                + " WHERE ThoiGianBaoBan IS NULL AND ThoiGianBanNua IS NOT NULL;");

            // Cảnh báo → ThoiGianCanhBaoBan
            migrationBuilder.Sql(
                "UPDATE KetQuaKichBan SET ThoiGianCanhBaoBan = GETUTCDATE()"
                + " WHERE CanhBaoDaGui IS NOT NULL;");

            // Bước 3: Xóa 7 cột mới
            migrationBuilder.DropColumn(name: "GiaBanNua", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "ThoiGianBanNua", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "DaDoiDungLo", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "GiaThoatHet", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "ThoiGianThoatHet", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "LyDoThoatHet", table: "KetQuaKichBan");
            migrationBuilder.DropColumn(name: "CanhBaoDaGui", table: "KetQuaKichBan");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSellTrailingStopFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CanhBaoDaGui",
                table: "KetQuaKichBan",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DinhTuLucMua",
                table: "KetQuaKichBan",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DungLoDuoi",
                table: "KetQuaKichBan",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // DO-NOT-CHANGE: Đóng im lặng dữ liệu cũ để tránh Pha 2 bắn hàng loạt tin bán
            // cho vị thế đã được Pha 3 đo (KetQuaDoLuong IS NOT NULL) nhưng chưa có
            // ThoiGianThoatHet (cột này mới thêm từ phương án B). Nếu BỎ câu UPDATE này,
            // lượt Pha 2 đầu tiên sau deploy sẽ quét toàn bộ vị thế cũ (ThoiGianThoatHet NULL)
            // → "Hết hạn theo dõi" cho mã >20 phiên, hoặc "dừng lỗ/chốt lời" cho mã 4–20 phiên.
            // KHÔNG ghi GiaThoatHet vì không có giá thoát thật tại thời điểm đóng.
            // Pha 3 đã đo xong (KetQuaDoLuong IS NOT NULL → bị loại bởi filter Pha 3).
            // Pha 2 filter mới loại qua điểu kiện ThoiGianThoatHet == null.
            migrationBuilder.Sql(@"
                UPDATE KetQuaKichBan
                SET ThoiGianThoatHet = SYSUTCDATETIME(),
                    LyDoThoatHet = N'HetHanTheoDoi'
                WHERE ThoiGianKichHoat IS NOT NULL
                  AND ThoiGianThoatHet IS NULL
                  AND KetQuaDoLuong IS NOT NULL
                  AND LoaiKichBan NOT IN (4, 5);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DO-NOT-CHANGE: Không hoàn tác UPDATE ở Up vì không phân biệt được
            // dòng nào do migration đóng và dòng nào Pha 2 thoát bình thường.
            // Rollback schema (DROP columns + AlterColumn) vẫn an toàn;
            // ThoiGianThoatHet/LyDoThoatHet trên các dòng cũ giữ lại là dữ liệu hợp lệ.
            migrationBuilder.DropColumn(
                name: "DinhTuLucMua",
                table: "KetQuaKichBan");

            migrationBuilder.DropColumn(
                name: "DungLoDuoi",
                table: "KetQuaKichBan");

            migrationBuilder.AlterColumn<string>(
                name: "CanhBaoDaGui",
                table: "KetQuaKichBan",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }
    }
}

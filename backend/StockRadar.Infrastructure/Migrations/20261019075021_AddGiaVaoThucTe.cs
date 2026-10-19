using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGiaVaoThucTe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiaVaoThucTe",
                table: "KetQuaKichBan",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // BUSINESS-RULE: vị thế đang mở được bổ sung trường kế hoạch bằng mặc định
            // (Q7): GiaVaoThucTe = GiaVaoLenhMin, KhoangCachDuoi = DinhTuLucMua − DungLoDuoi
            // để không đổi mức cắt đang chạy. Bỏ WHERE LaDuLieuBoSung IS NULL sẽ ghi đè
            // kế hoạch gốc của vị thế mở sau này; Id <> 3275 là dòng DCS chờ chủ sản phẩm duyệt.
            // LIMIT: JSON() bị từ chối cả trên SQL Server 2022 RTM compat 160 (lỗi 195) — dùng
            // giá trị thật (bit/số) để JSON_MODIFY vẫn ghi đúng kiểu JSON, không thành chuỗi.
            migrationBuilder.Sql(@"
                UPDATE k
                SET GiaVaoThucTe = TRY_CAST(JSON_VALUE(k.KeHoachGiaoDichJson, '$.GiaVaoLenhMin') AS decimal(18, 2)),
                    KeHoachGiaoDichJson =
                        JSON_MODIFY(
                        JSON_MODIFY(
                        JSON_MODIFY(
                        JSON_MODIFY(
                        JSON_MODIFY(
                        JSON_MODIFY(
                        JSON_MODIFY(
                        JSON_MODIFY(
                        JSON_MODIFY(k.KeHoachGiaoDichJson,
                            '$.HetHanVao', CONVERT(varchar(30), DATEADD(minute, 465, CAST(k.NgayDanhGia AS datetime2)), 127) + 'Z'),
                            '$.TyLeBanChotLoi1', 0.5),
                            '$.DoiDungLoVeGiaVao', CAST(1 AS bit)),
                            '$.CoDungLoDuoi', CASE WHEN k.LoaiKichBan IN (1, 3) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END),
                            '$.KhoangCachDuoi', CASE WHEN k.DinhTuLucMua IS NOT NULL AND k.DungLoDuoi IS NOT NULL
                                                      THEN CAST(k.DinhTuLucMua - k.DungLoDuoi AS decimal(18, 2))
                                                      ELSE NULL END),
                            '$.SoPhienDungTheoThoiGian', 5),
                            '$.NguongLaiToiThieuPhanTram', 3),
                            '$.SoPhienToiDa', 20),
                            '$.LaDuLieuBoSung', CAST(1 AS bit))
                FROM KetQuaKichBan k
                WHERE k.ThoiGianKichHoat IS NOT NULL
                  AND k.ThoiGianThoatHet IS NULL
                  AND k.KeHoachGiaoDichJson IS NOT NULL
                  AND k.LoaiKichBan NOT IN (4, 5)
                  AND k.Id <> 3275
                  AND JSON_VALUE(k.KeHoachGiaoDichJson, '$.LaDuLieuBoSung') IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GiaVaoThucTe",
                table: "KetQuaKichBan");

            // Best-effort: xóa các trường đã chèn ở Up, chỉ trên dòng do migration đánh dấu bổ sung.
            migrationBuilder.Sql(@"
                UPDATE KetQuaKichBan
                SET KeHoachGiaoDichJson =
                        JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
                        JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
                            KeHoachGiaoDichJson,
                            '$.HetHanVao', NULL), '$.TyLeBanChotLoi1', NULL),
                            '$.DoiDungLoVeGiaVao', NULL), '$.CoDungLoDuoi', NULL),
                            '$.KhoangCachDuoi', NULL), '$.SoPhienDungTheoThoiGian', NULL),
                            '$.NguongLaiToiThieuPhanTram', NULL), '$.SoPhienToiDa', NULL),
                            '$.LaDuLieuBoSung', NULL)
                WHERE JSON_VALUE(KeHoachGiaoDichJson, '$.LaDuLieuBoSung') = 'true';");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThietBiNhanThongBao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThietBiNhanThongBao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    NenTang = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "android"),
                    NgayDangKy = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LanCapNhatGanNhat = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThietBiNhanThongBao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThietBiNhanThongBao_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThietBiNhanThongBao_Token",
                table: "ThietBiNhanThongBao",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThietBiNhanThongBao_UserId",
                table: "ThietBiNhanThongBao",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThietBiNhanThongBao");
        }
    }
}

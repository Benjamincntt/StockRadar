using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddV3HoangLoanSau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cột mới cho cờ lệnh hoảng loạn sâu (FR-015a/FR-016a).
            migrationBuilder.AddColumn<bool>(
                name: "LaHoangLoanSau",
                table: "ViTheV3",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LaHoangLoanSau",
                table: "ViTheV3");
        }
    }
}

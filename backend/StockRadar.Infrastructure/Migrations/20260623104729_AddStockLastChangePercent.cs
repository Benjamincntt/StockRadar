using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLastChangePercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LastChangePercent",
                table: "Stocks",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastChangePercent",
                table: "Stocks");
        }
    }
}

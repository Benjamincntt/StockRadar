using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations;

public partial class AddMarketIndexHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "HistoryJson",
            table: "MarketIndices",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HistoryJson",
            table: "MarketIndices");
    }
}

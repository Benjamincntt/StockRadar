using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTradeStateToDailyOpportunities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TradeState",
            table: "DailyOpportunities",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TradeStateReason",
            table: "DailyOpportunities",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TradeState", table: "DailyOpportunities");
        migrationBuilder.DropColumn(name: "TradeStateReason", table: "DailyOpportunities");
    }
}

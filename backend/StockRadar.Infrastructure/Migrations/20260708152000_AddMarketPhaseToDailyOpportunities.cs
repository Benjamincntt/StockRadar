using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockRadar.Infrastructure.Persistence;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260708152000_AddMarketPhaseToDailyOpportunities")]
    public partial class AddMarketPhaseToDailyOpportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarketPhase",
                table: "DailyOpportunities",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Neutral");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketPhase",
                table: "DailyOpportunities");
        }
    }
}

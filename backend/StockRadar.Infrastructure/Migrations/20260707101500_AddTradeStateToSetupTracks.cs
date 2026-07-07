using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockRadar.Infrastructure.Persistence;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260707101500_AddTradeStateToSetupTracks")]
    public partial class AddTradeStateToSetupTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TradeState",
                table: "SetupTracks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeStateReason",
                table: "SetupTracks",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TradeState", table: "SetupTracks");
            migrationBuilder.DropColumn(name: "TradeStateReason", table: "SetupTracks");
        }
    }
}

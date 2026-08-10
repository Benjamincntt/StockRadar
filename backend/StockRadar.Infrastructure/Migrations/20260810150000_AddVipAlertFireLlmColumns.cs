using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockRadar.Infrastructure.Persistence;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260810150000_AddVipAlertFireLlmColumns")]
    public partial class AddVipAlertFireLlmColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LlmDecision",
                table: "VipAlertFires",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LlmReason",
                table: "VipAlertFires",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LlmLatencyMs",
                table: "VipAlertFires",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LlmModel",
                table: "VipAlertFires",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LlmShadowMode",
                table: "VipAlertFires",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LlmDecision", table: "VipAlertFires");
            migrationBuilder.DropColumn(name: "LlmReason", table: "VipAlertFires");
            migrationBuilder.DropColumn(name: "LlmLatencyMs", table: "VipAlertFires");
            migrationBuilder.DropColumn(name: "LlmModel", table: "VipAlertFires");
            migrationBuilder.DropColumn(name: "LlmShadowMode", table: "VipAlertFires");
        }
    }
}

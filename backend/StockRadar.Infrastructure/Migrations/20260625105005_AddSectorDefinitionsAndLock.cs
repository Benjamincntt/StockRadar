using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectorDefinitionsAndLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SectorLocked",
                table: "Stocks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SectorDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectorDefinitions_Name",
                table: "SectorDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectorDefinitions_SortOrder",
                table: "SectorDefinitions",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorDefinitions");

            migrationBuilder.DropColumn(
                name: "SectorLocked",
                table: "Stocks");
        }
    }
}

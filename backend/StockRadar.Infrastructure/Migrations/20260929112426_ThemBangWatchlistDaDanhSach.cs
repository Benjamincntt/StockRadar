using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemBangWatchlistDaDanhSach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Bảng Watchlists (mặc định / ngành tự động / tùy chỉnh).
            migrationBuilder.CreateTable(
                name: "Watchlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LaDanhSachNganh = table.Column<bool>(type: "bit", nullable: false),
                    MaNganh = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    LaMacDinh = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Watchlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_UserId",
                table: "Watchlists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_UserId_LaMacDinh",
                table: "Watchlists",
                column: "UserId",
                unique: true,
                filter: "[LaMacDinh] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_UserId_MaNganh",
                table: "Watchlists",
                columns: new[] { "UserId", "MaNganh" },
                unique: true,
                filter: "[MaNganh] IS NOT NULL");

            // 2. Tạo watchlist mặc định ("Mặc định") cho mỗi user đang có item — giữ dữ liệu cũ.
            migrationBuilder.Sql("INSERT INTO [Watchlists] ([UserId], [Name], [LaDanhSachNganh], [MaNganh], [ThuTu], [LaMacDinh], [CreatedAt]) " +
                                 "SELECT DISTINCT [UserId], N'Mặc định', 0, NULL, 0, 1, GETUTCDATE() FROM [WatchlistItems];");

            // 3. Thêm cột Id (identity) + WatchlistId (nullable để backfill an toàn).
            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "WatchlistItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "WatchlistId",
                table: "WatchlistItems",
                type: "int",
                nullable: true);

            // 4. Gán item hiện hữu vào watchlist mặc định của user tương ứng.
            migrationBuilder.Sql("UPDATE [wi] SET [wi].[WatchlistId] = [w].[Id] " +
                                 "FROM [WatchlistItems] AS [wi] " +
                                 "INNER JOIN [Watchlists] AS [w] ON [w].[UserId] = [wi].[UserId] AND [w].[LaMacDinh] = 1;");

            // 5. WatchlistId thành NOT NULL sau khi backfill.
            migrationBuilder.AlterColumn<int>(
                name: "WatchlistId",
                table: "WatchlistItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 6. Đổi PK (UserId, Symbol) → Id; bỏ cột UserId (user nằm trên Watchlists).
            migrationBuilder.DropForeignKey(
                name: "FK_WatchlistItems_Users_UserId",
                table: "WatchlistItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WatchlistItems",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WatchlistItems");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WatchlistItems",
                table: "WatchlistItems",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_WatchlistId_Symbol",
                table: "WatchlistItems",
                columns: new[] { "WatchlistId", "Symbol" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchlistItems_Watchlists_WatchlistId",
                table: "WatchlistItems",
                column: "WatchlistId",
                principalTable: "Watchlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Khôi phục schema cũ (UserId, Symbol). Lưu ý: gộp lại nhiều danh sách có thể
            // tạo trùng (UserId, Symbol) — giữ bản thêm sớm nhất.
            migrationBuilder.DropForeignKey(
                name: "FK_WatchlistItems_Watchlists_WatchlistId",
                table: "WatchlistItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WatchlistItems",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_WatchlistId_Symbol",
                table: "WatchlistItems");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "WatchlistItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE [wi] SET [wi].[UserId] = [w].[UserId] " +
                                 "FROM [WatchlistItems] AS [wi] " +
                                 "INNER JOIN [Watchlists] AS [w] ON [w].[Id] = [wi].[WatchlistId];");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "WatchlistItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.Sql("DELETE FROM [WatchlistItems] WHERE [Id] IN (" +
                                 "SELECT [Id] FROM (SELECT [wi].[Id], ROW_NUMBER() OVER (PARTITION BY [wi].[UserId], [wi].[Symbol] ORDER BY [wi].[AddedAt], [wi].[Id]) AS [rn] " +
                                 "FROM [WatchlistItems] AS [wi]) AS [ranked] WHERE [ranked].[rn] > 1);");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WatchlistItems",
                table: "WatchlistItems",
                columns: new[] { "UserId", "Symbol" });

            migrationBuilder.DropColumn(
                name: "Id",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "WatchlistId",
                table: "WatchlistItems");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchlistItems_Users_UserId",
                table: "WatchlistItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                name: "Watchlists");
        }
    }
}

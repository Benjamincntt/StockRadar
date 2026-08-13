using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLastCloseVolume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LastClose",
                table: "Stocks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "LastVolume",
                table: "Stocks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Backfill từ bar cuối của HistoryJson. Lấy phần đuôi chuỗi rồi cắt object cuối
            // thay vì OPENJSON toàn mảng — parse cả mảng tốn phút trên universe thật.
            migrationBuilder.Sql("""
                WITH tail AS (
                    SELECT Symbol, RIGHT(HistoryJson, 500) AS T
                    FROM Stocks
                    WHERE HistoryJson IS NOT NULL AND HistoryJson <> '[]'
                ),
                lastObj AS (
                    SELECT Symbol,
                           LEFT(SUBSTRING(T, LEN(T) - CHARINDEX('{', REVERSE(T)) + 1, LEN(T)),
                                CHARINDEX('}', SUBSTRING(T, LEN(T) - CHARINDEX('{', REVERSE(T)) + 1, LEN(T)))) AS O
                    FROM tail
                    WHERE CHARINDEX('{', REVERSE(T)) > 0
                )
                UPDATE s
                SET s.LastClose = COALESCE(TRY_CAST(JSON_VALUE(o.O, '$.close') AS decimal(18,2)), 0),
                    s.LastVolume = COALESCE(TRY_CAST(JSON_VALUE(o.O, '$.volume') AS bigint), 0)
                FROM Stocks s
                INNER JOIN lastObj o ON o.Symbol = s.Symbol
                WHERE ISJSON(o.O) = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastClose",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "LastVolume",
                table: "Stocks");
        }
    }
}

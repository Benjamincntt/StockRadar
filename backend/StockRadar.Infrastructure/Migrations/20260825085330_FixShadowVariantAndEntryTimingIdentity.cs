using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixShadowVariantAndEntryTimingIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cùng bug/cách sửa với FixSingletonStateIdentityColumns: SQL Server không cho ALTER COLUMN
            // để bỏ IDENTITY trực tiếp — phải drop PK, thêm cột thường, copy dữ liệu, xoá cột cũ, đổi tên,
            // tạo lại PK. Mỗi câu 1 batch riêng (tránh lỗi "Invalid column name" khi gộp chung 1 batch).
            // ShadowVariantSummaries.VariantMinPassScore và EntryTimingStates.Id đều là giá trị cố định
            // do code set tường minh (58/60/62 theo config, hoặc Id=1 singleton) — không phải ID tự tăng.
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] DROP CONSTRAINT [PK_ShadowVariantSummaries];");
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] ADD [VariantMinPassScore_New] int NOT NULL DEFAULT(0);");
            migrationBuilder.Sql("UPDATE [ShadowVariantSummaries] SET [VariantMinPassScore_New] = [VariantMinPassScore];");
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] DROP COLUMN [VariantMinPassScore];");
            migrationBuilder.Sql("EXEC sp_rename N'[ShadowVariantSummaries].[VariantMinPassScore_New]', N'VariantMinPassScore', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] ADD CONSTRAINT [PK_ShadowVariantSummaries] PRIMARY KEY ([VariantMinPassScore]);");

            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] DROP CONSTRAINT [PK_EntryTimingStates];");
            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] ADD [Id_New] int NOT NULL DEFAULT(1);");
            migrationBuilder.Sql("UPDATE [EntryTimingStates] SET [Id_New] = [Id];");
            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] DROP COLUMN [Id];");
            migrationBuilder.Sql("EXEC sp_rename N'[EntryTimingStates].[Id_New]', N'Id', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] ADD CONSTRAINT [PK_EntryTimingStates] PRIMARY KEY ([Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] DROP CONSTRAINT [PK_ShadowVariantSummaries];");
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] ADD [VariantMinPassScore_Old] int IDENTITY(1,1);");
            migrationBuilder.Sql("SET IDENTITY_INSERT [ShadowVariantSummaries] ON;");
            migrationBuilder.Sql("UPDATE [ShadowVariantSummaries] SET [VariantMinPassScore_Old] = [VariantMinPassScore];");
            migrationBuilder.Sql("SET IDENTITY_INSERT [ShadowVariantSummaries] OFF;");
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] DROP COLUMN [VariantMinPassScore];");
            migrationBuilder.Sql("EXEC sp_rename N'[ShadowVariantSummaries].[VariantMinPassScore_Old]', N'VariantMinPassScore', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [ShadowVariantSummaries] ADD CONSTRAINT [PK_ShadowVariantSummaries] PRIMARY KEY ([VariantMinPassScore]);");

            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] DROP CONSTRAINT [PK_EntryTimingStates];");
            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] ADD [Id_Old] int IDENTITY(1,1);");
            migrationBuilder.Sql("SET IDENTITY_INSERT [EntryTimingStates] ON;");
            migrationBuilder.Sql("UPDATE [EntryTimingStates] SET [Id_Old] = [Id];");
            migrationBuilder.Sql("SET IDENTITY_INSERT [EntryTimingStates] OFF;");
            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] DROP COLUMN [Id];");
            migrationBuilder.Sql("EXEC sp_rename N'[EntryTimingStates].[Id_Old]', N'Id', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [EntryTimingStates] ADD CONSTRAINT [PK_EntryTimingStates] PRIMARY KEY ([Id]);");
        }
    }
}

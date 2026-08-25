using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSingletonStateIdentityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server không cho ALTER COLUMN để bỏ IDENTITY trực tiếp — phải drop PK,
            // thêm cột thường, copy dữ liệu (nếu có), xoá cột IDENTITY cũ, đổi tên, tạo lại PK.
            // Mỗi câu là 1 batch riêng (Sql() riêng) — gộp chung một batch khiến SQL Server
            // không resolve được cột vừa ADD trong cùng lần biên dịch (lỗi "Invalid column name").
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] DROP CONSTRAINT [PK_HitCalibrationStates];");
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] ADD [Id_New] int NOT NULL DEFAULT(1);");
            migrationBuilder.Sql("UPDATE [HitCalibrationStates] SET [Id_New] = [Id];");
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] DROP COLUMN [Id];");
            migrationBuilder.Sql("EXEC sp_rename N'[HitCalibrationStates].[Id_New]', N'Id', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] ADD CONSTRAINT [PK_HitCalibrationStates] PRIMARY KEY ([Id]);");

            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] DROP CONSTRAINT [PK_FalsePositiveMiningStates];");
            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] ADD [Id_New] int NOT NULL DEFAULT(1);");
            migrationBuilder.Sql("UPDATE [FalsePositiveMiningStates] SET [Id_New] = [Id];");
            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] DROP COLUMN [Id];");
            migrationBuilder.Sql("EXEC sp_rename N'[FalsePositiveMiningStates].[Id_New]', N'Id', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] ADD CONSTRAINT [PK_FalsePositiveMiningStates] PRIMARY KEY ([Id]);");

            // Bù dòng Id=1 nếu bảng đang rỗng (đúng là nguyên nhân job weekly-review lỗi trên production).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [HitCalibrationStates] WHERE [Id] = 1)
    INSERT INTO [HitCalibrationStates] ([Id], [GlobalFactor], [TotalSamples], [PredictionBiasPercent], [UpdatedAt])
    VALUES (1, 1.0, 0, 0, NULL);
");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [FalsePositiveMiningStates] WHERE [Id] = 1)
    INSERT INTO [FalsePositiveMiningStates] ([Id], [FalsePositiveSetups], [GoodSetups], [ResultsJson], [UpdatedAt])
    VALUES (1, 0, 0, N'[]', GETUTCDATE());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] DROP CONSTRAINT [PK_HitCalibrationStates];");
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] ADD [Id_Old] int IDENTITY(1,1);");
            migrationBuilder.Sql("SET IDENTITY_INSERT [HitCalibrationStates] ON;");
            migrationBuilder.Sql("UPDATE [HitCalibrationStates] SET [Id_Old] = [Id];");
            migrationBuilder.Sql("SET IDENTITY_INSERT [HitCalibrationStates] OFF;");
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] DROP COLUMN [Id];");
            migrationBuilder.Sql("EXEC sp_rename N'[HitCalibrationStates].[Id_Old]', N'Id', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [HitCalibrationStates] ADD CONSTRAINT [PK_HitCalibrationStates] PRIMARY KEY ([Id]);");

            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] DROP CONSTRAINT [PK_FalsePositiveMiningStates];");
            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] ADD [Id_Old] int IDENTITY(1,1);");
            migrationBuilder.Sql("SET IDENTITY_INSERT [FalsePositiveMiningStates] ON;");
            migrationBuilder.Sql("UPDATE [FalsePositiveMiningStates] SET [Id_Old] = [Id];");
            migrationBuilder.Sql("SET IDENTITY_INSERT [FalsePositiveMiningStates] OFF;");
            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] DROP COLUMN [Id];");
            migrationBuilder.Sql("EXEC sp_rename N'[FalsePositiveMiningStates].[Id_Old]', N'Id', 'COLUMN';");
            migrationBuilder.Sql("ALTER TABLE [FalsePositiveMiningStates] ADD CONSTRAINT [PK_FalsePositiveMiningStates] PRIMARY KEY ([Id]);");
        }
    }
}

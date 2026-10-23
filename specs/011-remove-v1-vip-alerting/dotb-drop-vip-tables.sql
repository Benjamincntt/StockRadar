-- ============================================================================================
-- CHỈ THAM KHẢO — KHÔNG CHẠY TRÊN PRODUCTION. Đợt B dùng migration EF (dotnet ef migrations
-- add DropVipAlertTables) để xoá bảng, KHÔNG chạy tay file này. Giữ file chỉ để đối chiếu
-- thứ tự khoá ngoại khi đọc lại body migration EF (ràng buộc "đọc lại body migration").
-- ============================================================================================
-- ĐỢT B — xoá 3 bảng VIP (Spec 011). CHƯA ÁP DỤNG. Chỉ tham chiếu; việc xoá thật đi qua EF.
-- Lý do để rời (không đặt vào backend/.../Migrations ở Đợt A): Entity + DbSet của 3 bảng vẫn còn trong
-- ApplicationDbContext (model snapshot đang khớp). Đặt migration DropTable bây giờ sẽ làm lệch snapshot
-- và hỏng build/tests đang xanh. Đợt B phải: (1) xoá Entity+DbSet+konfig, (2) sinh migration mới, rồi hãy áp.
--
-- Thứ tự xoá theo khoá ngoại: PositionSellLegs (FK -> MasterAlertPositions, Cascade) trước,
-- rồi MasterAlertPositions, cuối cùng VipAlertFires (độc lập). SetupTracks.PositionId chỉ là index/mềm,
-- KHÔNG có FK -> MasterAlertPositions, nên không cần đụng SetupTracks.
--
-- An toàn: bọc transaction, chỉ chạy khi các bảng tồn tại.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[PositionSellLegs]', N'U') IS NOT NULL
    DROP TABLE [dbo].[PositionSellLegs];

IF OBJECT_ID(N'[dbo].[MasterAlertPositions]', N'U') IS NOT NULL
    DROP TABLE [dbo].[MasterAlertPositions];

IF OBJECT_ID(N'[dbo].[VipAlertFires]', N'U') IS NOT NULL
    DROP TABLE [dbo].[VipAlertFires];

COMMIT;

-- KIỂM TRA SAU KHI XOÁ (phải trả về 0 hàng cho mỗi bảng):
--   SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
--   WHERE TABLE_NAME IN ('PositionSellLegs','MasterAlertPositions','VipAlertFires');

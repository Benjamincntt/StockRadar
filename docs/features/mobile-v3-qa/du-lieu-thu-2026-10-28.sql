-- Dữ liệu thử BUG-03/04/05 (lượt A, DB LOCAL — Server=localhost, CSDL StockRadarDb).
-- CHƯA CHẠY — chờ owner duyệt. Chỉ chạy trên local, không bao giờ trên production.
-- Mã thật + NgàyTinHieu 2020-01-02 (thứ Hai) để dễ nhận, không lẫn dữ liệu thật.
-- Dấu thử nhận biết: SnapshotJson chứa 'TestQA-2026-10-28' (dùng cho cả kiểm tra trước và lệnh xóa).
-- Id để auto (bigint identity) — không ghi Id tay.
-- Lãi ròng theo công thức thật V3ChamKetQua.LaiRong = GiaThoat/GiaVao − 1 − 0,004 (phí khứ hồi):
--   HPG hoảng loạn:  17,85/15,5 − 1 − 0,004 =  0,1476129 → 14,7613% (Thang; thoát ≥ ngưỡng chốt 17,83)
--   VNM ChamMA:      74,5/75,2 − 1 − 0,004  = −0,0133085 → −1,3309% (Thua)

-- ===== ĐẾM TRƯỚC =====
SELECT 'TinHieuV3' AS bang, COUNT(*) AS so_dong
FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%';
SELECT 'ViTheV3' AS bang, COUNT(*) AS so_dong
FROM dbo.ViTheV3
WHERE TinHieuV3Id IN (SELECT Id FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%');

-- ===== CHÈN (bọc transaction: còn dấu cũ → ROLLBACK + DỪNG) =====
BEGIN TRAN;

IF EXISTS (SELECT 1 FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%')
BEGIN
    ROLLBACK TRAN;
    PRINT N'Da ton tai dong thu du lieu thi (TestQA-2026-10-28) - DUNG, khong chen tiep.';
END
ELSE
BEGIN
    -- 1) Tín hiệu HPG thuộc vị thế HOẢNG LOẠN SÂU đã thoát (KB1=1, TrangThai=2 DaVaoLenh).
    INSERT INTO dbo.TinHieuV3
        (Symbol, Loai, NgayTinHieu, GiaTinHieu, Hang, GiaTriXepHang, MuaMoCuaPhienSau,
         TrangThai, SnapshotJson, GiaVao, GiaThoat, PhanTramLoiNhuan, KetQua, CreatedAt, UpdatedAt)
    VALUES
        ('HPG', 1, '2020-01-02', 15.5, 1, 12.3400, 1,
         2, N'{"TestQA-2026-10-28":"du lieu thu BUG-05"}', 15.5, 17.85, 14.7613, N'Thang',
         SYSUTCDATETIME(), SYSUTCDATETIME());

    -- 2) Tín hiệu VNM thuộc vị thế THƯỜNG đã thoát ChamMA (N1=4).
    INSERT INTO dbo.TinHieuV3
        (Symbol, Loai, NgayTinHieu, GiaTinHieu, Hang, GiaTriXepHang, MuaMoCuaPhienSau,
         TrangThai, SnapshotJson, GiaVao, GiaThoat, PhanTramLoiNhuan, KetQua, CreatedAt, UpdatedAt)
    VALUES
        ('VNM', 4, '2020-01-02', 75.2, 2, 8.1500, 0,
         2, N'{"TestQA-2026-10-28":"du lieu thu BUG-04"}', 75.2, 74.5, -1.3309, N'Thua',
         SYSUTCDATETIME(), SYSUTCDATETIME());

    -- 3) Vị thế hoảng loạn sâu gắn tín hiệu HPG: TrangThai=2 DaThoat, LyDoThoat=ChotLoiHoangLoan,
    --    LaHoangLoanSau=1, giữ 20 phiên (≤40 theo FR-016a), ngưỡng chốt = 15,5 × 1,15 = 17,83,
    --    giá thoát 17,85 ≥ ngưỡng.
    INSERT INTO dbo.ViTheV3
        (TinHieuV3Id, Symbol, Loai, NgayTinHieu, TrangThai, NgayVao, GiaVao, KhoiLuong, GiaTriLenh,
         SoPhienDaGi, NguongChamMa, LaHoangLoanSau, GiaThamChieu, Exchange,
         NgayThoat, GiaThoat, LyDoThoat, PhanTramLoiNhuan, KetQua, Mfe, Mae,
         DaBanTinThoat, CreatedAt, UpdatedAt)
    SELECT t.Id, t.Symbol, t.Loai, t.NgayTinHieu, 2, '2020-01-03', 15.5, 1000, 15500.00,
           20, 17.83, 1, 15.5, N'HOSE',
           '2020-01-31', 17.85, N'ChotLoiHoangLoan', 14.7613, N'Thang', 16.5000, -2.0000,
           1, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM dbo.TinHieuV3 t
    WHERE t.Symbol = 'HPG' AND t.NgayTinHieu = '2020-01-02'
      AND t.SnapshotJson LIKE N'%TestQA-2026-10-28%';

    -- 4) Vị thế thường (không hoảng loạn) gắn tín hiệu VNM: LyDoThoat=ChamMA, LaHoangLoanSau=0,
    --    9601 cp (không tròn nghìn) — kiểm màn Hiệu quả hiện "Giữ 9 phiên", hết "Giữ 0 phiên".
    INSERT INTO dbo.ViTheV3
        (TinHieuV3Id, Symbol, Loai, NgayTinHieu, TrangThai, NgayVao, GiaVao, KhoiLuong, GiaTriLenh,
         SoPhienDaGi, NguongChamMa, LaHoangLoanSau, GiaThamChieu, Exchange,
         NgayThoat, GiaThoat, LyDoThoat, PhanTramLoiNhuan, KetQua, Mfe, Mae,
         DaBanTinThoat, CreatedAt, UpdatedAt)
    SELECT t.Id, t.Symbol, t.Loai, t.NgayTinHieu, 2, '2020-01-03', 75.2, 9601, 721995.20,
           9, 74.50, 0, 75.2, N'HOSE',
           '2020-01-17', 74.5, N'ChamMA', -1.3309, N'Thua', 3.1000, -1.5000,
           1, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM dbo.TinHieuV3 t
    WHERE t.Symbol = 'VNM' AND t.NgayTinHieu = '2020-01-02'
      AND t.SnapshotJson LIKE N'%TestQA-2026-10-28%';

    -- Đếm sau khi chèn (kỳ vọng mỗi bảng = 2); lệch → ROLLBACK toàn bộ, không để lại nửa dữ liệu.
    DECLARE @st INT, @sv INT;
    SELECT @st = COUNT(*) FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%';
    SELECT @sv = COUNT(*) FROM dbo.ViTheV3
    WHERE TinHieuV3Id IN (SELECT Id FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%');
    IF @st <> 2 OR @sv <> 2
    BEGIN
        ROLLBACK TRAN;
        PRINT N'So dong sau chen lech (TinHieuV3=' + CAST(@st AS NVARCHAR(10))
            + N', ViTheV3=' + CAST(@sv AS NVARCHAR(10)) + N') - DUNG, da rollback.';
    END
    ELSE
    BEGIN
        COMMIT TRAN;
        PRINT N'Da chen 2 tin hieu + 2 vi the thu (TestQA-2026-10-28).';
    END
END
GO

-- ===== XÓA SAU KHI TEST XONG (theo dấu thử, không theo ngày + mã) =====
BEGIN TRAN;
DELETE FROM dbo.ViTheV3
WHERE TinHieuV3Id IN (SELECT Id FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%');
DELETE FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%';
COMMIT TRAN;
GO

-- Đếm lại sau xóa (kỳ vọng cả hai = 0, tức về mức trước khi chèn).
SELECT 'TinHieuV3' AS bang, COUNT(*) AS sau_xoa
FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%';
SELECT 'ViTheV3' AS bang, COUNT(*) AS sau_xoa
FROM dbo.ViTheV3
WHERE TinHieuV3Id IN (SELECT Id FROM dbo.TinHieuV3 WHERE SnapshotJson LIKE N'%TestQA-2026-10-28%');
GO

-- Ghi chú khi test trên máy (lượt A): danh sách sắp mới nhất trước, nên dòng 2020-01-02
-- CÓ THỂ bị đẩy khỏi trang đầu màn Hiệu quả / picker. Phải cuộn xác nhận hai dòng HPG (hoảng
-- loạn sâu, Giữ 20 phiên) và VNM (Chạm MA, Giữ 9 phiên) hiện ra TRƯỚC khi chụp ảnh;
-- không thấy thì báo owner, đừng kết luận màn lỗi.

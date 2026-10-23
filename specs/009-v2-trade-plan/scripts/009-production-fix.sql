/* =====================================================================
  009-production-fix.sql — StockRadar Phase 9 (T051): gộp C-10, C-24, C-21, C-27 P3
  ---------------------------------------------------------------------
  CHỈ VIẾT — KHÔNG TỰ CHẠY. Chủ sản phẩm duyệt rồi chạy trên server:
      sqlcmd -S localhost -d StockRadarDb -U <user> -P <pwd> -C -i 009-production-fix.sql

  ĐIỀU KIỆN CHẠY:
    - Sau khi deploy code Phase 9b (Pha 3 chấm theo chu kỳ) và TRƯỚC 16:00
      cùng ngày (giờ VN) — runner 16:00 sẽ tự chấm lại mọi chu kỳ đã đóng.
    - MỘT transaction duy nhất: mỗi bước kiểm số dòng affected, lệch kỳ
      vọng → RAISERROR → ROLLBACK TOÀN BỘ (không dòng nào được ghi nửa chừng).

  QUY TẮC ÁP DỤNG: C-10 (hủy các dòng mua lặp), C-24 + C-32 (giá vào thực tế,
  C-32 thay phần "giữ nguyên dữ liệu" của C-26 cho 2 lệnh đã đóng),
  C-21 + C-31 (chốt điểm mua 2 offline, ATR14 từ lịch sử production),
  C-27 P3 (xóa kết quả đo cũ để Pha 3 mới chấm).

  KỲ VỌNG SAU LƯỢT PHA 3 16:00 (đối chiếu sau khi chạy — C-32):
    - DGW 1472 → "Ngang" (giá ra bình quân 45.60 so giá vào mới 45.80 = −0.4%).
    - PVS 2168 → "Thua"  (31.50 so 33.60 = −6.25%).
    Bước 2 phải chạy TRƯỚC bước 4 để hai dòng này được chấm theo giá vào mới.

  HỆ QUẢ ĐÃ BIẾT (chấp nhận theo C-10): sau Bước 1, ABB chỉ còn dòng 3650
  (mua 12/10) — lượt Pha 2 đầu tiên, luật thời gian (≥5 phiên, MFE<3%) sẽ
  bán hết dòng này.

  LOẠI TRỪ: DCS 3275 (C-25 — chủ sản phẩm tự chạy).
  ===================================================================== */

USE StockRadarDb;
GO
SET NOCOUNT ON;

/* ---------------- PREVIEW TRƯỚC KHI SỬA (13 dòng + các dòng chờ xóa Bước 4) ---------------- */
SELECT Id, Symbol, TrangThai, GiaVaoThucTe, ThoiGianThoatHet, LyDoThoatHet,
       KetQuaDoLuong, GiaThoatHet,
       JSON_VALUE(KeHoachGiaoDichJson, '$.GiaMuaDiem2') AS Diem2
FROM KetQuaKichBan
WHERE Id IN (3650, 1916, 2114, 5765, 6014, 4106, 4556, 5444, 5891, 4619, 5957, 1472, 2168)
ORDER BY Id;

SELECT COUNT(*) AS SoDongChoXoaBuoc4
FROM KetQuaKichBan
WHERE TrangThai >= 2 AND KetQuaDoLuong IS NOT NULL AND KetQuaDoLuong <> N'Huy';

BEGIN TRY
    DECLARE @rc INT, @n INT, @runAt DATETIME2 = GETUTCDATE();

    BEGIN TRAN;

    /* ============ BƯỚC 1 — C-10: hủy 6 dòng mua lặp (ABB 4106/4556/5444/5891, DCM 4619/5957) ============
       Điều kiện tiên quyết: cả 6 dòng phải ĐANG MỞ (ThoiGianThoatHet IS NULL).
       Đóng chu kỳ tại thời điểm chạy, lý do HuyMuaLap, KetQuaDoLuong='Huy'
       → Pha 3 mới vĩnh viễn không chấm (bộ lọc <> 'Huy'). */
    SELECT @rc = COUNT(*) FROM KetQuaKichBan
    WHERE Id IN (4106, 4556, 5444, 5891, 4619, 5957) AND ThoiGianThoatHet IS NULL;
    IF @rc <> 6
        RAISERROR(N'Bước 1 (C-10): chỉ thấy %d/6 dòng cần hủy còn đang mở — có dòng đã đóng hoặc Id thay đổi. Dừng toàn bộ.', 16, 1, @rc);

    UPDATE KetQuaKichBan
    SET ThoiGianThoatHet = @runAt,
        LyDoThoatHet     = N'HuyMuaLap',
        KetQuaDoLuong    = N'Huy',
        GiaThoatHet      = NULL,
        UpdatedAt        = @runAt
    WHERE Id IN (4106, 4556, 5444, 5891, 4619, 5957) AND ThoiGianThoatHet IS NULL;

    IF @@ROWCOUNT <> 6
    BEGIN
        SET @rc = @@ROWCOUNT;
        RAISERROR(N'Bước 1 (C-10): UPDATE chỉ affected %d/6 dòng. Dừng toàn bộ.', 16, 1, @rc);
    END
    PRINT N'Bước 1 (C-10): OK — 6 dòng HuyMuaLap đã đóng, KetQuaDoLuong = ''Huy''.';

    /* ============ BƯỚC 2 — C-24 + C-32: giá vào thực tế theo log TRIGGERED (4 dòng) ============
       - NTP 2114 = 52.80, QNS 5765 = 51.00: hai dòng CÒN MỞ (C-24).
       - DGW 1472 = 45.80, PVS 2168 = 33.60: hai dòng đã ĐÓNG, mua ngoài vùng (C-32,
         thay phần "giữ nguyên dữ liệu" của C-26) — phải sửa TRƯỚC Bước 4 để Pha 3
         chấm lại cả chu kỳ theo giá vào mới. */
    SELECT @rc = COUNT(*) FROM KetQuaKichBan
    WHERE Id IN (2114, 5765) AND ThoiGianThoatHet IS NULL;
    IF @rc <> 2
        RAISERROR(N'Bước 2 (C-24): chỉ thấy %d/2 dòng NTP/QNS còn đang mở. Dừng toàn bộ.', 16, 1, @rc);

    SELECT @rc = COUNT(*) FROM KetQuaKichBan
    WHERE Id IN (1472, 2168) AND ThoiGianThoatHet IS NOT NULL;
    IF @rc <> 2
        RAISERROR(N'Bước 2 (C-32): chỉ thấy %d/2 dòng DGW/PVS đã đóng — kiểm lại trạng thái trước khi sửa. Dừng toàn bộ.', 16, 1, @rc);

    UPDATE KetQuaKichBan
    SET GiaVaoThucTe = CASE Id WHEN 2114 THEN 52.80 WHEN 5765 THEN 51.00
                               WHEN 1472 THEN 45.80 WHEN 2168 THEN 33.60 END,
        UpdatedAt    = @runAt
    WHERE (Id IN (2114, 5765) AND ThoiGianThoatHet IS NULL)
       OR (Id IN (1472, 2168) AND ThoiGianThoatHet IS NOT NULL);

    IF @@ROWCOUNT <> 4
    BEGIN
        SET @rc = @@ROWCOUNT;
        RAISERROR(N'Bước 2 (C-24/C-32): UPDATE chỉ affected %d/4 dòng. Dừng toàn bộ.', 16, 1, @rc);
    END
    PRINT N'Bước 2 (C-24/C-32): OK — NTP 2114 = 52.80, QNS 5765 = 51.00 (mở); DGW 1472 = 45.80, PVS 2168 = 33.60 (đã đóng).';

    /* ============ BƯỚC 3 — C-21 + C-31: chốt điểm mua 2 (số liệu cố định, bảng dưới) ============
       ATR14 từ lịch sử PRODUCTION, trung bình true range 14 phiên TRƯỚC ngày mua
       (đúng công thức IndicatorMath.AtrAt), tính ngày 2026-10-21. Mức/băng tính bằng
       đúng MayNhanKichBanService.TinhDiemMua2 (HoiHoTro: muc = giaKhopDiem1 + ATR14;
       band = 0.3×ATR14). giaKhop NTP/QNS dùng giá khớp THẬT sau Bước 2 (C-24).

         mã  | id   | ngày mua 1 | khop    | CL1    | ATR14   | muc Diem2 | band
         ABB | 3650 | 12/10/2026 | 17.49   | 18.2   | 0.2571  | 17.7471   | 0.0771
         DCM | 1916 | 06/10/2026 | 33.13   | 35.4   | 0.9464  | 34.0764   | 0.2839
         NTP | 2114 | 06/10/2026 | 52.80*  | 55.5   | 1.3786  | 54.1786   | 0.4136
         QNS | 5765 | 16/10/2026 | 51.00*  | 53.1   | 1.3571  | 52.3571   | 0.4071
         FRT | 6014 | 19/10/2026 | 142.34  | 152.8  | 5.0786  | 147.4186  | 1.5236
       (* giá khớp thật sau Bước 2 — không phải biên vùng vào.)

       Không dòng nào NULL: cả 5 muc đều < CL1 (điều kiện C-21 "điểm 2 ≥ CL1 → NULL").
       Bảng cũ (ATR từ DB dev, lịch sử dừng 30/09) đã thay bằng C-31 2026-10-21. */
    SELECT @rc = COUNT(*) FROM KetQuaKichBan
    WHERE Id IN (3650, 1916, 2114, 5765, 6014)
      AND ThoiGianThoatHet IS NULL AND KeHoachGiaoDichJson IS NOT NULL;
    IF @rc <> 5
        RAISERROR(N'Bước 3 (C-21): chỉ thấy %d/5 dòng mở còn kế hoạch giao dịch. Dừng toàn bộ.', 16, 1, @rc);

    UPDATE KetQuaKichBan
    SET KeHoachGiaoDichJson = JSON_MODIFY(
            JSON_MODIFY(KeHoachGiaoDichJson, '$.GiaMuaDiem2', CAST(
                CASE Id WHEN 3650 THEN 17.7471 WHEN 1916 THEN 34.0764 WHEN 2114 THEN 54.1786
                        WHEN 5765 THEN 52.3571 WHEN 6014 THEN 147.4186 END AS DECIMAL(12,4))),
            '$.BandRongDiem2', CAST(
                CASE Id WHEN 3650 THEN 0.0771 WHEN 1916 THEN 0.2839 WHEN 2114 THEN 0.4136
                        WHEN 5765 THEN 0.4071 WHEN 6014 THEN 1.5236 END AS DECIMAL(10,4))),
        UpdatedAt = @runAt
    WHERE Id IN (3650, 1916, 2114, 5765, 6014)
      AND ThoiGianThoatHet IS NULL AND KeHoachGiaoDichJson IS NOT NULL;

    IF @@ROWCOUNT <> 5
    BEGIN
        SET @rc = @@ROWCOUNT;
        RAISERROR(N'Bước 3 (C-21): UPDATE chỉ affected %d/5 dòng. Dừng toàn bộ.', 16, 1, @rc);
    END

    /* Hậu kiểm C-21: mọi GiaMuaDiem2 vừa ghi phải < GiaChotLoi1 của chính dòng đó. */
    SELECT @rc = COUNT(*) FROM KetQuaKichBan
    WHERE Id IN (3650, 1916, 2114, 5765, 6014)
      AND CAST(JSON_VALUE(KeHoachGiaoDichJson, '$.GiaMuaDiem2') AS DECIMAL(18,4))
        >= CAST(JSON_VALUE(KeHoachGiaoDichJson, '$.GiaChotLoi1') AS DECIMAL(18,4));
    IF @rc <> 0
        RAISERROR(N'Bước 3 (C-21): %d dòng có điểm mua 2 >= chốt lời 1 — sai số liệu chốt offline. Dừng toàn bộ.', 16, 1, @rc);

    PRINT N'Bước 3 (C-21/C-31): OK — 5 dòng đã chốt GiaMuaDiem2 + BandRongDiem2 (đều < CL1).';

    /* ============ BƯỚC 4 — C-27 P3: xóa KetQuaDoLuong cũ của MỌI dòng V2 đã kích hoạt, trừ 'Huy' ============
       Chạy SAU Bước 2 để DGW 1472 / PVS 2168 được chấm lại theo giá vào mới (C-32):
       kỳ vọng lượt 16:00 → DGW "Ngang", PVS "Thua".
       Pha 3 mới (deploy trước đó) sẽ chấm lại toàn bộ chu kỳ đã đóng ở lượt 16:00;
       dòng còn mở trở về "Chờ đo". Số dòng là động → đếm trước, cập nhật, so khớp. */
    SELECT @n = COUNT(*) FROM KetQuaKichBan
    WHERE TrangThai >= 2 AND KetQuaDoLuong IS NOT NULL AND KetQuaDoLuong <> N'Huy';
    PRINT N'Bước 4 (C-27 P3): dự kiến xóa KetQuaDoLuong trên ' + CAST(@n AS NVARCHAR(10)) + N' dòng.';

    UPDATE KetQuaKichBan
    SET KetQuaDoLuong = NULL,
        UpdatedAt     = @runAt
    WHERE TrangThai >= 2 AND KetQuaDoLuong IS NOT NULL AND KetQuaDoLuong <> N'Huy';

    IF @@ROWCOUNT <> @n
    BEGIN
        SET @rc = @@ROWCOUNT;
        RAISERROR(N'Bước 4 (C-27 P3): UPDATE affected %d nhưng đếm trước là %d. Dừng toàn bộ.', 16, 1, @rc, @n);
    END
    PRINT N'Bước 4 (C-27 P3): OK — đã xóa ' + CAST(@n AS NVARCHAR(10)) + N' kết quả đo cũ.';

    COMMIT;
    PRINT N'COMMIT — cả 4 bước khớp kỳ vọng, dữ liệu đã được ghi.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    PRINT N'ROLLBACK TOÀN BỘ — không dòng nào được ghi. Lý do: ' + ERROR_MESSAGE();
END CATCH;
GO

/* ---------------- PREVIEW SAU KHI SỬA ---------------- */
SELECT Id, Symbol, TrangThai, GiaVaoThucTe, ThoiGianThoatHet, LyDoThoatHet,
       KetQuaDoLuong, GiaThoatHet,
       JSON_VALUE(KeHoachGiaoDichJson, '$.GiaMuaDiem2')  AS Diem2,
       JSON_VALUE(KeHoachGiaoDichJson, '$.BandRongDiem2') AS Band2
FROM KetQuaKichBan
WHERE Id IN (3650, 1916, 2114, 5765, 6014, 4106, 4556, 5444, 5891, 4619, 5957, 1472, 2168)
ORDER BY Id;

SELECT COUNT(*) AS SoDongConKetQuaKhongPhaiHuy
FROM KetQuaKichBan
WHERE TrangThai >= 2 AND KetQuaDoLuong IS NOT NULL AND KetQuaDoLuong <> N'Huy';
GO

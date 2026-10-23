using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Unit tests cho HieuQuaKichBanService (Performance Tracking V2).
/// Kiểm thử: tổng hợp tóm tắt, lọc theo kỳ, nhóm theo loại kịch bản,
/// phân trang lịch sử, chi tiết JSON và trạng thái rỗng.
/// </summary>
public sealed class HieuQuaServiceTests
{
    // ===== Helpers =====

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static HieuQuaKichBanService TaoService(ApplicationDbContext db) => new(db);

    private static string KeHoachJson(decimal entry, decimal sl, decimal tp1) =>
        JsonSerializer.Serialize(new KeHoachGiaoDich
        {
            GiaVaoLenhMin = entry,
            GiaVaoLenhMax = entry,
            GiaDungLo = sl,
            GiaChotLoi1 = tp1,
            GiaChotLoi2 = tp1,
            DieuKienHuy = "Test"
        });

    /// <summary>Tạo một bản ghi đã kích hoạt + (tùy chọn) đã đo lường.</summary>
    private static KetQuaKichBanEntity TaoEntity(
        string symbol,
        LoaiKichBan loai,
        string? ketQuaDoLuong,
        decimal? phanTram,
        decimal? rr,
        int soNgayTruoc = 1,
        decimal giaVao = 100m,
        decimal? giaThoat = null) =>
        new()
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = DateTime.UtcNow.AddDays(-soNgayTruoc),
            ThoiGianKichHoat = DateTime.UtcNow.AddDays(-soNgayTruoc),
            KeHoachGiaoDichJson = KeHoachJson(giaVao, giaVao * 0.95m, giaVao * 1.1m),
            KetQuaDoLuong = ketQuaDoLuong,
            PhanTramLoiNhuan = phanTram,
            TyLeLaiLoThucTe = rr,
            GiaThoat = giaThoat,
        };

    // ===== Test 1: Tổng hợp GetTomTatAsync =====

    [Fact]
    public async Task GetTomTat_TongHop_DungSoLuongVaTrungBinh()
    {
        // Arrange: 2 thắng, 1 thua, 1 ngang, 1 chờ đo
        var db = NewDb();
        db.KetQuaKichBan.AddRange(
            TaoEntity("AAA", LoaiKichBan.NoHuongLen, "Thang", 10m, 2m),
            TaoEntity("BBB", LoaiKichBan.NoHuongLen, "Thang", 6m, 1.2m),
            TaoEntity("CCC", LoaiKichBan.HoiHoTro, "Thua", -5m, -1m),
            TaoEntity("DDD", LoaiKichBan.HoiHoTro, "Ngang", 0.5m, 0.1m),
            TaoEntity("EEE", LoaiKichBan.KietSuc, null, null, null)); // chờ đo
        await db.SaveChangesAsync();

        // Act
        var tomTat = await TaoService(db).GetTomTatAsync("all");

        // Assert
        Assert.Equal(5, tomTat.TongKichHoat);
        Assert.Equal(2, tomTat.Thang);
        Assert.Equal(1, tomTat.Thua);
        Assert.Equal(1, tomTat.Ngang);
        Assert.Equal(1, tomTat.ChoDo);

        // Tỷ lệ thắng loại "chờ đo" khỏi mẫu số: 2 / (2+1+1) = 50%
        Assert.Equal(50.00m, tomTat.TyLeThang);

        // TB R:R = (2 + 1.2 − 1 + 0.1) / 4 = 0.575 → 0.58
        Assert.Equal(0.58m, tomTat.TbRR);

        // TB % = (10 + 6 − 5 + 0.5) / 4 = 2.875 → 2.88
        Assert.Equal(2.88m, tomTat.TbPhanTram);
    }

    // ===== Test 2: Lọc theo kỳ (period) =====

    [Fact]
    public async Task GetTomTat_LocTheoKy_WeekMonthAll()
    {
        // Arrange: 1 bản ghi 3 ngày trước, 1 bản ghi 20 ngày trước, 1 bản ghi 100 ngày trước
        var db = NewDb();
        db.KetQuaKichBan.AddRange(
            TaoEntity("AAA", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m, soNgayTruoc: 3),
            TaoEntity("BBB", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m, soNgayTruoc: 20),
            TaoEntity("CCC", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m, soNgayTruoc: 100));
        await db.SaveChangesAsync();

        var service = TaoService(db);

        // Act + Assert: week (7 ngày) → chỉ bản ghi 3 ngày
        var week = await service.GetTomTatAsync("week");
        Assert.Equal(1, week.TongKichHoat);

        // month (30 ngày) → bản ghi 3 + 20 ngày
        var month = await service.GetTomTatAsync("month");
        Assert.Equal(2, month.TongKichHoat);

        // all → cả 3
        var all = await service.GetTomTatAsync("all");
        Assert.Equal(3, all.TongKichHoat);
    }

    // ===== Test 3: Nhóm theo loại kịch bản (tên tiếng Việt) =====

    [Fact]
    public async Task GetTomTat_NhomTheoLoaiKichBan_DungTenViet()
    {
        // Arrange
        var db = NewDb();
        db.KetQuaKichBan.AddRange(
            TaoEntity("AAA", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m),
            TaoEntity("BBB", LoaiKichBan.NoHuongLen, "Thua", -3m, -0.5m),
            TaoEntity("CCC", LoaiKichBan.QuetThanhKhoan, "Ngang", 0m, 0m));
        await db.SaveChangesAsync();

        // Act
        var tomTat = await TaoService(db).GetTomTatAsync("all");

        // Assert: 2 nhóm, sắp xếp giảm dần theo Tong
        Assert.Equal(2, tomTat.TheoLoaiKichBan.Count);

        var noHuong = tomTat.TheoLoaiKichBan.First(s => s.TenKichBan == "Nổ hướng lên");
        Assert.Equal(2, noHuong.Tong);
        Assert.Equal(1, noHuong.Thang);
        Assert.Equal(1, noHuong.Thua);
        Assert.Equal(0, noHuong.Ngang);
        Assert.Equal(50.00m, noHuong.TyLeThang);
        Assert.Equal(0.25m, noHuong.TbRR); // (1 − 0.5) / 2

        var quet = tomTat.TheoLoaiKichBan.First(s => s.TenKichBan == "Quét thanh khoản");
        Assert.Equal(1, quet.Tong);
        Assert.Equal(1, quet.Ngang);
        Assert.Equal(0m, quet.TyLeThang);
    }

    // ===== Test 4: Phân trang GetLichSuAsync =====

    [Fact]
    public async Task GetLichSu_PhanTrang_DungSoItemVaTotalCount()
    {
        // Arrange: 25 bản ghi đã kích hoạt
        var db = NewDb();
        for (var i = 0; i < 25; i++)
            db.KetQuaKichBan.Add(TaoEntity($"S{i:D2}", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m));
        await db.SaveChangesAsync();

        // Act: trang 1, 10 item
        var (items, totalCount) = await TaoService(db).GetLichSuAsync(page: 1, pageSize: 10);

        // Assert
        Assert.Equal(25, totalCount);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public async Task GetLichSu_LoaiDongChuaKichHoatVaDongBiHuy()
    {
        // Arrange: 2 lệnh thật + 1 chưa từng kích hoạt (HetHanVao) + 1 bị huỷ sau kích hoạt ("Huy")
        var db = NewDb();
        db.KetQuaKichBan.AddRange(
            TaoEntity("AAA", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m),
            TaoEntity("BBB", LoaiKichBan.HoiHoTro, null, null, null),
            TaoEntityKy("X1", LoaiKichBan.NoHuongLen, TrangThaiKichBan.HuyLenh, null, "Huy"),
            TaoEntityKy("X2", LoaiKichBan.QuetThanhKhoan, TrangThaiKichBan.DaKichHoat,
                DateTime.UtcNow.AddDays(-2), "Huy"));
        await db.SaveChangesAsync();

        // Act
        var (items, totalCount) = await TaoService(db).GetLichSuAsync(page: 1, pageSize: 10);

        // Assert: TotalCount và tập item đều theo bộ lọc mới — chỉ 2 lệnh thật
        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
        Assert.Equal(new[] { "AAA", "BBB" }, items.Select(i => i.Symbol).OrderBy(s => s));
    }

    [Fact]
    public async Task GetLichSu_TrangCuoi_TraSoItemConLai()
    {
        // Arrange: 25 bản ghi
        var db = NewDb();
        for (var i = 0; i < 25; i++)
            db.KetQuaKichBan.Add(TaoEntity($"S{i:D2}", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m));
        await db.SaveChangesAsync();

        // Act: trang 3, 10 item → còn 5
        var (items, totalCount) = await TaoService(db).GetLichSuAsync(page: 3, pageSize: 10);

        // Assert
        Assert.Equal(25, totalCount);
        Assert.Equal(5, items.Count);
    }

    // ===== Test 5: GetChiTietAsync =====

    [Fact]
    public async Task GetChiTiet_TraVeDayDuJsonBlob()
    {
        // Arrange
        var db = NewDb();
        var entity = TaoEntity("HPG", LoaiKichBan.HoiHoTro, "Thang", 8m, 1.6m,
            giaVao: 100m, giaThoat: 108m);
        entity.BangChupChiBaoJson = "{\"rsi\":65}";
        entity.DanhSachBangChungJson = "[\"bang-chung-1\"]";
        db.KetQuaKichBan.Add(entity);
        await db.SaveChangesAsync();

        // Act
        var chiTiet = await TaoService(db).GetChiTietAsync((int)entity.Id);

        // Assert
        Assert.NotNull(chiTiet);
        Assert.Equal("HPG", chiTiet!.Lenh.Symbol);
        Assert.Equal("Hồi hỗ trợ", chiTiet.Lenh.LoaiKichBan);
        Assert.Equal("Thắng", chiTiet.Lenh.KetQua);
        Assert.Equal(100m, chiTiet.Lenh.GiaVao);
        Assert.Equal(108m, chiTiet.Lenh.GiaThoat);
        Assert.Equal("{\"rsi\":65}", chiTiet.BangChupChiBaoJson);
        Assert.Equal("[\"bang-chung-1\"]", chiTiet.BangChungJson);
        Assert.NotNull(chiTiet.KeHoachJson);
    }

    [Fact]
    public async Task GetChiTiet_KhongTonTai_TraVeNull()
    {
        // Arrange
        var db = NewDb();

        // Act
        var chiTiet = await TaoService(db).GetChiTietAsync(999999);

        // Assert
        Assert.Null(chiTiet);
    }

    // ===== Test 6: Trạng thái rỗng =====

    [Fact]
    public async Task GetTomTat_Rong_TraVeKhong()
    {
        // Arrange: DB rỗng
        var db = NewDb();

        // Act
        var tomTat = await TaoService(db).GetTomTatAsync("all");

        // Assert
        Assert.Equal(0, tomTat.TongKichHoat);
        Assert.Equal(0, tomTat.Thang);
        Assert.Equal(0, tomTat.Thua);
        Assert.Equal(0, tomTat.Ngang);
        Assert.Equal(0, tomTat.ChoDo);
        Assert.Equal(0m, tomTat.TyLeThang);
        Assert.Equal(0m, tomTat.TbRR);
        Assert.Equal(0m, tomTat.TbPhanTram);
        Assert.Empty(tomTat.TheoLoaiKichBan);
    }

    // ===== Test phụ: chỉ tính bản ghi đã kích hoạt trở lên =====

    [Fact]
    public async Task GetTomTat_BoQuaBanGhiChuaKichHoat()
    {
        // Arrange: 1 FORMING (chưa kích hoạt) + 1 đã kích hoạt
        var db = NewDb();
        var forming = TaoEntity("AAA", LoaiKichBan.NoHuongLen, null, null, null);
        forming.TrangThai = TrangThaiKichBan.DangHinhThanh; // < DaKichHoat
        var daKichHoat = TaoEntity("BBB", LoaiKichBan.NoHuongLen, "Thang", 5m, 1m);
        db.KetQuaKichBan.AddRange(forming, daKichHoat);
        await db.SaveChangesAsync();

        // Act
        var tomTat = await TaoService(db).GetTomTatAsync("all");

        // Assert: chỉ bản ghi đã kích hoạt được tính
        Assert.Equal(1, tomTat.TongKichHoat);
        Assert.Equal(1, tomTat.Thang);
    }

    // ===== Test phụ: loại dòng chưa từng kích hoạt (HetHanVao) và dòng bị huỷ sau kích hoạt ("Huy") =====

    /// <summary>Tạo bản ghi với ThoiGianKichHoat tùy ý (null = chưa từng kích hoạt).</summary>
    private static KetQuaKichBanEntity TaoEntityKy(
        string symbol,
        LoaiKichBan loai,
        TrangThaiKichBan trangThai,
        DateTime? thoiGianKichHoat,
        string? ketQuaDoLuong) =>
        new()
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = trangThai,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = thoiGianKichHoat ?? DateTime.UtcNow,
            ThoiGianKichHoat = thoiGianKichHoat,
            KeHoachGiaoDichJson = KeHoachJson(100m, 95m, 110m),
            KetQuaDoLuong = ketQuaDoLuong,
        };

    [Fact]
    public async Task GetTomTat_LoaiDongChuaTungKichHoatVaDongBiHuy()
    {
        // Arrange: 6 lệnh mua thật (2 thắng, 2 thua, 1 ngang, 1 chờ đo — phân bố theo kỳ)
        //          + 3 dòng phải loại:
        //          (a) HuyLenh + HetHanVao, chưa từng kích hoạt (ThoiGianKichHoat = null)
        //          (b) Huy sau kích hoạt vì mua lặp (đã có ThoiGianKichHoat)
        //          (c) HuyNgoaiVung sau kích hoạt
        var db = NewDb();
        db.KetQuaKichBan.AddRange(
            TaoEntity("A1", LoaiKichBan.NoHuongLen, "Thang", 8m, 1.6m, soNgayTruoc: 3),
            TaoEntity("A2", LoaiKichBan.HoiHoTro, "Thang", 5m, 1m, soNgayTruoc: 3),
            TaoEntity("B1", LoaiKichBan.NoHuongLen, "Thua", -4m, -0.8m, soNgayTruoc: 15),
            TaoEntity("B2", LoaiKichBan.QuetThanhKhoan, "Thua", -2m, -0.4m, soNgayTruoc: 15),
            TaoEntity("C1", LoaiKichBan.HoiHoTro, "Ngang", 0.3m, 0.06m, soNgayTruoc: 60),
            TaoEntity("D1", LoaiKichBan.NoHuongLen, null, null, null, soNgayTruoc: 10),
            TaoEntityKy("X1", LoaiKichBan.NoHuongLen, TrangThaiKichBan.HuyLenh, null, "Huy"),
            TaoEntityKy("X2", LoaiKichBan.HoiHoTro, TrangThaiKichBan.DaKichHoat,
                DateTime.UtcNow.AddDays(-2), "Huy"),
            TaoEntityKy("X3", LoaiKichBan.QuetThanhKhoan, TrangThaiKichBan.HuyLenh,
                DateTime.UtcNow.AddDays(-40), "Huy"));
        await db.SaveChangesAsync();

        var service = TaoService(db);

        // Act + Assert: period=all — chỉ 6 lệnh thật, 3 dòng huỷ bị loại ở mọi kỳ
        var all = await service.GetTomTatAsync("all");
        Assert.Equal(6, all.TongKichHoat);
        Assert.Equal(2, all.Thang);
        Assert.Equal(2, all.Thua);
        Assert.Equal(1, all.Ngang);
        Assert.Equal(1, all.ChoDo);
        Assert.Equal(all.TongKichHoat, all.Thang + all.Thua + all.Ngang + all.ChoDo);

        // month (30 ngày) — 5 lệnh: 2 thắng (3 ngày), 2 thua (15 ngày), 1 chờ đo (10 ngày)
        var month = await service.GetTomTatAsync("month");
        Assert.Equal(5, month.TongKichHoat);
        Assert.Equal(2, month.Thang);
        Assert.Equal(2, month.Thua);
        Assert.Equal(0, month.Ngang);
        Assert.Equal(1, month.ChoDo);
        Assert.Equal(month.TongKichHoat, month.Thang + month.Thua + month.Ngang + month.ChoDo);

        // week (7 ngày) — 2 lệnh thắng 3 ngày trước; dòng X2 huỷ 2 ngày trước không được lọt
        var week = await service.GetTomTatAsync("week");
        Assert.Equal(2, week.TongKichHoat);
        Assert.Equal(2, week.Thang);
        Assert.Equal(0, week.Thua);
        Assert.Equal(0, week.Ngang);
        Assert.Equal(0, week.ChoDo);

        // Theo loại kịch bản: không còn nhóm ma cho dòng chưa kích hoạt/bị huỷ
        Assert.Equal(3, all.TheoLoaiKichBan.First(s => s.TenKichBan == "Nổ hướng lên").Tong);
        Assert.Equal(2, all.TheoLoaiKichBan.First(s => s.TenKichBan == "Hồi hỗ trợ").Tong);
        Assert.Equal(1, all.TheoLoaiKichBan.First(s => s.TenKichBan == "Quét thanh khoản").Tong);
        Assert.Equal(all.TongKichHoat, all.TheoLoaiKichBan.Sum(s => s.Tong));
    }

    [Fact]
    public async Task GetTomTat_DongChuaCoKetQuaNhungDaHuyVanBiLoai()
    {
        // Arrange: dòng Huy chưa kịp ghi KetQuaDoLuong nhưng LyDoThoatHet = HuyMuaLap → không đếm
        // (lọc theo KetQuaDoLuong; kiểm hành vi hiện tại để chốt giới hạn).
        var db = NewDb();
        var huyChuaGhiKetQua = TaoEntityKy("Y1", LoaiKichBan.NoHuongLen, TrangThaiKichBan.DaKichHoat,
            DateTime.UtcNow.AddDays(-1), null);
        huyChuaGhiKetQua.LyDoThoatHet = "HuyMuaLap";
        db.KetQuaKichBan.Add(huyChuaGhiKetQua);
        await db.SaveChangesAsync();

        // Act
        var all = await TaoService(db).GetTomTatAsync("all");

        // Assert: vẫn vào mẫu với tư cách "chờ đo" — chỉ bị loại khi KetQuaDoLuong = "Huy".
        Assert.Equal(1, all.TongKichHoat);
        Assert.Equal(1, all.ChoDo);
    }
}

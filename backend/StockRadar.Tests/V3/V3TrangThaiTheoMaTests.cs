using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Tests.V3;

/// <summary>
/// Spec 016 D5 — trạng thái V3 rút gọn cho huy hiệu Watchlist: đang giữ hơn chờ mua hơn có tín hiệu mới.
/// Dữ liệu bịa, dùng DB trong bộ nhớ.
/// </summary>
public sealed class V3TrangThaiTheoMaTests
{
    private static readonly DateOnly NgayMoi = new(2026, 11, 5);
    private static readonly DateOnly NgayCu = new(2026, 11, 3);
    private static readonly DateTime T0 = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static V3HieuQuaService NewService(ApplicationDbContext db) =>
        new(db, Options.Create(new V3Options()));

    private static long _id;

    private static TinHieuV3Entity ThemTinHieu(
        ApplicationDbContext db, string symbol, DateOnly ngay, TrangThaiTinHieuV3 trangThai)
    {
        var e = new TinHieuV3Entity
        {
            Id = ++_id,
            Symbol = symbol,
            Loai = LoaiKichBanV3.KB1,
            NgayTinHieu = ngay,
            GiaTinHieu = 20m,
            Hang = 1,
            GiaTriXepHang = 1m,
            TrangThai = trangThai,
            CreatedAt = T0,
            UpdatedAt = T0,
        };
        db.TinHieuV3.Add(e);
        return e;
    }

    private static void ThemViThe(ApplicationDbContext db, TinHieuV3Entity tinHieu, TrangThaiViTheV3 trangThai) =>
        db.ViTheV3.Add(new ViTheV3Entity
        {
            Id = ++_id,
            TinHieuV3Id = tinHieu.Id,
            Symbol = tinHieu.Symbol,
            Loai = tinHieu.Loai,
            NgayTinHieu = tinHieu.NgayTinHieu,
            TrangThai = trangThai,
            CreatedAt = T0,
            UpdatedAt = T0,
        });

    [Fact]
    public async Task MaDangGiu_DuocGanDangGiu()
    {
        await using var db = NewDb();
        ThemViThe(db, ThemTinHieu(db, "AAA", NgayCu, TrangThaiTinHieuV3.DaVaoLenh), TrangThaiViTheV3.DangGiu);
        await db.SaveChangesAsync();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync(["AAA"]);

        Assert.Equal(TrangThaiV3RutGon.DangGiu, kq["AAA"]);
    }

    [Fact]
    public async Task MaChoMua_DuocGanChoMua()
    {
        await using var db = NewDb();
        ThemViThe(db, ThemTinHieu(db, "BBB", NgayMoi, TrangThaiTinHieuV3.DaVaoLenh), TrangThaiViTheV3.ChoMua);
        await db.SaveChangesAsync();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync(["BBB"]);

        Assert.Equal(TrangThaiV3RutGon.ChoMua, kq["BBB"]);
    }

    [Theory]
    [InlineData(TrangThaiTinHieuV3.DaBao)]
    [InlineData(TrangThaiTinHieuV3.BiCatDanhMuc)]
    public async Task MaCoTinHieuPhienMoiNhat_ChuaVaoLenh_DuocGanCoTinHieuMoi(TrangThaiTinHieuV3 trangThai)
    {
        await using var db = NewDb();
        ThemTinHieu(db, "CCC", NgayMoi, trangThai);
        await db.SaveChangesAsync();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync(["CCC"]);

        Assert.Equal(TrangThaiV3RutGon.CoTinHieuMoi, kq["CCC"]);
    }

    [Fact]
    public async Task TinHieuPhienCu_KhongTinh()
    {
        await using var db = NewDb();
        ThemTinHieu(db, "DDD", NgayCu, TrangThaiTinHieuV3.DaBao);
        ThemTinHieu(db, "EEE", NgayMoi, TrangThaiTinHieuV3.DaBao); // làm ngày mới nhất là NgayMoi
        await db.SaveChangesAsync();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync(["DDD", "EEE"]);

        Assert.False(kq.ContainsKey("DDD"));
        Assert.True(kq.ContainsKey("EEE"));
    }

    [Fact]
    public async Task TinHieuBiHuy_VaViTheDaThoat_KhongTinh()
    {
        await using var db = NewDb();
        ThemTinHieu(db, "FFF", NgayMoi, TrangThaiTinHieuV3.BiHuy);
        ThemViThe(db, ThemTinHieu(db, "GGG", NgayCu, TrangThaiTinHieuV3.DaVaoLenh), TrangThaiViTheV3.DaThoat);
        await db.SaveChangesAsync();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync(["FFF", "GGG"]);

        Assert.Empty(kq);
    }

    [Fact]
    public async Task MaKhongCoGi_KhongCoTrongKetQua()
    {
        await using var db = NewDb();
        ThemTinHieu(db, "HHH", NgayMoi, TrangThaiTinHieuV3.DaBao);
        await db.SaveChangesAsync();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync(["ZZZ"]);

        Assert.Empty(kq);
    }

    [Fact]
    public async Task DanhSachRong_TraRong_KhongTruyVan()
    {
        await using var db = NewDb();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync([]);

        Assert.Empty(kq);
    }

    [Fact]
    public async Task MaVuaDangGiuVuaCoTinHieuMoi_UuTienDangGiu()
    {
        await using var db = NewDb();
        ThemViThe(db, ThemTinHieu(db, "III", NgayCu, TrangThaiTinHieuV3.DaVaoLenh), TrangThaiViTheV3.DangGiu);
        ThemTinHieu(db, "III", NgayMoi, TrangThaiTinHieuV3.BiCatDanhMuc);
        await db.SaveChangesAsync();

        var kq = await NewService(db).LayTrangThaiTheoMaAsync(["III"]);

        Assert.Equal(TrangThaiV3RutGon.DangGiu, kq["III"]);
    }

    [Theory]
    [InlineData(true, true, TrangThaiV3RutGon.DangGiu)]
    [InlineData(false, true, TrangThaiV3RutGon.ChoMua)]
    public void ChonTrangThaiRutGon_UuTienDangGiuRoiChoMuaRoiTinHieu(bool dangGiu, bool coTinHieuMoi, string mongDoi)
    {
        var viThe = dangGiu
            ? new[] { TrangThaiViTheV3.ChoMua, TrangThaiViTheV3.DangGiu }
            : [TrangThaiViTheV3.ChoMua];

        var ketQua = V3HieuQuaService.ChonTrangThaiRutGon(viThe, coTinHieuMoi);

        Assert.Equal(mongDoi, ketQua);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// Giá tín hiệu V3 chụp theo CHIỀU lệnh (FR-040): mua → giá vào; bán → giá thoát
/// (chưa có giá thoát → fallback giá vào); bậc mua thứ 2 trở đi → không có ảnh chụp.
/// Lệch bình quân chỉ gộp lệnh CÙNG CHIỀU + CÙNG mức giá chụp trong một tín hiệu.
/// </summary>
public sealed class NhatKyN8GiaChupTests
{
    private static readonly DateTime T0 = new(2026, 10, 10, 2, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NhatKyService NewService(ApplicationDbContext db) =>
        new(new EfNhatKyRepository(db), new EfNhatKyTinHieuRepository(db), Options.Create(new RealizedPnlOptions()));

    private static void SeedV3(
        ApplicationDbContext db, long id,
        decimal? giaVao = 27_300m, decimal? giaThoat = null,
        LoaiKichBanV3 loai = LoaiKichBanV3.KB1)
    {
        db.TinHieuV3.Add(new TinHieuV3Entity
        {
            Id = id,
            Symbol = "HPG",
            Loai = loai,
            NgayTinHieu = DateOnly.FromDateTime(T0),
            GiaTinHieu = giaVao ?? 0m,
            Hang = 1,
            MuaMoCuaPhienSau = true,
            TrangThai = TrangThaiTinHieuV3.DaVaoLenh,
            GiaVao = giaVao,
            GiaThoat = giaThoat,
            CreatedAt = T0,
            UpdatedAt = T0,
        });
        db.SaveChanges();
    }

    private static TaoLenhRequest Lenh(string chieu, DateTime thoiDiem, decimal gia, decimal kl, string ma = "341") =>
        new("HPG", chieu, thoiDiem, gia, kl, null, "V3", ma, null, null, null, null);

    private static async Task<LenhDto> MuaAsync(NhatKyService svc, Guid user, decimal gia, decimal kl, DateTime? t = null) =>
        await svc.TaoLenhAsync(user, Lenh("Mua", t ?? T0, gia, kl), default);

    private static async Task<LenhDto> BanAsync(NhatKyService svc, Guid user, decimal gia, decimal kl, DateTime t) =>
        await svc.TaoLenhAsync(user, Lenh("Ban", t, gia, kl), default);

    [Fact]
    public async Task V3_Mua_ChupGiaVao_Ban_ChupGiaThoat()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m, giaThoat: 30_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var mua = await MuaAsync(svc, user, 27_500m, 1_000);
        var s1 = await BanAsync(svc, user, 30_500m, 1_000, T0.AddDays(1));

        Assert.Equal(27_300m, mua.GiaTinHieuChup);
        Assert.Equal(30_000m, s1.GiaTinHieuChup);
        Assert.Equal(500m, s1.LechSoTinHieu);
    }

    [Fact]
    public async Task V3_MuaLanHai_KhongCoBacMua2_LechNull()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var mua1 = await MuaAsync(svc, user, 27_500m, 100);
        var mua2 = await MuaAsync(svc, user, 28_700m, 100, T0.AddDays(1));

        Assert.Equal(27_300m, mua1.GiaTinHieuChup);
        Assert.Null(mua2.GiaTinHieuChup); // V3 chỉ một nhịp mua → bậc 2 không có ảnh chụp
        Assert.Null(mua2.LechSoTinHieu);
    }

    [Fact]
    public async Task V3_LenhBanSoGiaThoatKhongPhaiGiaVao()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m, giaThoat: 29_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_300m, 1_000);
        var s1 = await BanAsync(svc, user, 28_900m, 1_000, T0.AddDays(1));

        // 28.900 − 29.000 (giá thoát) = −100, không so với giá vào.
        Assert.Equal(-100m, s1.LechSoTinHieu);
        Assert.Equal(-0.34m, s1.LechSoTinHieuPhanTram);
    }

    [Fact]
    public async Task V3_ChuaCoGiaThoat_BanFallbackGiaVao()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m, giaThoat: null); // vị thế chưa đóng
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 500);
        var s1 = await BanAsync(svc, user, 28_900m, 500, T0.AddDays(1));

        Assert.Equal(27_300m, s1.GiaTinHieuChup); // ban1 = fallback giá vào khi chưa có giá thoát
        Assert.Equal(1_600m, s1.LechSoTinHieu);
    }

    [Fact]
    public async Task V3_LechBinhQuan_GopLenhBanCungMucChup()
    {
        // Hai lệnh bán cùng snapshot giá thoát → BQ gộp theo KL; mua không bị trộn.
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_000m, giaThoat: 30_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var mua = await MuaAsync(svc, user, 27_000m, 1_000);
        await BanAsync(svc, user, 30_500m, 500, T0.AddDays(1));
        var s2 = await BanAsync(svc, user, 31_000m, 500, T0.AddDays(2));

        Assert.Equal(0m, mua.LechSoTinHieu); // BQ khớp mua = 27.000 → lệch 0

        // BQ hai lệnh bán = (30.500×500 + 31.000×500)/1000 = 30.750 → lệch +750.
        var s2Sau = await svc.LayMotAsync(user, s2.Id, default);
        Assert.Equal(750m, s2Sau!.LechSoTinHieu);
    }

    [Fact]
    public async Task V3_SuaGiaKhopKhongDoiThuTu_GiuGiaDaChup()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m, giaThoat: 29_000m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        await MuaAsync(svc, user, 27_500m, 500);
        var s1 = await BanAsync(svc, user, 29_200m, 500, T0.AddDays(1));

        var sua = await svc.SuaLenhAsync(user, s1.Id, Lenh("Ban", T0.AddDays(1), 29_800m, 500), default);
        Assert.Equal(29_000m, sua!.GiaTinHieuChup); // bậc không đổi → không đọc lại nguồn
        Assert.Equal(800m, sua.LechSoTinHieu);
    }

    [Fact]
    public async Task V3_ThemLenhMuaSomHon_LenhCuVanChupGiaVao()
    {
        // Cả hai lệnh mua cùng một tín hiệu V3 → bậc 1 chụp giá vào, bậc 2 không có ảnh chụp.
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        var svc = NewService(db);
        var user = Guid.NewGuid();

        var old = await MuaAsync(svc, user, 28_700m, 100, T0.AddDays(1));
        await MuaAsync(svc, user, 27_500m, 100, T0); // khớp sớm hơn → old dịch xuống bậc 2

        var oldSau = await svc.LayMotAsync(user, old.Id, default);
        Assert.Null(oldSau!.GiaTinHieuChup); // V3 không có ảnh chụp cho nhịp mua thứ hai
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// US1 — ghi lệnh: validation (400), bán quá vị thế chặn cứng (Q7/422),
/// giá vốn bình quân (Q5), phí tiền → % hiệu dụng (FR-005), sửa/xoá recompute (AC3 US1).
/// </summary>
public sealed class NhatKyLenhTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NhatKyService NewService(ApplicationDbContext db, RealizedPnlOptions? fees = null)
    {
        INhatKyRepository repo = new EfNhatKyRepository(db);
        var options = Options.Create(fees ?? new RealizedPnlOptions());
        return new NhatKyService(repo, new EfNhatKyTinHieuRepository(db), options);
    }

    private static TaoLenhRequest Mua(decimal gia, decimal kl, decimal? phi = null, DateTime? thoiDiem = null, string symbol = "hpg") =>
        new(symbol, "Mua", thoiDiem ?? T0, gia, kl, phi, null, null, null, null, null, null);

    private static TaoLenhRequest Ban(decimal gia, decimal kl, DateTime? thoiDiem = null, string symbol = "hpg") =>
        new(symbol, "Ban", thoiDiem ?? T0.AddDays(1), gia, kl, null, null, null, null, null, null, null);

    [Fact]
    public async Task TaoLenh_ThieuGiaKhop_Bao400()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyValidationException>(() =>
            svc.TaoLenhAsync(Guid.NewGuid(), Mua(0m, 100), default));
    }

    [Fact]
    public async Task TaoLenh_ThieuKhoiLuong_Bao400()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyValidationException>(() =>
            svc.TaoLenhAsync(Guid.NewGuid(), Mua(27_500m, 0m), default));
    }

    [Fact]
    public async Task TaoLenh_ThoiDiemTuongLai_Bao400()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyValidationException>(() =>
            svc.TaoLenhAsync(Guid.NewGuid(), Mua(27_500m, 100, thoiDiem: DateTime.UtcNow.AddDays(1)), default));
    }

    [Fact]
    public async Task TaoLenh_DieuChinhThieuGhiChu_Bao400()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        // Điều chỉnh nhận cổ tức bằng cp +100 nhưng thiếu ghi chú (N7 bắt buộc).
        var req = new TaoLenhRequest("hpg", "DieuChinh", T0, null, null, null, null, null, null, null, 100m, null);
        await Assert.ThrowsAsync<NhatKyValidationException>(() => svc.TaoLenhAsync(userId, req, default));
    }

    [Fact]
    public async Task TaoLenh_DieuChinhThieuKLvaVon_Bao400()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var req = new TaoLenhRequest("hpg", "DieuChinh", T0, null, null, null, null, null, null, "cấn trừ tiền", 0m, 0m);
        await Assert.ThrowsAsync<NhatKyValidationException>(() => svc.TaoLenhAsync(Guid.NewGuid(), req, default));
    }

    [Fact]
    public async Task Ban_KhiChuaCoMua_ChanCungKhongLuu()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<NhatKyVuotBanException>(() => svc.TaoLenhAsync(userId, Ban(30_000m, 100), default));
        Assert.Equal(0m, ex.KhoiLuongHienCo);
        Assert.Equal(0, (await db.NhatKyGiaoDich.CountAsync(x => x.UserId == userId)));
    }

    [Fact]
    public async Task Ban_QuaViThe_ChanCungKemKhoLuongHienCo()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, Mua(27_500m, 500, thoiDiem: T0), default);

        var ex = await Assert.ThrowsAsync<NhatKyVuotBanException>(() =>
            svc.TaoLenhAsync(userId, Ban(30_000m, 800, thoiDiem: T0.AddDays(1)), default));
        Assert.Equal(500m, ex.KhoiLuongHienCo);
        Assert.Equal(800m, ex.KhoiLuongXinBan);
        // Chỉ có đúng 1 lệnh (lệnh mua) được lưu — lệnh bán bị chặn, không lưu (Q7).
        Assert.Equal(1, await db.NhatKyGiaoDich.CountAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task GiaVonBinhQuan_HaiLoKhacKL_QuyTheoKhoiLuong()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0m });
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, Mua(10_000m, 1_000, thoiDiem: T0), default);
        await svc.TaoLenhAsync(userId, Mua(12_000m, 2_000, thoiDiem: T0.AddDays(1)), default);

        var viThe = await svc.LayViTheAsync(userId, default);
        var hpg = Assert.Single(viThe);
        Assert.Equal(3_000m, hpg.KhoiLuongMo);
        // BQ = (1000×10000 + 2000×12000) / 3000 = 11333.3333
        Assert.Equal(11_333.3333m, hpg.GiaVonBinhQuan);
    }

    [Fact]
    public async Task Ban_MotPhan_KhongDoiGiaVonPhanConLai()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0m });
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, Mua(10_000m, 1_000, thoiDiem: T0), default);
        await svc.TaoLenhAsync(userId, Ban(11_000m, 400, thoiDiem: T0.AddDays(1)), default);

        var hpg = Assert.Single(await svc.LayViTheAsync(userId, default));
        Assert.Equal(600m, hpg.KhoiLuongMo);
        Assert.Equal(10_000m, hpg.GiaVonBinhQuan);
        Assert.Equal(1, hpg.SoLenhDaBan);
    }

    [Fact]
    public async Task DieuChinh_NhanCoTucBangCp_TangKLDoBQ()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0m });
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, Mua(20_000m, 1_000, thoiDiem: T0), default);
        // Nhận 100 cp cổ tức: KL mở 1100, tổng vốn không đổi → BQ giảm còn 18181.8182.
        var req = new TaoLenhRequest("hpg", "DieuChinh", T0.AddDays(1), null, null, null, null, null, null, "cổ tức cp 10%", 100m, null);
        await svc.TaoLenhAsync(userId, req, default);

        var hpg = Assert.Single(await svc.LayViTheAsync(userId, default));
        Assert.Equal(1_100m, hpg.KhoiLuongMo);
        Assert.Equal(18_181.8182m, hpg.GiaVonBinhQuan);
    }

    [Fact]
    public async Task SuaGiaLoDau_BinhQuanRecomputeNgay()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0m });
        var userId = Guid.NewGuid();

        var lo1 = await svc.TaoLenhAsync(userId, Mua(10_000m, 1_000, thoiDiem: T0), default);
        await svc.TaoLenhAsync(userId, Mua(12_000m, 1_000, thoiDiem: T0.AddDays(1)), default);

        // AC3 US1: sửa giá lô đầu 10.000 → 14.000 thì BQ phải đổi theo (projection on-the-fly).
        await svc.SuaLenhAsync(userId, lo1.Id, Mua(14_000m, 1_000, thoiDiem: T0), default);

        var hpg = Assert.Single(await svc.LayViTheAsync(userId, default));
        Assert.Equal(13_000m, hpg.GiaVonBinhQuan);
    }

    [Fact]
    public async Task PhiNull_DungPhanTramCuaConfig()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0.15m });
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, Mua(100_000m, 100), default);

        var hpg = Assert.Single(await svc.LayViTheAsync(userId, default));
        // 100.000 × (1 + 0.15%) = 100.150/cp.
        Assert.Equal(100_150m, hpg.GiaVonBinhQuan);
    }

    [Fact]
    public async Task PhiTienThuc_QuyVePhanTramHieuDung()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0.15m });
        var userId = Guid.NewGuid();

        // Giá 27.500 × 1.000 = 27.500.000; phí thực 55.000 → 0,2% hiệu dụng (FR-005 override config).
        await svc.TaoLenhAsync(userId, Mua(27_500m, 1_000, phi: 55_000m), default);

        var hpg = Assert.Single(await svc.LayViTheAsync(userId, default));
        Assert.Equal(27_555m, hpg.GiaVonBinhQuan);
    }

    [Fact]
    public async Task XoaLoBan_ViTheRecomputeLai()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0m });
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, Mua(10_000m, 1_000, thoiDiem: T0), default);
        var loBan = await svc.TaoLenhAsync(userId, Ban(11_000m, 400, thoiDiem: T0.AddDays(1)), default);

        Assert.True(await svc.XoaLenhAsync(userId, loBan.Id, default));

        var hpg = Assert.Single(await svc.LayViTheAsync(userId, default));
        Assert.Equal(1_000m, hpg.KhoiLuongMo);
        Assert.Equal(0, hpg.SoLenhDaBan);
    }

    [Fact]
    public async Task SymBoHoaHoaDonViVaChuanHoaChuHoa()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        var lenh = await svc.TaoLenhAsync(userId, Mua(10_000m, 100, symbol: "hpg"), default);
        Assert.Equal("HPG", lenh.Symbol);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.MuaTet;

/// <summary>
/// Spec 020 đợt 2c — giỏ Mùa Tết ghi trong Nhật ký lệnh với nguồn MUATET: ghi lệnh không cần bảng tín hiệu,
/// giỏ = mã có lệnh Mua MUATET đúng mùa còn giữ, thống kê khớp tín hiệu không đếm MUATET.
/// Giá nghìn đồng/cổ, lệnh bịa, không phải dữ liệu thật.
/// </summary>
public sealed class MuaTetNhatKyTests
{
    private static readonly DateTime T0 = new(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc); // 10:00 giờ VN; lệnh bán +3 ngày vẫn ở quá khứ
    private static readonly Guid UserA = Guid.NewGuid();
    private const string Mua2627 = "2026-27";

    private static NhatKyService TaoService()
    {
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        INhatKyRepository repo = new EfNhatKyRepository(db);
        return new NhatKyService(repo, new EfNhatKyTinHieuRepository(db), Options.Create(new RealizedPnlOptions()));
    }

    private static TaoLenhRequest Lenh(
        string symbol, string loai, decimal gia, decimal kl, DateTime? luc = null,
        string? nguon = NhatKyService.NguonMuaTet, string? ma = Mua2627) =>
        new(symbol, loai, luc ?? T0, gia, kl, null, nguon, ma, null, null, null, null);

    [Fact]
    public async Task Ghi_lenh_nguon_MUATET_khong_can_tin_hieu_va_khong_chup_gia()
    {
        var service = TaoService();

        var lenh = await service.TaoLenhAsync(UserA, Lenh("msb", "Mua", 16.5m, 500));

        Assert.Equal("MUATET", lenh.NguonTinHieu);
        Assert.Equal(Mua2627, lenh.MaTinHieu);
        Assert.Null(lenh.GiaTinHieuChup);
    }

    [Fact]
    public async Task Gio_chi_gom_ma_co_lenh_Mua_MUATET_dung_mua_va_con_giu()
    {
        var service = TaoService();
        await service.TaoLenhAsync(UserA, Lenh("MSB", "Mua", 16.5m, 500));
        await service.TaoLenhAsync(UserA, Lenh("MSB", "Ban", 17m, 200, T0.AddDays(3)));   // bán bớt → còn 300
        await service.TaoLenhAsync(UserA, Lenh("BAF", "Mua", 30m, 300));
        await service.TaoLenhAsync(UserA, Lenh("BAF", "Ban", 31m, 300, T0.AddDays(3)));   // bán hết → rời giỏ
        await service.TaoLenhAsync(UserA, Lenh("HPG", "Mua", 25m, 100, nguon: null, ma: null)); // không gắn nguồn
        await service.TaoLenhAsync(UserA, Lenh("VPI", "Mua", 59m, 100, ma: "2025-26"));       // mùa khác

        var gio = await service.LayGioMuaTetAsync(UserA, Mua2627);

        var msb = Assert.Single(gio);
        Assert.Equal("MSB", msb.Symbol);
        Assert.Equal(300m, msb.KhoiLuongMo);
        Assert.True(msb.GiaVonBinhQuan > 16.5m, "giá vốn bình quân gồm phí mua theo cấu hình");
        Assert.Equal(T0, msb.MuaDauUtc);
    }

    [Fact]
    public async Task Tai_khoan_khac_khong_thay_gio()
    {
        var service = TaoService();
        await service.TaoLenhAsync(UserA, Lenh("MSB", "Mua", 16.5m, 500));

        Assert.Empty(await service.LayGioMuaTetAsync(Guid.NewGuid(), Mua2627));
    }

    [Fact]
    public async Task Thong_ke_khop_tin_hieu_khong_dem_lenh_MUATET()
    {
        var service = TaoService();
        await service.TaoLenhAsync(UserA, Lenh("MSB", "Mua", 16.5m, 500));

        var tk = await service.LayThongKeAsync(UserA, "all");

        Assert.Equal(0, tk.TinHieuCoLenh);
        Assert.Null(tk.TyLeKhopTinHieu);
        Assert.Null(tk.LechTBSoTinHieuPhanTram);
    }
}

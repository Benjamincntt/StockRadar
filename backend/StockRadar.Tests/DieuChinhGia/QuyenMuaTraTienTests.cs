using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.DieuChinhGia;

public sealed class QuyenMuaTraTienTests
{
    private static readonly SuKienQuyen Hcm1607 = new(
        "HCM", new DateOnly(2026, 7, 16), 0.4m, 1m, 4, 1, 10m);

    private static readonly SuKienQuyen Ssi1708 = new("SSI", new DateOnly(2026, 8, 17), 1.0m, 1.2m);

    [Fact]
    public void GiaThamChieu_Hcm1607_2324()
    {
        var bo = new BoDieuChinhGiaTheoQuyen(NguonSuKienQuyenDanhSach.Rong);
        var gia = bo.TinhGiaThamChieu(26.95m, 0.4m, 1m, 0.25m, 10m);
        Assert.Equal(23.24m, Math.Round(gia, 2));
    }

    [Fact]
    public void GiaThamChieu_KhongQuyenMua_TrungCongThuc005()
    {
        var bo = new BoDieuChinhGiaTheoQuyen(NguonSuKienQuyenDanhSach.Rong);
        var moi = bo.TinhGiaThamChieu(24.5m, 1.0m, 1.2m, 0, 0);
        var cu = bo.TinhGiaThamChieu(24.5m, 1.0m, 1.2m);
        Assert.Equal(cu, moi);
        Assert.Equal(19.5833m, Math.Round(cu, 4));
    }

    [Fact]
    public void GiaThamChieu_KhongDungXapXiThuongMienPhi()
    {
        var bo = new BoDieuChinhGiaTheoQuyen(NguonSuKienQuyenDanhSach.Rong);
        var dung = bo.TinhGiaThamChieu(26.95m, 0.4m, 1m, 0.25m, 10m);
        var sai = (26.95m - 0.4m) / 1.25m;
        Assert.True(dung - sai >= 1.5m);
        Assert.Equal(21.24m, Math.Round(sai, 2));
    }

    [Fact]
    public void GetChangePercent_HcmQuaQuyenMua_KhoangCong9()
    {
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 7, 15), 26.8m, 27.1m, 26.75m, 26.95m, 2_654_300),
            new(new DateOnly(2026, 7, 17), 25.0m, 26.3m, 25.0m, 25.4m, 8_596_000)
        };
        var stock = new Stock("HCM", "HCM", "Chứng khoán", bars);
        var may = new SignalAnalyzer(new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([Hcm1607])));
        var pct = may.GetChangePercent(stock, 1);
        Assert.InRange(pct, 7.5m, 10.5m);
        Assert.NotInRange(pct, -7m, -4m);
        Assert.NotInRange(pct, 18m, 21m);

        var cham = may.LayLichSuChamDiem(stock);
        Assert.Equal(25.4m, cham[^1].Close);
        Assert.Equal(8_596_000, cham[^1].Volume);
    }

    [Fact]
    public void GetChangePercent_Ssi_HoiQuy005()
    {
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 8, 14), 24.4m, 24.6m, 24.3m, 24.5m, 1_000_000),
            new(new DateOnly(2026, 8, 17), 19.6m, 20.0m, 19.5m, 19.8m, 1_200_000)
        };
        var stock = new Stock("SSI", "SSI", "Chứng khoán", bars);
        var may = new SignalAnalyzer(new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([Ssi1708])));
        Assert.InRange(may.GetChangePercent(stock, 1), 0m, 2m);
    }
}

using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.DieuChinhGia;

public sealed class DieuChinhGiaTheoQuyenTests
{
    private static readonly SuKienQuyen Ssi1708 = new("SSI", new DateOnly(2026, 8, 17), 1.0m, 1.2m);

    private static SignalAnalyzer TaoMay(params SuKienQuyen[] suKien) =>
        new(new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach(suKien)));

    private static Stock TaoSsiHaiPhien()
    {
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 8, 14), 24.4m, 24.6m, 24.3m, 24.5m, 1_000_000),
            new(new DateOnly(2026, 8, 17), 19.6m, 20.0m, 19.5m, 19.8m, 1_200_000)
        };
        return new Stock("SSI", "SSI", "Chứng khoán", bars);
    }

    [Fact]
    public void GiaThamChieu_Ssi_Khoang1958()
    {
        var bo = new BoDieuChinhGiaTheoQuyen(NguonSuKienQuyenDanhSach.Rong);
        var giaThamChieu = bo.TinhGiaThamChieu(24.5m, 1.0m, 1.2m);
        Assert.Equal(19.5833m, Math.Round(giaThamChieu, 4));
    }

    [Fact]
    public void GetChangePercent_SsiQuaQuyen_KhongConAm19()
    {
        var may = TaoMay(Ssi1708);
        var pct = may.GetChangePercent(TaoSsiHaiPhien(), 1);
        Assert.InRange(pct, 0m, 2m);
        Assert.NotInRange(pct, -20m, -15m);
    }

    [Fact]
    public void GetChangePercent_KhongSuKien_TrungTho()
    {
        var may = new SignalAnalyzer();
        var stock = TaoSsiHaiPhien();
        var tho = may.GetChangePercent(stock.History, 1);
        var quaStock = may.GetChangePercent(stock, 1);
        Assert.Equal(tho, quaStock);
        Assert.InRange(tho, -20m, -18m);
    }
}

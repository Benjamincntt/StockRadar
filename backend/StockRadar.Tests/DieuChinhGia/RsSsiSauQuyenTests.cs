using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.DieuChinhGia;

public sealed class RsSsiSauQuyenTests
{
    [Fact]
    public void Rs5Phien_SsiKetThuc21Thang8_KhongConAm176ChiViGapQuyen()
    {
        var suKien = new SuKienQuyen("SSI", new DateOnly(2026, 8, 17), 1.0m, 1.2m);
        var may = new SignalAnalyzer(new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([suKien])));
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 8, 14), 24.5m, 24.6m, 24.3m, 24.5m, 1_000_000),
            new(new DateOnly(2026, 8, 17), 19.6m, 20.0m, 19.5m, 19.8m, 1_200_000),
            new(new DateOnly(2026, 8, 18), 19.9m, 20.1m, 19.7m, 20.0m, 900_000),
            new(new DateOnly(2026, 8, 19), 20.0m, 20.2m, 19.8m, 20.1m, 800_000),
            new(new DateOnly(2026, 8, 20), 20.1m, 20.3m, 19.9m, 20.2m, 850_000),
            new(new DateOnly(2026, 8, 21), 20.2m, 20.6m, 20.0m, 20.5m, 950_000)
        };
        var stock = new Stock("SSI", "SSI", "Chứng khoán", bars);

        var rsTho = Math.Round(may.GetChangePercent(stock.History, 5) - 0m, 2);
        var rsDieuChinh = may.GetRelativeStrength(stock, 0m, 5);

        Assert.InRange(rsTho, -20m, -14m);
        Assert.True(rsDieuChinh > -5m, $"RS điều chỉnh {rsDieuChinh} không còn ≈ −17.6");
        Assert.NotEqual(rsTho, rsDieuChinh);
    }
}

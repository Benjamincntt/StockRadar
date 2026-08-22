using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.DieuChinhGia;

public sealed class SuKienThuHaiTests
{
    [Fact]
    public void TachHaiMot_GapThoKhacLoiSuatDieuChinh()
    {
        var suKien = new SuKienQuyen("ABC", new DateOnly(2026, 3, 10), 0m, 2m);
        var may = new SignalAnalyzer(new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([suKien])));
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 3, 9), 100m, 101m, 99m, 100m, 500_000),
            new(new DateOnly(2026, 3, 10), 49.5m, 51m, 49m, 50m, 800_000)
        };
        var stock = new Stock("ABC", "ABC", "Khác", bars);

        var tho = may.GetChangePercent(stock.History, 1);
        var dieuChinh = may.GetChangePercent(stock, 1);

        Assert.InRange(tho, -51m, -49m);
        Assert.InRange(dieuChinh, -2m, 2m);
        Assert.NotEqual(tho, dieuChinh);
    }
}

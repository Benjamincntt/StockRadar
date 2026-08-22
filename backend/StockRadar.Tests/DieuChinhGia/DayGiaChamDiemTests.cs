using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.DieuChinhGia;

public sealed class DayGiaChamDiemTests
{
    [Fact]
    public void NenCuoiVaVolume_KhongDoi()
    {
        var suKien = new SuKienQuyen("SSI", new DateOnly(2026, 8, 17), 1.0m, 1.2m);
        var may = new SignalAnalyzer(new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([suKien])));
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 8, 14), 24.4m, 25.0m, 24.2m, 24.5m, 1_000_000),
            new(new DateOnly(2026, 8, 17), 19.6m, 20.0m, 19.5m, 19.8m, 1_200_000)
        };
        var stock = new Stock("SSI", "SSI", "Chứng khoán", bars);
        var cham = may.LayLichSuChamDiem(stock);

        Assert.Equal(bars[^1].Open, cham[^1].Open);
        Assert.Equal(bars[^1].High, cham[^1].High);
        Assert.Equal(bars[^1].Low, cham[^1].Low);
        Assert.Equal(bars[^1].Close, cham[^1].Close);
        Assert.Equal(bars[0].Volume, cham[0].Volume);
        Assert.Equal(bars[1].Volume, cham[1].Volume);
    }

    [Fact]
    public void HaiPhiaGdkhq_KhongDungCloseThoCungThang()
    {
        var suKien = new SuKienQuyen("SSI", new DateOnly(2026, 8, 17), 1.0m, 1.2m);
        var bo = new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([suKien]));
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 8, 14), 24.4m, 25.0m, 24.2m, 24.5m, 1_000_000),
            new(new DateOnly(2026, 8, 17), 19.6m, 20.0m, 19.5m, 19.8m, 1_200_000)
        };

        var cham = bo.TaoDayGiaDieuChinh("SSI", bars);
        var pctTho = (bars[^1].Close - bars[0].Close) / bars[0].Close * 100m;
        var pctCham = (cham[^1].Close - cham[0].Close) / cham[0].Close * 100m;

        Assert.InRange(pctTho, -20m, -18m);
        Assert.InRange(pctCham, 0m, 2m);
        Assert.True(cham[0].High < 21m, "Đỉnh hộp trước quyền phải về thang sau quyền");
    }
}

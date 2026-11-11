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
        var bo = new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([suKien]));
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 3, 9), 100m, 101m, 99m, 100m, 500_000),
            new(new DateOnly(2026, 3, 10), 49.5m, 51m, 49m, 50m, 800_000)
        };

        var cham = bo.TaoDayGiaDieuChinh("ABC", bars);
        var tho = PhanTramDoiGia.Tinh(bars, 1);
        var dieuChinh = PhanTramDoiGia.Tinh(cham, 1);

        Assert.InRange(tho, -51m, -49m);
        Assert.InRange(dieuChinh, -2m, 2m);
        Assert.NotEqual(tho, dieuChinh);
    }
}

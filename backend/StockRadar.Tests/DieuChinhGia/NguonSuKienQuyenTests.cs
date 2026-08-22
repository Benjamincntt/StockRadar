using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Tests.DieuChinhGia;

public sealed class NguonSuKienQuyenTests
{
    [Fact]
    public void ThieuNgay_KhongNap()
    {
        var json = """{ "suKien": [ { "ma": "SSI", "tienMat": 1.0, "heSoPhaLoang": 1.2 } ] }""";
        var nguon = new FileNguonSuKienQuyen(json, null);
        Assert.Empty(nguon.LayTheoMa("SSI"));
    }

    [Fact]
    public void HeSoPhaLoangKhongDuong_KhongNap()
    {
        var json = """{ "suKien": [ { "ma": "SSI", "ngayKhongHuongQuyen": "2026-08-17", "tienMat": 1.0, "heSoPhaLoang": 0 } ] }""";
        var nguon = new FileNguonSuKienQuyen(json, null);
        Assert.Empty(nguon.LayTheoMa("SSI"));
    }

    [Fact]
    public void TienMat1000_KhongApHeSoAm()
    {
        var suKien = new SuKienQuyen("SSI", new DateOnly(2026, 8, 17), 1000m, 1.2m);
        var bo = new BoDieuChinhGiaTheoQuyen(new NguonSuKienQuyenDanhSach([suKien]));
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2026, 8, 14), 24.5m, 24.6m, 24.3m, 24.5m, 1_000_000),
            new(new DateOnly(2026, 8, 17), 19.8m, 20.0m, 19.5m, 19.8m, 1_200_000)
        };

        var cham = bo.TaoDayGiaDieuChinh("SSI", bars);
        Assert.Equal(24.5m, cham[0].Close);
        Assert.Equal(19.8m, cham[1].Close);
    }

    [Fact]
    public void JsonHong_NguonRong()
    {
        var nguon = new FileNguonSuKienQuyen("{ khong-phai-json", null);
        Assert.Empty(nguon.LayTheoMa("SSI"));
    }

    [Fact]
    public void Them_GhiNhoVaThayCungNgay()
    {
        var nguon = new FileNguonSuKienQuyen("""{ "suKien": [] }""", null);
        nguon.Them(new SuKienQuyen("HCM", new DateOnly(2026, 8, 20), 0.5m, 1m));
        nguon.Them(new SuKienQuyen("HCM", new DateOnly(2026, 8, 20), 1m, 1.1m));
        var ds = nguon.LayTheoMa("HCM");
        Assert.Single(ds);
        Assert.Equal(1m, ds[0].TienMat);
        Assert.Equal(1.1m, ds[0].HeSoPhaLoang);
    }
}

using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Services;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.DieuChinhGia;

public sealed class DichVuSuKienQuyenTests
{
    [Fact]
    public void Them_TienMat1000_BiTuChoi()
    {
        var dv = new DichVuSuKienQuyen(NguonSuKienQuyenDanhSach.Rong);
        var ex = Assert.Throws<AppException>(() =>
            dv.Them("SSI", new ThemSuKienQuyenRequest(new DateOnly(2026, 8, 17), 1000m, 1.2m)));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Them_HopLe_LayLaiTheoMa()
    {
        var nguon = new NguonSuKienQuyenDanhSach([]);
        var dv = new DichVuSuKienQuyen(nguon);
        dv.Them("hcm", new ThemSuKienQuyenRequest(new DateOnly(2026, 8, 20), 0.5m, 1m));
        var ds = dv.LayTheoMa("HCM");
        Assert.Single(ds);
        Assert.Equal("HCM", ds[0].Symbol);
        Assert.Equal(0.5m, ds[0].Cash);
    }
}

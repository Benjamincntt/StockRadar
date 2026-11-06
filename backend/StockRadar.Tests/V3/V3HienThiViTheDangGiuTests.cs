using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// Spec 016 D9 — giá chốt lời và số phiên còn lại hiển thị cho vị thế V3 đang giữ (Home).
/// Số liệu là dữ liệu BỊA để kiểm hàm; hàm phải khớp ngưỡng mà luật bán realtime dùng.
/// </summary>
public sealed class V3HienThiViTheDangGiuTests
{
    private static readonly V3CauHinh Cfg = new();

    [Fact]
    public void GiaChotLoi_LenhThuong_CoNguongDaLuu_DungNguongDaLuu()
    {
        var gia = V3LuatBanThucTe.GiaChotLoiHienThi(LoaiKichBanV3.KB1, 100m, 108m, Cfg);

        Assert.Equal(108m, gia);
    }

    [Fact]
    public void GiaChotLoi_LenhThuong_ChuaCoNguong_LaGiaVaoCongPhi()
    {
        var gia = V3LuatBanThucTe.GiaChotLoiHienThi(LoaiKichBanV3.KB2, 100m, null, Cfg);

        Assert.Equal(100.4m, gia);
    }

    [Fact]
    public void GiaChotLoi_NguongBangKhong_CoiLaChuaCo()
    {
        var gia = V3LuatBanThucTe.GiaChotLoiHienThi(LoaiKichBanV3.KB4, 50m, 0m, Cfg);

        Assert.Equal(50.2m, gia);
    }

    [Fact]
    public void GiaChotLoi_HoangLoanSau_DungNguongDaLuuLaGiaVaoNhan1_15()
    {
        var gia = V3LuatBanThucTe.GiaChotLoiHienThi(LoaiKichBanV3.KB1, 100m, 115m, Cfg);

        Assert.Equal(115m, gia);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(120.0)]
    public void GiaChotLoi_N3_KhongCoGia_BanAtoTheoLich(double? nguong)
    {
        var gia = V3LuatBanThucTe.GiaChotLoiHienThi(LoaiKichBanV3.N3, 100m, (decimal?)nguong, Cfg);

        Assert.Null(gia);
    }

    [Fact]
    public void GiaChotLoi_ChuaMua_KhongCoGiaVaoVaNguong_LaNull()
    {
        var gia = V3LuatBanThucTe.GiaChotLoiHienThi(LoaiKichBanV3.KB1, null, null, Cfg);

        Assert.Null(gia);
    }

    [Fact]
    public void GiaChotLoi_TrungVoiNguongGiaCuaLuatBanRealtime()
    {
        // Ngưỡng đã lưu = max(chạm MA10, giá vào × 1,004) — đúng thứ Xet dùng; hiển thị phải ra cùng số.
        IReadOnlyList<decimal> closes = Enumerable.Repeat(104m, 11).ToArray();
        var nguongMa = V3LuatBanThucTe.NguongChamMa(LoaiKichBanV3.KB1, closes);
        var nguongDaLuu = Math.Max(nguongMa, 100m * Cfg.BanPhanTramGiaVao);

        var ketQua = V3LuatBanThucTe.Xet(
            LoaiKichBanV3.KB1, 100m, closes, giaKhop: 101m, soPhienDaGi: 3, giuToiDa: 15,
            denLichAtoN3: false, daQuaTplus25: true, sanTrangHomNay: false, Cfg);
        var hienThi = V3LuatBanThucTe.GiaChotLoiHienThi(LoaiKichBanV3.KB1, 100m, nguongDaLuu, Cfg);

        Assert.False(ketQua.Thoat);
        Assert.Equal(ketQua.NguongGia, hienThi);
    }

    [Theory]
    [InlineData(LoaiKichBanV3.KB1, false, 5, 10)]
    [InlineData(LoaiKichBanV3.KB2, false, 0, 15)]
    [InlineData(LoaiKichBanV3.KB4, false, 4, 6)]
    [InlineData(LoaiKichBanV3.N1, false, 15, 0)]
    [InlineData(LoaiKichBanV3.N3, false, 2, 10)]
    [InlineData(LoaiKichBanV3.KB1, true, 12, 28)]
    public void SoPhienConLai_TheoGiuToiDaCuaViThe(LoaiKichBanV3 loai, bool hoangLoan, int daGiu, int mongDoi)
    {
        var con = V3LuatBanThucTe.SoPhienConLai(Cfg, loai, hoangLoan, daGiu);

        Assert.Equal(mongDoi, con);
    }

    [Fact]
    public void SoPhienConLai_QuaHan_KhongAm()
    {
        var con = V3LuatBanThucTe.SoPhienConLai(Cfg, LoaiKichBanV3.KB4, false, 14);

        Assert.Equal(0, con);
    }
}

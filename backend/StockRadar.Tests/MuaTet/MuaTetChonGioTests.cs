using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.MuaTet;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19, sổ V4): lọc vũ trụ theo thanh khoản, chọn 10 mã gần đỉnh 1 năm
/// với tối đa 2 mã/ngành, chặn mã lẫn đơn vị giá, không dùng nến của phiên đang chạy.
/// Dữ liệu bịa có kiểm soát; giá nghìn đồng, TV20 tỷ đồng, vốn đồng.
/// </summary>
public sealed class MuaTetChonGioTests
{
    private static readonly DateOnly NgayCuoi = new(2026, 11, 13); // thứ Sáu
    private const decimal Von = 40_000_000m;

    private static MuaTetChonGio.MaDauVao Ma(
        string symbol, string sector, decimal close,
        long volume = 5_000_000, decimal? dinh = null, DateOnly? ngayCuoi = null, int soPhien = 250)
        => new(symbol, sector, NenDeu(close, volume, dinh, ngayCuoi ?? NgayCuoi, soPhien));

    private static List<OhlcvBar> NenDeu(
        decimal close, long volume, decimal? dinh, DateOnly ngayCuoi, int soPhien)
    {
        var ds = new List<OhlcvBar>(soPhien);
        for (var i = 0; i < soPhien; i++)
        {
            var ngay = ngayCuoi.AddDays(i - (soPhien - 1));
            var high = i == 0 ? Math.Max(close, dinh ?? close) : close;
            ds.Add(new OhlcvBar(ngay, close, high, close, close, volume));
        }

        return ds;
    }

    private static MuaTetChonGio.KetQuaChon Chon(IReadOnlyList<MuaTetChonGio.MaDauVao> cacMa, decimal von = Von)
        => MuaTetChonGio.Chon(cacMa, NgayCuoi, von);

    // ===== vũ trụ: top 100 thanh khoản =====

    [Fact]
    public void Chi_lay_100_ma_thanh_khoan_cao_nhat()
    {
        // 100 mã TV20 50 tỷ (được xét) + 20 mã TV20 1 tỷ nhưng gần đỉnh tuyệt đối (bị loại khỏi vũ trụ).
        var cacMa = new List<MuaTetChonGio.MaDauVao>();
        for (var i = 0; i < 100; i++)
            cacMa.Add(Ma($"T{i:000}", $"N{i % 40}", 10m, volume: 5_000_000, dinh: 12m));
        for (var i = 0; i < 20; i++)
            cacMa.Add(Ma($"X{i:00}", $"X{i % 20}", 10m, volume: 100_000));

        var kq = Chon(cacMa);

        Assert.Equal(10, kq.Gio.Count);
        Assert.DoesNotContain(kq.Gio, m => m.Symbol.StartsWith('X'));
        Assert.All(kq.Gio, m => Assert.Equal(50m, m.GiaTriGiaoDichTb20Ty));
    }

    // ===== tối đa 2 mã/ngành =====

    [Fact]
    public void Toi_da_2_ma_cung_nganh()
    {
        // 4 mã ngành "Bank" có gan_dinh cao nhất + 8 mã ngành riêng → chỉ 2 Bank được lấy.
        var cacMa = new List<MuaTetChonGio.MaDauVao>
        {
            Ma("B1", "Bank", 10m, dinh: 10m),
            Ma("B2", "Bank", 10m, dinh: 10.05m),
            Ma("B3", "Bank", 10m, dinh: 10.2m),
            Ma("B4", "Bank", 10m, dinh: 10.5m),
        };
        for (var i = 0; i < 8; i++)
            cacMa.Add(Ma($"O{i}", $"Nganh{i}", 10m, dinh: 11m + i * 0.1m));

        var kq = Chon(cacMa);

        Assert.Equal(10, kq.Gio.Count);
        Assert.Equal(new[] { "B1", "B2" }, kq.Gio.Where(m => m.Sector == "Bank").Select(m => m.Symbol));
        Assert.DoesNotContain(kq.Gio, m => m.Symbol is "B3" or "B4");
    }

    // ===== đủ 10 mã, xếp theo gan_dinh =====

    [Fact]
    public void Du_10_ma_va_xep_gan_dinh_giam_dan()
    {
        var cacMa = new List<MuaTetChonGio.MaDauVao>();
        for (var i = 0; i < 12; i++)
            cacMa.Add(Ma($"M{i:00}", $"Nganh{i}", 10m, dinh: 10m + i * 0.1m));

        var kq = Chon(cacMa);

        Assert.Equal(10, kq.Gio.Count);
        Assert.Equal("M00", kq.Gio[0].Symbol);
        Assert.Equal("M09", kq.Gio[^1].Symbol);
        Assert.True(kq.Gio[0].GanDinh > kq.Gio[1].GanDinh);
    }

    // ===== bẫy lẫn đơn vị giá =====

    [Fact]
    public void Loai_ma_lan_don_vi_gia()
    {
        // 249 nến đóng cửa 0,98 (nghìn đồng) + nến cuối lưu 970 (đồng) → trung vị 0,98 < 100, có giá ≥ 500.
        var lan = NenDeu(0.98m, 5_000_000, null, NgayCuoi, 250);
        lan[^1] = new OhlcvBar(NgayCuoi, 970m, 970m, 970m, 970m, 5_000_000);

        var cacMa = new List<MuaTetChonGio.MaDauVao> { new("LAN", "NganhLan", lan) };
        for (var i = 0; i < 10; i++)
            cacMa.Add(Ma($"S{i}", $"Nganh{i}", 10m, dinh: 11m + i * 0.1m));

        var kq = Chon(cacMa);

        Assert.Contains("LAN", kq.BiLoaiLanDonVi);
        Assert.DoesNotContain(kq.Gio, m => m.Symbol == "LAN");
        Assert.Equal(10, kq.Gio.Count);
    }

    // ===== không dùng nến của phiên đang chạy =====

    [Fact]
    public void Khong_dung_nen_hom_nay_khi_phien_chua_xong()
    {
        var thuSau = new DateOnly(2026, 11, 13);

        Assert.Equal(new DateOnly(2026, 11, 12), MuaTetChonGio.NgayCatNen(thuSau, new TimeOnly(9, 30)));
        Assert.Equal(thuSau, MuaTetChonGio.NgayCatNen(thuSau, new TimeOnly(15, 0)));
        // Thứ Bảy → lùi về thứ Sáu.
        Assert.Equal(thuSau, MuaTetChonGio.NgayCatNen(new DateOnly(2026, 11, 14), new TimeOnly(10, 0)));
    }

    [Fact]
    public void Nen_dang_chay_khong_lam_hong_gan_dinh()
    {
        // 250 nến tới 12/11: đóng cửa 10, đỉnh 20 → gan_dinh 0,5. Nến 13/11 đang chạy có đỉnh 100.
        var bars = NenDeu(10m, 5_000_000, 20m, new DateOnly(2026, 11, 12), 250);
        bars.Add(new OhlcvBar(new DateOnly(2026, 11, 13), 10m, 100m, 10m, 10m, 5_000_000));

        var ngayCat = MuaTetChonGio.NgayCatNen(new DateOnly(2026, 11, 13), new TimeOnly(10, 0));
        var kq = MuaTetChonGio.Chon([new MuaTetChonGio.MaDauVao("AAA", "Nganh", bars)], ngayCat, Von);

        Assert.Equal(0.5m, kq.Gio[0].GanDinh);
        Assert.Equal(new DateOnly(2026, 11, 12), kq.NgayCat);
    }

    // ===== bỏ mã vượt chỗ + số lô gợi ý =====

    [Fact]
    public void Bo_ma_vuot_cho_va_so_lo_goi_y()
    {
        // Vốn 40 triệu → 1 chỗ = 4 triệu. Lô 100 cổ, giá nghìn đồng.
        var cacMa = new[]
        {
            Ma("GIA_RE", "N1", 3.5m, dinh: 3.5m),
            Ma("GIA_VUA", "N2", 10m, dinh: 10m),
            Ma("GIA_CAO", "N3", 45m, dinh: 45m),
            Ma("GIA_RAT_CAO", "N4", 70m, dinh: 70m),
        };

        var kq = Chon(cacMa);

        Assert.Equal(4_000_000m, kq.GiaTriMotChoDong);
        var re = kq.Gio.Single(m => m.Symbol == "GIA_RE");
        var vua = kq.Gio.Single(m => m.Symbol == "GIA_VUA");
        var cao = kq.Gio.Single(m => m.Symbol == "GIA_CAO");

        Assert.Equal(11, re.SoLoGoiY);            // 4.000.000 / 350.000 = 11,4 → 11 lô
        Assert.Equal(4, vua.SoLoGoiY);           // 4.000.000 / 1.000.000 = 4 lô
        Assert.Equal(1, cao.SoLoGoiY);           // 4.000.000 / 4.500.000 = 0,89 → 1 lô, chưa vượt 1,5 lần chỗ
        // 1 lô 7.000.000 > 6.000.000 → bỏ khỏi giỏ, ghi vào danh sách bỏ vượt chỗ
        Assert.DoesNotContain(kq.Gio, m => m.Symbol == "GIA_RAT_CAO");
        Assert.Equal(new[] { "GIA_RAT_CAO" }, kq.BiBoVuotCho);
    }

    [Fact]
    public void Ma_vuot_cho_khong_chiem_suat_nganh_va_ma_ke_tiep_vao_thay()
    {
        // Ngành N1 có 3 mã; mã điểm cao nhất vượt chỗ → bị bỏ, 2 mã còn lại của N1 vẫn vào giỏ.
        var cacMa = new[]
        {
            Ma("DAT", "N1", 70m, dinh: 70m),
            Ma("RE1", "N1", 10m, dinh: 10.2m),
            Ma("RE2", "N1", 10m, dinh: 10.4m),
        };

        var kq = Chon(cacMa);

        Assert.Equal(new[] { "RE1", "RE2" }, kq.Gio.Select(m => m.Symbol).ToArray());
        Assert.Equal(new[] { "DAT" }, kq.BiBoVuotCho);
    }
}

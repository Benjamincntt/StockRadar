using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// SC-002 — luật BÁN realtime V3 (Phụ lục A + FR-015..FR-018): đánh giá trên giá khớp
/// trong phiên, ngưỡng max(chạm MAn, giá vào × 1,004), T+2.5, sàn trắng cấm bán,
/// hết "giữ tối đa" bán ATC, N3 bán ATO theo lịch.
/// </summary>
public sealed class V3LuatBanThucTeTests
{
    private static readonly V3CauHinh Cfg = new();

    /// <summary>n−1 giá đóng cửa trước hiện tại — mặc định 11 phiên giá 120 (đủ cho MA10 = 9 phiên gần nhất).</summary>
    private static IReadOnlyList<decimal> Closes(params decimal[] closes) => closes;

    private static V3LuatBanThucTe.KetQuaThoat Xet(
        LoaiKichBanV3 loai,
        decimal giaKhop,
        int soPhienDaGiu = 3,
        IReadOnlyList<decimal>? closes = null,
        decimal giaVao = 100m,
        bool denLichAtoN3 = false,
        bool daQuaTplus25 = true,
        bool sanTrangHomNay = false)
        => V3LuatBanThucTe.Xet(
            loai,
            giaVao,
            closes ?? Closes(Enumerable.Repeat(120m, 11).ToArray()),
            giaKhop,
            soPhienDaGiu,
            Cfg.GiuToiDaCua(loai),
            denLichAtoN3,
            daQuaTplus25,
            sanTrangHomNay,
            Cfg);

    [Theory]
    [InlineData("KB1", 10)]
    [InlineData("KB2", 10)]
    [InlineData("KB4", 10)]
    [InlineData("N1", 20)]
    [InlineData("N3", 0)]
    public void So_phien_ma_dung_tung_kich_ban(string loai, int expected)
    {
        Assert.Equal(expected, V3LuatBanThucTe.MaCuaKichBan(Enum.Parse<LoaiKichBanV3>(loai)));
    }

    [Fact]
    public void Nguong_cham_ma10_dung_trung_binh_9_phien_gan_nhat()
    {
        // 11 phiên: 2 phiên đầu khác biệt phải bị bỏ — chỉ 9 phiên cuối (giá 100) tính vào MA10.
        var closes = Closes(500m, 500m, 100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m);

        Assert.Equal(100m, V3LuatBanThucTe.NguongChamMa(LoaiKichBanV3.KB1, closes));
    }

    [Fact]
    public void Thieu_du_lieu_thi_nguong_ma_bang_0_khong_chan_theo_ma()
    {
        var closes = Closes(100m, 100m);

        Assert.Equal(0m, V3LuatBanThucTe.NguongChamMa(LoaiKichBanV3.KB1, closes));
    }

    [Fact]
    public void Ban_khi_gia_khop_dat_max_cham_ma_ngoai_gia_vao_ph()
    {
        // Ngưỡng = max(MA10=120, 100×1,004=100,4) = 120 → giá 101 tuy vượt phí nhưng dưới MA → chưa bán.
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.Khong, Xet(LoaiKichBanV3.KB1, 101m).LyDo);
        // 119,9 < 120 → giữ tiếp.
        Assert.False(Xet(LoaiKichBanV3.KB1, 119.9m).Thoat);
        // 120 đạt ngưỡng MA → thoát ChamMA với đúng ngưỡng ghi nhận.
        var quyet = Xet(LoaiKichBanV3.KB1, 120m);
        Assert.True(quyet.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.ChamMA, quyet.LyDo);
        Assert.Equal(120m, quyet.NguongGia);
    }

    [Fact]
    public void Khi_thieu_ma_thi_nguong_chi_con_gia_vao_nhan_ph()
    {
        // closes không đủ 9 phiên → nguongMa=0 → ngưỡng = 100×1,004 = 100,4.
        var closes = Closes(100m, 100m);

        Assert.False(Xet(LoaiKichBanV3.KB1, 100.3m, closes: closes).Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.ChamMA, Xet(LoaiKichBanV3.KB1, 100.4m, closes: closes).LyDo);
    }

    [Fact]
    public void Chua_qua_T25_thi_khong_duoc_ban_dau_gia()
    {
        // FR-017: chưa qua T+2.5 thì dù giá vượt ngưỡng cũng chưa thoát.
        var quyet = Xet(LoaiKichBanV3.KB1, 150m, daQuaTplus25: false);

        Assert.False(quyet.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.Khong, quyet.LyDo);
    }

    [Fact]
    public void Het_giu_toi_da_ban_atc_phien_nay()
    {
        // KB4 giữ tối đa 10 phiên — tới phiên 10 + qua T+2.5 → thoát HetHanATC (FR-016).
        var quyet = Xet(LoaiKichBanV3.KB4, giaKhop: 50m, soPhienDaGiu: 10);

        Assert.True(quyet.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.HetHanATC, quyet.LyDo);
        Assert.Equal(0m, quyet.NguongGia);
    }

    [Fact]
    public void San_trang_cam_ban_ke_ca_khi_het_han_gi()
    {
        // FR-018: phiên sàn trắng bên mua không được bán; đồng hồ giữ vẫn chạy (phiên sau tính tiếp).
        Assert.False(Xet(LoaiKichBanV3.KB1, 150m, sanTrangHomNay: true).Thoat);
        Assert.False(Xet(LoaiKichBanV3.KB4, 50m, soPhienDaGiu: 10, sanTrangHomNay: true).Thoat);
    }

    [Fact]
    public void N3_thoat_theo_lich_ato_khong_can_gia()
    {
        // FR: N3 bán lệnh ATO phiên TDM=2 tháng sau — chốt lịch, giá khớp irrelevant.
        var quyet = Xet(LoaiKichBanV3.N3, giaKhop: 1m, denLichAtoN3: true);

        Assert.True(quyet.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.AtoTheoLich, quyet.LyDo);
    }

    [Fact]
    public void N3_het_han_12_phien_thi_ban_atc()
    {
        Assert.True(Xet(LoaiKichBanV3.N3, 1m, soPhienDaGiu: 12).Thoat);
        Assert.False(Xet(LoaiKichBanV3.N3, 1m, soPhienDaGiu: 11).Thoat);
        // Chưa qua T+2.5 thì hết hạn cũng chưa bán.
        Assert.False(Xet(LoaiKichBanV3.N3, 1m, soPhienDaGiu: 12, daQuaTplus25: false).Thoat);
    }

    // ── Ngưỡng ĐÃ LƯU: nhịp mỗi phút không nạp lịch sử (16:00 tính sẵn và ghi vào vị thế) ────

    private static V3LuatBanThucTe.KetQuaThoat XetTheoNguong(
        LoaiKichBanV3 loai,
        decimal nguongDaLuu,
        decimal giaKhop,
        int soPhienDaGiu = 3,
        bool denLichAtoN3 = false,
        bool daQuaTplus25 = true,
        bool sanTrangHomNay = false)
        => V3LuatBanThucTe.XetTheoNguongDaLuu(
            loai,
            nguongDaLuu,
            giaKhop,
            soPhienDaGiu,
            Cfg.GiuToiDaCua(loai),
            denLichAtoN3,
            daQuaTplus25,
            sanTrangHomNay);

    [Fact]
    public void Nhip_moi_phut_chi_can_nguong_da_luu_khong_can_lich_su()
    {
        // Nhịp 16:00 lưu nguong = max(TB 9 đóng cửa trước = 120, 100×1,004) = 120 → chỉ so giá khớp.
        Assert.False(XetTheoNguong(LoaiKichBanV3.KB1, 120m, 119.9m).Thoat);

        var quyet = XetTheoNguong(LoaiKichBanV3.KB1, 120m, 120m);
        Assert.True(quyet.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.ChamMA, quyet.LyDo);
        Assert.Equal(120m, quyet.NguongGia);
    }

    [Fact]
    public void Nguong_da_luu_bang_0_thi_khong_thoat_theo_gia()
    {
        // 16:00 chưa tính được "chạm MAn" (mới có lệnh, thiếu lịch sử) và chưa nhân phí → nguong = 0:
        // thà GIỮ chờ nhịp sau còn hơn bán oan một mã chưa có ngưỡng.
        var quyet = XetTheoNguong(LoaiKichBanV3.KB1, 0m, giaKhop: 1_000m);

        Assert.False(quyet.Thoat);
        Assert.Equal(0m, quyet.NguongGia);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(119)]
    [InlineData(120)]
    [InlineData(150)]
    public void Nguong_da_luu_cho_ket_qua_giong_xet_theo_lich_su(int giaKhopNguyen)
    {
        // Attribute không nhận literal decimal → truyền int, riêng 119 là để thử mức 119,9 (dưới ngưỡng).
        decimal giaKhop = giaKhopNguyen switch
        {
            119 => 119.9m,
            _ => (decimal)giaKhopNguyen,
        };
        // Một luật — hai đường nhập: 11 phiên giá 120, giá vào 100 → nguong = max(120; 100,4) = 120.
        var closes = Closes(Enumerable.Repeat(120m, 11).ToArray());
        var theoLichSu = Xet(LoaiKichBanV3.KB1, giaKhop, closes: closes);
        var theoNguong = XetTheoNguong(LoaiKichBanV3.KB1, 120m, giaKhop);

        Assert.Equal(theoLichSu.Thoat, theoNguong.Thoat);
        Assert.Equal(theoLichSu.LyDo, theoNguong.LyDo);
    }

    [Fact]
    public void Nguong_da_luu_van_giu_cac_chan_gate_T25_va_san_trang()
    {
        Assert.False(XetTheoNguong(LoaiKichBanV3.KB1, 120m, 150m, daQuaTplus25: false).Thoat);
        Assert.False(XetTheoNguong(LoaiKichBanV3.KB1, 120m, 150m, sanTrangHomNay: true).Thoat);
        // Hết "giữ tối đa" vẫn bán ATC theo thời điểm, không cần ngưỡng giá.
        var hetHan = XetTheoNguong(LoaiKichBanV3.KB4, 120m, 50m, soPhienDaGiu: 10);
        Assert.True(hetHan.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.HetHanATC, hetHan.LyDo);
        // N3 không có ngưỡng giá: chỉ theo lịch ATO.
        Assert.Equal(
            V3LuatBanThucTe.LyDoThoat.AtoTheoLich,
            XetTheoNguong(LoaiKichBanV3.N3, 0m, 1m, denLichAtoN3: true).LyDo);
    }

    // ── SC-008: ngoại lệ HOẢNG LOẠN SÂU (FR-015a/FR-016a, research PH-21) — dữ liệu bịa có kiểm soát ────

    // Giá vào bịa 100 → ngưỡng hoảng loạn sâu = 100 × 1,15 = 115.
    private const decimal GiaVaoHL = 100m;
    private static decimal NguongHL => GiaVaoHL * Cfg.HoangLoanSauChotLoi; // 115

    private static V3LuatBanThucTe.KetQuaThoat XetHL(
        LoaiKichBanV3 loai,
        decimal giaKhop,
        int soPhienDaGiu = 3,
        IReadOnlyList<decimal>? closes = null,
        decimal giaVao = GiaVaoHL,
        bool daQuaTplus25 = true,
        bool sanTrangHomNay = false)
        => V3LuatBanThucTe.Xet(
            loai, giaVao,
            closes ?? Closes(Enumerable.Repeat(120m, 11).ToArray()),
            giaKhop, soPhienDaGiu,
            V3LuatBanThucTe.GiuToiDaTheoViThe(Cfg, loai, laHoangLoanSau: true),
            denLichAtoN3: false, daQuaTplus25, sanTrangHomNay, Cfg, laHoangLoanSau: true);

    [Theory]
    [InlineData("KB1", 0.149, true)]   // nhỏ hơn hẳn 0,15 → bật cờ
    [InlineData("KB1", 0.15, false)]   // đúng 0,15 → KHÔNG phải hoảng loạn sâu
    [InlineData("KB2", 0.05, true)]
    [InlineData("KB4", 0.149, true)]
    [InlineData("N1", 0.1, true)]
    [InlineData("N3", 0.05, false)]    // N3 không bao giờ là lệnh hoảng loạn sâu
    public void Co_hoang_loan_sau_bien_015_va_loai_N3(string loai, double breadth, bool expected)
    {
        var ket = V3LuatBanThucTe.LaHoangLoanSau(
            Enum.Parse<LoaiKichBanV3>(loai), (decimal)breadth, Cfg);
        Assert.Equal(expected, ket);
    }

    [Fact]
    public void Nguong_hoang_loan_bang_gia_vao_nhan_115_khong_phai_MA()
    {
        // MA10 = 120 (> 115). Lệnh thường bán ở 120; hoảng loạn sâu bán ở 115 → chứng minh bỏ MA.
        Assert.Equal(115m, NguongHL);
        Assert.False(XetHL(LoaiKichBanV3.KB1, 114.9m).Thoat);

        var quyet = XetHL(LoaiKichBanV3.KB1, 115m);
        Assert.True(quyet.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.ChotLoiHoangLoan, quyet.LyDo);
        Assert.Equal(115m, quyet.NguongGia);

        // Cùng giá 115 nhưng là lệnh thường (cờ tắt) → DƯỚI MA 120 → chưa bán, lý do không phải hoảng loạn.
        var thuong = Xet(LoaiKichBanV3.KB1, 115m);
        Assert.False(thuong.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.Khong, thuong.LyDo);
    }

    [Fact]
    public void Hoang_loan_sau_van_chan_T25_va_san_trang()
    {
        // Chưa qua T+2.5: dù giá vượt 115 cũng chưa bán.
        Assert.False(XetHL(LoaiKichBanV3.KB1, 130m, daQuaTplus25: false).Thoat);
        // Sàn trắng bên mua: cấm bán phiên đó.
        Assert.False(XetHL(LoaiKichBanV3.KB1, 130m, sanTrangHomNay: true).Thoat);
    }

    [Fact]
    public void Hoang_loan_sau_giu_toi_da_40_het_han_ban_atc()
    {
        Assert.Equal(40, V3LuatBanThucTe.GiuToiDaTheoViThe(Cfg, LoaiKichBanV3.KB1, laHoangLoanSau: true));
        // KB1 thường vẫn 15; N3 không đổi.
        Assert.Equal(15, V3LuatBanThucTe.GiuToiDaTheoViThe(Cfg, LoaiKichBanV3.KB1, laHoangLoanSau: false));
        Assert.Equal(12, V3LuatBanThucTe.GiuToiDaTheoViThe(Cfg, LoaiKichBanV3.N3, laHoangLoanSau: false));

        // Phiên 39 chưa hết hạn (giá dưới 115) → giữ; phiên 40 → HetHanATC.
        Assert.False(XetHL(LoaiKichBanV3.KB1, 50m, soPhienDaGiu: 39).Thoat);
        var hetHan = XetHL(LoaiKichBanV3.KB4, 50m, soPhienDaGiu: 40);
        Assert.True(hetHan.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.HetHanATC, hetHan.LyDo);
    }

    [Fact]
    public void Nhip_moi_phut_hoang_loan_tra_dung_ly_do_qua_nguong_da_luu()
    {
        // Nhịp mỗi phút chỉ có ngưỡng đã lưu (115). Cờ bật → lý do ChotLoiHoangLoan, không phải ChamMA.
        var quyet = V3LuatBanThucTe.XetTheoNguongDaLuu(
            LoaiKichBanV3.N1, 115m, 115m, soPhienDaGi: 5, giuToiDa: 40,
            denLichAtoN3: false, daQuaTplus25: true, sanTrangHomNay: false, laHoangLoanSau: true);
        Assert.True(quyet.Thoat);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.ChotLoiHoangLoan, quyet.LyDo);

        // Cờ tắt → vẫn là ChamMA (hành vi lệnh thường giữ nguyên).
        var thuong = V3LuatBanThucTe.XetTheoNguongDaLuu(
            LoaiKichBanV3.N1, 115m, 115m, soPhienDaGi: 5, giuToiDa: 15,
            denLichAtoN3: false, daQuaTplus25: true, sanTrangHomNay: false, laHoangLoanSau: false);
        Assert.Equal(V3LuatBanThucTe.LyDoThoat.ChamMA, thuong.LyDo);
    }
}

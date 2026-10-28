using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.V3;

/// <summary>
/// SC-002 (Phụ lục C / FR-019, FR-020) + SC-009 (bổ sung v4 / FR-020a, FR-014a):
/// chọn lệnh theo danh mục V3 với quy mô THEO LÔ GẦN NHẤT, và "phiên mua" của lệnh chờ.
/// Dữ liệu bịa có kiểm soát (mã "AAA"/"HPG"… chỉ minh họa). Lô mặc định 100 cp, giá nghìn đồng.
/// </summary>
public sealed class V3DanhMucXepHangTests
{
    private static readonly V3CauHinh Cfg = new();

    private static V3BoKichBan.UngVien UngVien(
        string symbol, LoaiKichBanV3 loai, int hang = 1, decimal giaTinHieu = 5m)
        => new(symbol, loai, hang, giaTinHieu, GiaTriThuocXepHang: 1m, ChiTieu: null!, MuaMoCuaPhienSau: false);

    private static V3DanhMucXepHang.TrangThaiDanhMuc DanhMuc(
        decimal giaTri = 10000m, decimal tienMat = 100000m, int choTrong = 5,
        IReadOnlySet<string>? maDangGiu = null)
        => new(giaTri, tienMat, choTrong, maDangGiu ?? new HashSet<string>());

    private static V3DanhMucXepHang.LenhChon[] Chon(
        IReadOnlyList<V3BoKichBan.UngVien> uv, V3DanhMucXepHang.TrangThaiDanhMuc dm, V3CauHinh cfg)
        => V3DanhMucXepHang.Chon(uv, dm, cfg).Lenh.ToArray();

    // ===== SC-002: chọn & xếp hạng =====

    [Fact]
    public void Mot_ma_trung_nhieu_kich_ban_thi_kich_ban_uu_tien_cao_nhat_thang()
    {
        var lenh = Chon(
            new[] { UngVien("FPT", LoaiKichBanV3.KB4), UngVien("FPT", LoaiKichBanV3.KB1) },
            DanhMuc(), Cfg);

        Assert.Single(lenh);
        Assert.Equal(LoaiKichBanV3.KB1, lenh[0].Loai);
    }

    [Fact]
    public void Thu_tu_chon_theo_uu_tien_kb1_kb2_kb4_n1_n3()
    {
        var ungVien = new[]
        {
            UngVien("N3A", LoaiKichBanV3.N3), UngVien("KB4A", LoaiKichBanV3.KB4),
            UngVien("N1A", LoaiKichBanV3.N1), UngVien("KB2A", LoaiKichBanV3.KB2),
            UngVien("KB1A", LoaiKichBanV3.KB1),
        };

        var lenh = Chon(ungVien, DanhMuc(), Cfg);

        Assert.Equal(new[] { "KB1A", "KB2A", "KB4A", "N1A", "N3A" }, lenh.Select(l => l.Symbol));
    }

    [Fact]
    public void Loai_ma_dang_giu_khoi_vong_chon()
    {
        var lenh = Chon(
            new[] { UngVien("FPT", LoaiKichBanV3.KB1), UngVien("HPG", LoaiKichBanV3.KB1, hang: 2) },
            DanhMuc(maDangGiu: new HashSet<string> { "FPT" }), Cfg);

        Assert.Equal(new[] { "HPG" }, lenh.Select(l => l.Symbol));
    }

    [Fact]
    public void Ti_trong_v5_nhan_15_the_hien_theo_khoi_luong()
    {
        // V=10000 → chuẩn 2000; KB1/KB2/N1 ×1,5 = 3000 → 600 cp; KB4/N3 ×1 = 2000 → 400 cp (giá 5).
        var lenh = Chon(
            new[]
            {
                UngVien("KB1A", LoaiKichBanV3.KB1), UngVien("KB4A", LoaiKichBanV3.KB4),
            },
            DanhMuc(), Cfg);

        Assert.Equal(600m, lenh.Single(l => l.Symbol == "KB1A").KhoiLuong);
        Assert.Equal(400m, lenh.Single(l => l.Symbol == "KB4A").KhoiLuong);
        Assert.Equal(3000m, lenh.Single(l => l.Symbol == "KB1A").GiaTriLenh);
    }

    [Fact]
    public void Toi_da_5_vi_the_theo_cho_trong()
    {
        var ungVien = Enumerable.Range(1, 7).Select(i => UngVien($"M{i}", LoaiKichBanV3.KB1, hang: i)).ToArray();

        var lenh = Chon(ungVien, DanhMuc(choTrong: 5), Cfg);

        Assert.Equal(5, lenh.Length);
        Assert.Equal(new[] { "M1", "M2", "M3", "M4", "M5" }, lenh.Select(l => l.Symbol));
    }

    [Fact]
    public void Het_cho_hoac_het_tien_mat_thi_khong_chon_lenh_nao()
    {
        // Hết chỗ → không lệnh. Hết tiền mặt (0) → mọi ứng viên thiếu lô → không lệnh (chuyển sang BiBo).
        Assert.Empty(Chon(new[] { UngVien("KB1A", LoaiKichBanV3.KB1) }, DanhMuc(choTrong: 0), Cfg));
        Assert.Empty(Chon(new[] { UngVien("KB1A", LoaiKichBanV3.KB1) }, DanhMuc(tienMat: 0m), Cfg));
    }

    [Fact]
    public void Cung_kich_ban_thi_hang_nho_hon_duoc_chon_truoc()
    {
        var lenh = Chon(
            new[] { UngVien("A2", LoaiKichBanV3.KB4, hang: 2), UngVien("A1", LoaiKichBanV3.KB4, hang: 1) },
            DanhMuc(), Cfg);

        Assert.Equal(new[] { "A1", "A2" }, lenh.Select(l => l.Symbol));
    }

    // ===== SC-009: quy mô theo lô (FR-020a) =====

    [Theory]
    [InlineData(30d, 300d)]   // 8000/30 = 266,7 cp → 2,67 lô → gần nhất 3 lô = 300
    [InlineData(25d, 300d)]   // 8000/25 = 320 → 3,2 lô → 3 lô = 300
    [InlineData(120d, 100d)]  // 8000/120 = 66,7 → 0,67 lô → tròn 1 lô = 100 (ít nhất 1 lô)
    public void Khoi_luong_lam_tron_toi_lo_gan_nhat_it_nhat_1_lo(decimal gia, decimal kyVong)
        => Assert.Equal(kyVong, V3LenhMua.KhoiLuongTheoLo(8000m, gia, tienMatCon: 100000m, lo: 100));

    [Fact]
    public void Khong_du_1_lo_thi_tra_0()
        => Assert.Equal(0m, V3LenhMua.KhoiLuongTheoLo(8000m, 30m, tienMatCon: 2000m, lo: 100));

    [Fact]
    public void Khong_vuot_tien_mat_tong_lenh_nho_hon_hon_tien_mat()
    {
        // Tiền mặt 5000, giá 5 (1 lô = 500). KB1 muốn 3000 (600cp), KB2 muốn 3000 → tổng 6000 > 5000 →
        // KB2 bị bớt lô theo tiền mặt còn; tổng tiền các lệnh không được vượt 5000.
        var kq = V3DanhMucXepHang.Chon(
            new[] { UngVien("KB1A", LoaiKichBanV3.KB1), UngVien("KB2A", LoaiKichBanV3.KB2) },
            DanhMuc(giaTri: 10000m, tienMat: 5000m), Cfg);

        Assert.DoesNotContain(kq.Lenh, l => l.KhoiLuong <= 0m);
        Assert.True(kq.Lenh.Sum(l => l.GiaTriLenh) <= 5000m);
    }

    [Fact]
    public void Bo_ma_thieu_lo_va_xet_ung_vien_ke_tiep()
    {
        // Tiền mặt 2000. A giá 30 (1 lô = 3000 > 2000) → bỏ. B giá 15 (1 lô = 1500 ≤ 2000) → chọn.
        // Dùng 2 kịch bản để không bị gộp mã: A=KB1, B=KB2.
        var kq = V3DanhMucXepHang.Chon(
            new[]
            {
                new V3BoKichBan.UngVien("AAA", LoaiKichBanV3.KB1, 1, 30m, 1m, null!, false),
                new V3BoKichBan.UngVien("BBB", LoaiKichBanV3.KB2, 1, 15m, 1m, null!, false),
            },
            DanhMuc(giaTri: 10000m, tienMat: 2000m), Cfg);

        Assert.Equal(new[] { "BBB" }, kq.Lenh.Select(l => l.Symbol));
        Assert.Equal(new[] { "AAA" }, kq.BiBoKhongDuLo.Select(u => u.Symbol));
    }

    [Fact]
    public void Khong_bao_gio_con_lenh_khoi_luong_0()
    {
        var lenh = Chon(
            new[] { UngVien("KB1A", LoaiKichBanV3.KB1, giaTinHieu: 30m) },
            DanhMuc(tienMat: 100000m), Cfg);
        Assert.All(lenh, l => Assert.True(l.KhoiLuong > 0m));
    }

    // ===== SC-009: phiên mua của lệnh chờ (FR-014a) =====

    private static readonly DateOnly Mon = new(2026, 10, 26); // thứ Hai
    private static readonly DateOnly Tue = new(2026, 10, 27);
    private static readonly DateOnly Fri = new(2026, 10, 23);

    [Fact]
    public void Kb1_buoc_sang_phien_ke_tiep_kb4_giu_nguyen_phien_tin_hieu()
    {
        Assert.Equal(Tue, V3LenhMua.NgayPhienMua(LoaiKichBanV3.KB1, Cfg, Mon));
        Assert.Equal(Mon, V3LenhMua.NgayPhienMua(LoaiKichBanV3.KB4, Cfg, Mon));
        // Thứ Sáu → phiên kế tiếp bỏ cuối tuần = thứ Hai.
        Assert.Equal(Mon, V3LenhMua.NgayPhienMua(LoaiKichBanV3.KB2, Cfg, Fri));
    }

    [Fact]
    public void Lenh_cho_chi_khop_dung_phien_mua_va_huy_khi_qua_phien()
    {
        // KB1 tín hiệu Thứ Hai → phiên mua Thứ Ba.
        Assert.True(V3LenhMua.LaDungPhienMua(LoaiKichBanV3.KB1, Cfg, Mon, Tue));
        Assert.False(V3LenhMua.LaDungPhienMua(LoaiKichBanV3.KB1, Cfg, Mon, Mon));
        Assert.False(V3LenhMua.LaDungPhienMua(LoaiKichBanV3.KB1, Cfg, Mon, new DateOnly(2026, 10, 28)));

        // 16:00: tới/sau phiên mua → huỷ. Thứ Hai chưa tới phiên mua (chưa huỷ), Thứ Ba trở đi huỷ.
        Assert.False(V3LenhMua.DaDenHoacQuaPhienMua(LoaiKichBanV3.KB1, Cfg, Mon, Mon));
        Assert.True(V3LenhMua.DaDenHoacQuaPhienMua(LoaiKichBanV3.KB1, Cfg, Mon, Tue));
        // KB4/N1/N3 phiên mua = phiên tín hiệu → 16:00 cùng ngày huỷ nếu chưa khớp.
        Assert.True(V3LenhMua.DaDenHoacQuaPhienMua(LoaiKichBanV3.KB4, Cfg, Mon, Mon));
    }

    // ===== SC-009: tin HUỶ (FR-014a) =====

    [Fact]
    public void Tin_huy_mua_in_ma_kich_ban_va_ly_do()
    {
        var msg = V3TelegramFormatter.FormatHuyMua("HPG", LoaiKichBanV3.KB1, "không khớp trong phiên mua");

        Assert.Contains("HUỶ mua", msg);
        Assert.Contains("HPG", msg);
        Assert.Contains("KB1", msg);
        Assert.Contains("không khớp trong phiên mua", msg);
    }
}

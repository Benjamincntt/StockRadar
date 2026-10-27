using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// SC-002 — luật chọn lệnh theo danh mục V3 (Phụ lục C / FR-019, FR-020):
/// tối đa 5 vị thế, ưu tiên KB1 &gt; KB2 &gt; KB4 &gt; N1 &gt; N3, mỗi mã chỉ thuộc
/// đúng một kịch bản (V3-B4), không trùng mã đang giữ, mỗi lệnh = V/5 (×1,5 cho KB1/KB2/N1),
/// không vượt tiền mặt.
/// </summary>
public sealed class V3DanhMucXepHangTests
{
    private static readonly V3CauHinh Cfg = new();

    private static V3BoKichBan.UngVien UngVien(
        string symbol,
        LoaiKichBanV3 loai,
        int hang = 1,
        decimal giaTinHieu = 50m)
        => new(symbol, loai, hang, giaTinHieu, GiaTriThuocXepHang: 1m, ChiTieu: null!, MuaMoCuaPhienSau: false);

    private static V3DanhMucXepHang.TrangThaiDanhMuc DanhMuc(
        decimal giaTri = 1000m,
        decimal tienMat = 500m,
        int choTrong = 5,
        IReadOnlySet<string>? maDangGiu = null)
        => new(giaTri, tienMat, choTrong, maDangGiu ?? new HashSet<string>());

    [Fact]
    public void Mot_ma_trung_nhieu_kich_ban_thi_kich_ban_uu_tien_cao_nhat_thang()
    {
        // V3-B4: FPT vừa vào KB4 vừa vào KB1 → chỉ được tính là ứng viên KB1.
        var lenh = V3DanhMucXepHang.Chon(
            new[] { UngVien("FPT", LoaiKichBanV3.KB4), UngVien("FPT", LoaiKichBanV3.KB1) },
            DanhMuc(),
            Cfg);

        Assert.Single(lenh);
        Assert.Equal(LoaiKichBanV3.KB1, lenh[0].Loai);
    }

    [Fact]
    public void Thu_tu_chon_theo_uu_tien_kb1_kb2_kb4_n1_n3()
    {
        var ungVien = new[]
        {
            UngVien("N3A", LoaiKichBanV3.N3),
            UngVien("KB4A", LoaiKichBanV3.KB4),
            UngVien("N1A", LoaiKichBanV3.N1),
            UngVien("KB2A", LoaiKichBanV3.KB2),
            UngVien("KB1A", LoaiKichBanV3.KB1),
        };

        var lenh = V3DanhMucXepHang.Chon(ungVien, DanhMuc(tienMat: 5000m), Cfg);

        Assert.Equal(
            new[] { "KB1A", "KB2A", "KB4A", "N1A", "N3A" },
            lenh.Select(l => l.Symbol));
    }

    [Fact]
    public void Loai_ma_dang_giu_khoi_vong_chon()
    {
        var lenh = V3DanhMucXepHang.Chon(
            new[] { UngVien("FPT", LoaiKichBanV3.KB1), UngVien("HPG", LoaiKichBanV3.KB1, hang: 2) },
            DanhMuc(maDangGiu: new HashSet<string> { "FPT" }),
            Cfg);

        Assert.Equal(new[] { "HPG" }, lenh.Select(l => l.Symbol));
    }

    [Fact]
    public void Kich_thuoc_lenh_v5_nhan_15_cho_kb1_kb2_n1_khong_nhan_cho_kb4_n3()
    {
        // V=1000 → chuẩn 200; KB1/KB2/N1 ×1,5 = 300, KB4/N3 ×1 = 200 (FR-020, Phụ lục C).
        var lenh = V3DanhMucXepHang.Chon(
            new[]
            {
                UngVien("KB1A", LoaiKichBanV3.KB1),
                UngVien("KB2A", LoaiKichBanV3.KB2),
                UngVien("N1A", LoaiKichBanV3.N1),
                UngVien("KB4A", LoaiKichBanV3.KB4),
                UngVien("N3A", LoaiKichBanV3.N3),
            },
            DanhMuc(giaTri: 1000m, tienMat: 5000m),
            Cfg);

        Assert.Equal(300m, lenh.Single(l => l.Symbol == "KB1A").GiaTriLenh);
        Assert.Equal(300m, lenh.Single(l => l.Symbol == "KB2A").GiaTriLenh);
        Assert.Equal(300m, lenh.Single(l => l.Symbol == "N1A").GiaTriLenh);
        Assert.Equal(200m, lenh.Single(l => l.Symbol == "KB4A").GiaTriLenh);
        Assert.Equal(200m, lenh.Single(l => l.Symbol == "N3A").GiaTriLenh);
    }

    [Fact]
    public void Khong_vuot_tien_mat_lenh_cuoi_bat_gia_tri_con_lai()
    {
        // Tiền mặt 500: KB1 cần 300 → còn 200; N3 sau đó chỉ được 200 (không vượt tiền mặt).
        var lenh = V3DanhMucXepHang.Chon(
            new[] { UngVien("KB1A", LoaiKichBanV3.KB1), UngVien("N3Z", LoaiKichBanV3.N3) },
            DanhMuc(giaTri: 1000m, tienMat: 500m),
            Cfg);

        Assert.Equal(2, lenh.Count);
        Assert.Equal(300m, lenh[0].GiaTriLenh);
        Assert.Equal(200m, lenh[1].GiaTriLenh);
    }

    [Fact]
    public void Toi_da_5_vi_the_theo_cho_trong()
    {
        var ungVien = Enumerable.Range(1, 7).Select(i => UngVien($"M{i}", LoaiKichBanV3.KB1, hang: i)).ToArray();

        var lenh = V3DanhMucXepHang.Chon(ungVien, DanhMuc(tienMat: 100000m, choTrong: 5), Cfg);

        Assert.Equal(5, lenh.Count);
        Assert.Equal(new[] { "M1", "M2", "M3", "M4", "M5" }, lenh.Select(l => l.Symbol));
    }

    [Fact]
    public void Het_cho_trong_hoac_het_tien_mat_thi_khong_chon_nua()
    {
        Assert.Empty(V3DanhMucXepHang.Chon(
            new[] { UngVien("KB1A", LoaiKichBanV3.KB1) },
            DanhMuc(choTrong: 0),
            Cfg));
        Assert.Empty(V3DanhMucXepHang.Chon(
            new[] { UngVien("KB1A", LoaiKichBanV3.KB1) },
            DanhMuc(tienMat: 0m),
            Cfg));
    }

    [Fact]
    public void Cung_kich_ban_thi_hang_nho_hon_duoc_chon_truoc()
    {
        var lenh = V3DanhMucXepHang.Chon(
            new[] { UngVien("A2", LoaiKichBanV3.KB4, hang: 2), UngVien("A1", LoaiKichBanV3.KB4, hang: 1) },
            DanhMuc(tienMat: 5000m),
            Cfg);

        Assert.Equal(new[] { "A1", "A2" }, lenh.Select(l => l.Symbol));
    }
}

using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// FR-001/FR-003 — máy chỉ báo toàn thị trường V3 theo research 01-phuong-phap.md:
/// (1) lợi nhuận ngày của chỉ số dùng tập UNI <i>của phiên liền trước</i> (bt.py: R.where(UNI.shift())),
/// (2) SECR20 chỉ lấy trung vị ngành — mã chưa có ngành thì null, không thay bằng RET20 của chính nó.
/// </summary>
public sealed class V3ChiSoToanThiTruongTests
{
    private static readonly DateOnly BatDau = new(2026, 9, 1);

    /// <summary>Danh sách <paramref name="soPhien"/> phiên giao dịch thật liên tiếp, bắt đầu từ 01/09/2026.</summary>
    private static List<DateOnly> Phien(int soPhien)
    {
        var ds = new List<DateOnly>(soPhien);
        var d = BatDau;
        while (ds.Count < soPhien)
        {
            if (TradingSessionMath.IsTradingDay(d))
                ds.Add(d);
            d = d.AddDays(1);
        }
        return ds;
    }

    private static Stock Ma(string symbol, string sector, IReadOnlyList<(decimal Close, long Volume)> nen)
    {
        var dates = Phien(nen.Count);
        var bars = new List<OhlcvBar>(nen.Count);
        for (var i = 0; i < nen.Count; i++)
        {
            var (c, v) = nen[i];
            bars.Add(new OhlcvBar(dates[i], c, c, c, c, v));
        }
        return new Stock(symbol, symbol, sector, bars, Exchange: "HOSE");
    }

    // ── (1) UNI.shift(): tập mã tính lợi nhuận ngày là UNI của phiên trước ────

    [Fact]
    public void Chi_so_loai_ma_khong_con_uni_o_phien_truoc_khoi_loi_nhuan_ngay()
    {
        var cfg = new V3CauHinh(); // UNI: TV20 ≥ 5 tỷ và close ≥ 5

        // AAA: luôn thuộc UNI, giá phẳng → lợi nhuận 0%.
        var aaa = Ma("AAA", "Thép",
            new (decimal, long)[] { (20m, 1_000_000L), (20m, 1_000_000L), (20m, 1_000_000L) });

        // BBB: thuộc UNI ở phiên 1 (close 20), RỚT UNI ở phiên 2 (close 4 < 5),
        // rồi bật lại thuộc UNI ở phiên 3 (close 8).
        var bbb = Ma("BBB", "Thép",
            new (decimal, long)[] { (20m, 1_000_000L), (4m, 1_000_000L), (8m, 1_000_000L) });

        var kt = V3ChiSoToanThiTruong.Tinh(new[] { aaa, bbb }, cfg);

        // Phiên 3 BBB vẫn thuộc UNI (TV20 = 10,67 tỷ, close 8 ≥ 5) — nhưng tập UNI hôm nay
        // KHÔNG được dùng cho cả lịch sử.
        Assert.True(kt.TheoMa["BBB"].ThuocUni);

        // Bước 1 (phiên 1→2): BBB thuộc UNI tại phiên 1 → lợi nhuận −80% clip còn −16%.
        //   TB(0%, −16%) = −8% → chỉ số 0,92.
        // Bước 2 (phiên 2→3): BBB KHÔNG thuộc UNI tại phiên 2 → chỉ còn AAA 0% → giữ 0,92.
        // Cách cũ (dùng UNI hôm nay cho cả lịch sử) sẽ cộng thêm +16% ở bước 2 → 0,9936.
        Assert.Equal(0.92m, kt.ChiSo);
        Assert.Equal(0.946667m, kt.ChiSoMa50, 6);   // TB(1; 0,92; 0,92)
        Assert.Equal(-2.8169m, kt.ChiSoDoLechMa50PhanTram, 4);
    }

    // ── (2) SECR20: không có ngành → null (FR-003) ───────────────────────────

    [Fact]
    public void Ma_khong_co_nganh_secr20_null_khong_lay_ret20_cua_chinh_no()
    {
        var cfg = new V3CauHinh();

        // 21 nến: 20 nến đầu giá 20, nến cuối 16 → RET20 = −20% (đủ điều kiện N1 nếu SECR20 bị thay bằng RET20).
        var nenGiam = Enumerable.Repeat((20m, 1_000_000L), 20).Append((16m, 1_000_000L)).ToList();
        var nenPhang = Enumerable.Repeat((20m, 1_000_000L), 21).ToList();

        var kt = V3ChiSoToanThiTruong.Tinh(
        [
            Ma("NOSE", string.Empty, nenGiam),
            Ma("STL1", "Thép", nenGiam),
            Ma("STL2", "Thép", nenPhang),
        ], cfg);

        // Mã không ngành: SECR20 = null chứ không phải −20 (RET20 của chính nó).
        Assert.Equal(-20m, kt.TheoMa["NOSE"].Ret20);
        Assert.Null(kt.TheoMa["NOSE"].Secr20);

        // Mã có ngành: SECR20 = trung vị RET20 của các mã UNI cùng ngành = TB(−20; 0) = −10.
        Assert.Equal(-10m, kt.TheoMa["STL1"].Secr20);
        Assert.Equal(-10m, kt.TheoMa["STL2"].Secr20);
    }

    [Fact]
    public void Ma_co_nganh_nhung_chua_co_ai_trong_uni_thi_secr20_null()
    {
        var cfg = new V3CauHinh();
        // Chỉ có một mã, không thuộc UNI (giá 3 < 5) → ngành của nó không có thành viên UNI nào.
        var rar = Ma("RAR", "Cao su",
            Enumerable.Repeat((3m, 100L), 21).ToList());

        var kt = V3ChiSoToanThiTruong.Tinh([rar], cfg);

        Assert.False(kt.TheoMa["RAR"].ThuocUni);
        Assert.Null(kt.TheoMa["RAR"].Secr20);
    }

    [Fact]
    public void Nganh_chi_co_mot_ma_uni_thi_secr20_bang_trung_vi_cua_nganh()
    {
        // Ngành chỉ có đúng một thành viên UNI → trung vị = RET20 của chính nó (đúng định nghĩa research 01),
        // khác với trường hợp RỖ NGÀNH ở test trên (SECR20 = null).
        var cfg = new V3CauHinh();
        var nenGiam = Enumerable.Repeat((20m, 1_000_000L), 20).Append((16m, 1_000_000L)).ToList();

        var kt = V3ChiSoToanThiTruong.Tinh([Ma("NOSE", "Thực phẩm", nenGiam)], cfg);

        Assert.Equal(-20m, kt.TheoMa["NOSE"].Secr20);
    }

    // ── (3) TinhChuoi: cùng luật nhưng tính cho TỪNG phiên (nạp chuỗi khi deploy) ──

    [Fact]
    public void Chuoi_chi_so_dung_uni_tung_ngay_khong_phai_uni_hom_nay_cho_ca_lich_su()
    {
        var cfg = new V3CauHinh();

        // Cùng dữ liệu với test (1): AAA luôn thuộc UNI giá phẳng; BBB rơi UNI ở phiên 2 rồi bật lại phiên 3.
        var aaa = Ma("AAA", "Thép",
            new (decimal, long)[] { (20m, 1_000_000L), (20m, 1_000_000L), (20m, 1_000_000L) });
        var bbb = Ma("BBB", "Thép",
            new (decimal, long)[] { (20m, 1_000_000L), (4m, 1_000_000L), (8m, 1_000_000L) });

        var chuoi = V3ChiSoToanThiTruong.TinhChuoi(new[] { aaa, bbb }, cfg);
        var dates = Phien(3);

        Assert.Equal(3, chuoi.Count);
        Assert.Equal(dates[0], chuoi[0].Ngay);

        // Phiên 1 (mồi): chưa có bước lợi nhuận, mức chỉ số = 1, UNI = {AAA, BBB}.
        Assert.Equal(0m, chuoi[0].LoiNhuan);
        Assert.Equal(1m, chuoi[0].ChiSo);
        Assert.Equal(new[] { "AAA", "BBB" }, chuoi[0].DanhSachUni);

        // Phiên 2: BBB close 4 < 5 → RỚT UNI ngay tại phiên này ⇒ UNI lưu cho phiên sau chỉ còn {AAA}.
        //   Bước 1→2 vẫn tính BBB (vì thuộc UNI ở phiên 1): TB(0%; −16%) = −8% → mức 0,92.
        Assert.Equal(1, chuoi[1].SoMaUni);
        Assert.Equal(new[] { "AAA" }, chuoi[1].DanhSachUni);
        Assert.Equal(-0.08m, chuoi[1].LoiNhuan);
        Assert.Equal(0.92m, chuoi[1].ChiSo);

        // Phiên 3: BBB trở lại UNI nhưng KHÔNG được góp lợi nhuận bước 2→3 (không thuộc UNI ở phiên 2)
        //   → chỉ số đi ngang 0,92, không phải 0,9936 như cách dùng UNI hôm nay cho cả lịch sử.
        Assert.Equal(2, chuoi[2].SoMaUni);
        Assert.Equal(0m, chuoi[2].LoiNhuan);
        Assert.Equal(0.92m, chuoi[2].ChiSo);
        Assert.Equal(0.946667m, chuoi[2].ChiSoMa50, 6);   // TB(1; 0,92; 0,92)
        Assert.Equal(-2.8169m, chuoi[2].ChiSoDoLechMa50PhanTram, 4);

        // Chuỗi và bản một phiên phải cùng một luật: phiên cuối của chuỗi == kết quả Tinh hôm nay.
        var kt = V3ChiSoToanThiTruong.Tinh(new[] { aaa, bbb }, cfg);
        Assert.Equal(kt.ChiSo, chuoi[^1].ChiSo);
        Assert.Equal(kt.ChiSoMa50, chuoi[^1].ChiSoMa50);
        Assert.Equal(kt.ChiSoDoLechMa50PhanTram, chuoi[^1].ChiSoDoLechMa50PhanTram);
        Assert.Equal(kt.SoMaUni, chuoi[^1].SoMaUni);
        Assert.Equal(kt.DanhSachUni, chuoi[^1].DanhSachUni);
        // Giá trị nhịp ATC cộng dồn: lastStored × (1 + ChiSoLoiNhuanHomNay).
        Assert.Equal(kt.ChiSoLoiNhuanHomNay, chuoi[^1].LoiNhuan);
    }

    [Fact]
    public void Chuoi_chi_so_tu_ngay_cho_truoc_giu_nguyen_gia_tri_cac_phien_muoi()
    {
        var cfg = new V3CauHinh();
        var aaa = Ma("AAA", "Thép",
            new (decimal, long)[] { (20m, 1_000_000L), (20m, 1_000_000L), (20m, 1_000_000L) });
        var bbb = Ma("BBB", "Thép",
            new (decimal, long)[] { (20m, 1_000_000L), (4m, 1_000_000L), (8m, 1_000_000L) });
        var dates = Phien(3);

        // Lọc ngày bắt đầu chỉ cắt số dòng ghi DB — không tính lại lịch sử (MA50 vẫn nhìn về trước).
        var toanBo = V3ChiSoToanThiTruong.TinhChuoi(new[] { aaa, bbb }, cfg);
        var tuPhien2 = V3ChiSoToanThiTruong.TinhChuoi(new[] { aaa, bbb }, cfg, tuNgay: dates[1]);

        Assert.Equal(2, tuPhien2.Count);
        Assert.Equal(dates[1], tuPhien2[0].Ngay);
        Assert.Equal(toanBo[1].ChiSoMa50, tuPhien2[0].ChiSoMa50);
        Assert.Equal(toanBo[2].ChiSoMa50, tuPhien2[1].ChiSoMa50);
    }
}

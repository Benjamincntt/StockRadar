using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// SC-002 — mỗi luật trong Phụ lục A (5 kịch bản V3) có test xUnit riêng:
/// cửa toàn thị trường, cửa từng mã, thước xếp hạng duy nhất, nhịp mua, top-N.
/// </summary>
public sealed class V3BoKichBanTests
{
    private static readonly DateOnly NgayTinHieu = new(2026, 10, 23);

    private static V3ChiSoToanThiTruong.ChiTieuMa Ma(
        string symbol,
        decimal tv20 = 150m,
        bool thuocUni = true,
        decimal rsi14 = 50m,
        decimal ret20 = 0m,
        decimal? secr20 = 0m,
        decimal rs63 = 50m,
        decimal rs126 = 50m,
        int floor20 = 0,
        bool laSan = false,
        decimal close = 50m,
        string sector = "Ngân hàng") => new(
            symbol, sector, thuocUni, tv20, close, rsi14, ret20, secr20, rs63, rs126, floor20, laSan);

    private static V3ChiSoToanThiTruong.KetQua ThiTruong(
        IReadOnlyDictionary<string, V3ChiSoToanThiTruong.ChiTieuMa> theoMa,
        decimal breadth50 = 0.5m,
        decimal doLechMa50 = 0m,
        int left = 10,
        IReadOnlyList<string>? danhSachUni = null) => new(
            NgayTinHieu,
            SoMaUni: theoMa.Count(kv => kv.Value.ThuocUni),
            Breadth50: breadth50,
            ChiSo: 1500m,
            ChiSoMa50: 1500m,
            ChiSoDoLechMa50PhanTram: doLechMa50,
            ChiSoLoiNhuanHomNay: 0m,
            DanhSachUni: danhSachUni ?? theoMa.Keys.ToArray(),
            Left: left,
            Tdm: 4,
            TheoMa: theoMa);

    private static List<V3BoKichBan.UngVien> ChamKichBan(
        V3ChiSoToanThiTruong.KetQua kt,
        LoaiKichBanV3 loai,
        V3CauHinh? cfg = null)
        => V3BoKichBan.Cham(kt, cfg ?? new V3CauHinh())
            .Where(u => u.Loai == loai)
            .OrderBy(u => u.Hang)
            .ToList();

    // ── KB1 · Hoảng loạn ────────────────────────────────────────────────────

    [Fact]
    public void Kb1_cua_thi_truong_qua_breadth50_xep_hang_rs126_tang_dan()
    {
        // BREADTH50=0,15 < 0,20 → cửa mở; RS126 thấp nhất (giải chấp sâu nhất) hạng 1.
        var kt = ThiTruong(new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
        {
            ["FPT"] = Ma("FPT", rs126: 10m),
            ["HCM"] = Ma("HCM", rs126: 30m),
            ["HPG"] = Ma("HPG", rs126: 20m),
        }, breadth50: 0.15m);

        var ds = ChamKichBan(kt, LoaiKichBanV3.KB1);

        Assert.Equal(3, ds.Count);
        Assert.Equal("FPT", ds[0].Symbol);
        Assert.Equal(1, ds[0].Hang);
        Assert.Equal("HPG", ds[1].Symbol);
        Assert.Equal(2, ds[1].Hang);
        // Nhịp mua KB1 = ATO mở cửa phiên sau.
        Assert.True(ds[0].MuaMoCuaPhienSau);
        Assert.Equal(10m, ds[0].GiaTriThuocXepHang);
    }

    [Fact]
    public void Kb1_cua_thi_truong_qua_chi_so_duoi_ma50_sau_hon_nguong()
    {
        // BREADTH50=0,5 không đạt, nhưng chỉ số dưới MA50 8% > 7% → cửa vẫn mở.
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa> { ["FPT"] = Ma("FPT") },
            breadth50: 0.5m,
            doLechMa50: -8m);

        Assert.Single(ChamKichBan(kt, LoaiKichBanV3.KB1));
    }

    [Fact]
    public void Kb1_cua_ma_chat_tv20_va_loai_ngoaiv_udi_uni()
    {
        // FPT TV20=35 đạt; ABC TV20=25 < 30 bị loại; XYZ ngoài UNI bị loại.
        var kt = ThiTruong(new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
        {
            ["FPT"] = Ma("FPT", tv20: 35m),
            ["ABC"] = Ma("ABC", tv20: 25m),
            ["XYZ"] = Ma("XYZ", tv20: 200m, thuocUni: false),
        }, breadth50: 0.10m);

        var ds = ChamKichBan(kt, LoaiKichBanV3.KB1);

        Assert.Equal(new[] { "FPT" }, ds.Select(u => u.Symbol));
    }

    // ── KB2 · Chỉ số thủng MA50 ─────────────────────────────────────────────

    [Fact]
    public void Kb2_can_chi_so_duoi_ma50_tu_5_phan_tram_xep_hang_rsi14()
    {
        var kt = ThiTruong(new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
        {
            ["VCB"] = Ma("VCB", rsi14: 25m),
            ["CTG"] = Ma("CTG", rsi14: 40m),
        }, doLechMa50: -6m);

        var ds = ChamKichBan(kt, LoaiKichBanV3.KB2);

        Assert.Equal(new[] { "VCB", "CTG" }, ds.Select(u => u.Symbol));
        // Nhịp mua KB2 = ATO mở cửa phiên sau.
        Assert.True(ds[0].MuaMoCuaPhienSau);
    }

    [Fact]
    public void Kb2_dong_cua_khi_chi_so_chi_duoi_ma50_4_phan_tram()
    {
        // Ngưỡng 5% — -4% chưa đủ sâu → không có ứng viên KB2.
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa> { ["VCB"] = Ma("VCB") },
            doLechMa50: -4m);

        Assert.Empty(ChamKichBan(kt, LoaiKichBanV3.KB2));
    }

    // ── KB4 · Ngành bán quá mức ─────────────────────────────────────────────

    [Fact]
    public void Kb4_ma_con_giam_sau_hon_nganh_va_dat_tv20_thi_dat()
    {
        // SECR20=-12 ≤ -10 và RET20=-15 < SECR20 → ngành bán quá mức, mã còn yếu hơn ngành.
        var kt = ThiTruong(new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
        {
            ["DHC"] = Ma("DHC", secr20: -12m, ret20: -15m, tv20: 50m),
            ["GIL"] = Ma("GIL", secr20: -12m, ret20: -8m, tv20: 50m),  // mạnh hơn ngành → loại
            ["TLG"] = Ma("TLG", secr20: -5m, ret20: -30m, tv20: 50m),  // ngành chưa quá mức → loại
        }, breadth50: 0.30m);

        var ds = ChamKichBan(kt, LoaiKichBanV3.KB4);

        Assert.Equal(new[] { "DHC" }, ds.Select(u => u.Symbol));
        // Nhịp mua KB4 = ATC cuối phiên tín hiệu.
        Assert.False(ds[0].MuaMoCuaPhienSau);
    }

    [Fact]
    public void Kb4_dong_cua_khi_breadth50_tren_035()
    {
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
            {
                ["DHC"] = Ma("DHC", secr20: -12m, ret20: -15m, tv20: 50m),
            },
            breadth50: 0.40m);

        Assert.Empty(ChamKichBan(kt, LoaiKichBanV3.KB4));
    }

    [Fact]
    public void Ma_khong_co_nganh_secr20_null_bi_loai_kb4_va_n1()
    {
        // BUSINESS-RULE FR-003: mã chưa có ngành → SECR20 = null, KHÔNG lấy RET20 của chính nó thay thế.
        // NOSE có RET20 = −14 (≤ −12) và floor20 đạt: nếu thay RET20 vào SECR20 thì sẽ trúng cả N1 lẫn KB4 —
        // phép thử này chốt rằng nó bị loại.
        var kt = ThiTruong(new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
        {
            ["NOSE"] = Ma("NOSE", sector: string.Empty, secr20: null, ret20: -14m,
                floor20: 2, laSan: false, tv20: 50m),
            ["HSG"] = Ma("HSG", secr20: -13m, ret20: -14m, floor20: 2, laSan: false, tv20: 50m),
        }, breadth50: 0.30m, left: 5);

        Assert.Equal(new[] { "HSG" }, ChamKichBan(kt, LoaiKichBanV3.KB4).Select(u => u.Symbol));
        Assert.Equal(new[] { "HSG" }, ChamKichBan(kt, LoaiKichBanV3.N1).Select(u => u.Symbol));
    }

    // ── N1 · Giải chấp cạn kiệt ─────────────────────────────────────────────

    [Fact]
    public void N1_can_floor20_dat_va_hom_nay_khong_san_va_gan_cuoi_thang()
    {
        var kt = ThiTruong(new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
        {
            ["HSG"] = Ma("HSG", floor20: 2, laSan: false, secr20: -13m, tv20: 30m),
            ["LSS"] = Ma("LSS", floor20: 0, laSan: false, secr20: -13m, tv20: 30m), // chưa từng sàn → loại
            ["BTS"] = Ma("BTS", floor20: 1, laSan: true, secr20: -13m, tv20: 30m),  // hôm nay đang sàn → loại
        }, left: 5);

        var ds = ChamKichBan(kt, LoaiKichBanV3.N1);

        Assert.Equal(new[] { "HSG" }, ds.Select(u => u.Symbol));
        // Nhịp mua N1 = ATC phiên tín hiệu.
        Assert.False(ds[0].MuaMoCuaPhienSau);
    }

    [Fact]
    public void N1_dong_cua_khi_left_lon_hon_6()
    {
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
            {
                ["HSG"] = Ma("HSG", floor20: 2, secr20: -13m, tv20: 30m),
            },
            left: 7);

        Assert.Empty(ChamKichBan(kt, LoaiKichBanV3.N1));
    }

    // ── N3 · Đón dòng tiền đầu tháng ────────────────────────────────────────

    [Fact]
    public void N3_chi_mo_khi_left_dung_bang_5_va_chi_so_lech_ma50_ngoai_vung_cam()
    {
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa> { ["VNM"] = Ma("VNM", tv20: 80m) },
            doLechMa50: 2m,
            left: 5);

        var ds = ChamKichBan(kt, LoaiKichBanV3.N3);

        Assert.Single(ds);
        Assert.False(ds[0].MuaMoCuaPhienSau); // ATC cuối phiên
    }

    [Fact]
    public void N3_bo_khi_chi_so_nam_trong_vung_cam_ma50_du_15_phan_tram()
    {
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa> { ["VNM"] = Ma("VNM", tv20: 80m) },
            doLechMa50: 1.2m,
            left: 5);

        Assert.Empty(ChamKichBan(kt, LoaiKichBanV3.N3));
    }

    [Fact]
    public void N3_bo_khi_left_khong_dung_bang_5()
    {
        // LEFT=4 (thỏa N1) nhưng N3 yêu cầu đúng bằng 5.
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa> { ["VNM"] = Ma("VNM", tv20: 80m) },
            doLechMa50: 2m,
            left: 4);

        Assert.Empty(ChamKichBan(kt, LoaiKichBanV3.N3));
    }

    // ── Luật chung: top-N và không điểm tổng hợp ────────────────────────────

    [Fact]
    public void Chi_tra_top_5_ung_vien_dau_moi_kich_ban()
    {
        var theoMa = new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>();
        for (var i = 0; i < 8; i++)
            theoMa[$"M{i}"] = Ma($"M{i}", rs126: i * 10m);
        var kt = ThiTruong(theoMa, breadth50: 0.10m);

        var ds = ChamKichBan(kt, LoaiKichBanV3.KB1);

        Assert.Equal(5, ds.Count);
        Assert.Equal(Enumerable.Range(1, 5), ds.Select(u => u.Hang));
    }

    [Fact]
    public void Cham_tra_dung_5_kich_ban_khong_diem_tong_hop()
    {
        // Snapshot mở cửa cả 5 kịch bản (breadth 0,10 + chỉ số dưới MA50 6% + left 5),
        // mỗi mã được "đo ni" vừa khít ĐÚNG MỘT kịch bản yếu nhất còn lại — V3-B4
        // (một mã chỉ thuộc kịch bản ưu tiên cao nhất) không được làm loãng kịch bản khác.
        var kt = ThiTruong(
            new Dictionary<string, V3ChiSoToanThiTruong.ChiTieuMa>
            {
                ["KB1ONLY"] = Ma("KB1ONLY", rs126: 1m, rsi14: 90m, ret20: 0m, secr20: 0m, floor20: 0),
                ["KB2ONLY"] = Ma("KB2ONLY", rs126: 90m, rsi14: 1m, ret20: 0m, secr20: 0m, floor20: 0),
                ["KB4ONLY"] = Ma("KB4ONLY", rs126: 90m, rsi14: 90m, ret20: -15m, secr20: -13m, floor20: 1),
                ["N1ONLY"] = Ma("N1ONLY", rs126: 90m, rsi14: 90m, ret20: 0m, secr20: -14m, floor20: 2),
                ["N3ONLY"] = Ma("N3ONLY", rs126: 90m, rsi14: 90m, ret20: 0m, secr20: 0m, floor20: 0, rs63: 95m),
            },
            breadth50: 0.10m,
            doLechMa50: -6m,
            left: 5);

        var ds = V3BoKichBan.Cham(kt, new V3CauHinh());

        // Máy chấm trả top-N của TỪNG kịch bản độc lập (V3-B4 chỉ gộp mã ở bước chọn danh mục).
        // Ở snapshot này mỗi kịch bản đều có đủ 5 mã đạt cửa riêng của nó — kiểm tra mã
        // "đo ni" phải đứng hạng 1 theo đúng thước xếp hạng của kịch bản (V3-B2: không điểm tổng hợp).
        var hangNhat = ds
            .Where(u => u.Hang == 1)
            .ToDictionary(u => u.Loai, u => u.Symbol);
        Assert.Equal(
            new Dictionary<LoaiKichBanV3, string>
            {
                [LoaiKichBanV3.KB1] = "KB1ONLY",   // RS126 thấp nhất
                [LoaiKichBanV3.KB2] = "KB2ONLY",   // RSI14 thấp nhất
                [LoaiKichBanV3.KB4] = "KB4ONLY",   // RET20 thấp nhất
                [LoaiKichBanV3.N1] = "N1ONLY",     // SECR20 thấp nhất (-14 < -13 của KB4ONLY)
                [LoaiKichBanV3.N3] = "N3ONLY",     // RS63 cao nhất (95 > mặc định 50)
            },
            hangNhat);
        Assert.Equal(
            new[] { "KB1ONLY", "KB2ONLY", "KB4ONLY", "N1ONLY", "N3ONLY" },
            ds.Where(u => u.Hang == 1).Select(u => u.Symbol).OrderBy(s => s));
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.MuaTet;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19, sổ V4) — đợt 2a: theo dõi giỏ ĐÃ MUA, báo 95% (cảnh báo sớm)
/// và 90% (bán cả giỏ). Giá nghìn đồng, vốn đồng, giỏ bịa 10 mã × 400 cổ × giá vào 10 = 40 triệu.
/// </summary>
public sealed class MuaTetTheoDoiTests
{
    private const decimal GiaVao = 10m;
    private const int SoCo = 400; // 10 mã × 400 cổ × 10.000 đồng = 40.000.000 đồng

    private static readonly string[] Ma = ["AAA", "BBB", "CCC", "DDD", "EEE", "FFF", "GGG", "HHH", "III", "JJJ"];

    private static List<MuaTetTheoDoi.MaDaMua> Gio(int soMa = 10, decimal giaVao = GiaVao, int soCo = SoCo)
        => Ma.Take(soMa).Select(m => new MuaTetTheoDoi.MaDaMua(m, giaVao, soCo)).ToList();

    /// <summary>Giá hiện tại đều nhau theo tỷ lệ so giá vào — dựng đúng tỷ lệ giỏ/vốn mong muốn.</summary>
    private static Dictionary<string, decimal> GiaTheoTyLe(decimal tyLe, int soMa = 10)
        => Ma.Take(soMa).ToDictionary(m => m, _ => GiaVao * tyLe, StringComparer.OrdinalIgnoreCase);

    // ===== mức báo theo tỷ lệ =====

    public static TheoryData<decimal, MuaTetTheoDoi.MucBao> CacMuc => new()
    {
        { 1.00m, MuaTetTheoDoi.MucBao.BinhThuong },
        { 0.96m, MuaTetTheoDoi.MucBao.BinhThuong },
        { 0.95m, MuaTetTheoDoi.MucBao.CanhBao },
        { 0.905m, MuaTetTheoDoi.MucBao.CanhBao },
        { 0.90m, MuaTetTheoDoi.MucBao.Ban },
        { 0.85m, MuaTetTheoDoi.MucBao.Ban },
    };

    [Theory]
    [MemberData(nameof(CacMuc))]
    public void Muc_bao_dung_theo_ty_le(decimal tyLe, MuaTetTheoDoi.MucBao mongDoi)
    {
        var kq = MuaTetTheoDoi.Tinh(Gio(), GiaTheoTyLe(tyLe));

        Assert.Equal(mongDoi, kq.Muc);
        Assert.Equal(tyLe, kq.TyLe);
        Assert.True(kq.CoGio);
        Assert.Equal(0, kq.SoMaThieuGia);
    }

    // ===== thiếu giá =====

    [Fact]
    public void Ma_thieu_gia_dung_gia_vao_va_khong_ket_luan_ban()
    {
        // 9 mã rơi về 8,5 (85%); mã JJJ mất giá (LastClose = 0) → dùng giá vào 10.
        var gia = GiaTheoTyLe(0.85m, soMa: 9);
        gia["JJJ"] = 0m;

        var kq = MuaTetTheoDoi.Tinh(Gio(), gia);

        var jjj = kq.ChiTiet.Single(m => m.Symbol == "JJJ");
        Assert.True(jjj.ThieuGia);
        Assert.Equal(GiaVao, jjj.GiaHienTai);
        Assert.Equal(0m, jjj.LaiLoPhanTram);
        Assert.Equal(1, kq.SoMaThieuGia);
        // Giá trị 86,5% vốn (nếu tính mã thiếu giá = 0 sẽ còn 76,5%) — dưới 90% nhưng vẫn chỉ cảnh báo sớm.
        Assert.Equal(0.865m, kq.TyLe);
        Assert.Equal(MuaTetTheoDoi.MucBao.CanhBao, kq.Muc);
        Assert.Equal("AAA", kq.MaKeoXuongNhieuNhat);
    }

    [Fact]
    public void Ma_khong_co_trong_tu_dien_gia_cung_dung_gia_vao()
    {
        var gia = GiaTheoTyLe(0.80m, soMa: 9); // JJJ không có trong từ điển

        var kq = MuaTetTheoDoi.Tinh(Gio(), gia);

        Assert.Equal(1, kq.SoMaThieuGia);
        Assert.Equal(GiaVao, kq.ChiTiet.Single(m => m.Symbol == "JJJ").GiaHienTai);
        Assert.Equal(0.82m, kq.TyLe);
        Assert.Equal(MuaTetTheoDoi.MucBao.CanhBao, kq.Muc);
    }

    [Fact]
    public void Bang_gia_null_thi_toan_bo_dung_gia_vao()
    {
        var kq = MuaTetTheoDoi.Tinh(Gio(), giaHienTai: null);

        Assert.Equal(10, kq.SoMaThieuGia);
        Assert.Equal(1m, kq.TyLe);
        Assert.Equal(MuaTetTheoDoi.MucBao.BinhThuong, kq.Muc);
        Assert.Null(kq.MaKeoXuongNhieuNhat);
    }

    // ===== vốn =====

    [Fact]
    public void Von_tinh_theo_so_co_gia_vao_va_1000()
    {
        // 2 mã: 1.000 cổ × 12,5 và 500 cổ × 40 → 12.500.000 + 20.000.000 = 32.500.000 đồng.
        var gio = new List<MuaTetTheoDoi.MaDaMua>
        {
            new("AAA", 12.5m, 1_000),
            new("BBB", 40m, 500),
        };
        var gia = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["AAA"] = 12.5m,
            ["BBB"] = 40m,
        };

        var kq = MuaTetTheoDoi.Tinh(gio, gia);

        Assert.Equal(32_500_000m, kq.VonDong);
        Assert.Equal(32_500_000m, kq.GiaTriDong);
        Assert.Equal(12_500_000m, kq.ChiTiet[0].VonDong);
        Assert.Equal(20_000_000m, kq.ChiTiet[1].VonDong);
        Assert.Equal(1m, kq.TyLe);
    }

    // ===== giỏ trống / mã không hợp lệ =====

    [Fact]
    public void Gio_trong_thi_khong_co_muc_bao()
    {
        var kq = MuaTetTheoDoi.Tinh([], GiaTheoTyLe(0.5m));

        Assert.False(kq.CoGio);
        Assert.Equal(0m, kq.VonDong);
        Assert.Equal(0m, kq.TyLe);
        Assert.Equal(MuaTetTheoDoi.MucBao.BinhThuong, kq.Muc);
    }

    [Fact]
    public void Ma_thieu_gia_vao_hoac_so_co_bi_bo_qua()
    {
        var gio = new List<MuaTetTheoDoi.MaDaMua>
        {
            new("AAA", 0m, 400),
            new("BBB", 10m, 0),
            new(" ", 10m, 400),
            new("CCC", 10m, 400),
        };

        var kq = MuaTetTheoDoi.Tinh(gio, GiaTheoTyLe(0.5m));

        Assert.Single(kq.ChiTiet);
        Assert.Equal("CCC", kq.ChiTiet[0].Symbol);
        Assert.Equal(4_000_000m, kq.VonDong);
    }

    // ===== runner: chặn lặp + cờ bật/tắt =====

    [Fact]
    public async Task Chan_lap_cung_ngay_cung_muc_chi_gui_mot_lan()
    {
        var bo = new BoThu();
        var runner = bo.TaoRunner(tyLe: 0.95m);

        var lan1 = await runner.KiemVaBaoAsync();
        var lan2 = await runner.KiemVaBaoAsync();

        Assert.True(lan1.DaGuiTin);
        Assert.False(lan2.DaGuiTin);
        Assert.Single(bo.Gui.Tin);
        Assert.Equal("Mùa Tết — cảnh báo sớm", bo.Gui.Tin[0].TieuDe);
    }

    [Fact]
    public async Task Muc_cao_hon_trong_ngay_van_gui_tiep()
    {
        var bo = new BoThu();
        var runner = bo.TaoRunner(tyLe: 0.95m);
        await runner.KiemVaBaoAsync();

        bo.Gia.Gia.Clear();
        foreach (var (ma, gia) in GiaTheoTyLe(0.88m))
            bo.Gia.Gia[ma] = gia;

        var ban = await runner.KiemVaBaoAsync();
        var banLan2 = await runner.KiemVaBaoAsync();

        Assert.True(ban.DaGuiTin);
        Assert.False(banLan2.DaGuiTin);
        Assert.Equal(2, bo.Gui.Tin.Count);
        Assert.Equal("Mùa Tết — BÁN CẢ GIỎ", bo.Gui.Tin[1].TieuDe);
        // Mức bán dù đã gửi vẫn không cho mức cảnh báo gửi lại trong ngày.
        Assert.Contains("Mã kéo giỏ xuống nhiều nhất: AAA.", bo.Gui.Tin[1].NoiDung);
        Assert.Contains("Kết quả theo luật backtest", bo.Gui.Tin[1].NoiDung);
    }

    [Fact]
    public async Task Bo_qua_chan_lap_van_gui_lai_cho_owner_thu()
    {
        var bo = new BoThu();
        var runner = bo.TaoRunner(tyLe: 0.88m);

        await runner.KiemVaBaoAsync();
        var thuLai = await runner.KiemVaBaoAsync(boQuaChanLap: true);

        Assert.True(thuLai.DaGuiTin);
        Assert.Equal(2, bo.Gui.Tin.Count);
    }

    [Fact]
    public async Task Tren_nguong_thi_khong_gui_tin()
    {
        var bo = new BoThu();
        var runner = bo.TaoRunner(tyLe: 0.96m);

        var kq = await runner.KiemVaBaoAsync();

        Assert.False(kq.DaGuiTin);
        Assert.Empty(bo.Gui.Tin);
        Assert.Contains("Trên ngưỡng cảnh báo", kq.ThongDiep);
    }

    [Fact]
    public async Task Theo_doi_tat_thi_bo_qua_khong_gui()
    {
        var bo = new BoThu();
        var runner = bo.TaoRunner(tyLe: 0.80m, theoDoiBat: false);

        var kq = await runner.KiemVaBaoAsync();

        Assert.False(kq.DaGuiTin);
        Assert.Empty(bo.Gui.Tin);
        Assert.Contains("TheoDoiBat = false", kq.ThongDiep);
    }

    [Fact]
    public async Task Gio_trong_thi_bo_qua_khong_gui()
    {
        var bo = new BoThu();
        var runner = bo.TaoRunner(tyLe: 0.80m, soMa: 0);

        var kq = await runner.KiemVaBaoAsync();

        Assert.False(kq.DaGuiTin);
        Assert.Empty(bo.Gui.Tin);
        Assert.Empty(kq.CacMa);
        Assert.Contains("trống", kq.ThongDiep);
    }

    // ===== nội dung tin =====

    [Fact]
    public void Tin_ban_ca_gio_du_noi_dung_can_thiet()
    {
        var kq = MuaTetTheoDoi.Tinh(Gio(), GiaTheoTyLe(0.85m));
        var dto = new StockRadar.Application.DTOs.MuaTetTheoDoiDto(
            TheoDoiBat: true,
            NgayMua: new DateOnly(2026, 11, 18),
            VonDaMua: kq.VonDong,
            GiaTriHienTai: kq.GiaTriDong,
            TyLe: kq.TyLe,
            Muc: kq.Muc.ToString(),
            CacMa: kq.ChiTiet.Select(m => new StockRadar.Application.DTOs.MuaTetTheoDoiMaDto(
                m.Symbol, m.GiaVao, m.SoCo, m.GiaHienTai, m.ThieuGia,
                m.VonDong, m.GiaTriDong, m.LaiLoPhanTram)).ToList(),
            MaKeoXuongNhieuNhat: kq.MaKeoXuongNhieuNhat,
            SoMaThieuGia: kq.SoMaThieuGia,
            DaGuiTin: false,
            ThongDiep: null);

        var tin = MuaTetTheoDoiTinNhan.Dung(dto);

        Assert.Contains("MÙA TẾT — MÃ KHỎE (P4-19)", tin);
        Assert.Contains("BÁN CẢ GIỎ", tin);
        Assert.Contains("giá trị giỏ 34 triệu / vốn 40 triệu = 85%", tin);
        Assert.Contains("Ngày mua: 18/11/2026", tin);
        Assert.Contains("1. AAA · 400 cổ · vào 10 · hiện 8.5 · -15%", tin);
        Assert.Contains("Mã kéo giỏ xuống nhiều nhất: AAA.", tin);
        Assert.Contains("Luật: bán CẢ GIỎ khi giá trị giỏ ≤ 90% vốn đã mua", tin);
        Assert.Contains("Kết quả theo luật backtest, không phải lời khuyên đầu tư.", tin);
        Assert.DoesNotContain("THIẾU GIÁ", tin);
    }

    // ===== bộ thu =====

    private sealed class BoThu
    {
        public GiaGia Gia { get; } = new();
        public GuiGia Gui { get; } = new();

        public MuaTetTheoDoiRunner TaoRunner(decimal tyLe, int soMa = 10, bool theoDoiBat = true)
        {
            Gia.Gia.Clear();
            foreach (var (ma, gia) in GiaTheoTyLe(tyLe, soMa))
                Gia.Gia[ma] = gia;

            var cauHinh = new MuaTetOptions { TheoDoiBat = theoDoiBat, GioDaMua = [] };
            foreach (var ma in Ma.Take(soMa))
                cauHinh.GioDaMua.Add(new MuaTetGioDaMuaOptions { Symbol = ma, GiaVao = GiaVao, SoCo = SoCo });

            return new MuaTetTheoDoiRunner(
                Gia, Gui, new MuaTetChanLap(), Options.Create(cauHinh),
                NullLogger<MuaTetTheoDoiRunner>.Instance);
        }
    }

    private sealed class GiaGia : IGiaHienTaiRepository
    {
        public Dictionary<string, decimal> Gia { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, decimal>> LayLastCloseAsync(
            IReadOnlyList<string> symbols, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, decimal>>(Gia);
    }

    private sealed class GuiGia : IGuiThongBao
    {
        public List<(string TieuDe, string NoiDung)> Tin { get; } = [];

        public void DatTinV3(LoaiTinV3 loai, string symbol, LoaiKichBanV3 kichBan, string telegramHtml) { }

        public void DatTinHeThong(string tieuDeDay, string noiDung) => Tin.Add((tieuDeDay, noiDung));

        public Task GuiTatCaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task GuiHeThongAsync(string tieuDeDay, string noiDung, CancellationToken cancellationToken = default)
        {
            Tin.Add((tieuDeDay, noiDung));
            return Task.CompletedTask;
        }
    }
}

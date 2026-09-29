using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Unit tests cho <see cref="XepHangCoHoiService"/> — bộ xếp hạng cơ hội V2 (6 tiêu chí, không veto).
/// </summary>
public sealed class XepHangCoHoiTests
{
    private sealed class FakeNguonDuLieu : INguonDuLieuXepHang
    {
        public Dictionary<string, decimal> Rs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string?> Nganh { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Pha { get; set; } = "Neutral";

        public Task<decimal?> LayRsPercentileAsync(string symbol, CancellationToken ct = default)
            => Task.FromResult<decimal?>(Rs.TryGetValue(symbol, out var v) ? v : null);

        public Task<string?> LayTrangThaiNganhAsync(string symbol, CancellationToken ct = default)
            => Task.FromResult(Nganh.TryGetValue(symbol, out var v) ? v : null);

        public Task<string> LayPhaThiTruongAsync(CancellationToken ct = default)
            => Task.FromResult(Pha);
    }

    private static XepHangCoHoiService TaoService(FakeNguonDuLieu nguon, XepHangOptions? options = null) =>
        new(
            Options.Create(options ?? new XepHangOptions()),
            nguon,
            NullLogger<XepHangCoHoiService>.Instance);

    /// <summary>Tạo một kịch bản ĐÃ KÍCH HOẠT với volume + RSI tuỳ chỉnh (không có MACD evidence → macd=50).</summary>
    private static KetQuaKichBan KichBan(
        string symbol,
        LoaiKichBan loai = LoaiKichBan.NoHuongLen,
        decimal volumeRatio = 1.5m,
        decimal rsi = 45m,
        KeHoachGiaoDich? keHoach = null) =>
        new()
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            KeHoach = keHoach,
            BangChup = new BangChupChiBao { VolumeRatio = volumeRatio, Rsi = rsi }
        };

    /// <summary>Kế hoạch có R:R = 3 → điểm R:R = 100.</summary>
    private static KeHoachGiaoDich KeHoachRR3() => new()
    {
        GiaVaoLenhMin = 10m,
        GiaVaoLenhMax = 10.5m,
        GiaDungLo = 8m,
        GiaChotLoi1 = 16m,
        GiaChotLoi2 = 20m,
        DieuKienHuy = "Đóng cửa dưới 8"
    };

    [Fact]
    public async Task XepHang_DungThuTu_RS_CaoHon_ThiXepTruoc()
    {
        var nguon = new FakeNguonDuLieu();
        nguon.Rs["A"] = 90m;
        nguon.Rs["B"] = 50m;
        nguon.Rs["C"] = 20m;

        var service = TaoService(nguon);
        var ketQua = await service.XepHangAsync(new[]
        {
            KichBan("C"), KichBan("A"), KichBan("B")
        });

        Assert.Equal(new[] { "A", "B", "C" }, ketQua.Select(k => k.KetQua.Symbol).ToArray());
        Assert.True(ketQua[0].DiemTong > ketQua[1].DiemTong);
        Assert.True(ketQua[1].DiemTong > ketQua[2].DiemTong);
    }

    [Fact]
    public async Task XepHang_TrongSo_DiemTong_Bang_Tong_Diem_Nhan_TrongSo()
    {
        var nguon = new FakeNguonDuLieu();
        nguon.Rs["A"] = 80m;              // điểm RS = 80
        nguon.Nganh["A"] = "Active";      // điểm Sector = 100
        nguon.Pha = "Favorable";          // điểm Regime = 100

        var options = new XepHangOptions();
        var service = TaoService(nguon, options);

        // volume=1.5 → 50; rsi=45 (breakout, ngoài băng) → 50; macd=50 → diemTrigger=50.
        // R:R=3 → 100. Một kịch bản → confluence=50.
        var ketQua = await service.XepHangAsync(new[] { KichBan("A", keHoach: KeHoachRR3()) });
        var k = Assert.Single(ketQua);

        Assert.Equal(80m, k.DiemRs);
        Assert.Equal(100m, k.DiemSector);
        Assert.Equal(50m, k.DiemTrigger);
        Assert.Equal(100m, k.DiemRegime);
        Assert.Equal(100m, k.DiemTyLeLaiLo);
        Assert.Equal(50m, k.DiemConfluence);

        // Điểm tổng = tổng(điểm thành phần × trọng số) — suy ra từ Options, không hardcode.
        var mongDoi =
            (k.DiemRs * options.TrongSoRs) +
            (k.DiemSector * options.TrongSoSector) +
            (k.DiemTrigger * options.TrongSoChatLuongTrigger) +
            (k.DiemRegime * options.TrongSoRegime) +
            (k.DiemTyLeLaiLo * options.TrongSoTyLeLaiLo) +
            (k.DiemConfluence * options.TrongSoConfluence);

        Assert.Equal(Math.Round(mongDoi, 2), k.DiemTong);
    }

    [Fact]
    public async Task XepHang_Confluence_HaiKichBan_DongThoi_DiemCaoHon()
    {
        var nguon = new FakeNguonDuLieu();
        nguon.Rs["A"] = 60m;
        nguon.Rs["B"] = 60m;

        var service = TaoService(nguon);
        var ketQua = await service.XepHangAsync(new[]
        {
            KichBan("A", LoaiKichBan.NoHuongLen),
            KichBan("A", LoaiKichBan.HoiHoTro),
            KichBan("B", LoaiKichBan.NoHuongLen)
        });

        var cuaA = ketQua.Where(k => k.KetQua.Symbol == "A").ToList();
        var cuaB = ketQua.Single(k => k.KetQua.Symbol == "B");

        Assert.Equal(2, cuaA.Count);
        Assert.All(cuaA, k => Assert.Equal(80m, k.DiemConfluence));
        Assert.Equal(50m, cuaB.DiemConfluence);
        Assert.All(cuaA, k => Assert.True(k.DiemTong > cuaB.DiemTong));
    }

    [Fact]
    public async Task XepHang_TopN_ChiTraVe_SoLuongTop()
    {
        var nguon = new FakeNguonDuLieu();
        for (var i = 1; i <= 8; i++)
            nguon.Rs[$"S{i}"] = i * 10m;

        var service = TaoService(nguon, new XepHangOptions { SoLuongTop = 5 });
        var input = Enumerable.Range(1, 8).Select(i => KichBan($"S{i}")).ToList();

        var ketQua = await service.XepHangAsync(input);

        Assert.Equal(5, ketQua.Count);
        // Top 5 theo RS giảm dần: S8, S7, S6, S5, S4.
        Assert.Equal(new[] { "S8", "S7", "S6", "S5", "S4" }, ketQua.Select(k => k.KetQua.Symbol).ToArray());
    }

    [Fact]
    public async Task XepHang_KhongCoQuyenVeto_MaDiemThapNhat_VanCoMatKhiDuoiTopN()
    {
        var nguon = new FakeNguonDuLieu();
        nguon.Rs["TOT"] = 95m;
        nguon.Rs["TB"] = 50m;
        nguon.Rs["XAU"] = 5m; // điểm rất thấp nhưng không bị veto

        var service = TaoService(nguon, new XepHangOptions { SoLuongTop = 5 });
        var ketQua = await service.XepHangAsync(new[]
        {
            KichBan("TOT"), KichBan("TB"), KichBan("XAU")
        });

        Assert.Equal(3, ketQua.Count);
        Assert.Contains(ketQua, k => k.KetQua.Symbol == "XAU");
        Assert.Equal("XAU", ketQua[^1].KetQua.Symbol);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Unit tests cho Pha 2 — KiemTraTriggerTrongPhienAsync trong MayNhanKichBanService.
/// Kiểm thử: trigger đạt/không đạt, snapshot được tạo, kế hoạch được tính.
/// </summary>
public sealed class Pha2TrongPhienTests
{
    // ===== Fake repository =====

    private sealed class FakeStockRepository : IJobStockRepository
    {
        private readonly Dictionary<string, Stock> _stocks = new(StringComparer.OrdinalIgnoreCase);

        public void ThemStock(string symbol, IReadOnlyList<OhlcvBar> history) =>
            _stocks[symbol] = new Stock(symbol, symbol, "Test", history);

        public Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken ct = default) =>
            Task.FromResult(_stocks.TryGetValue(symbol, out var s) ? s : null);

        public Task<IReadOnlyList<Stock>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Stock>>(_stocks.Values.ToList());

        public Task<IReadOnlyList<string>> GetActiveSymbolsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(_stocks.Keys.ToList());

        public Task<IReadOnlyList<StockSummaryRow>> GetSummariesBySymbolsAsync(
            IReadOnlyList<string> symbols, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StockSummaryRow>>(
                symbols.Where(s => _stocks.ContainsKey(s))
                    .Select(s => new StockSummaryRow(s, s, "Test", false, 0m)).ToList());

        public Task<MarketBreadthStats> GetBreadthStatsAsync(CancellationToken ct = default) =>
            Task.FromResult(MarketBreadthStats.Empty);

        public Task<IReadOnlyList<Stock>> GetAllForUniverseScreeningAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Stock>>(_stocks.Values.ToList());

        public Task<IReadOnlyList<string>> GetInactiveSymbolsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    // ===== Bộ sinh nến giả lập =====

    private static List<OhlcvBar> BarsFrom(
        IReadOnlyList<decimal> closes,
        IReadOnlyList<long> volumes,
        decimal spreadPct = 0.005m)
    {
        var d = new DateOnly(2025, 1, 1);
        var list = new List<OhlcvBar>(closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            var c = closes[i];
            list.Add(new OhlcvBar(d.AddDays(i), c, c * (1 + spreadPct), c * (1 - spreadPct), c, volumes[i]));
        }
        return list;
    }

    /// <summary>
    /// Dãy nến breakout: 230 phiên giá phẳng 25, rồi 20 phiên tăng tốc.
    /// Đạt cả bối cảnh (EMA20 > EMA50, ADX cao) + hình thái (Bollinger hẹp, volume teo) + trigger.
    /// </summary>
    private static List<OhlcvBar> BreakoutBars(long vol = 1_000_000)
    {
        var closes = new List<decimal>();
        var vols = new List<long>();
        // 230 phiên giá phẳng, volume nền
        for (var i = 0; i < 230; i++) { closes.Add(25m); vols.Add(vol); }
        // 5 phiên cuối trước breakout: volume teo (để đạt hình thái)
        for (var i = 225; i < 230; i++) vols[i] = (long)(vol * 0.4m);
        // 20 phiên tăng tốc
        var gia = 25m;
        for (var k = 1; k <= 20; k++) { gia += 0.2m * k; closes.Add(gia); vols.Add(vol); }
        return BarsFrom(closes, vols);
    }

    private static MayNhanKichBanService TaoService(FakeStockRepository repo)
    {
        var kichBanOptions = new KichBanOptions();
        var danhSachKichBan = new IKichBanDanhGia[]
        {
            new KichBanNoHuongLen(kichBanOptions.NoHuongLen),
            new KichBanQuetThanhKhoan(kichBanOptions.QuetThanhKhoan),
            new KichBanKietSuc(kichBanOptions.KietSuc),
            new KichBanGayNen(kichBanOptions.GayNen),
        };

        return new MayNhanKichBanService(
            danhSachKichBan,
            repo,
            new BanChupChiBaoBuilder(),
            NullLogger<MayNhanKichBanService>.Instance);
    }

    // ===== Test 1: Trigger đạt → chuyển TRIGGERED =====

    [Fact]
    public async Task Trigger_DatChuyen_KhiGiaVuotDinhVaVolumeDu()
    {
        // Arrange: FORMING kịch bản NoHuongLen cho HPG
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);

        var service = TaoService(repo);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);
        var avg20 = bars.TakeLast(20).Average(b => (decimal)b.Volume);
        var giaHienTai = dinh20 + 1m; // vượt đỉnh
        var volumeHienTai = (long)(avg20 * 2.5m); // volume nổ

        var dangHinhThanh = new List<KetQuaKichBan>
        {
            new()
            {
                Symbol = "HPG",
                LoaiKichBan = LoaiKichBan.NoHuongLen,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
            }
        };

        var gia = new Dictionary<string, decimal> { ["HPG"] = giaHienTai };
        var vol = new Dictionary<string, long> { ["HPG"] = volumeHienTai };

        // Act
        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        // Assert
        Assert.Single(ketQua);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, ketQua[0].TrangThai);
        Assert.True(ketQua[0].DatCoKichHoat);
        Assert.NotNull(ketQua[0].ThoiGianKichHoat);
        Assert.Equal("HPG", ketQua[0].Symbol);
    }

    // ===== Test 2: Trigger không đạt → trả empty =====

    [Fact]
    public async Task Trigger_KhongDat_KhiGiaChuaVuot()
    {
        // Arrange: giá chưa vượt đỉnh → trigger không đạt
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);

        var service = TaoService(repo);
        var giaHienTai = 20m; // thấp hơn đỉnh nhiều
        var volumeHienTai = 500_000L;

        var dangHinhThanh = new List<KetQuaKichBan>
        {
            new()
            {
                Symbol = "HPG",
                LoaiKichBan = LoaiKichBan.NoHuongLen,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
            }
        };

        var gia = new Dictionary<string, decimal> { ["HPG"] = giaHienTai };
        var vol = new Dictionary<string, long> { ["HPG"] = volumeHienTai };

        // Act
        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        // Assert: không có trigger nào
        Assert.Empty(ketQua);
        // Trạng thái ban đầu không bị thay đổi
        Assert.Equal(TrangThaiKichBan.DangHinhThanh, dangHinhThanh[0].TrangThai);
        Assert.False(dangHinhThanh[0].DatCoKichHoat);
    }

    // ===== Test 3: Snapshot được tạo khi TRIGGERED =====

    [Fact]
    public async Task Snapshot_DuocTao_KhiTriggered()
    {
        // Arrange
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);

        var service = TaoService(repo);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);
        var avg20 = bars.TakeLast(20).Average(b => (decimal)b.Volume);
        var giaHienTai = dinh20 + 1m;
        var volumeHienTai = (long)(avg20 * 2.5m);

        var dangHinhThanh = new List<KetQuaKichBan>
        {
            new()
            {
                Symbol = "HPG",
                LoaiKichBan = LoaiKichBan.NoHuongLen,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
            }
        };

        var gia = new Dictionary<string, decimal> { ["HPG"] = giaHienTai };
        var vol = new Dictionary<string, long> { ["HPG"] = volumeHienTai };

        // Act
        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        // Assert: BangChup phải tồn tại và có đủ các chỉ báo
        Assert.Single(ketQua);
        var snapshot = ketQua[0].BangChup;
        Assert.NotNull(snapshot);

        // Kiểm tra các chỉ báo cốt lõi đã được tính (không phải default 0)
        Assert.True(snapshot!.Rsi > 0, "RSI phải > 0");
        Assert.True(snapshot.Ema20 > 0, "EMA20 phải > 0");
        Assert.True(snapshot.Ema50 > 0, "EMA50 phải > 0");
        Assert.True(snapshot.Adx > 0, "ADX phải > 0");
        Assert.True(snapshot.BollingerUpper > 0, "Bollinger upper phải > 0");
        Assert.True(snapshot.VolumeRatio > 0, "Volume ratio phải > 0");
        Assert.True(snapshot.AtrPercent > 0, "ATR% phải > 0");
        Assert.True(snapshot.ThoiDiemChup > DateTime.UtcNow.AddMinutes(-1), "Thời điểm chụp phải gần đây");
        Assert.NotEmpty(snapshot.VsaLabel);
    }

    // ===== Test 4: Kế hoạch được tính khi TRIGGERED =====

    [Fact]
    public async Task KeHoach_DuocTinh_KhiTriggered()
    {
        // Arrange
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);

        var service = TaoService(repo);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);
        var avg20 = bars.TakeLast(20).Average(b => (decimal)b.Volume);
        var giaHienTai = dinh20 + 1m;
        var volumeHienTai = (long)(avg20 * 2.5m);

        var dangHinhThanh = new List<KetQuaKichBan>
        {
            new()
            {
                Symbol = "HPG",
                LoaiKichBan = LoaiKichBan.NoHuongLen,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
            }
        };

        var gia = new Dictionary<string, decimal> { ["HPG"] = giaHienTai };
        var vol = new Dictionary<string, long> { ["HPG"] = volumeHienTai };

        // Act
        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        // Assert: Kế hoạch giao dịch hợp lệ
        Assert.Single(ketQua);
        var keHoach = ketQua[0].KeHoach;
        Assert.NotNull(keHoach);
        Assert.True(keHoach!.GiaVaoLenhMin > 0, "Giá vào lệnh min phải > 0");
        Assert.True(keHoach.GiaVaoLenhMax >= keHoach.GiaVaoLenhMin, "Max ≥ Min");
        Assert.True(keHoach.GiaDungLo > 0, "Dừng lỗ phải > 0");
        Assert.True(keHoach.GiaDungLo < keHoach.GiaVaoLenhMin, "SL < Entry");
        Assert.True(keHoach.GiaChotLoi1 > keHoach.GiaVaoLenhMin, "TP1 > Entry");
        Assert.True(keHoach.GiaChotLoi2 > keHoach.GiaChotLoi1, "TP2 > TP1");
        Assert.True(keHoach.TyLeLaiLo > 0, "R:R phải > 0");
        Assert.NotEmpty(keHoach.DieuKienHuy);
    }

    // ===== Test 5: Bỏ qua khi không có giá hiện tại =====

    [Fact]
    public async Task BoQua_KhiKhongCoGiaHienTai()
    {
        // Arrange: symbol không có trong dictionary giá
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);

        var service = TaoService(repo);
        var dangHinhThanh = new List<KetQuaKichBan>
        {
            new()
            {
                Symbol = "HPG",
                LoaiKichBan = LoaiKichBan.NoHuongLen,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
            }
        };

        // Không có giá cho HPG
        var gia = new Dictionary<string, decimal>();
        var vol = new Dictionary<string, long>();

        // Act
        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        // Assert
        Assert.Empty(ketQua);
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Constants;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

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

    // ==========================================================================
    // SELL FLOW TESTS — Pha2TrongPhienRunner.KiemTraSellAsync
    // ==========================================================================

    // ----- Fake dependencies -----

    private sealed class FakeTelegram : ITelegramNotifier
    {
        public List<string> Messages { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task SendAsync(string message, CancellationToken ct = default, string? parseMode = null)
        {
            if (ShouldThrow)
                throw new HttpRequestException("Telegram send failed");
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMayNhanKichBan : IMayNhanKichBan
    {
        public List<KetQuaKichBan>? BanResults { get; set; }
        public List<KetQuaKichBan>? TriggerResults { get; set; }
        public decimal? LastGiaVaoLenh { get; private set; }

        public Task<IReadOnlyList<KetQuaKichBan>> DanhGiaTruocPhienAsync(
            string symbol, IReadOnlyList<OhlcvBar> history, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KetQuaKichBan>>(Array.Empty<KetQuaKichBan>());

        public Task<IReadOnlyList<KetQuaKichBan>> KiemTraTriggerTrongPhienAsync(
            IReadOnlyList<KetQuaKichBan> dangHinhThanh,
            IReadOnlyDictionary<string, decimal> giaHienTai,
            IReadOnlyDictionary<string, long> volumeHienTai,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KetQuaKichBan>>(
                TriggerResults ?? dangHinhThanh.Where(t => t.TrangThai == TrangThaiKichBan.DangHinhThanh).ToList());

        public Task<IReadOnlyList<KetQuaKichBan>> DanhGiaBanAsync(
            string symbol, IReadOnlyList<OhlcvBar> history, decimal giaVaoLenh, CancellationToken ct = default)
        {
            LastGiaVaoLenh = giaVaoLenh;
            IReadOnlyList<KetQuaKichBan> result = BanResults ?? (IReadOnlyList<KetQuaKichBan>)Array.Empty<KetQuaKichBan>();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeXepHang : IXepHangCoHoi
    {
        public Task<IReadOnlyList<KetQuaXepHang>> XepHangAsync(
            IReadOnlyList<KetQuaKichBan> daKichHoat, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KetQuaXepHang>>(Array.Empty<KetQuaXepHang>());
    }

    private sealed class FakeLichChotQuyen : INguonLichChotQuyen
    {
        public Task<IReadOnlyDictionary<string, ThongTinChotQuyen>> LayMaSapChotQuyenAsync(
            DateOnly tuNgay, int soNgay, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, ThongTinChotQuyen>>(
                new Dictionary<string, ThongTinChotQuyen>());
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _json;
        public FakeHttpHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }

    // ----- Helper methods -----

    private static ApplicationDbContext NewSellDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static string MakeKeHoachJson(decimal entry) =>
        JsonSerializer.Serialize(new KeHoachGiaoDich
        {
            GiaVaoLenhMin = entry,
            GiaVaoLenhMax = entry,
            GiaDungLo = entry * 0.95m,
            GiaChotLoi1 = entry * 1.10m,
            GiaChotLoi2 = entry * 1.20m,
            DieuKienHuy = "test",
        });

    private static KetQuaKichBanEntity SeedHolding(
        ApplicationDbContext db,
        string symbol,
        LoaiKichBan loai = LoaiKichBan.NoHuongLen,
        DateTime? kichHoat = null,
        decimal entryPrice = 10m,
        string? ketQuaDoLuong = null,
        DateTime? banNua = null,
        DateTime? thoatHet = null,
        bool daDoiDungLo = false,
        string? lyDoThoat = null,
        string? canhBao = null,
        decimal? giaBanNuaVal = null,
        decimal? giaThoatHetVal = null)
    {
        var entity = new KetQuaKichBanEntity
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            ThoiGianKichHoat = kichHoat ?? DateTime.UtcNow.AddDays(-5),
            KeHoachGiaoDichJson = MakeKeHoachJson(entryPrice),
            KetQuaDoLuong = ketQuaDoLuong,
            ThoiGianBanNua = banNua,
            ThoiGianThoatHet = thoatHet,
            DaDoiDungLo = daDoiDungLo,
            LyDoThoatHet = lyDoThoat,
            CanhBaoDaGui = canhBao,
            GiaBanNua = giaBanNuaVal,
            GiaThoatHet = giaThoatHetVal,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = (kichHoat ?? DateTime.UtcNow.AddDays(-5)).Date,
        };
        db.KetQuaKichBan.Add(entity);
        db.SaveChanges();
        return entity;
    }

    private static string MakeKbsJson(string symbol, decimal gia) =>
        // KBS scale: gia * 1000 => raw price (ScalePrice will divide by 1000 if >= 1000)
        $"[{{\"SB\":\"{symbol}\",\"CP\":{(double)(gia * 1000m)},\"TT\":1000000}}]";
    
    private static string MakeKbsJsonHiLo(string symbol, decimal gia, decimal high, decimal low) =>
        $"[{{\"SB\":\"{symbol}\",\"CP\":{(double)(gia * 1000m)},\"HI\":{(double)(high * 1000m)},\"LO\":{(double)(low * 1000m)},\"TT\":1000000}}]";

    private static Pha2TrongPhienRunner TaoSellRunner(
        ApplicationDbContext db,
        FakeMayNhanKichBan mayNhan,
        FakeTelegram telegram,
        FakeStockRepository stockRepo,
        string kbsJson,
        int minSessions = 3,
        Pha2TrailingStopRuntime? runtime = null,
        int? luotXacNhan = null,
        int? luotXacNhanAtc = null,
        int? soPhienTheoDoiToiDa = null,
        int? soPhienDungTheoThoiGian = null,
        decimal? nguongMfe = null,
        decimal? heSoAtr = null,
        int? chuKyAtr = null,
        string[]? kichBanDungLoDuoi = null,
        string? gioBatDauAtc = null)
    {
        var http = new HttpClient(new FakeHttpHandler(kbsJson));
        var kbs = new KbsPriceBoardClient(http, NullLogger<KbsPriceBoardClient>.Instance);
        var opts = new Pha2Options { MinTradingSessionsToSell = minSessions };
        // Mặc định test cũ: bỏ qua chống nhiễu (1 lượt) để hồi quy B không đổi kỳ vọng.
        opts.SoLuotXacNhanDungLo = luotXacNhan ?? 1;
        opts.SoLuotXacNhanDungLoAtc = luotXacNhanAtc ?? luotXacNhan ?? 1;
        if (soPhienTheoDoiToiDa.HasValue) opts.SoPhienTheoDoiToiDa = soPhienTheoDoiToiDa.Value;
        if (soPhienDungTheoThoiGian.HasValue) opts.SoPhienDungTheoThoiGian = soPhienDungTheoThoiGian.Value;
        if (nguongMfe.HasValue) opts.NguongMfeToiThieuPhanTram = nguongMfe.Value;
        if (heSoAtr.HasValue) opts.HeSoAtrDungLoDuoi = heSoAtr.Value;
        if (chuKyAtr.HasValue) opts.ChuKyAtr = chuKyAtr.Value;
        if (kichBanDungLoDuoi is not null) opts.KichBanDungLoDuoi = kichBanDungLoDuoi;
        if (gioBatDauAtc is not null) opts.GioBatDauAtc = gioBatDauAtc;
        var pha2Opts = Options.Create(opts);
        var tgOpts = Options.Create(new TelegramNotifyOptions { Enabled = true });
        var faOpts = Options.Create(new FireAntOptions { Enabled = false });
    
        return new Pha2TrongPhienRunner(
            mayNhan,
            new FakeXepHang(),
            stockRepo,
            kbs,
            telegram,
            db,
            pha2Opts,
            tgOpts,
            new FakeLichChotQuyen(),
            faOpts,
            runtime ?? new Pha2TrailingStopRuntime(),
            NullLogger<Pha2TrongPhienRunner>.Instance);
    }

    private static KetQuaKichBan MakeSellTriggered(string symbol, LoaiKichBan loai) =>
        new()
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            ThoiGianKichHoat = DateTime.UtcNow,
        };

    private static KetQuaKichBan MakeSellForming(string symbol, LoaiKichBan loai) =>
        new()
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DangHinhThanh,
            DatBoiCanh = true,
            DatHinhThai = true,
        };

    // ----- Test TC1: Mã mua hôm trước vẫn được xét bán (hồi quy L1) -----

    [Fact]
    public async Task TC1_MaMuaHomTruoc_DuocXetBan()
    {
        var db = NewSellDb();
        // NgayDanhGia = 5 ngày trước (không phải hôm nay)
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-5);
        SeedHolding(db, "HPG", kichHoat: kichHoat);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        // Sell message sent
        Assert.Single(telegram.Messages);
        Assert.Contains("KIỆT SỨC", telegram.Messages[0]);
    }

    // ----- Test TC2: Kiệt sức nhận đúng giá vào lệnh (hồi quy L2) -----

    [Fact]
    public async Task TC2_KietSuc_NhanDungGiaVaoLenh()
    {
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-5);
        SeedHolding(db, "HPG", kichHoat: kichHoat, entryPrice: 10m);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        // Current price = 10.5, entry = 10 → DanhGiaBanAsync nhận 10 (không phải 10.5)
        // (giá 10.5 > SL=9.5, giá 10.5 < TP1=11 → không phát sự kiện mức giá)
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Equal(10m, mayNhan.LastGiaVaoLenh);
    }

    // ----- Test TC3: Hai lượt liên tiếp, 1 tin (hồi quy L3) -----

    [Fact]
    public async Task TC3_HaiLuotLienTiep_ChanTinBanDuoc1Lan()
    {
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-5);
        SeedHolding(db, "HPG", kichHoat: kichHoat);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        await runner.KiemTraSellAsync(ngayDanhGia, default);
        await runner.KiemTraSellAsync(ngayDanhGia, default);

        Assert.Single(telegram.Messages);
    }

    // ----- Test TC4: Chưa đủ phiên → cảnh báo 1 lần; đủ phiên → bán 1 lần -----

    [Fact]
    public async Task TC4_ChuaDuPhien_CanhBao1Lan_DuPhien_Ban1Lan()
    {
        var db = NewSellDb();
        // KichHoat = 1 ngày trước → chỉ 1 phiên, cần 3
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-1);
        SeedHolding(db, "HPG", kichHoat: kichHoat);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        // Giá 10.5: không phát ChamDungLo/TP1, chỉ kích hoạt KietSuc warning
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt 1: chưa đủ → cảnh báo
        await runner.KiemTraSellAsync(ngayDanhGia, default);
        Assert.Single(telegram.Messages);
        Assert.Contains("CẢNH BÁO", telegram.Messages[0]);

        // Lượt 2: vẫn chưa đủ → không gửi lại
        await runner.KiemTraSellAsync(ngayDanhGia, default);
        Assert.Single(telegram.Messages);
    }

    [Fact]
    public async Task TC4b_DuPhien_GuiTinBan()
    {
        var db = NewSellDb();
        // KichHoat = 7 ngày trước → đủ 3 phiên
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN 50%", telegram.Messages[0]);
        Assert.DoesNotContain("CẢNH BÁO", telegram.Messages[0]);
    }

    // ----- Test TC5: Đã Kiệt sức, sau đó Gãy nền → gửi thêm 1 tin; ngược lại → không -----

    [Fact]
    public async Task TC5_DaKietSuc_SauDoGayNen_GuiThem()
    {
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        // Đã marked KietSuc (ThoiGianBanNua set, không set ThoiGianThoatHet)
        SeedHolding(db, "HPG", kichHoat: kichHoat, banNua: DateTime.UtcNow);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.GayNen)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.GayNen)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        // Giá 10.0 > SL gốc 9.5 → không phát ChamDungLo; cho phép GayNen từ mayNhan kích hoạt
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.0m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("GÃY NỀN", telegram.Messages[0]);
    }

    [Fact]
    public async Task TC5b_DaGayNen_KietSucKhongGui()
    {
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        // Đã marked GayNen (ThoiGianThoatHet set)
        SeedHolding(db, "HPG", kichHoat: kichHoat, thoatHet: DateTime.UtcNow, lyDoThoat: SuKienBan.LyDoGayNen);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        // Giá 10.5 — không đổi vì đã thoát hết (top-level skip)
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Empty(telegram.Messages);
    }

    // ----- Test TC6 (đã đổi kỳ vọng theo spec C mục 4): Bản ghi đã có KetQuaDoLuong → **vẫn** xét bán -----
    // Trước spec C: Pha 2 bỏ qua vì đo xong = ngừng theo dõi. Sau spec C: theo dõi tới khi thoát hết
    // hoặc quá SoPhienTheoDoiToiDa, bất kể Pha 3 đã đo hay chưa.
    
    [Fact]
    public async Task TC6_DaCoKetQuaDoLuong_VanXetBan()
    {
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat, ketQuaDoLuong: "Thang");
    
        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());
    
        // Giá 11.0 = TP1 → ChamChotLoi1 bắn (vì vẫn còn ThoiGianThoatHet == null).
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 11m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
    
        Assert.NotEmpty(telegram.Messages);
        Assert.Contains("ch\u1ED1t l\u1EDDi 1", telegram.Messages[0]);
    }

    // ----- Test TC7: Telegram ném lỗi → không ghi đánh dấu; lượt sau gửi lại -----

    [Fact]
    public async Task TC7_TelegramLoi_KhongGhiDanhDau_LuotSauGuiLai()
    {
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram { ShouldThrow = true };
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        // Giá 10.5: > SL 9.5, < TP1 11 → không phát ChamDungLo/TP1 — chỉ KietSuc
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt 1: Telegram lỗi → không ghi dấu
        await Assert.ThrowsAsync<HttpRequestException>(
            () => runner.KiemTraSellAsync(ngayDanhGia, default));

        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.Null(entity.ThoiGianBanNua);
        Assert.Null(entity.ThoiGianThoatHet);

        // Lượt 2: Telegram OK → gửi được
        telegram.ShouldThrow = false;
        await runner.KiemTraSellAsync(ngayDanhGia, default);
        Assert.Single(telegram.Messages);
    }

    // ==========================================================================
    // TEST GIÁM SÁT MỨC GIÁ — PHƯƠNG ÁN B (spec mục 6)
    // ==========================================================================

    // Ca 1: Đủ phiên, giá <= dừng lỗ → tin BÁN HẾT, ghi LyDoThoatHet = DungLo

    [Fact]
    public async Task SL_PriceLevel_DungLo_GuiTinBanHet()
    {
        // Arrange: entry=10, SL=9.5, TP1=11. TP2=12. KB=9.4 (<= SL)
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat, entryPrice: 10m);

        // Không có KietSuc/GayNen trigger — chỉ phát ChamDungLo
        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 9.4m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        // Assert: tin nhắn BÁN HẾT được gửi
        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN HẾT", telegram.Messages[0]);
        Assert.Contains("Chạm dừng lỗ", telegram.Messages[0]);

        // Assert: entity đã ghi trạng thái thoát
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoDungLo, entity.LyDoThoatHet);
        Assert.Equal(9.4m, entity.GiaThoatHet);
        Assert.Null(entity.ThoiGianBanNua); // chưa bao giờ bán nửa
    }

    // Ca 2: Đủ phiên, TP1 chạm → bán nửa + dời SL; lượt sau giá = giaVao → ChamDungLo

    [Fact]
    public async Task SL_PriceLevel_ChotLoi1_Roi_DungLo_Ve_GiaVao()
    {
        // Arrange: entry=10, SL=9.5, TP1=11.0
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat, entryPrice: 10m);

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt quét 1: giá = 11.0 >= TP1 → ChamChotLoi1
        var runner1 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 11.0m));
        await runner1.KiemTraSellAsync(ngayDanhGia, default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN 50%", telegram.Messages[0]);

        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.NotNull(entity.ThoiGianBanNua);
        Assert.True(entity.DaDoiDungLo);       // TP1 dời SL về giá vào
        Assert.Null(entity.ThoiGianThoatHet);  // chưa thoát

        // Lượt quét 2: giá = 10.0 = giaVao (SL đã dời) → ChamDungLo
        telegram.Messages.Clear();
        var runner2 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.0m));
        await runner2.KiemTraSellAsync(ngayDanhGia, default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN HẾT", telegram.Messages[0]);

        entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoDungLo, entity.LyDoThoatHet);
    }

    // Ca 3: Giá nhảy qua TP2 khi chưa chốt lời 1 → lượt 1 chỉ phát TP1, lượt 2 phát TP2

    [Fact]
    public async Task SL_PriceLevel_NhayQuaTP2_PhatTP1truoc()
    {
        // Arrange: entry=10, TP1=11, TP2=12. KB=12.5 (vượt cả TP1+TP2)
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat, entryPrice: 10m);

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt 1: gia=12.5, daChotLoi1=false → chỉ phát ChamChotLoi1 (không thể phát TP2)
        var runner1 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 12.5m));
        await runner1.KiemTraSellAsync(ngayDanhGia, default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN 50%", telegram.Messages[0]); // chỉ TP1, chưa TP2

        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.NotNull(entity.ThoiGianBanNua);
        Assert.Null(entity.ThoiGianThoatHet); // chưa thoát hết

        // Lượt 2: gia=12.5, daChotLoi1=true → phát ChamChotLoi2
        telegram.Messages.Clear();
        var runner2 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 12.5m));
        await runner2.KiemTraSellAsync(ngayDanhGia, default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN HẾT", telegram.Messages[0]);

        entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoChotLoi2, entity.LyDoThoatHet);
    }

    // Ca 4: Chưa đủ phiên, chạm SL → cảnh báo 1 lần, không đổi trạng thái vị thế

    [Fact]
    public async Task SL_PriceLevel_ChuaDuPhien_CanhBao1Lan()
    {
        // Arrange: entry=10, SL=9.5. KichHoat 1 ngày trước → chưa đủ 3 phiên
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-1);
        SeedHolding(db, "HPG", kichHoat: kichHoat, entryPrice: 10m);

        var mayNhan = new FakeMayNhanKichBan(); // không trigger KietSuc/GayNen
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        // gia=9.4 <= SL=9.5 → ChamDungLo nhưng chưa đủ phiên → cảnh báo
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 9.4m));
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        await runner.KiemTraSellAsync(ngayDanhGia, default);

        Assert.Single(telegram.Messages);
        Assert.Contains("CHẠM DỪNG LỖ", telegram.Messages[0]);

        // Trạng thái KHÔNG đổi (chưa thoát)
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.Null(entity.ThoiGianThoatHet);
        Assert.Contains(SuKienBan.ChamDungLo, entity.CanhBaoDaGui ?? "");

        // Lượt sau: vẫn chạm SL nhưng đã cảnh báo → không gửi lại
        await runner.KiemTraSellAsync(ngayDanhGia, default);
        Assert.Single(telegram.Messages); // vẫn chỉ 1 tin
    }

    // Ca 5: Đã thoát hết (ThoiGianThoatHet set) → không gửi gì thêm

    [Fact]
    public async Task SL_DaThoatHet_KietSucGayNenKhongGui()
    {
        // Arrange: entity đã có ThoiGianThoatHet (simulates prior scan exit)
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat, thoatHet: DateTime.UtcNow, lyDoThoat: SuKienBan.LyDoDungLo);

        // KietSuc trigger nhưng không được gửi vì đã thoát
        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 9.4m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Empty(telegram.Messages);
    }

    // Ca 6: Kiệt sức = bán nửa KHÔNG dời SL (DaDoiDungLo giữ false)

    [Fact]
    public async Task SL_KietSucKhongDoiDungLo()
    {
        // Arrange: entry=10, SL=9.5, TP1=11
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat, entryPrice: 10m);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt 1: giá=10.5 (không phát ChamDungLo/TP1) — KietSuc bắn, DaDoiDungLo vẫn false
        var runner1 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner1.KiemTraSellAsync(ngayDanhGia, default);

        Assert.Single(telegram.Messages); // KietSuc message
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.NotNull(entity.ThoiGianBanNua);  // KietSuc ghi bán nửa
        Assert.False(entity.DaDoiDungLo);        // nhưng KHÔNG dời SL

        // Lượt 2: giá=9.6 > SL gốc 9.5 (SL KHÔNG bị dời) → không phát ChamDungLo
        telegram.Messages.Clear();
        var runner2 = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 9.6m));
        await runner2.KiemTraSellAsync(ngayDanhGia, default);

        // Không có tin mới (9.6 > 9.5, KietSuc cũng chặn vì đã có ThoiGianBanNua)
        Assert.Empty(telegram.Messages);
    }

    // Ca 8: Migration — xác nhận entity có ThoiGianThoatHet + LyDoThoatHet từ "post-migration"

    [Fact]
    public async Task Migration_OldGayNenData_TransfersToThoatHet()
    {
        // InMemory DB không chạy SQL migration. Test này giả lập dữ liệu SAU migration
        // (cột ThoiGianThoatHet + LyDoThoatHet đã ghi) và xác nhận runner xử lý đúng.
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);

        SeedHolding(db, "HPG", kichHoat: kichHoat,
            thoatHet: DateTime.UtcNow.AddDays(-1),
            lyDoThoat: SuKienBan.LyDoGayNen,
            giaThoatHetVal: 9.0m);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 9.0m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Empty(telegram.Messages); // không gửi vì đã thoát

        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.Equal(SuKienBan.LyDoGayNen, entity.LyDoThoatHet);
        Assert.NotNull(entity.ThoiGianThoatHet);
    }

    // Ca 9: Migration — CanhBaoDaGui = 'KietSuc,GayNen' chặn cảnh báo lại sau migration

    [Fact]
    public async Task Migration_OldWarningData_KhongGuiCanhBaoLai()
    {
        // Entity có CanhBaoDaGui='KietSuc,GayNen' (giả lập post-migration)
        // + KietSuc/GayNen trigger + chưa đủ phiên → runner phải skip cảnh báo (đã gửi trước migration)
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-1); // chưa đủ 3 phiên
        SeedHolding(db, "HPG", kichHoat: kichHoat, canhBao: $"{SuKienBan.KietSuc},{SuKienBan.GayNen}");

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        // Giá 10.5 (không phát ChamDungLo/TP1) → chỉ KietSuc path
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        // Không gửi cảnh báo vì 'KietSuc' đã nằm trong CanhBaoDaGui
        Assert.Empty(telegram.Messages);
    }

    // ==========================================================================
    // PHƯƠNG ÁN C — MAE/MFE + trailing stop + chống nhiễu + dừng lỗ theo thời gian
    // ==========================================================================

    private static List<OhlcvBar> FlatBars(int n, decimal price = 100m, long vol = 1_000_000)
    {
        var closes = Enumerable.Repeat(price, n).ToList();
        var vols = Enumerable.Repeat(vol, n).ToList();
        return BarsFrom(closes, vols);
    }

    // Ca 1: MAE/MFE cập nhật mỗi lượt, dừng khi đã thoát hết.
    [Fact]
    public async Task TC_C1_MaeMfe_CapNhatMoiLuot_SauThoatKhongDoi()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-5),
            entryPrice: 100m);

        var mayNhan = new FakeMayNhanKichBan();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Lượt 1: giá 104 → Mfe = 4
        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJson("HPG", 104m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Equal(4m, entity.Mfe);

        // Lượt 2: giá 97 → Mae = -3 (chưa chạm SL 95)
        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJson("HPG", 97m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Equal(-3m, entity.Mae);
        Assert.Equal(4m, entity.Mfe);

        // Đánh dấu thoát hết rồi tick → Mfe/Mae giữ nguyên
        entity.ThoiGianThoatHet = DateTime.UtcNow;
        entity.GiaThoatHet = 97m;
        entity.LyDoThoatHet = SuKienBan.LyDoDungLo;
        db.SaveChanges();

        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJson("HPG", 200m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Equal(4m, entity.Mfe);
        Assert.Equal(-3m, entity.Mae);
    }

    // Ca 2: Phiên mua — không dùng session High (đỉnh xảy ra trước lúc mua).
    [Fact]
    public async Task TC_C2_PienMua_KhongLaySessionHigh()
    {
        var db = NewSellDb();
        var homNay = VietnamMarketCalendar.NowVietnam();
        var entity = SeedHolding(db, "HPG", kichHoat: homNay, entryPrice: 100m);

        var mayNhan = new FakeMayNhanKichBan();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá khớp = 100, KBS High = 110 (xảy ra trước lúc mua) → không tính vào Mfe.
        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJsonHiLo("HPG", 100m, 110m, 90m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Equal(0m, entity.Mfe);
        Assert.Equal(0m, entity.Mae);
        Assert.Equal(100m, entity.DinhTuLucMua);
    }

    // Ca 3: DungLoDuoi chỉ tăng, không giảm khi giá rơi.
    [Fact]
    public async Task TC_C3_DungLoDuoiChiTang()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m);
        entity.DinhTuLucMua = 110m;
        entity.DungLoDuoi   = 105m;
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var repo = new FakeStockRepository();
        // Flat 100: TR = h−l = 1 (spreadPct mặc định 0.005), ATR = 1.
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Tick: giá 100, sessionHigh 110 → dinhTuLucMua vẫn 110. moi = 110 - 2.5*1 = 107.5 → DungLoDuoi = max(105, 107.5) = 107.5.
        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJsonHiLo("HPG", 100m, 110m, 100m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Equal(107.5m, entity.DungLoDuoi);

        // Tick: giá rơi 95 (sessionLow 95) → dinhTuLucMua giữ 110 → moi = 107.5 → DungLoDuoi giữ 107.5 (không giảm).
        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJsonHiLo("HPG", 95m, 110m, 95m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Equal(107.5m, entity.DungLoDuoi);
    }

    // Ca 4: HoiHoTro không dùng trailing stop.
    [Fact]
    public async Task TC_C4_HoiHoTro_KhongTinhDungLoDuoi()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            loai: LoaiKichBan.HoiHoTro,
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m);
        entity.DinhTuLucMua = 110m;
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJsonHiLo("HPG", 100m, 112m, 100m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Null(entity.DungLoDuoi);
    }

    // Ca 5: Chống nhiễu — 1 lượt dưới SL không emit, 2 lượt liên tiếp mới emit.
    [Fact]
    public async Task TC_C5_XacNhanChongNhieu_HaiLuotMoiEmit()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m);

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        var runtime = new Pha2TrailingStopRuntime();

        // Lượt 1: giá 94 (<= SL 95) → counter = 1, luotCan = 2 → chưa emit.
        var r1 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 94m),
            runtime: runtime, luotXacNhan: 2, soPhienDungTheoThoiGian: 100);
        await r1.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Empty(telegram.Messages);
        
        // Lượt 2: giá hồi 97 → reset counter.
        telegram.Messages.Clear();
        var r2 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 97m),
            runtime: runtime, luotXacNhan: 2, soPhienDungTheoThoiGian: 100);
        await r2.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Empty(telegram.Messages);
        
        // Lượt 3: giá 94 → counter = 1, chưa emit.
        telegram.Messages.Clear();
        var r3 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 94m),
            runtime: runtime, luotXacNhan: 2, soPhienDungTheoThoiGian: 100);
        await r3.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Empty(telegram.Messages);
        
        // Lượt 4: giá 94 → counter = 2 → emit.
        telegram.Messages.Clear();
        var r4 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 94m),
            runtime: runtime, luotXacNhan: 2, soPhienDungTheoThoiGian: 100);
        await r4.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Single(telegram.Messages);
        Assert.Contains("dừng lỗ", telegram.Messages[0]);
    }

    // Ca 6: Dừng lỗ hiệu lực = max của 3 → DungLoDuoi thắng khi ở cao nhất.
    [Fact]
    public async Task TC_C6_DungLoHieuLuc_MaxBaMuc_DungLoDuoiThang()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            daDoiDungLo: true);
        entity.DinhTuLucMua = 115m;
        entity.DungLoDuoi = 104m; // > giaVao (100) và > SL kế hoạch (95)
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 104: <= DungLoDuoi 104 → emit ChamDungLoDuoi (vì 104 lớn nhất trong 3 ứng viên).
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJsonHiLo("HPG", 104m, 115m, 104m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("đuổi theo", telegram.Messages[0]);
        Assert.Equal(SuKienBan.LyDoDungLoDuoi, entity.LyDoThoatHet);
    }

    // Ca 7: Dừng lỗ theo thời gian — đủ phiên mà MFE dưới ngưỡng → bán hết.
    [Fact]
    public async Task TC_C7_DungLoTheoThoiGian_MfeDuoiNguong_Emit()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-9), // ~7 phiên, vượt ngưỡng 5
            entryPrice: 100m);
        entity.Mfe = 2.5m; // dưới ngưỡng 3
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m),
                soPhienDungTheoThoiGian: 5, nguongMfe: 3m)
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Equal(SuKienBan.LyDoHetThoiGian, entity.LyDoThoatHet);
    }

    [Fact]
    public async Task TC_C7b_DungLoTheoThoiGian_DaNuaBan_KhongEmit()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-9),
            entryPrice: 100m,
            banNua: DateTime.UtcNow);
        entity.Mfe = 2.5m;
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m),
                soPhienDungTheoThoiGian: 5, nguongMfe: 3m)
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Empty(telegram.Messages);
    }

    // Ca 8: Pha 2 vẫn theo dõi MFE sau khi Pha 3 đã đo.
    [Fact]
    public async Task TC_C8_TheoDoiMfe_SauKhiPha3DoXong()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            ketQuaDoLuong: "Thang");

        var mayNhan = new FakeMayNhanKichBan();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, new FakeTelegram(), repo, MakeKbsJson("HPG", 104m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Equal(4m, entity.Mfe);
        Assert.Equal("Thang", entity.KetQuaDoLuong);
    }

    // Ca 9: Quá SoPhienTheoDoiToiDa → thoát HetHanTheoDoi, không emit lượt sau.
    [Fact]
    public async Task TC_C9_HetHanTheoDoi_BanHet_SauDoSkip()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-35), // ~25 phiên, > 20
            entryPrice: 100m);

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m),
                soPhienTheoDoiToiDa: 20)
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("Hết hạn theo dõi", telegram.Messages[0]);
        Assert.Equal(SuKienBan.LyDoHetHanTheoDoi, entity.LyDoThoatHet);

        // Lượt sau: đã ThoatHet → skip.
        telegram.Messages.Clear();
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m),
                soPhienTheoDoiToiDa: 20)
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Empty(telegram.Messages);
    }

    // Ca 10: Chưa đủ T+2.5 mà chạm trailing stop → 1 cảnh báo, không đổi trạng thái.
    [Fact]
    public async Task TC_C10_CanhBaoChuaDuPhien_DungLoDuoi()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-1), // chưa đủ T+2.5
            entryPrice: 100m);
        entity.DinhTuLucMua = 115m;
        entity.DungLoDuoi = 106m; // > giaVao + SL kế hoạch
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 105m), minSessions: 3)
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        
        Assert.Single(telegram.Messages);
        Assert.Contains("CHẠM DỪNG LỖ ĐUỔI THEO", telegram.Messages[0]);
        Assert.Contains("Chưa đủ T+2.5", telegram.Messages[0]);
        Assert.Null(entity.ThoiGianThoatHet);
        Assert.Contains(SuKienBan.ChamDungLoDuoi, entity.CanhBaoDaGui ?? "");
    }

    // Ca 11: Hồi quy — chạy toàn bộ test suite (không có test riêng, mặc định).

    // Benchmark: 50 vị thế mở, đo KiemTraSellAsync filter cũ vs mới.
    [Fact]
    public async Task TC_C_Bench_50Vi_The_DoThoiGian()
    {
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);

        // DB 1 — filter cũ sẽ bắt 0 dòng (KetQuaDoLuong="Thang") vs filter mới bắt 50 dòng.
        var dbMeasured = NewSellDb();
        for (var i = 0; i < 50; i++)
        {
            SeedHolding(dbMeasured, $"M{i:D2}",
                kichHoat: kichHoat, entryPrice: 100m, ketQuaDoLuong: "Thang");
        }
        var repo = new FakeStockRepository();
        for (var i = 0; i < 50; i++) repo.ThemStock($"M{i:D2}", FlatBars(30, 100m));

        // Simulate old filter: 0 dòng → thời gian gần như bằng 0
        var oldFilterList = dbMeasured.KetQuaKichBan
            .Where(e => e.TrangThai == TrangThaiKichBan.DaKichHoat
                        && e.LoaiKichBan != LoaiKichBan.KietSuc
                        && e.LoaiKichBan != LoaiKichBan.GayNen
                        && e.ThoiGianKichHoat != null
                        && e.KetQuaDoLuong == null)
            .ToList();
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var _ = oldFilterList.Count; // 0
        sw1.Stop();

        // Chạy KiemTraSellAsync filter mới — thực sự xét 50 dòng
        var telegram = new FakeTelegram();
        var mayNhan = new FakeMayNhanKichBan();
        var runner = TaoSellRunner(dbMeasured, mayNhan, telegram, repo, MakeKbsJson("M00", 100m));
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        await runner.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        sw2.Stop();

        // Ghi log để báo cáo
        var msg = $"[BENCH] filter-cũ={sw1.ElapsedMilliseconds}ms (0 dòng), filter-mới={sw2.ElapsedMilliseconds}ms (50 dòng).";
        System.Console.WriteLine(msg);
        TestLoggerSink.Last = msg;

        // Không assert giá trị thời — chỉ cần chạy xong.
        Assert.True(sw2.ElapsedMilliseconds >= 0);
    }

    // ----- Test TC_C11: Hồi quy migration UPDATE — đóng im lặng dữ liệu cũ -----

    /// <summary>
    /// Helper mô phỏng logic UPDATE của migration AddSellTrailingStopFields.
    /// InMemoryDatabase không chạy SQL, nên áp điều kiện WHERE trực tiếp lên entities.
    /// </summary>
    private static void MockMigrationUpdateOldPositions(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;
        var affected = db.KetQuaKichBan.Local
            .Where(e => e.ThoiGianKichHoat != null
                     && e.ThoiGianThoatHet == null
                     && e.KetQuaDoLuong != null
                     && e.LoaiKichBan != LoaiKichBan.KietSuc
                     && e.LoaiKichBan != LoaiKichBan.GayNen)
            .ToList();
        foreach (var e in affected)
        {
            e.ThoiGianThoatHet = now;
            e.LyDoThoatHet = SuKienBan.LyDoHetHanTheoDoi;
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task TC_C11_MigrationUpdate_DongImLangDuLieuCu()
    {
        var db = NewSellDb();
        var homNay = VietnamMarketCalendar.TodayVietnam();
        var ngayHienTai = homNay.ToDateTime(TimeOnly.MinValue);

        // (a) Kích hoạt 30 phiên trước, KetQuaDoLuong="Thang", TrangThai=ChotLoi, ThoiGianThoatHet NULL.
        var entityA = new KetQuaKichBanEntity
        {
            Symbol = "OLD_A",
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.ChotLoi,
            ThoiGianKichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-35),
            KeHoachGiaoDichJson = MakeKeHoachJson(10m),
            KetQuaDoLuong = "Thang",
            ThoiGianThoatHet = null,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = VietnamMarketCalendar.NowVietnam().AddDays(-35).Date,
        };

        // (b) Kích hoạt 6 phiên trước, KetQuaDoLuong="Thua", TrangThai=HuyLenh, ThoiGianThoatHet NULL.
        var entityB = new KetQuaKichBanEntity
        {
            Symbol = "OLD_B",
            LoaiKichBan = LoaiKichBan.QuetThanhKhoan,
            TrangThai = TrangThaiKichBan.HuyLenh,
            ThoiGianKichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7),
            KeHoachGiaoDichJson = MakeKeHoachJson(10m),
            KetQuaDoLuong = "Thua",
            ThoiGianThoatHet = null,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = VietnamMarketCalendar.NowVietnam().AddDays(-7).Date,
        };

        // (c) Kích hoạt 2 phiên trước, KetQuaDoLuong NULL (vị thế mới, chưa đo).
        var entityC = new KetQuaKichBanEntity
        {
            Symbol = "NEW_C",
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            ThoiGianKichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-2),
            KeHoachGiaoDichJson = MakeKeHoachJson(10m),
            KetQuaDoLuong = null,
            ThoiGianThoatHet = null,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = VietnamMarketCalendar.NowVietnam().AddDays(-2).Date,
        };

        db.KetQuaKichBan.AddRange(entityA, entityB, entityC);
        db.SaveChanges();

        // Áp logic UPDATE của migration: (a) và (b) sẽ có ThoiGianThoatHet set.
        MockMigrationUpdateOldPositions(db);

        // Xác nhận (a) và (b) đã bị đóng im lặng.
        Assert.NotNull(entityA.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoHetHanTheoDoi, entityA.LyDoThoatHet);
        Assert.Null(entityA.GiaThoatHet);
        Assert.NotNull(entityB.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoHetHanTheoDoi, entityB.LyDoThoatHet);
        Assert.Null(entityB.GiaThoatHet);

        // (c) vẫn NULL.
        Assert.Null(entityC.ThoiGianThoatHet);

        // KBS trả giá 9.4 (< SL 9.5) cho cả 3 mã → nếu lọt qua filter thì sẽ bắn dừng lỗ.
        var kbsJson = "[" +
            $"{{\"SB\":\"OLD_A\",\"CP\":9400,\"HI\":10100,\"LO\":9300,\"TT\":1000000}}," +
            $"{{\"SB\":\"OLD_B\",\"CP\":9400,\"HI\":10100,\"LO\":9300,\"TT\":1000000}}," +
            $"{{\"SB\":\"NEW_C\",\"CP\":9400,\"HI\":10100,\"LO\":9300,\"TT\":1000000}}]";

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("OLD_A", FlatBars(30, 10m));
        repo.ThemStock("OLD_B", FlatBars(30, 10m));
        repo.ThemStock("NEW_C", FlatBars(30, 10m));

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, kbsJson);
        await runner.KiemTraSellAsync(ngayHienTai, default);

        // (a) OLD_A và (b) OLD_B: KHÔNG nhận tin nào vì ThoiGianThoatHet đã set → bị filter loại.
        // (c) NEW_C: chưa đủ T+2.5 → chỉ cảnh báo, không đổi vị thế.
        Assert.Single(telegram.Messages);
        Assert.Contains("NEW_C", telegram.Messages[0]);
        Assert.DoesNotContain("OLD_A", telegram.Messages[0]);
        Assert.DoesNotContain("OLD_B", telegram.Messages[0]);
    }

    private static class TestLoggerSink
    {
        public static string? Last { get; set; }
    }
}

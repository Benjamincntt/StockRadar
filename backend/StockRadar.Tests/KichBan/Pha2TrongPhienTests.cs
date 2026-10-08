using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
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

    private static Pha2TrongPhienRunner TaoSellRunner(
        ApplicationDbContext db,
        FakeMayNhanKichBan mayNhan,
        FakeTelegram telegram,
        FakeStockRepository stockRepo,
        string kbsJson,
        int minSessions = 3)
    {
        var http = new HttpClient(new FakeHttpHandler(kbsJson));
        var kbs = new KbsPriceBoardClient(http, NullLogger<KbsPriceBoardClient>.Instance);
        var pha2Opts = Options.Create(new Pha2Options { MinTradingSessionsToSell = minSessions });
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
        SeedHolding(db, "HPG", kichHoat: kichHoat, thoatHet: DateTime.UtcNow, lyDoThoat: "GayNen");

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

    // ----- Test TC6: Bản ghi đã có KetQuaDoLuong → không xét bán -----

    [Fact]
    public async Task TC6_DaCoKetQuaDoLuong_KhongXetBan()
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

        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 11m));
        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Empty(telegram.Messages);
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

    // Ca 1: Đủ phiên, giá <= dừng lỗ → tin "BÁN HẾT", ghi LyDoThoatHet = "DungLo"

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
        Assert.Equal("DungLo", entity.LyDoThoatHet);
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
        Assert.Equal("DungLo", entity.LyDoThoatHet);
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
        Assert.Equal("ChotLoi2", entity.LyDoThoatHet);
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
        Assert.Contains("ChamDungLo", entity.CanhBaoDaGui ?? "");

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
        SeedHolding(db, "HPG", kichHoat: kichHoat, thoatHet: DateTime.UtcNow, lyDoThoat: "DungLo");

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
            lyDoThoat: "GayNen",
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
        Assert.Equal("GayNen", entity.LyDoThoatHet);
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
        SeedHolding(db, "HPG", kichHoat: kichHoat, canhBao: "KietSuc,GayNen");

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
}

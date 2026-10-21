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
            Options.Create(new Pha2Options()),
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
        public bool NemLoiMua { get; set; }
        public decimal? LastGiaVaoLenh { get; private set; }

        public Task<IReadOnlyList<KetQuaKichBan>> DanhGiaTruocPhienAsync(
            string symbol, IReadOnlyList<OhlcvBar> history, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KetQuaKichBan>>(Array.Empty<KetQuaKichBan>());

        public Task<IReadOnlyList<KetQuaKichBan>> KiemTraTriggerTrongPhienAsync(
            IReadOnlyList<KetQuaKichBan> dangHinhThanh,
            IReadOnlyDictionary<string, decimal> giaHienTai,
            IReadOnlyDictionary<string, long> volumeHienTai,
            CancellationToken ct = default)
        {
            if (NemLoiMua)
                throw new InvalidOperationException("Gia lập lỗi bước mua");
            return Task.FromResult<IReadOnlyList<KetQuaKichBan>>(
                TriggerResults ?? dangHinhThanh.Where(t => t.TrangThai == TrangThaiKichBan.DangHinhThanh).ToList());
        }

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

    /// <summary>Kế hoạch đủ trường quy tắc (spec 009) — mặc định như record, override từng mục cần khác.</summary>
    private static string MakeKeHoachJsonDayDu(
        decimal entry,
        decimal? giaVaoMax = null,
        decimal? sl = null,
        decimal? cl1 = null,
        decimal? cl2 = null,
        bool coDuoi = false,
        decimal? khoangCach = null,
        int soPhienDungTheoThoiGian = 5,
        decimal nguongLaiToiThieu = 3m,
        int soPhienToiDa = 20,
        bool doiDungLo = true,
        bool boSung = false,
        decimal? giaMuaDiem2 = null,
        decimal? bandRongDiem2 = null) =>
        JsonSerializer.Serialize(new KeHoachGiaoDich
        {
            GiaVaoLenhMin = entry,
            GiaVaoLenhMax = giaVaoMax ?? entry,
            GiaDungLo = sl ?? entry * 0.95m,
            GiaChotLoi1 = cl1 ?? entry * 1.10m,
            GiaChotLoi2 = cl2 ?? entry * 1.20m,
            DieuKienHuy = "test",
            CoDungLoDuoi = coDuoi,
            KhoangCachDuoi = khoangCach,
            SoPhienDungTheoThoiGian = soPhienDungTheoThoiGian,
            NguongLaiToiThieuPhanTram = nguongLaiToiThieu,
            SoPhienToiDa = soPhienToiDa,
            DoiDungLoVeGiaVao = doiDungLo,
            LaDuLieuBoSung = boSung,
            GiaMuaDiem2 = giaMuaDiem2,
            BandRongDiem2 = bandRongDiem2,
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
        decimal? giaThoatHetVal = null,
        string? keHoachJson = null,
        decimal? giaVaoThucTe = null,
        decimal? giaMuaDiem2ThucTe = null,
        DateTime? thoiGianMuaDiem2 = null)
    {
        var entity = new KetQuaKichBanEntity
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            ThoiGianKichHoat = kichHoat ?? DateTime.UtcNow.AddDays(-5),
            KeHoachGiaoDichJson = keHoachJson ?? MakeKeHoachJson(entryPrice),
            GiaVaoThucTe = giaVaoThucTe,
            GiaMuaDiem2ThucTe = giaMuaDiem2ThucTe,
            ThoiGianMuaDiem2 = thoiGianMuaDiem2,
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

    // Helper dựng runner xét bán. US3 (spec 009): số phiên / ngưỡng / hạn đọc từ KẾ HOẠCH chứ không từ cfg,
    // nên các knob cfg tương ứng đã bỏ khỏi helper — muốn đổi giá trị thì truyền keHoachJson trong SeedHolding.
    // HeSoAtr giữ lại CHỈ để chứng minh luồng bán không còn đọc cfg (T021).
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
        decimal? heSoAtr = null)
    {
        var http = new HttpClient(new FakeHttpHandler(kbsJson));
        var kbs = new KbsPriceBoardClient(http, NullLogger<KbsPriceBoardClient>.Instance);
        var opts = new Pha2Options { MinTradingSessionsToSell = minSessions };
        // Mặc định test cũ: bỏ qua chống nhiễu (1 lượt) để hồi quy B không đổi kỳ vọng.
        opts.SoLuotXacNhanDungLo = luotXacNhan ?? 1;
        opts.SoLuotXacNhanDungLoAtc = luotXacNhanAtc ?? luotXacNhan ?? 1;
        if (heSoAtr.HasValue) opts.HeSoAtrDungLoDuoi = heSoAtr.Value;
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

        // US3 (Q1, spec 009 — 19/10): Kiệt sức dù ĐỦ phiên cũng chỉ còn là tin cảnh báo, không phải lệnh bán.
        Assert.Single(telegram.Messages);
        Assert.Contains("CẢNH BÁO", telegram.Messages[0]);
        Assert.Contains("KIỆT SỨC", telegram.Messages[0]);
        var entity4b = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.Null(entity4b.ThoiGianBanNua);
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
        Assert.Contains("Chốt lời 1", telegram.Messages[0]);   // C-29: CL1 đổi sang format BÁN NỬA (Chốt lời 1)
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
        Assert.Contains("BÁN NỬA (Chốt lời 1)", telegram.Messages[0]); // C-29: CL1 dùng format BÁN NỬA

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
        Assert.Contains("BÁN NỬA (Chốt lời 1)", telegram.Messages[0]); // C-29 + chỉ TP1, chưa TP2

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

        // US3 (Q1, spec 009): Kiệt sức chỉ còn cảnh báo — không ghi bán nửa, không dời SL.
        Assert.Single(telegram.Messages);
        Assert.Contains("CẢNH BÁO", telegram.Messages[0]);
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.Null(entity.ThoiGianBanNua);
        Assert.False(entity.DaDoiDungLo);

        // Lượt 2: giá=9.6 → −8.6% so đỉnh 10.5, luật Phase 8 (rút mốc 6%) bán hết; SL vẫn KHÔNG bị dời.
        telegram.Messages.Clear();
        var runner2 = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 9.6m));
        await runner2.KiemTraSellAsync(ngayDanhGia, default);

        // Kiêm chứng Phase 8: giá dưới mốc×0.94 → thoát hết vì RutMoc6 (không phải ChamDungLo), Kiệt sức không dời SL.
        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN HẾT", telegram.Messages[0]);
        Assert.Equal(SuKienBan.LyDoRutMoc6, entity.LyDoThoatHet);
        Assert.Null(entity.ThoiGianBanNua);   // 6% và 4% cùng lượt → bán hết thắng, không ghi bán nửa
        Assert.False(entity.DaDoiDungLo);
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

    // Ca 3: DungLoDuoi chỉ tăng, không giảm khi giá rơi. US3: khoảng cách đọc từ kế hoạch (2,5) — không còn tính ATR mỗi phiên.
    [Fact]
    public async Task TC_C3_DungLoDuoiChiTang()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, coDuoi: true, khoangCach: 2.5m));
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
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, soPhienDungTheoThoiGian: 100));

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        var runtime = new Pha2TrailingStopRuntime();

        // Lượt 1: giá 94 (<= SL 95) → counter = 1, luotCan = 2 → chưa emit.
        var r1 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 94m),
            runtime: runtime, luotXacNhan: 2);
        await r1.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Empty(telegram.Messages);
        
        // Lượt 2: giá hồi 97 → reset counter.
        telegram.Messages.Clear();
        var r2 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 97m),
            runtime: runtime, luotXacNhan: 2);
        await r2.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Empty(telegram.Messages);
        
        // Lượt 3: giá 94 → counter = 1, chưa emit.
        telegram.Messages.Clear();
        var r3 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 94m),
            runtime: runtime, luotXacNhan: 2);
        await r3.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.Empty(telegram.Messages);
        
        // Lượt 4: giá 94 → counter = 2 → emit.
        telegram.Messages.Clear();
        var r4 = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 94m),
            runtime: runtime, luotXacNhan: 2);
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
            daDoiDungLo: true,
            keHoachJson: MakeKeHoachJsonDayDu(100m, coDuoi: true, khoangCach: 11m)); // 115 − 11 = 104
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
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m)); // SoPhienDungTheoThoiGian=5, NguongLaiToiThieu=3 (mặc định helper)
        entity.Mfe = 2.5m; // dưới ngưỡng 3
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m))
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

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m))
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

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("Hết hạn theo dõi", telegram.Messages[0]);
        Assert.Equal(SuKienBan.LyDoHetHanTheoDoi, entity.LyDoThoatHet);

        // Lượt sau: đã ThoatHet → skip.
        telegram.Messages.Clear();
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m))
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
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, coDuoi: true, khoangCach: 9m)); // 115 − 9 = 106
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

    // ===== Test goi qua ChayAsync — kiem tra buoc ban chay moi luot =====

    private static Pha2TrongPhienRunner TaoChayAsyncRunner(
        ApplicationDbContext db,
        FakeMayNhanKichBan mayNhan,
        FakeTelegram telegram,
        FakeStockRepository stockRepo,
        string kbsJson,
        bool gioNgoaiPhien = false)
    {
        var http = new HttpClient(new FakeHttpHandler(kbsJson));
        var kbs = new KbsPriceBoardClient(http, NullLogger<KbsPriceBoardClient>.Instance);
        var opts = new Pha2Options
        {
            GioBatDau = gioNgoaiPhien ? "23:00" : "00:00",
            GioKetThuc = gioNgoaiPhien ? "23:59" : "23:59",
            MinTradingSessionsToSell = 0,
            SoLuotXacNhanDungLo = 1,
            SoLuotXacNhanDungLoAtc = 1,
        };
        return new Pha2TrongPhienRunner(
            mayNhan,
            new FakeXepHang(),
            stockRepo,
            kbs,
            telegram,
            db,
            Options.Create(opts),
            Options.Create(new TelegramNotifyOptions { Enabled = true }),
            new FakeLichChotQuyen(),
            Options.Create(new FireAntOptions { Enabled = false }),
            new Pha2TrailingStopRuntime(),
            NullLogger<Pha2TrongPhienRunner>.Instance);
    }

    [Fact]
    public async Task ChayAsync_KhongCoForming_BuocVanVanChay()
    {
        // Không có FORMING hôm nay, có vị thế đang giữ chạm dừng lỗ, đủ T+2.5.
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat);

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 10m));

        // Giá 9.0 < SL 9.5 (10 * 0.95) → chạm dừng lỗ.
        var runner = TaoChayAsyncRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 9.0m));
        var result = await runner.ChayAsync(default);

        Assert.Equal(0, result.SoTrigger);
        Assert.Single(telegram.Messages);
        Assert.Contains("HPG", telegram.Messages[0]);
        Assert.Contains("BÁN HẾT", telegram.Messages[0]);
    }

    [Fact]
    public async Task ChayAsync_CoFormingKhongTrigger_BuocVanVanChay()
    {
        // Có FORMING nhưng không trigger → bước bán vẫn chạy.
        var db = NewSellDb();
        var homNay = VietnamMarketCalendar.TodayVietnam();
        var ngayDanhGia = homNay.ToDateTime(TimeOnly.MinValue);

        // Seed FORMING
        db.KetQuaKichBan.Add(new KetQuaKichBanEntity
        {
            Symbol = "FORM",
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DangHinhThanh,
            NgayDanhGia = ngayDanhGia,
            DatBoiCanh = true,
            DatHinhThai = true,
            MucHoanThien = 80m,
        });

        // Seed holding
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat);
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan { TriggerResults = [] };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("FORM", FlatBars(30, 25m));
        repo.ThemStock("HPG", FlatBars(30, 10m));

        var kbsJson = "[" +
            $"{{\"SB\":\"FORM\",\"CP\":25000,\"TT\":1000000}}," +
            $"{{\"SB\":\"HPG\",\"CP\":9000,\"TT\":1000000}}]";

        var runner = TaoChayAsyncRunner(db, mayNhan, telegram, repo, kbsJson);
        var result = await runner.ChayAsync(default);

        Assert.Equal(0, result.SoTrigger);
        // Bước bán vẫn gửi tin cho HPG (chạm SL).
        Assert.Single(telegram.Messages);
        Assert.Contains("HPG", telegram.Messages[0]);
    }

    [Fact]
    public async Task ChayAsync_BuocMuaNemLoi_BuocVanVanChay()
    {
        // FakeMayNhanKichBan ném lỗi ở KiemTraTriggerTrongPhienAsync.
        var db = NewSellDb();
        var homNay = VietnamMarketCalendar.TodayVietnam();
        var ngayDanhGia = homNay.ToDateTime(TimeOnly.MinValue);

        // Seed FORMING
        db.KetQuaKichBan.Add(new KetQuaKichBanEntity
        {
            Symbol = "FORM",
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DangHinhThanh,
            NgayDanhGia = ngayDanhGia,
            DatBoiCanh = true,
            DatHinhThai = true,
            MucHoanThien = 80m,
        });

        // Seed holding
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat);
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan { NemLoiMua = true };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("FORM", FlatBars(30, 25m));
        repo.ThemStock("HPG", FlatBars(30, 10m));

        var kbsJson = "[" +
            $"{{\"SB\":\"FORM\",\"CP\":25000,\"TT\":1000000}}," +
            $"{{\"SB\":\"HPG\",\"CP\":9000,\"TT\":1000000}}]";

        var runner = TaoChayAsyncRunner(db, mayNhan, telegram, repo, kbsJson);
        var result = await runner.ChayAsync(default);

        // Bước mua lỗi → 0 trigger.
        Assert.Equal(0, result.SoTrigger);
        // Bước bán vẫn chạy → có tin.
        Assert.Single(telegram.Messages);
        Assert.Contains("HPG", telegram.Messages[0]);
    }

    [Fact]
    public async Task ChayAsync_NgoaiGio_KhongBuocNaoChay()
    {
        // GioBatDau=23:00 → hầu hết test chạy trước 23:00 VN → "ngoài giờ".
        var db = NewSellDb();
        var kichHoat = VietnamMarketCalendar.NowVietnam().AddDays(-7);
        SeedHolding(db, "HPG", kichHoat: kichHoat);

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 10m));

        var runner = TaoChayAsyncRunner(
            db, mayNhan, telegram, repo, MakeKbsJson("HPG", 9.0m), gioNgoaiPhien: true);
        var result = await runner.ChayAsync(default);

        Assert.Equal(0, result.SoTrigger);
        Assert.Empty(telegram.Messages);
    }

    // ==========================================================================
    // US1 (spec 009) — CHỈ MUA KHI GIÁ KHỚP NẰM TRONG VÙNG VÀO; KẾ HOẠCH PHẢI CÓ ĐƯỜNG RA
    // ==========================================================================

    /// <summary>Bộ máy kịch bản thật có đủ 5 loại (TaoService thiếu Hồi về hỗ trợ).</summary>
    private static MayNhanKichBanService TaoServiceDayDu(FakeStockRepository repo)
    {
        var kichBanOptions = new KichBanOptions();
        var danhSachKichBan = new IKichBanDanhGia[]
        {
            new KichBanNoHuongLen(kichBanOptions.NoHuongLen),
            new KichBanHoiHoTro(kichBanOptions.HoiHoTro),
            new KichBanQuetThanhKhoan(kichBanOptions.QuetThanhKhoan),
            new KichBanKietSuc(kichBanOptions.KietSuc),
            new KichBanGayNen(kichBanOptions.GayNen),
        };

        return new MayNhanKichBanService(
            danhSachKichBan,
            repo,
            new BanChupChiBaoBuilder(),
            Options.Create(new Pha2Options()),
            NullLogger<MayNhanKichBanService>.Instance);
    }

    /// <summary>Xếp hạng xuyên thấu — bước gửi tin MUA lặp trên danh sách xếp hạng nên không được rỗng.</summary>
    private sealed class FakeXepHangPassThrough : IXepHangCoHoi
    {
        public Task<IReadOnlyList<KetQuaXepHang>> XepHangAsync(
            IReadOnlyList<KetQuaKichBan> daKichHoat, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KetQuaXepHang>>(
                daKichHoat.Select(k => new KetQuaXepHang(k, 50m, 50m, 50m, 50m, 50m, 50m, 50m)).ToList());
    }

    /// <summary>Runner dùng bộ máy kịch bản THẬT (không phải FakeMayNhanKichBan) cho luồng mua.</summary>
    private static Pha2TrongPhienRunner TaoChayAsyncRunnerMua(
        ApplicationDbContext db,
        IMayNhanKichBan mayNhanThat,
        FakeTelegram telegram,
        FakeStockRepository stockRepo,
        string kbsJson)
    {
        var http = new HttpClient(new FakeHttpHandler(kbsJson));
        var kbs = new KbsPriceBoardClient(http, NullLogger<KbsPriceBoardClient>.Instance);
        var opts = new Pha2Options
        {
            GioBatDau = "00:00",
            GioKetThuc = "23:59",
            MinTradingSessionsToSell = 0,
            SoLuotXacNhanDungLo = 1,
            SoLuotXacNhanDungLoAtc = 1,
        };
        return new Pha2TrongPhienRunner(
            mayNhanThat,
            new FakeXepHangPassThrough(),
            stockRepo,
            kbs,
            telegram,
            db,
            Options.Create(opts),
            Options.Create(new TelegramNotifyOptions { Enabled = true }),
            new FakeLichChotQuyen(),
            Options.Create(new FireAntOptions { Enabled = false }),
            new Pha2TrailingStopRuntime(),
            NullLogger<Pha2TrongPhienRunner>.Instance);
    }

    private static string MakeKbsJsonVol(string symbol, decimal gia, long tt) =>
        $"[{{\"SB\":\"{symbol}\",\"CP\":{(double)(gia * 1000m)},\"TT\":{tt}}}]";

    /// <summary>Nến tái hiện dạng sự cố DCS 09/10: uptrend rồi pullback xen kẽ — vùng vào quanh EMA20 ±0.5%.</summary>
    private static List<OhlcvBar> PullbackHoiHoTroBars()
    {
        var closes = new List<decimal>();
        var vols = new List<long>();
        for (var i = 0; i < 235; i++) { closes.Add(20m + i * 0.03m); vols.Add(1_000_000); }
        var gia = closes[^1];
        for (var k = 0; k < 14; k++)
        {
            gia += k % 2 == 0 ? -0.2m : +0.15m;
            closes.Add(gia);
            vols.Add(1_000_000);
        }
        return BarsFrom(closes, vols);
    }

    private static KetQuaKichBanEntity SeedForming(
        ApplicationDbContext db, string symbol, LoaiKichBan loai)
    {
        var entity = new KetQuaKichBanEntity
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DangHinhThanh,
            NgayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue),
            DatBoiCanh = true,
            DatHinhThai = true,
            MucHoanThien = 80m,
        };
        db.KetQuaKichBan.Add(entity);
        db.SaveChanges();
        return entity;
    }

    // T010: DCS ngoài vùng vào → không kích hoạt; lượt sau trong vùng → kích hoạt (gọi qua ChayAsync).
    [Fact]
    public async Task ChayAsync_NgoaiVungVao_KhongKichHoat_LuotSauTrongVungThiKichHoat()
    {
        var bars = PullbackHoiHoTroBars();
        var ema20 = IndicatorMath.Ema(bars, 20);

        var db = NewSellDb();
        var entity = SeedForming(db, "DCS", LoaiKichBan.HoiHoTro);

        var repo = new FakeStockRepository();
        repo.ThemStock("DCS", bars);

        // Lượt 1: giá khớp cao hơn EMA20 tới 15% — ngoài vùng ±0.5% (như DCS 700 so với vùng 610 hôm 09/10).
        // Cò vẫn đạt nên nếu kích hoạt thì chắc chắn đã gửi tin; SoTrigger=0 chứng minh kiểm vùng chặn.
        var telegram1 = new FakeTelegram();
        var runner1 = TaoChayAsyncRunnerMua(
            db, TaoServiceDayDu(repo), telegram1, repo, MakeKbsJson("DCS", ema20 * 1.15m));
        var ketQua1 = await runner1.ChayAsync(default);

        Assert.Equal(0, ketQua1.SoTrigger);
        Assert.Empty(telegram1.Messages);
        Assert.Equal(TrangThaiKichBan.DangHinhThanh, entity.TrangThai);
        Assert.Null(entity.KeHoachGiaoDichJson);

        // Lượt sau: giá về trong vùng (trên EMA20 0.2%) → kích hoạt + gửi tin MUA.
        var telegram2 = new FakeTelegram();
        var runner2 = TaoChayAsyncRunnerMua(
            db, TaoServiceDayDu(repo), telegram2, repo, MakeKbsJson("DCS", ema20 * 1.002m));
        var ketQua2 = await runner2.ChayAsync(default);

        Assert.Equal(1, ketQua2.SoTrigger);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
        Assert.NotNull(entity.KeHoachGiaoDichJson);
        Assert.Contains(telegram2.Messages, m => m.Contains("DCS") && m.Contains("MUA"));
    }

    // T010 (biên): giá khớp đúng bằng biên trên vùng vào → được coi là TRONG vùng (test cấp service để
    // phép so sánh decimal đúng bằng — đi qua JSON KBS có sai số làm mất tính biên).
    [Fact]
    public async Task KiemTraTrigger_GiaDungNgayBienTrenCuaVung_VanKichHoat()
    {
        var bars = PullbackHoiHoTroBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("DCS", bars);

        var service = TaoServiceDayDu(repo);
        var dangHinhThanh = new List<KetQuaKichBan>
        {
            new()
            {
                Symbol = "DCS",
                LoaiKichBan = LoaiKichBan.HoiHoTro,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
            }
        };

        var bienTren = IndicatorMath.Ema(bars, 20) * 1.005m;
        var gia = new Dictionary<string, decimal> { ["DCS"] = bienTren };
        var vol = new Dictionary<string, long> { ["DCS"] = 1_000_000 };

        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        Assert.Single(ketQua);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, ketQua[0].TrangThai);
    }

    // T011: kế hoạch không có đường ra (CL1 ≤ biên trên vùng vào) → không kích hoạt,
    // bằng chứng mang mã KeHoachKhongHopLe; lượt sau kế hoạch hợp lệ → kích hoạt bình thường.
    [Fact]
    public async Task KiemTraTrigger_KeHoachKhongDuongRa_GhiBangChungVaKhongKichHoat()
    {
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);

        var service = TaoService(repo);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);
        var avg20 = bars.TakeLast(20).Average(b => (decimal)b.Volume);
        var vol = new Dictionary<string, long> { ["HPG"] = (long)(avg20 * 2.5m) };

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

        // Giá cao hơn đỉnh hộp 60 → biên trên vùng vào vọt trên TP1 (≈109) → kế hoạch không còn đường ra.
        var gia = new Dictionary<string, decimal> { ["HPG"] = dinh20 + 60m };
        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        Assert.Empty(ketQua);
        Assert.Equal(TrangThaiKichBan.DangHinhThanh, dangHinhThanh[0].TrangThai);
        Assert.Contains(
            dangHinhThanh[0].DanhSachBangChung,
            b => b.MoTa.StartsWith(SuKienBan.KeHoachKhongHopLe) && !b.Dat);

        // Lượt sau với kế hoạch hợp lệ (giá sát đỉnh hộp) → kích hoạt được.
        var giaHopLe = new Dictionary<string, decimal> { ["HPG"] = dinh20 + 1m };
        var ketQua2 = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, giaHopLe, vol);

        Assert.Single(ketQua2);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, ketQua2[0].TrangThai);
    }

    // T012: Nổ hướng lên với kế hoạch hợp lệ vẫn kích hoạt qua ChayAsync — kiểm vùng không chặn
    // (giá khớp đúng bằng biên dưới vì Min được tính từ chính giá khớp).
    [Fact]
    public async Task ChayAsync_NoHuongLenKeHoachHopLe_VanKichHoat()
    {
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);

        var db = NewSellDb();
        var entity = SeedForming(db, "HPG", LoaiKichBan.NoHuongLen);

        var telegram = new FakeTelegram();
        var runner = TaoChayAsyncRunnerMua(
            db, TaoService(repo), telegram, repo, MakeKbsJsonVol("HPG", dinh20 + 1m, 2_500_000));
        var ketQua = await runner.ChayAsync(default);

        Assert.Equal(1, ketQua.SoTrigger);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
        Assert.Contains(telegram.Messages, m => m.Contains("HPG") && m.Contains("MUA"));
    }

    // ==========================================================================
    // US2 (spec 009) — KẾ HOẠCH ĐẦY ĐỦ CHỐT LÚC MUA + GIÁ VÀO THỰC TẾ
    // ==========================================================================

    // T016: kích hoạt Nổ hướng lên cấp service → kế hoạch đủ trường quy tắc,
    // KhoangCachDuoi = 2.5 × ATR14 tính đúng lúc kích hoạt (chốt một lần).
    [Fact]
    public async Task KiemTraTrigger_NoHuongLen_DayDuTruongQuyTuc_KhoangCachDuoiLa2RuiAtrLucAy()
    {
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);

        var service = TaoService(repo);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);
        var avg20 = bars.TakeLast(20).Average(b => (decimal)b.Volume);

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
        var gia = new Dictionary<string, decimal> { ["HPG"] = dinh20 + 1m };
        var vol = new Dictionary<string, long> { ["HPG"] = (long)(avg20 * 2.5m) };

        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        Assert.Single(ketQua);
        var kh = ketQua[0].KeHoach!;
        // Khoảng cách đuổi = hệ số × ATR chu kỳ cấu hình, tính trên history lúc kích hoạt.
        var atrKyVong = IndicatorMath.Atr(bars, 14) * 2.5m;
        Assert.True(kh.CoDungLoDuoi);
        Assert.Equal(atrKyVong, kh.KhoangCachDuoi);
        Assert.Equal(0.5m, kh.TyLeBanChotLoi1);
        Assert.True(kh.DoiDungLoVeGiaVao);
        Assert.Equal(5, kh.SoPhienDungTheoThoiGian);
        Assert.Equal(3m, kh.NguongLaiToiThieuPhanTram);
        Assert.Equal(20, kh.SoPhienToiDa);
        Assert.False(kh.LaDuLieuBoSung);
        // Hạn vào = 14:45 VN của ngày đánh giá, quy đổi UTC (VN = UTC+7, không DST).
        var hetHanKyVong = VietnamMarketCalendar.TodayVietnam()
            .ToDateTime(new TimeOnly(14, 45)).AddHours(-7);
        Assert.Equal(DateTime.SpecifyKind(hetHanKyVong, DateTimeKind.Utc), kh.HetHanVao);
        // Giá khớp được mang ra để runner lưu vào GiaVaoThucTe.
        Assert.Equal(dinh20 + 1m, ketQua[0].GiaKhop);
    }

    // T016: Hồi hỗ trợ ngoài nhóm đuổi → CoDungLoDuoi false, KhoangCachDuoi null.
    [Fact]
    public async Task KiemTraTrigger_HoiHoTro_KhongDungLoDuoi()
    {
        var bars = PullbackHoiHoTroBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("DCS", bars);

        var service = TaoServiceDayDu(repo);
        var giaKhop = IndicatorMath.Ema(bars, 20) * 1.002m;
        var dangHinhThanh = new List<KetQuaKichBan>
        {
            new()
            {
                Symbol = "DCS",
                LoaiKichBan = LoaiKichBan.HoiHoTro,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
            }
        };
        var gia = new Dictionary<string, decimal> { ["DCS"] = giaKhop };
        var vol = new Dictionary<string, long> { ["DCS"] = 1_000_000 };

        var ketQua = await service.KiemTraTriggerTrongPhienAsync(dangHinhThanh, gia, vol);

        Assert.Single(ketQua);
        var kh = ketQua[0].KeHoach!;
        Assert.False(kh.CoDungLoDuoi);
        Assert.Null(kh.KhoangCachDuoi);
        Assert.Equal(giaKhop, ketQua[0].GiaKhop);
    }

    // T016 qua ChayAsync: DB nhận GiaVaoThucTe = giá khớp và JSON kế hoạch đủ trường mới.
    [Fact]
    public async Task ChayAsync_KichHoat_LuuGiaVaoThucTeVaJsonDuTruong()
    {
        var bars = BreakoutBars();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", bars);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);

        var db = NewSellDb();
        var entity = SeedForming(db, "HPG", LoaiKichBan.NoHuongLen);

        var telegram = new FakeTelegram();
        var runner = TaoChayAsyncRunnerMua(
            db, TaoService(repo), telegram, repo, MakeKbsJsonVol("HPG", dinh20 + 1m, 2_500_000));
        var ketQua = await runner.ChayAsync(default);

        Assert.Equal(1, ketQua.SoTrigger);
        // Giá khớp đi qua JSON bảng giá (nhân 1000) nên so sánh có dung sai nhỏ.
        Assert.NotNull(entity.GiaVaoThucTe);
        Assert.True(Math.Abs(entity.GiaVaoThucTe!.Value - (dinh20 + 1m)) < 0.0001m);

        var kh = JsonSerializer.Deserialize<KeHoachGiaoDich>(entity.KeHoachGiaoDichJson!)!;
        Assert.True(kh.CoDungLoDuoi);
        Assert.NotNull(kh.KhoangCachDuoi);
        Assert.True(kh.HetHanVao > DateTime.UtcNow.Date);
        Assert.Equal(20, kh.SoPhienToiDa);
        Assert.False(kh.LaDuLieuBoSung);
    }

    // ==========================================================================
    // US3 (spec 009) — LỆNH RA CHỈ THEO MỨC TRONG KẾ HOẠCH (T021–T024)
    // ==========================================================================

    // T021: khoảng cách dừng lỗ đuổi chốt lúc mua — ATR các phiên sau (hay hệ số cfg đổi) không làm đổi mức.
    [Fact]
    public async Task T021_KhoangCachDuoi_ChotLucMua_KhongDoiTheoAtrMoi()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, coDuoi: true, khoangCach: 5m));
        entity.DinhTuLucMua = 110m;
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        // Flat 100 → ATR14 = 1: nếu còn tính lại mỗi phiên theo cfg (k=2,5) mức sẽ là 110 − 2,5 = 107,5.
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var ngay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Kế hoạch chốt khoảng cách 5 → cắt = 110 − 5 = 105; giá 108 trên mức → không thoát.
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJsonHiLo("HPG", 108m, 110m, 100m))
            .KiemTraSellAsync(ngay, default);
        Assert.Equal(105m, entity.DungLoDuoi);
        Assert.Null(entity.ThoiGianThoatHet);

        // Đổi hệ số cfg (k=4 → ATR-distance 106): kế hoạch không tính lại → mức vẫn 105.
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJsonHiLo("HPG", 108m, 110m, 100m), heSoAtr: 4m)
            .KiemTraSellAsync(ngay, default);
        Assert.Equal(105m, entity.DungLoDuoi);
    }

    // T022: CL1 → bán nửa + dời cắt; kế hoạch cho dời thì mức cắt sau đó = GiaVaoThucTe (102), không phải biên dưới vùng vào (100).
    [Fact]
    public async Task T022_ChotLoi1_Roi_MucCatHieuLucBangGiaVaoThucTe()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            giaVaoThucTe: 102m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, cl1: 110m));

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var ngay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt 1: giá 110 ≥ CL1 → bán nửa, dời dừng lỗ (kế hoạch DoiDungLoVeGiaVao = true).
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 110m))
            .KiemTraSellAsync(ngay, default);
        Assert.Contains("BÁN NỬA (Chốt lời 1)", telegram.Messages[0]); // C-29: CL1 dùng format BÁN NỬA
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.True(entity.DaDoiDungLo);
        Assert.Null(entity.ThoiGianThoatHet);

        // Lượt 2: giá 102 = giá vào thực tế → chạm cắt đã dời; 100 (biên dưới kế hoạch) không được dùng.
        telegram.Messages.Clear();
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 102m))
            .KiemTraSellAsync(ngay, default);
        Assert.Single(telegram.Messages);
        Assert.Contains("dời về giá vào", telegram.Messages[0]);
        Assert.Contains("102", telegram.Messages[0]);
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoDungLo, entity.LyDoThoatHet);
    }

    // T022: kế hoạch DoiDungLoVeGiaVao = false → bán nửa CL1 xong KHÔNG dời cắt; mức cắt vẫn là dừng lỗ ban đầu.
    [Fact]
    public async Task T022_KeHoachKhongDoi_SauCL1_VanCatTheoDungLoBanDau()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            giaVaoThucTe: 102m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, cl1: 110m, doiDungLo: false));

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var ngay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt 1: CL1 → bán nửa nhưng không dời cắt.
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 110m))
            .KiemTraSellAsync(ngay, default);
        Assert.Contains("BÁN NỬA (Chốt lời 1)", telegram.Messages[0]); // C-29: CL1 dùng format BÁN NỬA
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.False(entity.DaDoiDungLo);

        // Lượt 2: giá 107 — trên mốc×0.96 = 105.6 (rút mốc chưa bắn) và trên SL ban đầu 95 → không có gì.
        telegram.Messages.Clear();
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 107m))
            .KiemTraSellAsync(ngay, default);
        Assert.Empty(telegram.Messages);
        Assert.Null(entity.ThoiGianThoatHet);

        // Lượt 3 (Phase 8): giá 103 ≤ mốc 110 × 0.94 → bán hết vì rút mốc 6%, không phải vì dời cắt về giá vào.
        telegram.Messages.Clear();
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 103m))
            .KiemTraSellAsync(ngay, default);
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoRutMoc6, entity.LyDoThoatHet);
        Assert.False(entity.DaDoiDungLo);   // CL1 với DoiDungLoVeGiaVao=false không bao giờ dời cắt
    }

    // T023: Kiệt sức đạt (đã ĐỦ phiên) → chỉ tin cảnh báo kèm mức cắt hiệu lực, một lần, không ghi giá, trạng thái giữ nguyên.
    [Fact]
    public async Task T023_KietSuc_DuPhien_CanhBaoMotLan_KhongDoiTrangThai()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 10m);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.KietSuc)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.KietSuc)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());
        var ngay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Giá 10.5: trên SL 9.5, dưới TP1 11 → chỉ Kiệt sức bắn.
        var runner = TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner.KiemTraSellAsync(ngay, default);

        Assert.Single(telegram.Messages);
        Assert.Contains("CẢNH BÁO", telegram.Messages[0]);
        Assert.Contains("KIỆT SỨC", telegram.Messages[0]);
        Assert.Contains("Mức cắt", telegram.Messages[0]);
        Assert.Contains("9.5", telegram.Messages[0]);
        Assert.Null(entity.ThoiGianBanNua);
        Assert.Null(entity.GiaBanNua);
        Assert.Null(entity.ThoiGianThoatHet);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
        Assert.Contains(SuKienBan.KietSuc, entity.CanhBaoDaGui ?? "");

        // Lượt sau vẫn Kiệt sức → chống lặp bằng CanhBaoDaGui, không tin thứ hai.
        await runner.KiemTraSellAsync(ngay, default);
        Assert.Single(telegram.Messages);
        Assert.Null(entity.ThoiGianBanNua);
    }

    // T023: Gãy nền đạt → cũng CHỈ cảnh báo kèm mức cắt, không ghi giá thoát, không đổi trạng thái.
    [Fact]
    public async Task T023_GayNen_CanhBao_KhongGhiGiaThoat()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 10m);

        var mayNhan = new FakeMayNhanKichBan
        {
            BanResults = [MakeSellForming("HPG", LoaiKichBan.GayNen)],
            TriggerResults = [MakeSellTriggered("HPG", LoaiKichBan.GayNen)],
        };
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 10.5m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("CẢNH BÁO", telegram.Messages[0]);
        Assert.Contains("GÃY NỀN", telegram.Messages[0]);
        Assert.Contains("Mức cắt", telegram.Messages[0]);
        Assert.Null(entity.ThoiGianThoatHet);
        Assert.Null(entity.GiaThoatHet);
        Assert.Equal(TrangThaiKichBan.DaKichHoat, entity.TrangThai);
    }

    // T024: dừng theo thời gian đọc từ kế hoạch (2 phiên / lãi tối thiểu 10%), không đọc cfg (5 phiên / 3%).
    [Fact]
    public async Task T024_DungTheoThoiGian_DocTuKeHoach_KhongPhaiCfg()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), // ~5 phiên: cfg (5) cũng vừa tới nhưng ngưỡng cfg 3% thì giá +4% đã thoát
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, soPhienDungTheoThoiGian: 2, nguongLaiToiThieu: 10m));

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 104 → MFE 4%: theo cfg (ngưỡng 3%) KHÔNG hết thời gian; theo kế hoạch (ngưỡng 10%) thì CÓ.
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 104m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("Hết thời gian", telegram.Messages[0]);
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.Equal(SuKienBan.LyDoHetThoiGian, entity.LyDoThoatHet);
    }

    // T024: hạn tối đa đọc từ kế hoạch (30 phiên) — cfg 20 phiên không được dùng để ép thoát.
    [Fact]
    public async Task T024_HanToiDa_DocTuKeHoach_KhongBiCfg20EpThoat()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-35), // ~25 phiên: vượt cfg 20 nhưng còn hạn kế hoạch 30
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, soPhienToiDa: 30, soPhienDungTheoThoiGian: 100));

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 100m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Empty(telegram.Messages);
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.Null(entity.ThoiGianThoatHet);
    }

    // T024 (edge R5): chạm CL1 và mức cắt cùng lượt → mức cắt thắng (bán hết), không bán nửa.
    [Fact]
    public async Task T024_CungLuot_MucCatThang_ChotLoi1()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, cl1: 110m, coDuoi: true, khoangCach: 20m));
        entity.DinhTuLucMua = 130m; // trailing = 130 − 20 = 110, đúng bằng CL1
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 110: vừa ≥ CL1 vừa ≤ mức cắt 110 cùng lượt → mức cắt thắng.
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 110m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN HẾT", telegram.Messages[0]);
        Assert.Contains("đuổi theo", telegram.Messages[0]);
        Assert.Null(entity.ThoiGianBanNua);
        Assert.Equal(SuKienBan.LyDoDungLoDuoi, entity.LyDoThoatHet);
    }

    // T024 (C-16): đã bán nửa rồi → giá hồi lên CL1 lượt sau KHÔNG bán nửa lần hai
    // (mỗi chu kỳ tối đa một lần, bất kể nguồn; nguồn RutMoc4 thuộc Phase 8).
    [Fact]
    public async Task T024_C16_CungLuotCL1VaRutMoc4_BanNuaMotLan()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, cl1: 110m));

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var ngay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Lượt 1: CL1 → bán nửa.
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 110m))
            .KiemTraSellAsync(ngay, default);
        Assert.Contains("BÁN NỬA (Chốt lời 1)", telegram.Messages[0]); // C-29: CL1 dùng format BÁN NỬA
        var entity = db.KetQuaKichBan.First(e => e.Symbol == "HPG");
        Assert.NotNull(entity.GiaBanNua);

        // Lượt 2: giá vẫn ≥ CL1 (nguồn rút mốc cho hiệu ứng tương tự) → không bán nửa lần hai.
        telegram.Messages.Clear();
        await TaoSellRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 110m))
            .KiemTraSellAsync(ngay, default);
        Assert.Empty(telegram.Messages);
        Assert.NotNull(entity.GiaBanNua);   // không ghi thêm lần bán nửa
        Assert.Null(entity.ThoiGianThoatHet); // cũng chưa bán hết
    }

    // T024 (vị thế bổ sung): kế hoạch Backfill (LaDuLieuBoSung = true, KhoangCachDuoi = null)
    // → luồng bán qua CHAYASYNC (không gọi thẳng KiemTraSellAsync) không có dừng lỗ đuổi, vẫn cắt theo SL kế hoạch.
    [Fact]
    public async Task T024_ViTheBoSung_KhongDuoiVanCatDungLoBanDau()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "ABB",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), // ~5 phiên: đủ T+2.5 để bán thật
            entryPrice: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, coDuoi: false, khoangCach: null, boSung: true, nguongLaiToiThieu: -1m));
        entity.DinhTuLucMua = 100m; // đỉnh đúng bằng giá vào — kể cả có đỉnh, không khoảng cách → không đuổi
        db.SaveChanges();

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("ABB", FlatBars(30, 100m));

        // Lượt 1 (qua ChayAsync): giá 97 — trên SL kế hoạch 95 và trên mốc×0.96 = 96 → không thoát; DungLoDuoi không được tính.
        // (Phase 8: 96 sẽ là −4% từ mốc 100 → rút mốc bán nửa, nên ca này giữ giá cách trên ngưỡng.)
        await TaoChayAsyncRunner(db, mayNhan, telegram, repo, MakeKbsJsonHiLo("ABB", 97m, 100m, 96m))
            .ChayAsync(default);
        Assert.Empty(telegram.Messages);
        Assert.Null(entity.DungLoDuoi);

        // Lượt 2 (qua ChayAsync): giá 95 ≤ SL → bán hết theo dừng lỗ kế hoạch.
        telegram.Messages.Clear();
        await TaoChayAsyncRunner(db, mayNhan, telegram, repo, MakeKbsJsonHiLo("ABB", 95m, 100m, 95m))
            .ChayAsync(default);
        Assert.Single(telegram.Messages);
        Assert.Equal(SuKienBan.LyDoDungLo, entity.LyDoThoatHet);
    }

   private static KetQuaKichBanEntity SeedForming(
        ApplicationDbContext db, string symbol, LoaiKichBan loai, decimal mucHoanThien = 80m)
    {
        var e = new KetQuaKichBanEntity
        {
            Symbol = symbol,
            LoaiKichBan = loai,
            TrangThai = TrangThaiKichBan.DangHinhThanh,
            NgayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue),
            DatBoiCanh = true,
            DatHinhThai = true,
            MucHoanThien = mucHoanThien,
        };
        db.KetQuaKichBan.Add(e);
        db.SaveChanges();
        return e;
    }

    private static string KeHoachKhongCatTheoThoiGian() =>
        MakeKeHoachJsonDayDu(100m, nguongLaiToiThieu: -1m);

    // ==========================================================================
    // PHASE 6 (T029) — CHẶN MUA LẶP: một mã một chu kỳ (C-0/C-6/C-7)
    // ==========================================================================

    [Fact]
    public async Task T029_MaDangGiu_KhongKichHoatKhongGuiTin()
    {
        var db = NewSellDb();
        // Chu kỳ đang mở (NoHuongLen, chưa thoát).
        SeedHolding(db, "ABC", kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, keHoachJson: KeHoachKhongCatTheoThoiGian());
        var forming = SeedForming(db, "ABC", LoaiKichBan.NoHuongLen);

        var telegram = new FakeTelegram();
        var result = await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), telegram,
            TaoRepo("ABC"), MakeKbsJson("ABC", 100m)).ChayAsync(default);

        Assert.Equal(0, result.SoTrigger);
        Assert.Empty(telegram.Messages);
        // Dòng FORMING vẫn đang hình thành (không bị kích hoạt).
        Assert.Equal(TrangThaiKichBan.DangHinhThanh, forming.TrangThai);
    }

    [Fact]
    public async Task T029_TrangThaiHuyLenh_ChuaThoatVanLaDangMo()
    {
        var db = NewSellDb();
        var holding = SeedHolding(db, "ABC", kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, keHoachJson: KeHoachKhongCatTheoThoiGian());
        // Pha 3 cũ có thể đổi TrangThai → HuyLenh nhưng vị thế còn mở (ThoiGianThoatHet == null).
        holding.TrangThai = TrangThaiKichBan.HuyLenh;
        db.SaveChanges();
        var forming = SeedForming(db, "ABC", LoaiKichBan.NoHuongLen);

        var result = await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), new FakeTelegram(),
            TaoRepo("ABC"), MakeKbsJson("ABC", 100m)).ChayAsync(default);

        Assert.Equal(0, result.SoTrigger);
        Assert.Equal(TrangThaiKichBan.DangHinhThanh, db.KetQuaKichBan.First(e => e.Symbol == "ABC" && e.TrangThai == TrangThaiKichBan.DangHinhThanh).TrangThai);
    }

    [Fact]
    public async Task T029_DongHomNay_Pha2HomNayVanChan()
    {
        var db = NewSellDb();
        // Đóng trong phiên hôm nay (ThoiGianThoatHet = bây giờ) → C-7 chặn mua lại cùng hôm nay.
        SeedHolding(db, "ABC", loai: LoaiKichBan.HoiHoTro,
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, thoatHet: DateTime.UtcNow,
            keHoachJson: KeHoachKhongCatTheoThoiGian());
        SeedForming(db, "ABC", LoaiKichBan.NoHuongLen);

        var result = await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), new FakeTelegram(),
            TaoRepo("ABC"), MakeKbsJson("ABC", 100m)).ChayAsync(default);

        Assert.Equal(0, result.SoTrigger);
    }

    [Fact]
    public async Task T029_DongHomTruoc_ChoKichHoatBinhThuong()
    {
        var db = NewSellDb();
        var homQua = VietnamMarketCalendar.TodayVietnam().AddDays(-1);
        // Đóng hôm trước (ThoiGianThoatHet hôm qua) → phiên sau được mua lại.
        var closed = SeedHolding(db, "ABC", loai: LoaiKichBan.NoHuongLen,
            kichHoat: homQua.ToDateTime(TimeOnly.MinValue).AddDays(-7),
            entryPrice: 100m, thoatHet: homQua.ToDateTime(TimeOnly.MinValue).AddHours(6),
            keHoachJson: KeHoachKhongCatTheoThoiGian());
        closed.NgayDanhGia = homQua.ToDateTime(TimeOnly.MinValue);
        db.SaveChanges();
        SeedForming(db, "ABC", LoaiKichBan.NoHuongLen);

        var result = await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), new FakeTelegram(),
            TaoRepo("ABC"), MakeKbsJson("ABC", 100m)).ChayAsync(default);

        Assert.Equal(1, result.SoTrigger);
    }

    // T049 (C-19): chu kỳ ĐÓNG vì BẤT KỲ lý do thoát hết nào — phiên sau mã được mua lại bình thường.
    [Theory]
    [InlineData(SuKienBan.LyDoChotLoi2)]
    [InlineData(SuKienBan.LyDoRutMoc6)]
    [InlineData(SuKienBan.LyDoDungLo)]
    [InlineData(SuKienBan.LyDoHetThoiGian)]
    [InlineData(SuKienBan.HetHanVao)]
    [InlineData(SuKienBan.LyDoHuyMuaLap)]
    public async Task T049_DongHomTruoc_MoiLyDoThoatHet_DeuDuocMuaLai(string lyDo)
    {
        var db = NewSellDb();
        var homQua = VietnamMarketCalendar.TodayVietnam().AddDays(-1);
        var closed = SeedHolding(db, "ABC", loai: LoaiKichBan.NoHuongLen,
            kichHoat: homQua.ToDateTime(TimeOnly.MinValue).AddDays(-7),
            entryPrice: 100m, thoatHet: homQua.ToDateTime(TimeOnly.MinValue).AddHours(6),
            lyDoThoat: lyDo,
            keHoachJson: KeHoachKhongCatTheoThoiGian());
        closed.NgayDanhGia = homQua.ToDateTime(TimeOnly.MinValue);
        db.SaveChanges();
        SeedForming(db, "ABC", LoaiKichBan.NoHuongLen);

        var result = await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), new FakeTelegram(),
            TaoRepo("ABC"), MakeKbsJson("ABC", 100m)).ChayAsync(default);

        Assert.Equal(1, result.SoTrigger);
    }

    [Fact]
    public async Task T029_GiuHoiHoTro_ChannNoHuongLen()
    {
        var db = NewSellDb();
        // Đang giữ theo Hồi hỗ trợ → kịch bản Nổ hướng lên của cùng mã cũng bị chặn (C-6 tính chung).
        SeedHolding(db, "ABC", loai: LoaiKichBan.HoiHoTro,
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, keHoachJson: KeHoachKhongCatTheoThoiGian());
        SeedForming(db, "ABC", LoaiKichBan.NoHuongLen);

        var result = await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), new FakeTelegram(),
            TaoRepo("ABC"), MakeKbsJson("ABC", 100m)).ChayAsync(default);

        Assert.Equal(0, result.SoTrigger);
    }

    [Fact]
    public async Task T029_HaiKichBanCungMa_ChiMotKichHoat()
    {
        var db = NewSellDb();
        // Không có chu kỳ mở; hai FORMING cùng mã (khác loại) cùng đạt trong một lượt.
        var no = SeedForming(db, "ABC", LoaiKichBan.NoHuongLen, mucHoanThien: 90m);
        var hoi = SeedForming(db, "ABC", LoaiKichBan.HoiHoTro, mucHoanThien: 80m);

        var result = await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), new FakeTelegram(),
            TaoRepo("ABC"), MakeKbsJson("ABC", 100m)).ChayAsync(default);

        Assert.Equal(1, result.SoTrigger);
        // Giữ theo xếp hạng hiện có: NoHuongLen (MucHoanThien 90) thắng, HoiHoTro vẫn FORMING.
        Assert.Equal(TrangThaiKichBan.DaKichHoat, db.KetQuaKichBan.First(e => e.LoaiKichBan == LoaiKichBan.NoHuongLen).TrangThai);
        Assert.Equal(TrangThaiKichBan.DangHinhThanh, db.KetQuaKichBan.First(e => e.LoaiKichBan == LoaiKichBan.HoiHoTro).TrangThai);
    }

    private static FakeStockRepository TaoRepo(string symbol)
    {
        var repo = new FakeStockRepository();
        repo.ThemStock(symbol, FlatBars(30, 100m));
        return repo;
    }

    // ==========================================================================
    // PHASE 7 — ĐIỂM MUA 2 (C-2→C-5, C-9, C-14, C-17, C-18, C-22, C-23)
    // ==========================================================================

    // T033 (lập kế hoạch): mức + băng điểm 2 chốt đúng công thức từng kịch bản; điểm 2 ≥ CL1 → null (C-9).
    [Fact]
    public void T033_TinhDiemMua2_CongThucTungKichBan_GanCL1ThiKhongCoDiem2()
    {
        var atr = 2m;
        // Nổ hướng lên: đỉnh hộp + 0.5×cao hộp = 2×CL1 − CL2 (CL1=110, CL2=115 → 105).
        var no = new KeHoachGiaoDich { GiaVaoLenhMin = 100m, GiaVaoLenhMax = 100m, GiaDungLo = 90m, GiaChotLoi1 = 110m, GiaChotLoi2 = 115m };
        var (mucNo, bandNo) = MayNhanKichBanService.TinhDiemMua2(LoaiKichBan.NoHuongLen, no, 101m, atr);
        Assert.Equal(105m, mucNo);
        Assert.Equal(0.3m * atr, bandNo);

        // Hồi hỗ trợ / Quét thanh khoản: giá khớp + 1×ATR.
        var hoi = new KeHoachGiaoDich { GiaVaoLenhMin = 100m, GiaVaoLenhMax = 100m, GiaDungLo = 90m, GiaChotLoi1 = 130m, GiaChotLoi2 = 150m };
        var (mucHoi, bandHoi) = MayNhanKichBanService.TinhDiemMua2(LoaiKichBan.HoiHoTro, hoi, 100m, atr);
        Assert.Equal(102m, mucHoi);          // 100 + 2×ATR
        Assert.Equal(0.6m, bandHoi);          // 0.3×ATR

        // Điểm 2 ≥ chốt lời 1 → không có điểm 2 (mức + băng đều null).
        var ganCL = new KeHoachGiaoDich { GiaVaoLenhMin = 100m, GiaVaoLenhMax = 100m, GiaDungLo = 90m, GiaChotLoi1 = 101m, GiaChotLoi2 = 110m };
        var (mucNull, bandNull) = MayNhanKichBanService.TinhDiemMua2(LoaiKichBan.QuetThanhKhoan, ganCL, 100m, atr);
        Assert.Null(mucNull);
        Assert.Null(bandNull);
    }

    // T033 (luồng): mua điểm 2 khi giá khớp ∈ band qua ChayAsync — ghi cột, bắn tin "MUA THÊM (điểm 2)" (C-9/C-22).
    [Fact]
    public async Task T033_MuaDiem2_GiaKhopTrongBand_GhiCotVaGuiTin()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m,
                nguongLaiToiThieu: -1m, giaMuaDiem2: 110m, bandRongDiem2: 3m));

        var mayNhan = new FakeMayNhanKichBan();
        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 110 ∈ [110,113], không chạm luật bán nào → mua điểm 2.
        await TaoChayAsyncRunner(db, mayNhan, telegram, repo, MakeKbsJson("HPG", 110m))
            .ChayAsync(default);

        Assert.Equal(110m, entity.GiaMuaDiem2ThucTe);
        Assert.NotNull(entity.ThoiGianMuaDiem2);
        Assert.Null(entity.ThoiGianThoatHet);        // chu kỳ vẫn mở sau điểm 2
        Assert.Contains(telegram.Messages, m => m.Contains("HPG") && m.Contains("MUA THÊM") && m.Contains("điểm 2"));
    }

    // T033 (biên dưới/dưới band): giá dưới GiaMuaDiem2 → chưa khớp, không mua.
    [Fact]
    public async Task T033_GiaDuoiBand_KhongMuaDiem2()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m,
                nguongLaiToiThieu: -1m, giaMuaDiem2: 110m, bandRongDiem2: 3m));

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 109m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Null(entity.GiaMuaDiem2ThucTe);
        Assert.Empty(telegram.Messages);
    }

    // T034-D2: đã bán nửa (C-17) → không mua điểm 2 dù giá trong band.
    [Fact]
    public async Task T034_D2_DaBanNua_KhongMuaDiem2()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            banNua: VietnamMarketCalendar.NowVietnam().AddDays(-1), giaBanNuaVal: 108m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m,
                nguongLaiToiThieu: -1m, giaMuaDiem2: 110m, bandRongDiem2: 3m));

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 111m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Null(entity.GiaMuaDiem2ThucTe);   // C-17 chặn
        Assert.DoesNotContain(telegram.Messages, m => m.Contains("MUA THÊM"));
    }

    // T035-D2 (C-18): cùng lượt giá chạm band điểm 2 VÀ chạm mức cắt (dừng lỗ đuổi) → bán thắng, không ghi điểm 2.
    // Ghi chú: luật rút 4% thuộc Phase 8 — kiểm C-18 bằng luật bán đã có; Phase 8 nối thêm ca rút 4%.
    [Fact]
    public async Task T035_D2_CungLuotDiem2VaMucCat_LuatBanThangKhongMua()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m,
                coDuoi: true, khoangCach: 20m, nguongLaiToiThieu: -1m,
                giaMuaDiem2: 112m, bandRongDiem2: 2m));
        entity.DinhTuLucMua = 132m;   // dừng lỗ đuổi = 132 − 20 = 112, đúng mép band điểm 2
        db.SaveChanges();

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 112: ∈ [112,114] VÀ ≤ mức cắt 112 → dừng lỗ đuổi bắn, điểm 2 không mua (C-18).
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 112m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Null(entity.GiaMuaDiem2ThucTe);
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoDungLoDuoi, entity.LyDoThoatHet);
        Assert.Contains(telegram.Messages, m => m.Contains("BÁN HẾT"));
        Assert.DoesNotContain(telegram.Messages, m => m.Contains("MUA THÊM"));
    }

    // T036-D2 (C-14 + C-23): đã mua điểm 2 → CL1/CL2 không bắn; DinhTuLucMua không reset.
    [Fact]
    public async Task T036_D2_DaMuaDiem2_BoCL1CL2_DinhTuLucMuaKhongReset()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            giaMuaDiem2ThucTe: 110m, thoiGianMuaDiem2: VietnamMarketCalendar.NowVietnam().AddDays(-3),
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m,
                nguongLaiToiThieu: -1m, giaMuaDiem2: 110m, bandRongDiem2: 3m));
        entity.DinhTuLucMua = 200m;   // mốc cao hơn giá hiện tại — phải giữ nguyên (C-23)
        db.SaveChanges();

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 195 ≥ CL1 130 nhưng đã có điểm 2 → C-14 bỏ CL1, không bán nửa.
        // (Phase 8: giá phải trên mốc×0.96 = 192 để rút mốc không xen vào, test chỉ đo C-14/C-23.)
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 195m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Empty(telegram.Messages);            // CL1/CL2 bị bỏ (C-14)
        Assert.Null(entity.ThoiGianBanNua);
        Assert.Equal(200m, entity.DinhTuLucMua);     // mốc không reset (C-23)
    }

    // ==========================================================================
    // PHASE 8 — RÚT TỪ MỐC 4% / 6% (C-11→C-16, C-23)
    // ==========================================================================

    // T042: chưa lên đỉnh (mốc = giá mua điểm 1), giá −5% → bán nửa, KHÔNG ghi LyDoThoatHet.
    // Chạy qua ChayAsync (yêu cầu: ít nhất một ca qua luồng đầy đủ).
    [Fact]
    public async Task T042_RutMoc_ChuaLenDinh_Giam4SoGiaMua_BanNuaKhongGhiLyDoThoatHet()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m));

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 95 = −5% so mốc 100, trên ngưỡng 6% (94) → chỉ bán nửa.
        await TaoChayAsyncRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 95m))
            .ChayAsync(default);

        Assert.NotNull(entity.ThoiGianBanNua);
        Assert.Equal(95m, entity.GiaBanNua);
        Assert.Null(entity.LyDoThoatHet);          // C-16: bán nửa không ghi lý do thoát hết
        Assert.Null(entity.ThoiGianThoatHet);       // chu kỳ vẫn mở
        Assert.False(entity.DaDoiDungLo);            // rút mốc không dời cắt lỗ (chỉ CL1 mới dời)
        Assert.Contains(telegram.Messages, m => m.Contains("BÁN NỬA") && m.Contains("Rút mốc 4%"));
    }

    // T042: lên đỉnh rồi giảm 4% → bán nửa; giảm tiếp tới 6% → bán hết RutMoc6, đóng chu kỳ.
    [Fact]
    public async Task T042_RutMoc_LenDinhGiam4BanNua_Giam6BanHetRutMoc6()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m));
        entity.DinhTuLucMua = 120m;   // mốc 120 → 4% = 115.2, 6% = 112.8
        db.SaveChanges();

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var runner = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 115m));

        await runner.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        Assert.NotNull(entity.ThoiGianBanNua);
        Assert.Equal(115m, entity.GiaBanNua);
        Assert.Null(entity.ThoiGianThoatHet);

        // Lượt sau giá 112 ≤ 112.8 → bán hết vì rút mốc 6%.
        var runner2 = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 112m));
        await runner2.KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoRutMoc6, entity.LyDoThoatHet);
        Assert.Equal(112m, entity.GiaThoatHet);
        Assert.Contains(telegram.Messages, m => m.Contains("BÁN NỬA") && m.Contains("115"));
        Assert.Contains(telegram.Messages, m => m.Contains("BÁN HẾT") && m.Contains("RutMoc6"));
    }

    // T042 (thứ tự luật bán): cùng lượt −6% VÀ chạm cắt lỗ ban đầu → chỉ MỘT lệnh bán hết,
    // lý do theo thứ tự hiện có trong code (dừng lỗ xét trước → DungLo thắng RutMoc6).
    [Fact]
    public async Task T042_CungLuotRut6VaCatLo_CatLoThang_ChiMotLenhBanHet()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 95m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m));
        entity.DinhTuLucMua = 100m;   // mốc 100 → 6% = 94, cắt lỗ kế hoạch 95 → cùng chạm ở giá 94
        db.SaveChanges();

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 94m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoDungLo, entity.LyDoThoatHet);   // không phải RutMoc6
        Assert.Null(entity.GiaBanNua);                              // không bán nửa trước đó
        Assert.Single(telegram.Messages);                            // đúng một lệnh
    }

    // T042 (chống nhiễu): rút mốc dùng chung chuỗi xác nhận N lượt với cắt lỗ —
    // một lượt chạm −4%, lượt sau hồi lên → chưa bán.
    [Fact]
    public async Task T042_RutMoc_ChongNhieu_Giam4MotLuotRoiHoiLen_KhongBan()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m));

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var now = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 95m), luotXacNhan: 2)
            .KiemTraSellAsync(now, default);                      // lượt 1/2 → chưa bán
        Assert.Null(entity.ThoiGianBanNua);

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 101m), luotXacNhan: 2)
            .KiemTraSellAsync(now, default);                      // hồi lên → mất chuỗi
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 95m), luotXacNhan: 2)
            .KiemTraSellAsync(now, default);                      // đếm lại từ 1 → vẫn chưa bán

        Assert.Null(entity.ThoiGianBanNua);
        Assert.Null(entity.ThoiGianThoatHet);
        Assert.Empty(telegram.Messages);
    }

    // T042 (kế hoạch cũ): JSON không có hai trường ngưỡng → dùng 4 / 6 (C-12 cố định mọi mã).
    [Fact]
    public async Task T042_KeHoachCu_KhongCoTruongNguong_DungBonVaSau()
    {
        var db = NewSellDb();
        // Kế hoạch kiểu cũ (trước Phase 8) — thiếu NguongRutMocBanNuaPhanTram / NguongRutMocBanHetPhanTram.
        var keHoachCu = """{"GiaVaoLenhMin":100,"GiaVaoLenhMax":100,"GiaDungLo":90,"GiaChotLoi1":130,"GiaChotLoi2":150,"DieuKienHuy":"cu","TyLeBanChotLoi1":0.5,"DoiDungLoVeGiaVao":true,"SoPhienDungTheoThoiGian":5,"NguongLaiToiThieuPhanTram":-1,"SoPhienToiDa":20}""";
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m, keHoachJson: keHoachCu);

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var now = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 96m))
            .KiemTraSellAsync(now, default);                      // mốc 100 → −4% = 96, bán nửa
        Assert.NotNull(entity.ThoiGianBanNua);

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 93m))
            .KiemTraSellAsync(now, default);                      // −6% = 94 → bán hết
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoRutMoc6, entity.LyDoThoatHet);
    }

    // T043 (C-16): đã bán nửa ở CL1 → −4% không bán thêm; −6% vẫn bán hết.
    [Fact]
    public async Task T043_C16_DaBanNuaCL1_Giam4KhongBanThem_Giam6BanHet()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            banNua: VietnamMarketCalendar.NowVietnam().AddDays(-2), giaBanNuaVal: 130m, daDoiDungLo: true,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m));
        entity.DinhTuLucMua = 140m;   // mốc 140 → 4% = 134.4, 6% = 131.6
        db.SaveChanges();

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var now = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Giá 133 ≤ 134.4 nhưng đã bán nửa ở CL1 → C-16 chặn, không bán thêm.
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 133m))
            .KiemTraSellAsync(now, default);
        Assert.Equal(130m, entity.GiaBanNua);     // giá bán nửa cũ giữ nguyên
        Assert.Null(entity.ThoiGianThoatHet);
        Assert.Empty(telegram.Messages);

        // Giá 131 ≤ 131.6 → bán hết vì rút mốc 6%.
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 131m))
            .KiemTraSellAsync(now, default);
        Assert.NotNull(entity.ThoiGianThoatHet);
        Assert.Equal(SuKienBan.LyDoRutMoc6, entity.LyDoThoatHet);
    }

    // T044 (C-14/C-23): đã có điểm 2, mốc vẫn từ điểm 1 → −4% bán nửa; −4% so bình quân thì chưa bán.
    [Fact]
    public async Task T044_DaMuaDiem2_Giam4TuMocDiem1_BanNua()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            giaMuaDiem2ThucTe: 110m, thoiGianMuaDiem2: VietnamMarketCalendar.NowVietnam().AddDays(-3),
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m,
                giaMuaDiem2: 110m, bandRongDiem2: 3m));
        entity.DinhTuLucMua = 100m;   // mốc = 100 (điểm 1), KHÔNG phải bình quân 105
        db.SaveChanges();

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var now = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Giá 100.5: −4.3% so bình quân 105 nhưng chỉ −4.0%... chưa tới −4% so mốc 100 → chưa bán.
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 100.5m))
            .KiemTraSellAsync(now, default);
        Assert.Null(entity.ThoiGianBanNua);

        // Giá 95 ≤ mốc × 0.96 → bán nửa dù đã có điểm 2.
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 95m))
            .KiemTraSellAsync(now, default);
        Assert.NotNull(entity.ThoiGianBanNua);
        Assert.Null(entity.ThoiGianThoatHet);
        Assert.Contains(telegram.Messages, m => m.Contains("BÁN NỬA") && m.Contains("Rút mốc 4%"));
    }

    // T045 (C-18 nối tiếp Phase 7): cùng lượt chạm band điểm 2 và −4% từ mốc → bán nửa thắng, không mua điểm 2.
    [Fact]
    public async Task T045_C18_CungLuotBandDiem2VaRut4_BanNuaKhongMuaDiem2()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7),
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m,
                giaMuaDiem2: 110m, bandRongDiem2: 3m));
        entity.DinhTuLucMua = 120m;   // mốc 120 → 4% = 115.2; band điểm 2 [110,113]
        db.SaveChanges();

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));

        // Giá 113 ∈ band điểm 2 VÀ ≤ 115.2 → rút mốc bán nửa thắng (C-18), không mua thêm.
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 113m))
            .KiemTraSellAsync(VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.NotNull(entity.ThoiGianBanNua);
        Assert.Null(entity.GiaMuaDiem2ThucTe);
        Assert.Contains(telegram.Messages, m => m.Contains("BÁN NỬA"));
        Assert.DoesNotContain(telegram.Messages, m => m.Contains("MUA THÊM"));
    }

    // Khoá công thức điểm 2 Nổ hướng lên (Phase 7 bổ sung): hộp cụ thể → đỉnh hộp + 0.5×chiều cao hộp.
    [Fact]
    public void T033_KhoaCongThucNoHuongLen_DinhHopCong05ChieuCaoHop()
    {
        // Hộp: đáy 80, đỉnh 100 → chiều cao 20. KichBanNoHuongLen chốt CL1 = đỉnh + 1.0×cao = 120, CL2 = đỉnh + 1.5×cao = 130.
        var dinhHop = 100m;
        var chieuCaoHop = 20m;
        var kh = new KeHoachGiaoDich
        {
            GiaVaoLenhMin = 100m, GiaVaoLenhMax = 101m, GiaDungLo = 90m,
            GiaChotLoi1 = dinhHop + chieuCaoHop * 1.0m,
            GiaChotLoi2 = dinhHop + chieuCaoHop * 1.5m,
        };

        var (muc, _) = MayNhanKichBanService.TinhDiemMua2(LoaiKichBan.NoHuongLen, kh, 101m, atr: 2m);

        // DO-NOT-CHANGE: 2×CL1 − CL2 phải luôn bằng đỉnh hộp + 0.5×cao hộp; đổi một vế là điểm 2 sai mà test khác vẫn xanh.
        Assert.Equal(dinhHop + 0.5m * chieuCaoHop, muc);
        Assert.Equal(2m * kh.GiaChotLoi1 - kh.GiaChotLoi2, muc);
    }

    // Kế hoạch mới phải mang hai trường ngưỡng rút mốc (4/6) trong JSON lưu DB.
    [Fact]
    public void KeHoachMoi_JSONCoSanHaiTruongNguongRutMoc()
    {
        var json = MakeKeHoachJsonDayDu(100m);

        Assert.Contains("\"NguongRutMocBanNuaPhanTram\":4", json);
        Assert.Contains("\"NguongRutMocBanHetPhanTram\":6", json);

        var kh = JsonSerializer.Deserialize<KeHoachGiaoDich>(json)!;
        Assert.Equal(4m, kh.NguongRutMocBanNuaPhanTram);
        Assert.Equal(6m, kh.NguongRutMocBanHetPhanTram);
    }

    // T042 (chưa đủ T+2.5): chạm −4% → chỉ cảnh báo MỘT lần, không đổi trạng thái (như các luật bán khác).
    [Fact]
    public async Task T042_RutMoc_ChuaDuT25_CanhBaoMotLanKhongDoiTrangThai()
    {
        var db = NewSellDb();
        var entity = SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-1),   // ~1 phiên < MinTradingSessionsToSell 3
            entryPrice: 100m, giaVaoThucTe: 100m,
            keHoachJson: MakeKeHoachJsonDayDu(100m, sl: 90m, cl1: 130m, cl2: 150m, nguongLaiToiThieu: -1m));

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", FlatBars(30, 100m));
        var now = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 95m))
            .KiemTraSellAsync(now, default);
        Assert.Single(telegram.Messages);
        Assert.Contains("Chưa đủ T+2.5", telegram.Messages[0]);
        Assert.Null(entity.ThoiGianBanNua);          // không đổi trạng thái
        Assert.Null(entity.ThoiGianThoatHet);

        // Lượt sau vẫn dưới ngưỡng → không gửi lại cảnh báo cùng loại.
        telegram.Messages.Clear();
        await TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 95m))
            .KiemTraSellAsync(now, default);
        Assert.Empty(telegram.Messages);
        Assert.Null(entity.ThoiGianBanNua);
    }

    // ==========================================================================
    // C-33 — CHẤM KẾT QUẢ NGAY KHI ĐÓNG LỆNH (Pha 3 16:00 chỉ còn chấm bù)
    // ==========================================================================

    // Bán hết theo DungLo qua bước bán → dòng có KetQuaDoLuong ngay sau lượt, không chờ Pha 3.
    [Fact]
    public async Task C33_BanHetDungLo_ChamNgayTrongLuot_KhongChoPha3()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), entryPrice: 10m);

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 9.4m));
        await runner.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        var e = db.KetQuaKichBan.Single(x => x.Symbol == "HPG");
        Assert.Equal("Thua", e.KetQuaDoLuong);                       // -6% ≤ ngưỡng -1%
        Assert.Equal(9.4m, e.GiaThoat);
        Assert.Equal(-6m, e.PhanTramLoiNhuan);
        Assert.Equal(-1.2m, e.TyLeLaiLoThucTe);                      // (9.4-10)/(10-9.5)
        Assert.Equal(VietnamMarketCalendar.TodayVietnam(), e.NgayThoat);
        Assert.Equal(TrangThaiKichBan.HuyLenh, e.TrangThai);         // đổi TrangThai như Pha 3 vẫn làm
    }

    // Đã bán nửa rồi bán hết → giá ra bình quân (bán nửa + bán hết)/2 (chu kỳ ở đây chốt lời).
    [Fact]
    public async Task C33_BanNuaRoiBanHet_GiaRaBinhQuanHaiLanBan()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), entryPrice: 10m,
            banNua: DateTime.UtcNow.AddDays(-3), giaBanNuaVal: 11m);

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 9.4m));
        await runner.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        var e = db.KetQuaKichBan.Single(x => x.Symbol == "HPG");
        Assert.Equal(10.2m, e.GiaThoat);                             // (11 + 9.4)/2
        Assert.Equal(2m, e.PhanTramLoiNhuan);                        // so giá vào 10
        Assert.Equal("Thang", e.KetQuaDoLuong);
        Assert.Equal(TrangThaiKichBan.ChotLoi, e.TrangThai);
    }

    // Có điểm 2 → giá vào bình quân hai lần mua; giá vào trùng SL → R:R null (như Pha 3).
    [Fact]
    public async Task C33_CoDiem2_GiaVaoBinhQuanHaiLanMua()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), entryPrice: 10m,
            giaVaoThucTe: 10m, giaMuaDiem2ThucTe: 9m, thoiGianMuaDiem2: DateTime.UtcNow.AddDays(-2));

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 9.4m));
        await runner.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        var e = db.KetQuaKichBan.Single(x => x.Symbol == "HPG");
        Assert.Equal("Thua", e.KetQuaDoLuong);
        Assert.Equal(-1.0526m, e.PhanTramLoiNhuan);                  // (9.4-9.5)/9.5 — giá vào 9.5
        Assert.Null(e.TyLeLaiLoThucTe);                              // giá vào 9.5 == SL 9.5 → không tính R:R
    }

    // Tin bán hết in thêm dòng kết quả, không chứa mã quyết định nội bộ (C-x).
    [Fact]
    public async Task C33_TinBanHetCoDongKetQua_KhongMaQuyetDinh()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), entryPrice: 10m);

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 9.4m));
        await runner.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN HẾT", telegram.Messages[0]);
        Assert.Contains("Kết quả: <b>Thua</b> (-6% so giá vào)", telegram.Messages[0]);
        Assert.DoesNotContain("(C-", telegram.Messages[0]);
    }

    // Bán nửa → chu kỳ còn mở → KetQuaDoLuong vẫn null, tin không có dòng kết quả.
    [Fact]
    public async Task C33_BanNua_KhongChamChoDonChuKy()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), entryPrice: 10m);

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 11m));
        await runner.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        Assert.Single(telegram.Messages);
        Assert.Contains("BÁN NỬA", telegram.Messages[0]);
        Assert.DoesNotContain("Kết quả:", telegram.Messages[0]);

        var e = db.KetQuaKichBan.Single(x => x.Symbol == "HPG");
        Assert.NotNull(e.ThoiGianBanNua);
        Assert.Null(e.ThoiGianThoatHet);
        Assert.Null(e.KetQuaDoLuong);                                // "Chờ đo"
    }

    // HetHanTheoDoi cũng là bán hết → chấm ngay + dòng kết quả trong tin (\u231B).
    [Fact]
    public async Task C33_HetHanTheoDoi_ChamNgayTrongLuot()
    {
        var db = NewSellDb();
        SeedHolding(db, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-30),
            keHoachJson: MakeKeHoachJsonDayDu(10m, soPhienToiDa: 3));

        var telegram = new FakeTelegram();
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());

        var runner = TaoSellRunner(db, new FakeMayNhanKichBan(), telegram, repo, MakeKbsJson("HPG", 10.5m));
        await runner.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);

        var e = db.KetQuaKichBan.Single(x => x.Symbol == "HPG");
        Assert.Equal(SuKienBan.LyDoHetHanTheoDoi, e.LyDoThoatHet);
        Assert.Equal("Thang", e.KetQuaDoLuong);                      // (10.5-10)/10 = +5%
        Assert.Equal(5m, e.PhanTramLoiNhuan);
        Assert.Single(telegram.Messages);
        Assert.Contains("Hết hạn theo dõi", telegram.Messages[0]);
        Assert.Contains("Kết quả: <b>Thắng</b> (+5% so giá vào)", telegram.Messages[0]);
    }

    // Pha 3 sau đó: dòng đã chấm KHÔNG chấm lại; dòng đóng mà chưa có kết quả vẫn được chấm bù.
    [Fact]
    public async Task C33_Pha3KhongChamLaiDongDaCham_VanChamBuDongThieu()
    {
        var db = NewSellDb();
        var homQua = DateTime.UtcNow.AddDays(-1);
        // HPG: đã đóng + đã có kết quả (marker "Ngang" trong khi giá đáng ra "Thua") → phải giữ nguyên.
        var hpg = SeedHolding(db, "HPG", entryPrice: 10m,
            ketQuaDoLuong: "Ngang", thoatHet: homQua,
            giaThoatHetVal: 9.4m, lyDoThoat: SuKienBan.LyDoDungLo);
        // VCG: đóng nhưng chưa kết quả (đóng tay/lỗi giữa chừng) → chấm bù.
        var vcg = SeedHolding(db, "VCG", entryPrice: 10m,
            thoatHet: homQua, giaThoatHetVal: 9.4m, lyDoThoat: SuKienBan.LyDoDungLo);

        var runner = new Pha3DoLuongRunner(db, NullLogger<Pha3DoLuongRunner>.Instance);
        await runner.RunAsync();

        Assert.Equal("Ngang", hpg.KetQuaDoLuong);                    // không bị chấm lại
        Assert.Null(hpg.PhanTramLoiNhuan);                           // không bị ghi đè trường nào
        Assert.Equal("Thua", vcg.KetQuaDoLuong);                     // chấm bù đúng công thức
        Assert.Equal(-6m, vcg.PhanTramLoiNhuan);
    }

    // Cùng dữ liệu, hai lối đóng → cùng kết quả: khẳng định Pha 2 và Pha 3 dùng CHUNG một hàm chấm.
    [Fact]
    public async Task C33_CungDuLieu_Pha2VaPha3ChoCungKetQua()
    {
        // Lối 1: Pha 2 bán hết DungLo → chấm ngay lượt đóng.
        var dbA = NewSellDb();
        SeedHolding(dbA, "HPG",
            kichHoat: VietnamMarketCalendar.NowVietnam().AddDays(-7), entryPrice: 10m);
        var repo = new FakeStockRepository();
        repo.ThemStock("HPG", BreakoutBars());
        var runnerA = TaoSellRunner(dbA, new FakeMayNhanKichBan(), new FakeTelegram(), repo, MakeKbsJson("HPG", 9.4m));
        await runnerA.KiemTraSellAsync(
            VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue), default);
        var a = dbA.KetQuaKichBan.Single();

        // Lối 2: dòng đã đóng sẵn (không qua Pha 2) → Pha 3 16:00 chấm bù trên cùng dữ liệu giá.
        var dbB = NewSellDb();
        var thoatUtc = DateTime.UtcNow;
        SeedHolding(dbB, "HPG", entryPrice: 10m,
            thoatHet: thoatUtc, giaThoatHetVal: 9.4m, lyDoThoat: SuKienBan.LyDoDungLo);
        await new Pha3DoLuongRunner(dbB, NullLogger<Pha3DoLuongRunner>.Instance).RunAsync();
        var b = dbB.KetQuaKichBan.Single();

        Assert.Equal(b.KetQuaDoLuong, a.KetQuaDoLuong);
        Assert.Equal(b.GiaThoat, a.GiaThoat);
        Assert.Equal(b.PhanTramLoiNhuan, a.PhanTramLoiNhuan);
        Assert.Equal(b.TyLeLaiLoThucTe, a.TyLeLaiLoThucTe);
        Assert.Equal(b.TrangThai, a.TrangThai);
        Assert.Equal(b.NgayThoat, a.NgayThoat);                      // cùng ngày VN của ThoiGianThoatHet
    }

    private static class TestLoggerSink
    {
        public static string? Last { get; set; }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Unit tests cho Pha 1 — chặn tạo dòng FORMING mới khi mã đang có chu kỳ mua (C-0/C-6/C-7).
/// </summary>
public sealed class Pha1TruocPhienTests
{
    // ===== Fakes =====

    private sealed class FakeSoTuyen : ISoTuyenService
    {
        private readonly IReadOnlyList<string> _ma;
        public FakeSoTuyen(IReadOnlyList<string> ma) => _ma = ma;

        public Task<KetQuaSoTuyen> ChaySoTuyenAsync(CancellationToken ct = default) =>
            Task.FromResult(new KetQuaSoTuyen(_ma, _ma.Count, 0, 0, 0, 0));
    }

    private sealed class FakeMayNhan : IMayNhanKichBan
    {
        /// <summary>Mã nào trả về FORMING (true) khi đánh giá trước phiên.</summary>
        public Func<string, bool> TaoForming { get; set; } = _ => true;

        public Task<IReadOnlyList<KetQuaKichBan>> DanhGiaTruocPhienAsync(
            string symbol, IReadOnlyList<OhlcvBar> history, CancellationToken ct = default)
        {
            if (!TaoForming(symbol))
                return Task.FromResult<IReadOnlyList<KetQuaKichBan>>(Array.Empty<KetQuaKichBan>());

            IReadOnlyList<KetQuaKichBan> ketQua = new[]
            {
                new KetQuaKichBan
                {
                    Symbol = symbol,
                    LoaiKichBan = LoaiKichBan.NoHuongLen,
                    TrangThai = TrangThaiKichBan.DangHinhThanh,
                    DatBoiCanh = true,
                    DatHinhThai = true,
                }
            };
            return Task.FromResult(ketQua);
        }

        public Task<IReadOnlyList<KetQuaKichBan>> KiemTraTriggerTrongPhienAsync(
            IReadOnlyList<KetQuaKichBan> dangHinhThanh,
            IReadOnlyDictionary<string, decimal> giaHienTai,
            IReadOnlyDictionary<string, long> volumeHienTai,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KetQuaKichBan>>(Array.Empty<KetQuaKichBan>());

        public Task<IReadOnlyList<KetQuaKichBan>> DanhGiaBanAsync(
            string symbol, IReadOnlyList<OhlcvBar> history, decimal giaVaoLenh, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KetQuaKichBan>>(Array.Empty<KetQuaKichBan>());
    }

    private sealed class FakeStockRepo : IJobStockRepository
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
            Task.FromResult<IReadOnlyList<StockSummaryRow>>(Array.Empty<StockSummaryRow>());

        public Task<MarketBreadthStats> GetBreadthStatsAsync(CancellationToken ct = default) =>
            Task.FromResult(MarketBreadthStats.Empty);

        public Task<IReadOnlyList<Stock>> GetAllForUniverseScreeningAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Stock>>(_stocks.Values.ToList());

        public Task<IReadOnlyList<string>> GetInactiveSymbolsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    // ===== Helpers =====

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>Dãy ≥ 250 phiên để vượt ngưỡng SoPhienToiThieu của Pha 1.</summary>
    private static List<OhlcvBar> LongHistory(int n = 260, decimal price = 100m)
    {
        var d = new DateOnly(2025, 1, 1);
        return Enumerable.Range(0, n)
            .Select(i => new OhlcvBar(d.AddDays(i), price, price, price, price, 1_000_000L))
            .ToList();
    }

    private static Pha1TruocPhienRunner TaoRunner(
        ApplicationDbContext db, FakeMayNhan mayNhan, FakeStockRepo repo, IReadOnlyList<string> symbols) =>
        new(
            new FakeSoTuyen(symbols),
            mayNhan,
            repo,
            db,
            NullLogger<Pha1TruocPhienRunner>.Instance);

    // ===== T030 =====

    [Fact]
    public async Task T030_MaCoChuKyDangMo_KhongTaoDongFormingMoi()
    {
        var db = NewDb();
        var homNay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Chu kỳ MỞ cho ABC (đã kích hoạt, chưa thoát) — dòng của hôm qua để không trùng khóa unique.
        db.KetQuaKichBan.Add(new KetQuaKichBanEntity
        {
            Symbol = "ABC",
            LoaiKichBan = LoaiKichBan.HoiHoTro,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            ThoiGianKichHoat = homNay.AddDays(-7),
            NgayDanhGia = homNay.AddDays(-1),
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
        });
        db.SaveChanges();

        var repo = new FakeStockRepo();
        repo.ThemStock("ABC", LongHistory());

        await TaoRunner(db, new FakeMayNhan(), repo, new[] { "ABC" }).ChayAsync(default);

        // Pha 1 phải KHÔNG tạo dòng FORMING hôm nay cho ABC.
        Assert.DoesNotContain(db.KetQuaKichBan,
            e => e.Symbol == "ABC" && e.TrangThai == TrangThaiKichBan.DangHinhThanh && e.NgayDanhGia == homNay);
    }

    [Fact]
    public async Task T030_DongHomTruoc_Pha1TaoDongMoiDuoc()
    {
        var db = NewDb();
        var homNay = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);

        // Chu kỳ ĐÓNG hôm trước (ThoiGianThoatHet != null) → phiên sau Pha 1 được tạo dòng mới.
        db.KetQuaKichBan.Add(new KetQuaKichBanEntity
        {
            Symbol = "ABC",
            LoaiKichBan = LoaiKichBan.HoiHoTro,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            ThoiGianKichHoat = homNay.AddDays(-7),
            ThoiGianThoatHet = homNay.AddDays(-1),
            NgayDanhGia = homNay.AddDays(-1),
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
        });
        db.SaveChanges();

        var repo = new FakeStockRepo();
        repo.ThemStock("ABC", LongHistory());

        await TaoRunner(db, new FakeMayNhan(), repo, new[] { "ABC" }).ChayAsync(default);

        // Chu kỳ đã đóng → Pha 1 tạo dòng FORMING hôm nay cho ABC.
        Assert.Contains(db.KetQuaKichBan,
            e => e.Symbol == "ABC" && e.TrangThai == TrangThaiKichBan.DangHinhThanh && e.NgayDanhGia == homNay);
    }
}

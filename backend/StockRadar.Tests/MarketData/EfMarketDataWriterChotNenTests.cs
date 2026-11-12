using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.MarketData;

/// <summary>
/// Spec 018 FR-004 (T009) — bộ ghi từ chối tạo nến ngày hôm nay ngoài khung KBS sync,
/// trừ khi có cờ sau đóng cửa và đã qua 14:45 của ngày giao dịch. Realtime và bộ nhớ đệm
/// nằm ở MarketSyncService trước bước ghi nên vẫn chạy bình thường khi bộ ghi bỏ qua.
/// </summary>
public sealed class EfMarketDataWriterChotNenTests
{
    // 2026-11-12 là thứ Năm — ngày giao dịch, không nghỉ trong VietnamHolidays. Việt Nam UTC+7, không DST.
    private static DateTimeOffset ToUtc(int gioVN, int phutVN) =>
        new(new DateTime(2026, 11, 12, gioVN, phutVN, 0), TimeSpan.FromHours(7));

    private sealed class DongHoCoDinh(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }

    private static async Task<(ApplicationDbContext Db, EfMarketDataWriter Writer)> TaoBoGhiAsync(
        int gioVN, int phutVN)
    {
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Stocks.Add(new StockEntity { Symbol = "HPG", Name = "HPG", HistoryJson = "[]" });
        await db.SaveChangesAsync();

        var writer = new EfMarketDataWriter(
            db,
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            new DongHoCoDinh(ToUtc(gioVN, phutVN)));
        return (db, writer);
    }

    private static StockQuoteSyncDto QuoteHPG() =>
        new("HPG", null, 15m, 16m, 14m, 15m, 1_000, 0m);

    [Fact]
    public async Task BanDem_NgoaiKhung_KhongCoCua_KhongGhi()
    {
        var (db, writer) = await TaoBoGhiAsync(gioVN: 3, phutVN: 0);

        var updated = await writer.UpsertQuotesAsync([QuoteHPG()], choPhepSauDongCua: false);

        Assert.Equal(0, updated);
        var entity = await db.Stocks.AsNoTracking().SingleAsync(s => s.Symbol == "HPG");
        Assert.Equal("[]", entity.HistoryJson);
    }

    [Fact]
    public async Task BanDem_CoCuaSauDongCua_VanKhongGhi_ViTruoc1445()
    {
        var (db, writer) = await TaoBoGhiAsync(gioVN: 3, phutVN: 0);

        var updated = await writer.UpsertQuotesAsync([QuoteHPG()], choPhepSauDongCua: true);

        Assert.Equal(0, updated);
        var entity = await db.Stocks.AsNoTracking().SingleAsync(s => s.Symbol == "HPG");
        Assert.Equal("[]", entity.HistoryJson);
    }

    [Fact]
    public async Task TrongKhung_GhiNenHomNay()
    {
        var (db, writer) = await TaoBoGhiAsync(gioVN: 10, phutVN: 0);

        var updated = await writer.UpsertQuotesAsync([QuoteHPG()], choPhepSauDongCua: false);

        Assert.Equal(1, updated);
        var entity = await db.Stocks.AsNoTracking().SingleAsync(s => s.Symbol == "HPG");
        Assert.Contains("2026-11-12", entity.HistoryJson);
    }

    [Fact]
    public async Task Luc1515_CoCuaSauDongCua_GhiDuoc()
    {
        var (db, writer) = await TaoBoGhiAsync(gioVN: 15, phutVN: 15);

        var updated = await writer.UpsertQuotesAsync([QuoteHPG()], choPhepSauDongCua: true);

        Assert.Equal(1, updated);
        var entity = await db.Stocks.AsNoTracking().SingleAsync(s => s.Symbol == "HPG");
        Assert.Contains("2026-11-12", entity.HistoryJson);
    }

    [Fact]
    public async Task NgoaiKhungBoGhiBoQua_RealtimeVanDuocDay()
    {
        var writer = new BoGhiChotNgay();
        var publisher = new NguoiDayGia();
        var cache = new BoNhoGia();
        var sync = new MarketSyncService(writer, publisher, cache);

        var result = await sync.ApplyAsync(new MarketSyncRequest(null, [QuoteHPG()], SauDongCua: false));

        Assert.Equal(0, result.StocksUpdated);
        Assert.Single(publisher.CacTickDaDay);
        Assert.Single(cache.CacTickDaLuu);
    }

    /// <summary>Bộ ghi giả mô phỏng hành vi chặn ngoài khung: luôn trả 0 mã đã ghi.</summary>
    private sealed class BoGhiChotNgay : IMarketDataWriter
    {
        public Task<int> UpsertQuotesAsync(
            IReadOnlyList<StockQuoteSyncDto> quotes,
            bool choPhepSauDongCua,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task UpsertIndexAsync(
            MarketIndexSyncDto index,
            bool choPhepSauDongCua,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> UpsertStockHistoryAsync(
            string symbol, string? name, string? sector,
            IReadOnlyList<OhlcvBar> bars, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpsertUniverseStockAsync(UniverseStockUpsert upsert, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkUniverseInactiveAsync(
            string symbol, string reason, DateTime updatedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkUniverseActiveAsync(
            string symbol, decimal avgVolume, DateTime updatedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetTradingRestrictedAsync(
            string symbol, bool restricted, string? status, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeactivateUniverseExceptAsync(
            IReadOnlyCollection<string> activeSymbols, DateTime updatedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NguoiDayGia : IMarketRealtimePublisher
    {
        public List<QuoteTickDto> CacTickDaDay { get; } = [];

        public Task PublishQuotesAsync(IReadOnlyList<QuoteTickDto> quotes, CancellationToken cancellationToken = default)
        {
            CacTickDaDay.AddRange(quotes);
            return Task.CompletedTask;
        }

        public Task PublishIndexAsync(IndexTickDto index, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class BoNhoGia : IQuoteTickCache
    {
        public List<QuoteTickDto> CacTickDaLuu { get; } = [];

        public IReadOnlyList<QuoteTickDto> GetQuotes() => CacTickDaLuu;

        public QuoteTickDto? GetQuote(string symbol) =>
            CacTickDaLuu.FirstOrDefault(t => t.Symbol == symbol);

        public IndexTickDto? GetIndex() => null;

        public void SetQuotes(IReadOnlyList<QuoteTickDto> quotes) => CacTickDaLuu.AddRange(quotes);

        public void SetIndex(IndexTickDto? index)
        {
        }
    }
}

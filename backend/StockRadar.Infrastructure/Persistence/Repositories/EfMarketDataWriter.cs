using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence.Caching;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfMarketDataWriter(ApplicationDbContext db, IMemoryCache cache) : IMarketDataWriter
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    /// <summary>Job 2: ghép nến phiên ngày T — không cắt lịch sử Job 1.</summary>
    public async Task<int> UpsertQuotesAsync(
        IReadOnlyList<StockQuoteSyncDto> quotes,
        CancellationToken cancellationToken = default)
    {
        var sessionDate = TodayVietnam();
        var updated = 0;

        foreach (var quote in quotes)
        {
            if (string.IsNullOrWhiteSpace(quote.Symbol) || quote.Close <= 0)
                continue;

            var symbol = quote.Symbol.Trim().ToUpperInvariant();
            var entity = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol, cancellationToken);
            var bar = new OhlcvBar(
                sessionDate,
                quote.Open > 0 ? quote.Open : quote.Close,
                quote.High > 0 ? quote.High : quote.Close,
                quote.Low > 0 ? quote.Low : quote.Close,
                quote.Close,
                Math.Max(0, quote.Volume));

            if (entity is null)
                continue;

            var history = DeserializeHistory(entity.HistoryJson);
            entity.HistoryJson = SerializeHistory(MergeBars(history, bar));
            entity.LastChangePercent = quote.ChangePercent;
            if (!string.IsNullOrWhiteSpace(quote.Name))
                entity.Name = quote.Name.Trim();
            if (!string.IsNullOrWhiteSpace(quote.Sector) && !entity.SectorLocked)
                entity.Sector = quote.Sector.Trim();

            updated++;
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            CacheInvalidation.InvalidateMarketData(cache);
        }

        return updated;
    }

    /// <summary>Job 1: ghi toàn bộ lịch sử 2000-01-01 → T-1.</summary>
    public async Task<int> UpsertStockHistoryAsync(
        string symbol,
        string? name,
        string? sector,
        IReadOnlyList<OhlcvBar> bars,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol) || bars.Count == 0)
            return 0;

        var sym = symbol.Trim().ToUpperInvariant();
        var incoming = bars
            .Where(b => b.Close > 0)
            .GroupBy(b => b.Date)
            .Select(g => g.Last())
            .OrderBy(b => b.Date)
            .ToList();

        if (incoming.Count == 0)
            return 0;

        var entity = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == sym, cancellationToken);
        if (entity is null)
        {
            db.Stocks.Add(new Entities.StockEntity
            {
                Symbol = sym,
                Name = string.IsNullOrWhiteSpace(name) ? sym : name.Trim(),
                Sector = NormalizeSector(sector),
                HistoryJson = SerializeHistory(incoming),
                LastChangePercent = 0
            });
            await db.SaveChangesAsync(cancellationToken);
            CacheInvalidation.InvalidateMarketData(cache);
            return incoming.Count;
        }

        var history = DeserializeHistory(entity.HistoryJson);
        entity.HistoryJson = SerializeHistory(MergeBars(history, incoming));
        if (!string.IsNullOrWhiteSpace(name))
            entity.Name = name.Trim();
        if (!string.IsNullOrWhiteSpace(sector) && !entity.SectorLocked)
            entity.Sector = sector.Trim();

        await db.SaveChangesAsync(cancellationToken);
        CacheInvalidation.InvalidateMarketData(cache);
        return incoming.Count;
    }

    public async Task UpsertUniverseStockAsync(
        UniverseStockUpsert upsert,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(upsert.Symbol) || upsert.Bars.Count == 0)
            return;

        var sym = upsert.Symbol.Trim().ToUpperInvariant();
        var incoming = upsert.Bars
            .Where(b => b.Close > 0)
            .GroupBy(b => b.Date)
            .Select(g => g.Last())
            .OrderBy(b => b.Date)
            .ToList();

        if (incoming.Count == 0)
            return;

        var entity = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == sym, cancellationToken);
        if (entity is null)
        {
            entity = new Entities.StockEntity { Symbol = sym };
            db.Stocks.Add(entity);
        }

        entity.Name = string.IsNullOrWhiteSpace(upsert.Name) ? sym : upsert.Name.Trim();
        if (!entity.SectorLocked)
            entity.Sector = NormalizeSector(upsert.Sector);
        entity.Exchange = string.IsNullOrWhiteSpace(upsert.Exchange) ? entity.Exchange : upsert.Exchange.Trim();
        entity.HistoryJson = SerializeHistory(incoming);
        entity.LastChangePercent = 0;
        entity.IsActive = upsert.IsActive;
        entity.TradingRestricted = upsert.TradingRestricted;
        entity.TradingStatus = upsert.TradingStatus;
        entity.AvgVolume30d = upsert.AvgVolume30d;
        entity.FirstTradeDate = upsert.FirstTradeDate;
        entity.UniverseUpdatedAt = upsert.UniverseUpdatedAt;

        await db.SaveChangesAsync(cancellationToken);
        CacheInvalidation.InvalidateMarketData(cache);
    }

    public async Task MarkUniverseInactiveAsync(
        string symbol,
        string reason,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var entity = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == sym, cancellationToken);
        if (entity is null)
            return;

        entity.IsActive = false;
        entity.TradingStatus = reason;
        entity.UniverseUpdatedAt = updatedAt;
        await db.SaveChangesAsync(cancellationToken);
        CacheInvalidation.InvalidateMarketData(cache);
    }

    public async Task SetTradingRestrictedAsync(
        string symbol,
        bool restricted,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var entity = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == sym, cancellationToken);
        if (entity is null)
            return;

        entity.TradingRestricted = restricted;
        if (!string.IsNullOrWhiteSpace(status))
            entity.TradingStatus = status.Trim();
        entity.UniverseUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        CacheInvalidation.InvalidateMarketData(cache);
    }

    public async Task DeactivateUniverseExceptAsync(
        IReadOnlyCollection<string> activeSymbols,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        var active = activeSymbols
            .Select(s => s.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = await db.Stocks
            .Where(s => s.IsActive && !active.Contains(s.Symbol))
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
            return;

        foreach (var entity in stale)
        {
            entity.IsActive = false;
            entity.UniverseUpdatedAt = updatedAt;
            entity.TradingStatus = "Rời universe Job 1";
        }

        await db.SaveChangesAsync(cancellationToken);
        CacheInvalidation.InvalidateMarketData(cache);
    }

    public async Task UpsertIndexAsync(MarketIndexSyncDto index, CancellationToken cancellationToken = default)
    {
        var symbol = string.IsNullOrWhiteSpace(index.Symbol) ? "VNINDEX" : index.Symbol.ToUpperInvariant();
        var trend = index.ChangePercent switch
        {
            > 0.5m => MarketTrend.Uptrend,
            < -0.5m => MarketTrend.Downtrend,
            _ => MarketTrend.Sideway
        };
        var score = Math.Clamp(50 + (int)(index.ChangePercent * 10), 0, 100);

        var marketIndex = new MarketIndex(symbol, index.Price, index.ChangePercent, score, trend);
        var entity = await db.MarketIndices.FirstOrDefaultAsync(m => m.Symbol == symbol, cancellationToken);
        var sessionDate = TodayVietnam();
        var bar = new OhlcvBar(sessionDate, index.Price, index.Price, index.Price, index.Price, 0);

        if (entity is null)
        {
            var history = MergeBars([], bar);
            var change5d = EntityMapper.ComputeChangePercent(history, 5, index.ChangePercent);
            var with5d = marketIndex with { ChangePercent5d = change5d };
            db.MarketIndices.Add(EntityMapper.ToEntity(with5d, SerializeHistory(history)));
        }
        else
        {
            var history = MergeBars(DeserializeHistory(entity.HistoryJson), bar);
            var change5d = EntityMapper.ComputeChangePercent(history, 5, index.ChangePercent);
            entity.Price = marketIndex.Price;
            entity.ChangePercent = marketIndex.ChangePercent;
            entity.Score = marketIndex.Score;
            entity.Trend = (int)marketIndex.Trend;
            entity.HistoryJson = SerializeHistory(history);
            entity.UpdatedAt = DateTime.UtcNow;
            _ = change5d;
        }

        await db.SaveChangesAsync(cancellationToken);
        cache.Remove("market:index");
    }

    private static List<OhlcvBar> MergeBars(IReadOnlyList<OhlcvBar> existing, OhlcvBar bar) =>
        MergeBars(existing, [bar]);

    private static List<OhlcvBar> MergeBars(IReadOnlyList<OhlcvBar> existing, IReadOnlyList<OhlcvBar> incoming)
    {
        var byDate = existing.ToDictionary(h => h.Date);
        foreach (var bar in incoming)
            byDate[bar.Date] = bar;

        return byDate.Values.OrderBy(h => h.Date).ToList();
    }

    private static DateOnly TodayVietnam()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        return DateOnly.FromDateTime(local);
    }

    private static List<OhlcvBar> DeserializeHistory(string json)
    {
        var bars = JsonSerializer.Deserialize<List<OhlcvBar>>(json, EntityMapper.JsonOptions);
        return bars ?? [];
    }

    private static string SerializeHistory(IReadOnlyList<OhlcvBar> history) =>
        JsonSerializer.Serialize(history, EntityMapper.JsonOptions);

    private static string NormalizeSector(string? sector) =>
        string.IsNullOrWhiteSpace(sector) ? "" : sector.Trim();
}

using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfDailyOpportunityRepository(ApplicationDbContext db) : IDailyOpportunityRepository
{
    public async Task ReplaceForDateAsync(
        DateOnly forTradingDate,
        IReadOnlyList<DailyOpportunityRecord> items,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.DailyOpportunities
            .Where(o => o.ForTradingDate == forTradingDate)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            db.DailyOpportunities.RemoveRange(existing);

        if (items.Count > 0)
        {
            db.DailyOpportunities.AddRange(items.Select(r => new DailyOpportunityEntity
            {
                ForTradingDate = r.ForTradingDate,
                Rank = r.Rank,
                Symbol = r.Symbol,
                Name = r.Name,
                Sector = r.Sector,
                Score = r.Score,
                Price = r.Price,
                ChangePercent = r.ChangePercent,
                VolumeRatio = r.VolumeRatio,
                GeneratedAt = r.GeneratedAt,
                BuyScore = r.BuyScore,
                PredictedHitPercent = r.PredictedHitPercent,
                PredictedSampleCount = r.PredictedSampleCount,
                SetupDna = r.SetupDna,
                Recommendation = r.Recommendation,
                TradeState = r.TradeState,
                TradeStateReason = r.TradeStateReason,
                EntryPointJson = r.EntryPointJson,
                ExplainJson = r.ExplainJson,
            }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyOpportunityRecord>> GetForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyOpportunities.AsNoTracking()
            .Where(o => o.ForTradingDate == forTradingDate)
            .OrderBy(o => o.Rank)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<DateOnly?> GetLatestForDateAsync(CancellationToken cancellationToken = default)
    {
        return await db.DailyOpportunities.AsNoTracking()
            .MaxAsync(o => (DateOnly?)o.ForTradingDate, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetSymbolsForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        return await db.DailyOpportunities.AsNoTracking()
            .Where(o => o.ForTradingDate == forTradingDate)
            .OrderBy(o => o.Rank)
            .Select(o => o.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetScoresBySymbolsForDateAsync(
        DateOnly forTradingDate,
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default)
    {
        if (symbols.Count == 0)
            return new Dictionary<string, int>();

        var normalized = symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = await db.DailyOpportunities.AsNoTracking()
            .Where(o => o.ForTradingDate == forTradingDate && normalized.Contains(o.Symbol))
            .Select(o => new { o.Symbol, o.Score })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Symbol, r => r.Score, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<OpportunityTradeStateRow>> GetTradeStatesSinceAsync(
        DateOnly fromDate,
        CancellationToken cancellationToken = default) =>
        await db.DailyOpportunities.AsNoTracking()
            .Where(o => o.ForTradingDate >= fromDate)
            .Select(o => new OpportunityTradeStateRow(
                o.ForTradingDate,
                o.Symbol,
                o.TradeState,
                o.TradeStateReason))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DailyOpportunityRecord>> GetSinceAsync(
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyOpportunities.AsNoTracking()
            .Where(o => o.ForTradingDate >= fromDate)
            .OrderBy(o => o.ForTradingDate)
            .ThenBy(o => o.Rank)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRecord).ToList();
    }

    private static DailyOpportunityRecord ToRecord(DailyOpportunityEntity e) =>
        new(
            e.ForTradingDate,
            e.Rank,
            e.Symbol,
            e.Name,
            e.Sector,
            e.Score,
            e.Price,
            e.ChangePercent,
            e.VolumeRatio,
            e.GeneratedAt,
            e.BuyScore,
            e.PredictedHitPercent,
            e.PredictedSampleCount,
            e.SetupDna,
            e.Recommendation,
            e.TradeState,
            e.TradeStateReason,
            e.EntryPointJson,
            e.ExplainJson);
}

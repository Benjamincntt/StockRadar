using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfDailyAnalysisRunRepository(ApplicationDbContext db) : IDailyAnalysisRunRepository
{
    public async Task UpsertAsync(
        DateOnly forTradingDate,
        DateTime generatedAt,
        int stocksScored,
        int opportunitiesSaved,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.DailyAnalysisRuns
            .FirstOrDefaultAsync(r => r.ForTradingDate == forTradingDate, cancellationToken);

        if (entity is null)
        {
            db.DailyAnalysisRuns.Add(new DailyAnalysisRunEntity
            {
                ForTradingDate = forTradingDate,
                GeneratedAt = generatedAt,
                StocksScored = stocksScored,
                OpportunitiesSaved = opportunitiesSaved
            });
        }
        else
        {
            entity.GeneratedAt = generatedAt;
            entity.StocksScored = stocksScored;
            entity.OpportunitiesSaved = opportunitiesSaved;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DailyAnalysisRunRecord?> GetForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        var row = await db.DailyAnalysisRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ForTradingDate == forTradingDate, cancellationToken);

        return row is null
            ? null
            : new DailyAnalysisRunRecord(
                row.ForTradingDate,
                row.GeneratedAt,
                row.StocksScored,
                row.OpportunitiesSaved);
    }
}

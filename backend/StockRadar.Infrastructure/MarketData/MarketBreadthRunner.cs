using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Tính snapshot breadth + regime cho một phiên và lưu DB (idempotent). Chạy cuối daily analysis,
/// trước khi analyzer counter-trend cần regime gate (Phase 0C).
/// </summary>
internal sealed class MarketBreadthRunner(
    ApplicationDbContext db,
    IMarketBreadthAnalyzer analyzer,
    IMarketRegimeClassifier classifier,
    IMarketBreadthSnapshotRepository repository,
    IOptions<ReversalBounceOptions> options,
    ILogger<MarketBreadthRunner> logger)
{
    public async Task<MarketBreadthSnapshot?> RunAsync(
        IReadOnlyList<Stock> universe,
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled)
            return null;

        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);
        var metrics = analyzer.Analyze(universe, indexHistory, forTradingDate);
        var previous = await repository.GetPreviousAsync(forTradingDate, cancellationToken);
        var snapshot = classifier.Classify(metrics, previous, cfg.ToRegimeThresholds());

        await repository.UpsertAsync(snapshot, cancellationToken);

        logger.LogInformation(
            "Market regime {Regime} cho {Date}: {AboveMa20}% trên MA20, {Floor} mã sàn, VN-Index drawdown {Drawdown}% (streak +{Streak}).",
            snapshot.Regime,
            forTradingDate,
            snapshot.PctAboveMa20,
            snapshot.FloorCount,
            snapshot.VnIndexDrawdownPercent,
            snapshot.ImproveStreak);

        return snapshot;
    }

    private async Task<IReadOnlyList<OhlcvBar>> LoadIndexHistoryAsync(CancellationToken cancellationToken)
    {
        var entity = await db.MarketIndices.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Symbol == "VNINDEX", cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.HistoryJson))
            return [];

        return JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, EntityMapper.JsonOptions) ?? [];
    }
}

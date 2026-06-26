using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class RadarService(
    IStockRepository stocks,
    SmartMoneyEvaluationService smartMoneyEval,
    ISignalAnalyzer signalAnalyzer,
    ISessionRadarRepository sessionRadar) : IRadarService
{
    public async Task<PagedResult<RadarItemDto>> GetRadarAsync(
        RadarQuery query,
        CancellationToken cancellationToken = default)
    {
        var context = await smartMoneyEval.BuildContextAsync(cancellationToken);
        var all = await stocks.GetAllAsync(cancellationToken);
        var items = new List<RadarItemDto>();

        foreach (var stock in all)
        {
            var eval = smartMoneyEval.EvaluateStock(stock, context);
            var signals = signalAnalyzer.DetectSignals(stock, context.Index.ChangePercent);
            items.Add(new RadarItemDto(
                stock.Symbol,
                stock.Name,
                stock.Sector,
                eval.Score,
                stock.LatestPrice,
                signalAnalyzer.GetChangePercent(stock),
                eval.VolumeRatio,
                eval.RelativeStrength5d,
                signals));
        }

        var filtered = items
            .Where(item => MatchesFilter(item, query))
            .Where(item => query.Sector is null || item.Sector.Equals(query.Sector, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Score);

        return filtered.ToPagedResult(query);
    }

    public Task<RadarLiveSnapshotDto> GetLiveRadarAsync(
        RadarLiveQuery query,
        CancellationToken cancellationToken = default) =>
        sessionRadar.GetLiveSnapshotAsync(query, cancellationToken);

    private static bool MatchesFilter(RadarItemDto item, RadarQuery query)
    {
        var any = query.Breakout || query.Accumulation || query.RelativeStrength
                  || query.VolumeSpike || query.Shakeout || query.Distribution;
        if (!any)
            return true;

        return (query.Breakout && item.Signals.Contains(SignalType.Breakout))
               || (query.Accumulation && item.Signals.Contains(SignalType.Accumulation))
               || (query.RelativeStrength && item.Signals.Contains(SignalType.RelativeStrength))
               || (query.VolumeSpike && item.Signals.Contains(SignalType.VolumeSpike))
               || (query.Shakeout && item.Signals.Contains(SignalType.Shakeout))
               || (query.Distribution && item.Signals.Contains(SignalType.Distribution));
    }
}

using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Cảnh báo tín hiệu kỹ thuật mới trên watchlist sau Job 2 / phân tích.</summary>
internal sealed class WatchlistPatternAlertPublisher(
    ISignalAnalyzer signalAnalyzer,
    ISignalFormatter formatter,
    IAlertRepository alerts,
    IMarketRealtimePublisher publisher,
    IOptions<PriceRunupFilterOptions> runupFilter)
{
    public const string SourceTag = "Tín hiệu kỹ thuật";

    public Task<int> PublishAsync(
        IReadOnlyList<(Stock Stock, DailyOpportunityRecord Opp)> watchlist,
        decimal indexChangePercent,
        CancellationToken cancellationToken) =>
        PublishAsync(
            watchlist,
            indexChangePercent,
            runupFilter.Value.ToSettings(),
            cancellationToken);

    public async Task<int> PublishAsync(
        IReadOnlyList<(Stock Stock, DailyOpportunityRecord Opp)> watchlist,
        decimal indexChangePercent,
        BasePriceFilterSettings filter,
        CancellationToken cancellationToken)
    {
        var sent = 0;

        foreach (var (stock, opp) in watchlist)
        {
            if (stock.History.Count < 2)
                continue;

            if (signalAnalyzer.HasExceededMaxGainFromBase(stock.History, filter))
                continue;

            var current = signalAnalyzer.DetectSignals(stock, indexChangePercent);
            var trimmed = stock with
            {
                History = stock.History.Take(stock.History.Count - 1).ToList()
            };
            var previous = signalAnalyzer.DetectSignals(trimmed, indexChangePercent);
            var volumeRatio = signalAnalyzer.GetVolumeRatio(stock.History);
            var relativeStrength = signalAnalyzer.GetRelativeStrength(stock, indexChangePercent);

            foreach (var type in current.Except(previous))
            {
                var title = formatter.FormatAlertTitle(type, stock.Symbol);
                var description = formatter.FormatDescription(type, stock.Symbol, volumeRatio);
                var message =
                    $"{description}\nWatchlist #{opp.Rank} · điểm {opp.Score} · ngành {opp.Sector}";

                var alert = new Alert(
                    Guid.NewGuid(),
                    stock.Symbol,
                    type,
                    title,
                    message,
                    DateTime.UtcNow,
                    MapCategory(type),
                    volumeRatio,
                    relativeStrength,
                    SourceTag);

                await alerts.AddAsync(alert, cancellationToken);
                await publisher.PublishAlertAsync(DtoMapper.ToDto(alert), cancellationToken);
                sent++;
            }
        }

        return sent;
    }

    private static AlertCategory MapCategory(SignalType type) => type switch
    {
        SignalType.Distribution => AlertCategory.Sell,
        _ => AlertCategory.Buy
    };
}

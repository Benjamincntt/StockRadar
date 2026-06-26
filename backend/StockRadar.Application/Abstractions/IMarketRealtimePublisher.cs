using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IMarketRealtimePublisher
{
    Task PublishQuotesAsync(IReadOnlyList<QuoteTickDto> quotes, CancellationToken cancellationToken = default);

    Task PublishIndexAsync(IndexTickDto index, CancellationToken cancellationToken = default);

    Task PublishRadarAsync(RadarLiveSnapshotDto snapshot, CancellationToken cancellationToken = default);

    Task PublishAlertAsync(AlertDto alert, CancellationToken cancellationToken = default);
}

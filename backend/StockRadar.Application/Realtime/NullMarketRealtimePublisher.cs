using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Realtime;

public sealed class NullMarketRealtimePublisher : IMarketRealtimePublisher
{
    public Task PublishQuotesAsync(IReadOnlyList<QuoteTickDto> quotes, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishIndexAsync(IndexTickDto index, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishRadarAsync(RadarLiveSnapshotDto snapshot, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishAlertAsync(AlertDto alert, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

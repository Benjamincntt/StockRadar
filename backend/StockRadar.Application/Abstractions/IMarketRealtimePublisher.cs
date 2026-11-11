using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IMarketRealtimePublisher
{
    Task PublishQuotesAsync(IReadOnlyList<QuoteTickDto> quotes, CancellationToken cancellationToken = default);

    Task PublishIndexAsync(IndexTickDto index, CancellationToken cancellationToken = default);
}

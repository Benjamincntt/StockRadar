using Microsoft.AspNetCore.SignalR;
using StockRadar.Api.Hubs;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Realtime;

public sealed class SignalRMarketRealtimePublisher(IHubContext<MarketHub> hub) : IMarketRealtimePublisher
{
    // Gửi theo group symbol mà client đã Subscribe — trước đây Clients.All phát toàn
    // thị trường cho MỌI kết nối, mobile parse hàng nghìn quote trên UI isolate mỗi batch.
    // Client ngoài group vẫn lấy giá đại trà qua poll snapshot /api/quotes định kỳ.
    public Task PublishQuotesAsync(IReadOnlyList<QuoteTickDto> quotes, CancellationToken cancellationToken = default)
    {
        if (quotes.Count == 0)
            return Task.CompletedTask;

        var groups = quotes
            .Where(q => !string.IsNullOrWhiteSpace(q.Symbol))
            .Select(q => MarketHub.GroupName(q.Symbol))
            .Distinct()
            .ToArray();
        if (groups.Length == 0)
            return Task.CompletedTask;

        return hub.Clients.Groups(groups).SendAsync(MarketHub.QuotesUpdated, quotes, cancellationToken);
    }

    public Task PublishIndexAsync(IndexTickDto index, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.IndexUpdated, index, cancellationToken);

    public Task PublishRadarAsync(RadarLiveSnapshotDto snapshot, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.RadarUpdated, snapshot, cancellationToken);

    public Task PublishAlertAsync(AlertDto alert, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.AlertCreated, alert, cancellationToken);

    public Task PublishTradeEventAsync(TradeEventDto tradeEvent, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.TradeEventCreated, tradeEvent, cancellationToken);
}

using Microsoft.AspNetCore.SignalR;
using StockRadar.Api.Hubs;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Api.Realtime;

public sealed class SignalRMarketRealtimePublisher(IHubContext<MarketHub> hub) : IMarketRealtimePublisher
{
    public Task PublishQuotesAsync(IReadOnlyList<QuoteTickDto> quotes, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.QuotesUpdated, quotes, cancellationToken);

    public Task PublishIndexAsync(IndexTickDto index, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.IndexUpdated, index, cancellationToken);

    public Task PublishRadarAsync(RadarLiveSnapshotDto snapshot, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.RadarUpdated, snapshot, cancellationToken);

    public Task PublishAlertAsync(AlertDto alert, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.AlertCreated, alert, cancellationToken);

    public Task PublishTradePrintAsync(TradePrintDto print, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(MarketHub.TradePrintCreated, print, cancellationToken);
}

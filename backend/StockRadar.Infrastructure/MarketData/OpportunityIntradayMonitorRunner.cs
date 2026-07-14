using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Quét bảng giá KBS trong phiên — phát hiện lô lớn + nhãn VSA + dòng tiền.</summary>
internal sealed class OpportunityIntradayMonitorRunner(
    KbsPriceBoardClient kbs,
    IJobStockRepository stocks,
    IMarketSyncService sync,
    IQuoteTickCache quoteCache,
    IMarketRealtimePublisher publisher,
    ITradeEventStore tradeStore,
    OrderFlowSnapshotTracker boardSnapshots,
    TradeEventDetector tradeDetector,
    TradeEventAggregator tradeAggregator,
    SessionFlowTracker sessionFlow,
    IntradayMonitorStatusTracker monitorStatus,
    TopOpportunityVipAlertPublisher vipAlerts,
    IOptions<OpportunityMonitorOptions> options,
    ILogger<OpportunityIntradayMonitorRunner> logger) : IOpportunityIntradayMonitorService
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var sessionDate = VietnamMarketCalendar.TodayVietnam();
        var topMap = await vipAlerts.LoadTodayTopMapAsync(cancellationToken);
        var openPositions = await vipAlerts.LoadOpenPositionMapAsync(cancellationToken);

        var symbols = await stocks.GetActiveSymbolsAsync(cancellationToken);
        if (symbols.Count == 0)
        {
            logger.LogDebug("Trade scan: no active universe symbols.");
            monitorStatus.RecordScan(DateTime.UtcNow, 0, 0);
            return 0;
        }

        var batchSize = Math.Max(10, cfg.BatchSize);
        var eventsPublished = 0;
        var ticks = new List<QuoteTickDto>();
        var scannedAt = DateTime.UtcNow;

        for (var i = 0; i < symbols.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = symbols.Skip(i).Take(batchSize).ToList();
            var board = await kbs.FetchAsync(batch, cancellationToken);

            foreach (var row in board)
            {
                ticks.Add(new QuoteTickDto(row.Symbol, row.Close, row.ChangePercent, row.SessionVolume, scannedAt));

                var previous = boardSnapshots.GetPrevious(row.Symbol);
                var scan = tradeDetector.DetectScan(row, previous, cfg);
                boardSnapshots.Update(row);

                if (scan is not null)
                {
                    var flow = sessionFlow.Update(
                        scan.Symbol,
                        scan.ForeignNetDelta,
                        scan.PropDelta,
                        scan.BookImbalance);

                    foreach (var aggregated in tradeAggregator.Add(scan))
                    {
                        var flowSnap = sessionFlow.Get(aggregated.Symbol) ?? flow;
                        var dto = ToDto(aggregated, flowSnap, scannedAt);
                        tradeStore.Add(dto);
                        await publisher.PublishTradeEventAsync(dto, cancellationToken);
                        eventsPublished++;
                    }
                }
                else if (previous is not null)
                {
                    sessionFlow.Update(
                        row.Symbol,
                        OrderBookMetrics.ForeignNetDelta(row, previous),
                        OrderBookMetrics.PropDelta(row, previous),
                        OrderBookMetrics.BookImbalance(row));
                }

                if (topMap.TryGetValue(row.Symbol, out var topOpp))
                {
                    await vipAlerts.ProcessQuoteAsync(
                        topOpp,
                        row,
                        scan,
                        sessionDate,
                        cancellationToken);
                }

                if (openPositions.TryGetValue(row.Symbol, out var pos))
                {
                    await vipAlerts.ProcessPositionAsync(
                        pos,
                        row,
                        scan,
                        sessionDate,
                        pos.MarketPhaseAtEntry ?? "Neutral",
                        cancellationToken);
                }
            }

            var quotes = board.Select(r => new StockQuoteSyncDto(
                r.Symbol, null, r.Open, r.High, r.Low, r.Close, r.SessionVolume, r.ChangePercent, null)).ToList();
            if (quotes.Count > 0)
                await sync.ApplyAsync(new MarketSyncRequest(null, quotes), cancellationToken);
        }

        foreach (var expired in tradeAggregator.FlushExpired())
        {
            var flowSnap = sessionFlow.Get(expired.Symbol);
            if (flowSnap is null)
                continue;

            var dto = ToDto(expired, flowSnap, scannedAt);
            tradeStore.Add(dto);
            await publisher.PublishTradeEventAsync(dto, cancellationToken);
            eventsPublished++;
        }

        if (ticks.Count > 0)
        {
            quoteCache.SetQuotes(ticks);
            await publisher.PublishQuotesAsync(ticks, cancellationToken);
        }

        if (eventsPublished > 0)
            logger.LogInformation("Trade scan: {Count} sự kiện từ {Symbols} mã.", eventsPublished, symbols.Count);

        monitorStatus.RecordScan(scannedAt, symbols.Count, eventsPublished);
        return eventsPublished;
    }

    private static TradeEventDto ToDto(
        AggregatedTradeEvent evt,
        SessionFlowSnapshot flow,
        DateTime at) =>
        new(
            evt.Symbol,
            evt.Label,
            evt.Price,
            evt.Volume,
            evt.ValueVnd,
            evt.SpreadPct,
            evt.BookImbalance,
            evt.ForeignNetDelta,
            flow.SessionForeignNet,
            flow.SessionPropNet,
            flow.SessionPressure,
            at,
            evt.IsAggregated);
}

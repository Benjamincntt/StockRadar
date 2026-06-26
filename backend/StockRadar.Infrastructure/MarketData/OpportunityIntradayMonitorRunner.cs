using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Job 3: lệnh đột biến trong phiên — quét toàn universe Job 1.</summary>
internal sealed class OpportunityIntradayMonitorRunner(
    KbsPriceBoardClient kbs,
    IDailyOpportunityRepository opportunities,
    IJobStockRepository stocks,
    ISignalAnalyzer signalAnalyzer,
    IAlertRepository alerts,
    IMarketSyncService sync,
    IQuoteTickCache quoteCache,
    IMarketRealtimePublisher publisher,
    IZaloNotifier zalo,
    IntradayAlertTracker alertTracker,
    OrderFlowSnapshotTracker flowSnapshots,
    OrderFlowAnalyzer flowAnalyzer,
    IOptions<OpportunityMonitorOptions> options,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<ZaloNotifyOptions> zaloOptions,
    ILogger<OpportunityIntradayMonitorRunner> logger) : IOpportunityIntradayMonitorService
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var runup = runupFilter.Value;
        var symbols = await stocks.GetActiveSymbolsAsync(cancellationToken);
        if (symbols.Count == 0)
        {
            logger.LogDebug("Monitor: no active universe symbols.");
            return 0;
        }

        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var opps = await opportunities.GetForDateAsync(targetDate, cancellationToken);
        if (opps.Count == 0)
        {
            var latest = await opportunities.GetLatestForDateAsync(cancellationToken);
            if (latest is not null)
                opps = await opportunities.GetForDateAsync(latest.Value, cancellationToken);
        }

        var oppMap = opps.ToDictionary(o => o.Symbol, StringComparer.OrdinalIgnoreCase);
        var stockMap = (await stocks.GetAllAsync(cancellationToken))
            .ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);
        var batchSize = Math.Max(10, cfg.BatchSize);
        var alertsSent = 0;
        var ticks = new List<QuoteTickDto>();
        var scannedAt = DateTime.UtcNow;
        var cooldown = TimeSpan.FromMinutes(Math.Max(1, zaloOptions.Value.CooldownMinutes));
        var zaloCfg = zaloOptions.Value;

        for (var i = 0; i < symbols.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = symbols.Skip(i).Take(batchSize).ToList();
            var board = await kbs.FetchAsync(batch, cancellationToken);

            foreach (var row in board)
            {
                ticks.Add(new QuoteTickDto(row.Symbol, row.Close, row.ChangePercent, row.SessionVolume, scannedAt));

                if (stockMap.TryGetValue(row.Symbol, out var stock)
                    && signalAnalyzer.HasExceededMaxGainFromBase(
                        stock.History,
                        runup.ToSettings(),
                        row.Close))
                    continue;

                var previous = flowSnapshots.GetPrevious(row.Symbol);
                var events = flowAnalyzer.Detect(row, previous, cfg);
                flowSnapshots.Update(row);

                foreach (var evt in events)
                {
                    var eventKey = evt.Source.ToString();
                    if (!alertTracker.ShouldSend(row.Symbol, eventKey, cooldown))
                        continue;

                    oppMap.TryGetValue(row.Symbol, out var opp);
                    await SaveAppAlertAsync(row, opp, evt, cancellationToken);

                    if (zaloCfg.Enabled
                        && opp is not null
                        && !string.IsNullOrWhiteSpace(zaloCfg.WebhookUrl))
                        await zalo.SendAsync(evt.Message, row.Symbol, cancellationToken);

                    alertsSent++;
                }
            }

            var quotes = board.Select(r => new StockQuoteSyncDto(
                r.Symbol, null, r.Open, r.High, r.Low, r.Close, r.SessionVolume, r.ChangePercent, null)).ToList();
            if (quotes.Count > 0)
                await sync.ApplyAsync(new MarketSyncRequest(null, quotes), cancellationToken);
        }

        if (ticks.Count > 0)
        {
            quoteCache.SetQuotes(ticks);
            await publisher.PublishQuotesAsync(ticks, cancellationToken);
        }

        if (alertsSent > 0)
            logger.LogInformation("Monitor: {Alerts} order-flow alerts from {Count} symbols.", alertsSent, symbols.Count);

        return alertsSent;
    }

    private async Task SaveAppAlertAsync(
        KbsPriceBoardClient.KbsBoardRow row,
        DailyOpportunityRecord? opp,
        OrderFlowEvent evt,
        CancellationToken cancellationToken)
    {
        var sourceLabel = OrderFlowSourceLabels.Label(evt.Source);
        var title = $"{row.Symbol} — {sourceLabel}";
        var message = evt.Message;
        if (opp is not null)
            message += $"\nWatchlist #{opp.Rank} · điểm {opp.Score}";

        var alert = new Alert(
            Guid.NewGuid(),
            row.Symbol,
            MapSignal(evt.Source),
            title,
            message,
            DateTime.UtcNow,
            OrderFlowSourceLabels.IsBuySide(evt.Source) ? AlertCategory.Buy : AlertCategory.Sell,
            evt.DeltaVolume > 0 ? Math.Round((decimal)evt.DeltaVolume / 100_000m, 2) : null,
            row.ChangePercent,
            OrderFlowSourceLabels.SourceTag);

        await alerts.AddAsync(alert, cancellationToken);
        await publisher.PublishAlertAsync(DtoMapper.ToDto(alert), cancellationToken);
    }

    private static SignalType MapSignal(OrderFlowSource source) => source switch
    {
        OrderFlowSource.ForeignBuy or OrderFlowSource.LargeBid => SignalType.VolumeSpike,
        OrderFlowSource.ForeignSell or OrderFlowSource.LargeAsk => SignalType.Distribution,
        OrderFlowSource.Proprietary => SignalType.Accumulation,
        _ => SignalType.VolumeSpike
    };
}

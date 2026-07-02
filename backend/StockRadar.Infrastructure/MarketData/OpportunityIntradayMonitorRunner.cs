using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Quét bảng giá KBS trong phiên — phát hiện khớp lệnh (mua/bán + KL + giá).</summary>
internal sealed class OpportunityIntradayMonitorRunner(
    KbsPriceBoardClient kbs,
    IJobStockRepository stocks,
    IMarketSyncService sync,
    IQuoteTickCache quoteCache,
    IMarketRealtimePublisher publisher,
    ITradePrintStore tradeStore,
    OrderFlowSnapshotTracker boardSnapshots,
    TradePrintDetector tradeDetector,
    IntradayMonitorStatusTracker monitorStatus,
    IOptions<OpportunityMonitorOptions> options,
    ILogger<OpportunityIntradayMonitorRunner> logger) : IOpportunityIntradayMonitorService
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var symbols = await stocks.GetActiveSymbolsAsync(cancellationToken);
        if (symbols.Count == 0)
        {
            logger.LogDebug("Trade scan: no active universe symbols.");
            monitorStatus.RecordScan(DateTime.UtcNow, 0, 0);
            return 0;
        }

        var batchSize = Math.Max(10, cfg.BatchSize);
        var printsPublished = 0;
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
                var print = tradeDetector.Detect(row, previous, cfg);
                boardSnapshots.Update(row);

                if (print is null)
                    continue;

                var dto = new TradePrintDto(print.Symbol, print.Side, print.Price, print.Volume, scannedAt);
                tradeStore.Add(dto);
                await publisher.PublishTradePrintAsync(dto, cancellationToken);
                printsPublished++;
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

        if (printsPublished > 0)
            logger.LogInformation("Trade scan: {Count} khớp lệnh từ {Symbols} mã.", printsPublished, symbols.Count);

        monitorStatus.RecordScan(scannedAt, symbols.Count, printsPublished);
        return printsPublished;
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Job 2: sync OHLCV phiên — chỉ mã active universe từ Job 1.</summary>
internal sealed class DailySessionSyncRunner(
    IJobStockRepository stocks,
    KbsPriceBoardClient kbs,
    KbsIndexClient indexClient,
    KbsStockListingClient listings,
    IMarketSyncService sync,
    IMarketDataWriter writer,
    IOptions<MarketJobsOptions> options,
    ILogger<DailySessionSyncRunner> logger) : IDailySessionSyncService
{
    public async Task<DailySessionSyncResultDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value.DailySession;
        var sessionDate = VietnamMarketCalendar.TodayVietnam();

        logger.LogInformation("Job 2 — sync phiên universe {Date}.", sessionDate);

        var symbols = await stocks.GetActiveSymbolsAsync(cancellationToken);
        symbols = symbols
            .Where(s => !s.Equals("VNINDEX", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbols.Count == 0)
        {
            logger.LogWarning("Job 2: universe trống — chạy Job 1 trước.");
            return new DailySessionSyncResultDto(0, false, sessionDate, DateTime.UtcNow);
        }

        var listingMap = await listings.GetListingsAsync(bypassCache: true, cancellationToken);
        var tradable = new List<string>();
        foreach (var symbol in symbols)
        {
            if (listingMap.TryGetValue(symbol, out var listing) && listing.TradingRestricted)
            {
                await writer.SetTradingRestrictedAsync(
                    symbol,
                    true,
                    listing.TradingStatus,
                    cancellationToken);
                logger.LogInformation("Job 2 bỏ qua {Symbol}: hạn chế GD.", symbol);
                continue;
            }

            tradable.Add(symbol);
        }

        var batchSize = Math.Max(10, cfg.BatchSize);
        var stocksUpdated = 0;

        for (var i = 0; i < tradable.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = tradable.Skip(i).Take(batchSize).ToList();
            var board = await kbs.FetchAsync(batch, cancellationToken);

            var quotes = board
                .Select(r => new StockQuoteSyncDto(
                    r.Symbol, null, r.Open, r.High, r.Low, r.Close, r.SessionVolume, r.ChangePercent, null))
                .ToList();

            if (quotes.Count == 0)
                continue;

            var result = await sync.ApplyAsync(new MarketSyncRequest(null, quotes), cancellationToken);
            stocksUpdated += result.StocksUpdated;

            logger.LogInformation(
                "Job 2 [{Done}/{Total}] batch {Batch} mã — tổng {Updated} đã ghi",
                Math.Min(i + batchSize, tradable.Count),
                tradable.Count,
                quotes.Count,
                stocksUpdated);
        }

        MarketIndexSyncDto? index = null;
        var vnIndex = await indexClient.FetchVnIndexAsync(cancellationToken);
        if (vnIndex is not null)
        {
            index = new MarketIndexSyncDto("VNINDEX", vnIndex.Price, vnIndex.ChangePercent);
            await sync.ApplyAsync(new MarketSyncRequest(index, []), cancellationToken);
        }

        logger.LogInformation("Job 2 xong universe: {Stocks}/{Total} mã.", stocksUpdated, tradable.Count);

        return new DailySessionSyncResultDto(
            stocksUpdated,
            index is not null,
            sessionDate,
            DateTime.UtcNow);
    }
}

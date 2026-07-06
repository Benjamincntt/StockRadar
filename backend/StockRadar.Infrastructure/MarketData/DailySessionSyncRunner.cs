using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Job 2: append nến phiên T — chỉ mã active từ Job 1; KBS chỉ lấy giá phiên, không quét listing.</summary>
internal sealed class DailySessionSyncRunner(
    IJobStockRepository stocks,
    KbsPriceBoardClient kbs,
    KbsIndexClient indexClient,
    IMarketSyncService sync,
    IJobMarketIndexProvider marketIndex,
    DarvasBreakoutAlertPublisher darvasBreakoutAlerts,
    IOptions<MarketJobsOptions> options,
    ILogger<DailySessionSyncRunner> logger) : IDailySessionSyncService
{
    public async Task<DailySessionSyncResultDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value.DailySession;
        var sessionDate = VietnamMarketCalendar.TodayVietnam();

        if (!VietnamMarketCalendar.IsTradingDay(sessionDate))
        {
            logger.LogInformation("Job 2 — bỏ qua {Date} (không phải ngày giao dịch).", sessionDate);
            return new DailySessionSyncResultDto(0, false, sessionDate, DateTime.UtcNow);
        }

        logger.LogInformation("Job 2 — append phiên {Date} cho universe Job 1.", sessionDate);

        var tradable = (await stocks.GetActiveSymbolsAsync(cancellationToken))
            .Where(s => !s.Equals("VNINDEX", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tradable.Count == 0)
        {
            logger.LogWarning("Job 2: universe trống — chạy Job 1 trước.");
            return new DailySessionSyncResultDto(0, false, sessionDate, DateTime.UtcNow);
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

        logger.LogInformation("Job 2 xong: {Stocks}/{Total} mã universe.", stocksUpdated, tradable.Count);

        var darvasAlerts = 0;
        try
        {
            var market = await marketIndex.GetCurrentAsync(cancellationToken);
            var universe = await stocks.GetAllAsync(cancellationToken);
            darvasAlerts = await darvasBreakoutAlerts.PublishAsync(
                universe,
                market.ChangePercent,
                cancellationToken);
            if (darvasAlerts > 0)
                logger.LogInformation("Job 2: {Count} cảnh báo breakout Darvas.", darvasAlerts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job 2: quét breakout Darvas thất bại — bỏ qua.");
        }

        return new DailySessionSyncResultDto(
            stocksUpdated,
            index is not null,
            sessionDate,
            DateTime.UtcNow,
            UniverseDeactivated: 0,
            darvasAlerts);
    }
}

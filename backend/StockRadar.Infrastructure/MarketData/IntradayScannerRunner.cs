using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Quét đột biến phiên HOSE: KL ≥ 1M, |%| ≥ 3. Metadata từ DB (Job 1).</summary>
internal sealed class IntradayScannerRunner(
    KbsPriceBoardClient kbs,
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
    ISignalAnalyzer signalAnalyzer,
    ISessionRadarRepository sessionRadar,
    IQuoteTickCache quoteCache,
    IMarketRealtimePublisher publisher,
    IOptions<IntradayScannerOptions> options,
    ILogger<IntradayScannerRunner> logger) : IIntradayScannerService
{
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var exchange = cfg.Exchange.Trim().ToUpperInvariant();
        var sessionDate = VietnamMarketCalendar.TodayVietnam();

        logger.LogInformation(
            "Intraday scan {Exchange}: Vol≥{Vol}, |%|≥{Pct}.",
            exchange,
            cfg.MinSessionVolume,
            cfg.MinAbsChangePercent);

        var symbols = await stocks.GetActiveSymbolsAsync(cancellationToken);
        if (symbols.Count == 0)
        {
            logger.LogWarning("Không có mã active trong universe.");
            return 0;
        }

        var stockMap = (await stocks.GetAllAsync(cancellationToken))
            .ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);
        var index = await marketIndex.GetCurrentAsync(cancellationToken);

        var batchSize = Math.Max(10, cfg.BatchSize);
        var hits = new List<SessionRadarHitRecord>();
        var ticks = new List<QuoteTickDto>();
        var scannedAt = DateTime.UtcNow;

        for (var i = 0; i < symbols.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = symbols.Skip(i).Take(batchSize).ToList();
            var board = await kbs.FetchAsync(batch, cancellationToken);

            foreach (var row in board)
            {
                if (row.SessionVolume < cfg.MinSessionVolume)
                    continue;

                if (Math.Abs(row.ChangePercent) < cfg.MinAbsChangePercent)
                    continue;

                stockMap.TryGetValue(row.Symbol, out var stock);

                var volumeRatio = stock is not null
                    ? signalAnalyzer.GetVolumeRatio(stock.History)
                    : 1m;

                var relativeStrength = stock is not null
                    ? signalAnalyzer.GetRelativeStrength(stock, index.ChangePercent)
                    : 0m;

                var signalTypes = stock is not null
                    ? signalAnalyzer.DetectSignals(stock, index.ChangePercent)
                    : [];

                var signalNames = signalTypes.Select(s => s.ToString()).ToList();

                hits.Add(new SessionRadarHitRecord(
                    row.Symbol,
                    stock?.Name ?? row.Symbol,
                    stock?.Sector ?? "",
                    row.Close,
                    row.ChangePercent,
                    row.SessionVolume,
                    volumeRatio,
                    relativeStrength,
                    signalNames));

                ticks.Add(new QuoteTickDto(
                    row.Symbol,
                    row.Close,
                    row.ChangePercent,
                    row.SessionVolume,
                    scannedAt));
            }
        }

        hits = hits
            .OrderByDescending(h => Math.Abs(h.ChangePercent))
            .ThenByDescending(h => h.SessionVolume)
            .ToList();

        await sessionRadar.ReplaceSessionHitsAsync(
            sessionDate,
            exchange,
            hits,
            scannedAt,
            cancellationToken);

        if (ticks.Count > 0)
        {
            quoteCache.SetQuotes(ticks);
            await publisher.PublishQuotesAsync(ticks, cancellationToken);
        }

        await publisher.PublishRadarAsync(
            new RadarLiveSnapshotDto(exchange, sessionDate, scannedAt, hits.Count, hits.Select(ToLiveDto).ToList()),
            cancellationToken);

        logger.LogInformation("Intraday scan xong: {Count} mã đột biến {Exchange}.", hits.Count, exchange);
        return hits.Count;
    }

    private static RadarLiveItemDto ToLiveDto(SessionRadarHitRecord h) =>
        new(
            h.Symbol,
            h.Name,
            h.Sector,
            h.Price,
            h.ChangePercent,
            h.SessionVolume,
            h.VolumeRatio,
            h.RelativeStrength,
            h.Signals,
            DateTime.UtcNow);
}

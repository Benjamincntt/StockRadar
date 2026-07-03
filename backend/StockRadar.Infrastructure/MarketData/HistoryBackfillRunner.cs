using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Job 1: quét HOSE+HNX+UPCOM → lọc universe (KL, IPO 1 năm, hạn chế GD) → backfill full history.
/// </summary>
internal sealed class HistoryBackfillRunner(
    KbsGroupListingClient groups,
    KbsHistoryClient history,
    KbsSectorLookupClient sectors,
    KbsStockListingClient listings,
    IMarketDataWriter writer,
    HistoryBackfillState state,
    IOptions<MarketJobsOptions> options,
    ILogger<HistoryBackfillRunner> logger) : IHistoryBackfillService
{
    public HistoryBackfillStatusDto GetStatus() => state.Get();

    public Task<HistoryBackfillResultDto> RunAsync(
        HistoryBackfillRequest? request = null,
        CancellationToken cancellationToken = default) =>
        RunInternalAsync(request ?? new HistoryBackfillRequest(), cancellationToken);

    private async Task<HistoryBackfillResultDto> RunInternalAsync(
        HistoryBackfillRequest request,
        CancellationToken cancellationToken)
    {
        state.SetRunning();

        var cfg = options.Value.History;
        var bypassCache = cfg.BypassCache;
        var isNight = string.Equals(request.Mode, "night", StringComparison.OrdinalIgnoreCase);
        var delayMs = isNight ? cfg.NightDelayBetweenSymbolsMs : cfg.DelayBetweenSymbolsMs;
        var delay = TimeSpan.FromMilliseconds(Math.Max(100, delayMs));

        var start = ParseStartDate(request.StartDate ?? cfg.StartDate);
        var end = ParseEndDate(request.EndDate)
            ?? VietnamMarketCalendar.PreviousTradingDay(VietnamMarketCalendar.TodayVietnam());
        var screeningStart = end.AddDays(-Math.Max(cfg.ScreeningLookbackDays, cfg.VolumeLookbackSessions + 5));
        var filterSettings = new UniverseFilterSettings(
            cfg.MinAvgDailyVolume,
            cfg.VolumeLookbackSessions,
            cfg.ExcludeIpoWithinDays,
            cfg.MinClosePrice);

        var failed = new List<string>();
        var succeeded = 0;
        var excluded = 0;
        var barsWritten = 0;
        var inUniverse = 0;
        var total = 0;
        var universeSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var candidates = await ResolveSymbolsAsync(request.Groups, bypassCache, cancellationToken);
            total = candidates.Count;

            logger.LogInformation(
                "Job 1 ({Mode}) — {Count} mã ứng viên, lọc giá >{MinPrice:N0}, TB KL≥{MinVol:N0}/{VolSessions} phiên, loại IPO {IpoDays} ngày.",
                isNight ? "đêm" : "nhanh",
                total,
                cfg.MinClosePrice,
                cfg.MinAvgDailyVolume,
                cfg.VolumeLookbackSessions,
                cfg.ExcludeIpoWithinDays);

            if (total == 0)
                return Finish(total, 0, 0, 0, 0, 0, failed);

            var listingMap = await listings.GetListingsAsync(bypassCache, cancellationToken);
            var sectorMap = await sectors.GetSymbolSectorsAsync(cancellationToken, bypassCache: true);
            state.SetTotal(total);
            var updatedAt = DateTime.UtcNow;

            for (var i = 0; i < candidates.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var symbol = candidates[i];
                state.Update(i, total, symbol);

                try
                {
                    listingMap.TryGetValue(symbol, out var listing);
                    var exchange = listing?.Exchange ?? "";
                    var restricted = listing?.TradingRestricted ?? false;
                    var status = listing?.TradingStatus;

                    var screeningBars = await FetchBarsAsync(symbol, screeningStart, end, cancellationToken);
                    if (screeningBars.Count == 0)
                    {
                        failed.Add(symbol);
                        await writer.MarkUniverseInactiveAsync(symbol, "Không có dữ liệu screening", updatedAt, cancellationToken);
                        continue;
                    }

                    var screen = StockUniverseFilter.Screen(
                        screeningBars,
                        restricted,
                        status,
                        filterSettings,
                        end);

                    if (!screen.Passes)
                    {
                        excluded++;
                        await writer.MarkUniverseInactiveAsync(symbol, screen.Reason, updatedAt, cancellationToken);
                        logger.LogDebug("Loại {Symbol}: {Reason}", symbol, screen.Reason);
                        continue;
                    }

                    var fullBars = await FetchBarsAsync(symbol, start, end, cancellationToken);
                    if (fullBars.Count == 0)
                    {
                        failed.Add(symbol);
                        await writer.MarkUniverseInactiveAsync(symbol, "Backfill full thất bại", updatedAt, cancellationToken);
                        continue;
                    }

                    sectorMap.TryGetValue(symbol, out var sector);
                    var name = listing?.Name;

                    await writer.UpsertUniverseStockAsync(new UniverseStockUpsert(
                        symbol,
                        name,
                        sector,
                        exchange,
                        fullBars,
                        true,
                        false,
                        null,
                        screen.AvgVolume30d,
                        screen.FirstTradeDate,
                        updatedAt), cancellationToken);

                    barsWritten += fullBars.Count;
                    succeeded++;
                    inUniverse++;
                    universeSymbols.Add(symbol);

                    logger.LogInformation(
                        "Job 1 [{Done}/{Total}] {Symbol} ✓ universe — {Bars} nến, TB KL {Avg:N0}",
                        i + 1,
                        total,
                        symbol,
                        fullBars.Count,
                        screen.AvgVolume30d);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Backfill thất bại {Symbol}.", symbol);
                    failed.Add(symbol);
                }

                if (i < candidates.Count - 1)
                    await Task.Delay(delay, cancellationToken);
            }

            await DeactivateStaleUniverseAsync(universeSymbols, updatedAt, cancellationToken);

            return Finish(total, total, inUniverse, succeeded, barsWritten, excluded, failed);
        }
        catch (OperationCanceledException)
        {
            state.CancelRunning();
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backfill lịch sử thất bại.");
            return Finish(total, total, inUniverse, succeeded, barsWritten, excluded, failed);
        }
    }

    private Task DeactivateStaleUniverseAsync(
        HashSet<string> activeSymbols,
        DateTime updatedAt,
        CancellationToken cancellationToken) =>
        writer.DeactivateUniverseExceptAsync(activeSymbols, updatedAt, cancellationToken);

    private async Task<IReadOnlyList<OhlcvBar>> FetchBarsAsync(
        string symbol,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var kbsBars = await history.FetchDailyHistoryAsync(symbol, start, end, cancellationToken);
        return kbsBars
            .Select(b => new OhlcvBar(
                DateOnly.FromDateTime(b.Time.LocalDateTime),
                b.Open,
                b.High,
                b.Low,
                b.Close,
                b.Volume))
            .OrderBy(b => b.Date)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> ResolveSymbolsAsync(
        string[]? requestGroups,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        if (requestGroups is { Length: > 0 })
            return await groups.GetUnionSymbolsAsync(requestGroups, bypassCache, cancellationToken);

        var cfg = options.Value.History;
        if (string.Equals(cfg.Universe, "AllListed", StringComparison.OrdinalIgnoreCase))
        {
            var map = await listings.GetListingsAsync(bypassCache, cancellationToken);
            return map.Keys.OrderBy(s => s).ToList();
        }

        if (string.Equals(cfg.Universe, "Groups", StringComparison.OrdinalIgnoreCase)
            || cfg.Groups.Length > 0)
            return await groups.GetUnionSymbolsAsync(cfg.Groups, bypassCache, cancellationToken);

        return await groups.GetSymbolsByGroupAsync(cfg.Exchange, bypassCache, cancellationToken);
    }

    private HistoryBackfillResultDto Finish(
        int total,
        int screened,
        int inUniverse,
        int succeeded,
        int barsWritten,
        int excluded,
        List<string> failed)
    {
        state.Finish(total);
        logger.LogInformation(
            "Job 1 xong: {InUniverse}/{Total} vào universe, {Excluded} loại, {Failed} lỗi, {Bars} nến.",
            inUniverse,
            total,
            excluded,
            failed.Count,
            barsWritten);

        return new HistoryBackfillResultDto(
            total,
            screened,
            inUniverse,
            succeeded,
            failed.Count,
            excluded,
            barsWritten,
            failed,
            DateTime.UtcNow);
    }

    private static DateOnly ParseStartDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : new DateOnly(2000, 1, 1);

    private static DateOnly? ParseEndDate(string? value) =>
        value is not null && DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}

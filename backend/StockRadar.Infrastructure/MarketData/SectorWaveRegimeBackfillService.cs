using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Backfill một lần trạng thái Sóng ngành (spec 007) cho các phiên đã qua, dùng đúng lịch sử
/// OHLCV đã cắt tới từng ngày (point-in-time — không nhìn thấy dữ liệu sau ngày đó), tái dùng
/// nguyên vẹn <see cref="ISmartMoneyOpportunitySelector.BuildContext"/> để đảm bảo khớp 100%
/// với cách <see cref="DailyAnalysisRunner"/> tính sóng ngành mỗi ngày.
/// KHÔNG đụng Buy Score / Top / DailyOpportunities — chỉ ghi bảng SectorWaveRegimes.
/// </summary>
internal sealed class SectorWaveRegimeBackfillService(
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
    ISmartMoneyOpportunitySelector smartMoney,
    ISectorWaveRegimeEngine sectorWaveRegimeEngine,
    ISectorWaveRegimeRepository sectorWaveRegimes,
    ISignalAnalyzer signals,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    ILogger<SectorWaveRegimeBackfillService> logger) : ISectorWaveRegimeBackfillService
{
    public async Task<SectorWaveRegimeBackfillResultDto> RunAsync(
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        var runup = runupFilter.Value.ToSettings();
        var sm = smartMoneyOptions.Value.ToSettings();

        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var all = await stocks.GetAllAsync(cancellationToken);

        var tradingDates = index.Bars
            .Select(b => b.Date)
            .Where(d => d >= fromDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        logger.LogInformation(
            "Backfill sóng ngành từ {FromDate} — {Count} phiên tìm thấy trong lịch sử VNINDEX.",
            fromDate, tradingDates.Count);

        var running = new Dictionary<string, SectorWaveRegimeState?>(StringComparer.OrdinalIgnoreCase);
        var rowsWritten = 0;

        foreach (var tradingDate in tradingDates)
        {
            var indexBarsUpToDate = index.Bars.Where(b => b.Date <= tradingDate).ToList();
            if (indexBarsUpToDate.Count == 0)
                continue;

            var truncatedIndex = index with
            {
                History = indexBarsUpToDate,
                ChangePercent = signals.GetChangePercent(indexBarsUpToDate, 1),
                ChangePercent5d = signals.GetChangePercent(indexBarsUpToDate, 5),
            };

            var truncatedUniverse = all
                .Select(s => s with { History = s.History.Where(b => b.Date <= tradingDate).ToList() })
                .ToList();

            var context = smartMoney.BuildContext(truncatedUniverse, truncatedIndex, runup, sm);

            foreach (var (sector, snapshot) in context.SectorSnapshots)
            {
                if (!running.TryGetValue(sector, out var previous))
                {
                    previous = await sectorWaveRegimes.GetLatestAsync(sector, cancellationToken);
                    if (previous is not null && previous.TradingDate >= fromDate)
                        previous = null; // trong phạm vi backfill — tính lại từ đầu, không kế thừa bản ghi cũ cùng khoảng.
                }

                var next = sectorWaveRegimeEngine.Advance(sector, previous, snapshot, tradingDate, sm.SectorWaveThresholds);
                await sectorWaveRegimes.UpsertAsync(next, cancellationToken);
                running[sector] = next;
                rowsWritten++;
            }
        }

        logger.LogInformation(
            "Backfill sóng ngành xong — {Dates} phiên, {Rows} dòng (ngành×phiên) đã ghi.",
            tradingDates.Count, rowsWritten);

        return new SectorWaveRegimeBackfillResultDto(fromDate, tradingDates, rowsWritten, DateTime.UtcNow);
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Phân tích SmartMoney sau Job 2 → watchlist phiên mai.</summary>
internal sealed class DailyAnalysisRunner(
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
    ISmartMoneyOpportunitySelector smartMoney,
    ISignalAnalyzer signals,
    IDailyOpportunityRepository opportunities,
    IDailyAnalysisRunRepository analysisRuns,
    IDailyCriterionScoringService criterionScoring,
    IOptions<MarketJobsOptions> options,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    ILogger<DailyAnalysisRunner> logger) : IDailyAnalysisService
{
    public async Task<DailyAnalysisResultDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value.DailyAnalysis;
        var runup = runupFilter.Value;
        var sm = smartMoneyOptions.Value.ToSettings();
        var forTradingDate = VietnamMarketCalendar.GetActiveOpportunityDate(new TimeSpan(15, 10, 0));
        var generatedAt = DateTime.UtcNow;

        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var all = await stocks.GetAllAsync(cancellationToken);
        if (all.Count == 0)
        {
            logger.LogWarning("Phân tích: DB trống — chạy Job 1 trước.");
            return new DailyAnalysisResultDto(forTradingDate, 0, 0, generatedAt, 0);
        }

        logger.LogInformation("Phân tích — {Count} mã universe (DB trực tiếp, không cache API)...", all.Count);

        var context = smartMoney.BuildContext(all, index, runup.ToSettings(), sm);
        logger.LogInformation(
            "VNINDEX {Trend} ({Change:0.##}% / 5d {Change5d:0.##}%), pha {Phase}, loc tang >{MaxGain}% so voi dinh nen.",
            index.Trend,
            index.ChangePercent,
            index.IndexChange5d,
            context.MarketPhase,
            runup.MaxGainFromBasePercent);

        var topSectors = context.SectorSnapshots.Values
            .OrderBy(s => s.Rank)
            .Take(5)
            .Select(s => $"{s.Name}=#{s.Rank}")
            .ToList();
        if (topSectors.Count > 0)
            logger.LogInformation("Ngành mạnh: {Sectors}", string.Join(", ", topSectors));

        var candidates = new List<(Domain.Entities.Stock Stock, SmartMoneyEvaluation Eval)>();
        var runupExcluded = 0;
        foreach (var stock in all)
        {
            var eval = smartMoney.Evaluate(stock, context);
            if (!smartMoney.PassesFilter(eval, sm))
            {
                if (eval.Reasons.Any(r => r.Contains("FOMO", StringComparison.OrdinalIgnoreCase)
                    || r.Contains("so voi", StringComparison.OrdinalIgnoreCase)))
                    runupExcluded++;
                continue;
            }
            if (cfg.MinScore > 0 && eval.Score < cfg.MinScore)
                continue;
            candidates.Add((stock, eval));
        }

        var ordered = candidates
            .OrderByDescending(x => x.Eval.Score)
            .ThenBy(x => x.Eval.SectorRank)
            .ThenByDescending(x => x.Eval.RelativeStrength5d)
            .ThenBy(x => x.Stock.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cfg.MaxResults > 0)
            ordered = ordered.Take(cfg.MaxResults).ToList();

        var records = ordered
            .Select((item, rank) => new DailyOpportunityRecord(
                forTradingDate,
                rank + 1,
                item.Stock.Symbol,
                item.Stock.Name,
                item.Stock.Sector,
                item.Eval.Score,
                item.Stock.LatestPrice,
                signals.GetChangePercent(item.Stock, 1),
                item.Eval.VolumeRatio,
                generatedAt))
            .ToList();

        await opportunities.ReplaceForDateAsync(forTradingDate, records, cancellationToken);
        await analysisRuns.UpsertAsync(
            forTradingDate,
            generatedAt,
            all.Count,
            records.Count,
            cancellationToken);

        logger.LogInformation(
            "Phân tích xong: {Saved} cơ hội cho {ForDate} (từ {Total} mã), {RunupExcluded} loại vì vượt nền.",
            records.Count,
            forTradingDate,
            all.Count,
            runupExcluded);

        try
        {
            var scored = await criterionScoring.RunAfterAnalysisAsync(cancellationToken);
            logger.LogInformation("Chấm điểm tiêu chí T-1: {Count} mã lưu snapshot.", scored);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chấm điểm tiêu chí thất bại — bỏ qua.");
        }

        return new DailyAnalysisResultDto(
            forTradingDate,
            all.Count,
            records.Count,
            generatedAt,
            0);
    }
}

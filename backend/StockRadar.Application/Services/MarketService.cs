using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class MarketService(
    IStockRepository stocks,
    IMarketIndexProvider marketIndex,
    IQuoteTickCache quoteCache,
    IChartBarProvider chartBars,
    ISignalAnalyzer signalAnalyzer,
    ISignalFormatter formatter,
    IDailyOpportunityRepository dailyOpportunities,
    IDailyAnalysisRunRepository analysisRuns,
    IDailyAnalysisService dailyAnalysis,
    SmartMoneyEvaluationService smartMoneyEval,
    IOptions<MarketJobsOptions> jobOptions) : IMarketService
{
    private readonly int _analysisCooldownMinutes =
        jobOptions.Value.DailyAnalysis.ManualAnalysisCooldownMinutes;
    public async Task<MarketOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        return DtoMapper.ToDto(index);
    }

    public async Task<IReadOnlyList<QuoteTickDto>> GetQuoteSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = quoteCache.GetQuotes();
        if (cached.Count > 0)
            return cached;

        var all = await stocks.GetAllAsync(cancellationToken);
        var now = DateTime.UtcNow;

        return all
            .Select(stock =>
            {
                var last = stock.History.LastOrDefault();
                if (last is null)
                    return null;

                var change = stock.LastChangePercent != 0
                    ? stock.LastChangePercent
                    : signalAnalyzer.GetChangePercent(stock);

                return new QuoteTickDto(
                    stock.Symbol,
                    last.Close,
                    change,
                    last.Volume,
                    now);
            })
            .Where(t => t is not null)
            .Cast<QuoteTickDto>()
            .OrderBy(t => t.Symbol)
            .ToList();
    }

    public async Task<IReadOnlyList<SparklineDto>> GetSparklinesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var unique = symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();

        if (unique.Count == 0)
            return [];

        var results = await Task.WhenAll(unique.Select(sym => BuildSparklineAsync(sym, cancellationToken)));
        return results.Where(r => r is not null).Cast<SparklineDto>().ToList();
    }

    private async Task<SparklineDto?> BuildSparklineAsync(string symbol, CancellationToken cancellationToken)
    {
        var bars = await chartBars.FetchAsync(symbol, "5m", cancellationToken);
        if (bars.Count < 2)
            bars = await chartBars.FetchAsync(symbol, "1D", cancellationToken);

        if (bars.Count < 2)
            return null;

        var prices = bars.Select(b => b.Close).ToList();
        var reference = bars[0].Open > 0 ? bars[0].Open : prices[0];
        return new SparklineDto(symbol, prices, reference);
    }

    public async Task<PagedResult<SectorDto>> GetSectorsAsync(
        PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var context = await smartMoneyEval.BuildContextAsync(cancellationToken);
        var sectors = context.SectorSnapshots.Values
            .OrderBy(s => s.Rank)
            .Select(s => new SectorDto(
                s.Name,
                (int)Math.Round(s.CompositeScore * 100),
                s.AvgChange5d));

        return sectors.ToPagedResult(query);
    }

    public async Task<OpportunitiesListDto> GetOpportunitiesAsync(
        PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Normalize();
        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var cached = await dailyOpportunities.GetForDateAsync(targetDate, cancellationToken);
        var analysisRun = await analysisRuns.GetForDateAsync(targetDate, cancellationToken);

        if (cached.Count == 0 && analysisRun is null)
        {
            var message = TradingCalendar.IsTradingDay(TradingCalendar.TodayVietnam())
                ? $"Chưa có danh sách cơ hội cho phiên {targetDate:dd/MM/yyyy}. Chạy phân tích SmartMoney sau Job 2."
                : $"Hôm nay không phải phiên giao dịch. Danh sách cơ hội cho {targetDate:dd/MM/yyyy} chưa được tạo.";

            var (canRunEmpty, availableAtEmpty) = GetAnalysisCooldownState(null);
            return new OpportunitiesListDto(
                [],
                query.Page,
                query.PageSize,
                0,
                false,
                message,
                targetDate,
                null,
                true,
                canRunEmpty,
                availableAtEmpty);
        }

        var lastAnalysisAt = GetLastSuccessfulAnalysisAt(analysisRun, cached);
        var (canRun, analysisAvailableAt) = GetAnalysisCooldownState(lastAnalysisAt);

        if (cached.Count == 0 && analysisRun is not null)
        {
            return new OpportunitiesListDto(
                [],
                query.Page,
                query.PageSize,
                0,
                true,
                $"Đã phân tích {analysisRun.GeneratedAt.ToLocalTime():dd/MM/yyyy HH:mm} — không có mã đạt SmartMoney ({analysisRun.StocksScored} mã quét).",
                targetDate,
                analysisRun.GeneratedAt,
                false,
                canRun,
                analysisAvailableAt);
        }

        var generatedAt = cached.Max(r => r.GeneratedAt);
        var dtos = cached
            .OrderBy(r => r.Rank)
            .Select(r => new OpportunityDto(
                r.Symbol,
                r.Name,
                r.Score,
                r.Price,
                r.ChangePercent,
                r.VolumeRatio,
                r.Sector,
                r.GeneratedAt))
            .ToList();

        var page = dtos.Skip(query.Skip).Take(query.PageSize).ToList();
        return new OpportunitiesListDto(
            page,
            query.Page,
            query.PageSize,
            dtos.Count,
            true,
            null,
            targetDate,
            generatedAt,
            false,
            canRun,
            analysisAvailableAt);
    }

    public async Task<DailyAnalysisResultDto> RunOpportunityAnalysisAsync(
        CancellationToken cancellationToken = default)
    {
        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var cached = await dailyOpportunities.GetForDateAsync(targetDate, cancellationToken);
        var analysisRun = await analysisRuns.GetForDateAsync(targetDate, cancellationToken);
        var lastAnalysisAt = GetLastSuccessfulAnalysisAt(analysisRun, cached);
        var (canRun, analysisAvailableAt) = GetAnalysisCooldownState(lastAnalysisAt);

        if (!canRun)
        {
            var waitUntil = analysisAvailableAt!.Value.ToLocalTime();
            throw new AppException(
                "Phân tích quá gần",
                $"Vui lòng chờ {_analysisCooldownMinutes} phút kể từ lần phân tích thành công cuối. Chạy lại sau {waitUntil:HH:mm}.",
                429);
        }

        return await dailyAnalysis.RunAsync(cancellationToken);
    }

    private static DateTime? GetLastSuccessfulAnalysisAt(
        DailyAnalysisRunRecord? analysisRun,
        IReadOnlyList<DailyOpportunityRecord> cached)
    {
        if (analysisRun is not null)
            return analysisRun.GeneratedAt;

        if (cached.Count > 0)
            return cached.Max(r => r.GeneratedAt);

        return null;
    }

    private (bool CanRun, DateTime? AvailableAt) GetAnalysisCooldownState(DateTime? lastSuccessUtc)
    {
        if (lastSuccessUtc is null)
            return (true, null);

        var availableAt = lastSuccessUtc.Value.AddMinutes(_analysisCooldownMinutes);
        if (DateTime.UtcNow >= availableAt)
            return (true, null);

        return (false, availableAt);
    }

    public async Task<PagedResult<SignalDto>> GetSignalsAsync(
        PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var all = await stocks.GetAllAsync(cancellationToken);

        var signals = all
            .Where(stock => stock.History.Count > 0)
            .SelectMany(stock =>
            {
                var detectedAt = TradingCalendar.GetSignalDetectedAt(stock.History[^1].Date);
                return signalAnalyzer.DetectSignals(stock, index.ChangePercent).Select(type => new SignalDto(
                    stock.Symbol,
                    type,
                    formatter.FormatTitle(type, stock.Symbol),
                    formatter.FormatDescription(type, stock.Symbol, signalAnalyzer.GetVolumeRatio(stock.History)),
                    detectedAt));
            })
            .OrderByDescending(s => s.CreatedAt);

        return signals.ToPagedResult(query);
    }
}

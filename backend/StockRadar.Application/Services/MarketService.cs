using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Enums;
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
    ISetupTrackRepository setupTracks,
    SmartMoneyEvaluationService smartMoneyEval,
    IEngineTrustQueryService engineTrust,
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
        var trust = await engineTrust.GetAsync(cancellationToken);
        var targetCached = await dailyOpportunities.GetForDateAsync(targetDate, cancellationToken);
        var analysisRun = await analysisRuns.GetForDateAsync(targetDate, cancellationToken);
        var cached = targetCached;
        var displayDate = targetDate;
        string? fallbackNote = null;

        if (cached.Count == 0)
        {
            var latest = await dailyOpportunities.GetLatestForDateAsync(cancellationToken);
            if (latest is not null && latest != targetDate)
            {
                var previous = await dailyOpportunities.GetForDateAsync(latest.Value, cancellationToken);
                if (previous.Count > 0)
                {
                    cached = previous;
                    displayDate = latest.Value;
                    fallbackNote = analysisRun is not null
                        ? BuildAnalyzedFallbackNote(targetDate, displayDate, analysisRun)
                        : $"Chưa có list cho phiên {targetDate:dd/MM/yyyy}. Hiển thị bản gần nhất ({displayDate:dd/MM/yyyy}).";
                }
            }
        }

        var lastAnalysisAt = analysisRun?.GeneratedAt;
        var (canRun, analysisAvailableAt) = GetAnalysisCooldownState(lastAnalysisAt);
        var analysisStatus = ResolveAnalysisStatus(analysisRun, targetDate, displayDate, targetCached.Count);
        var scanTimestamp = lastAnalysisAt ?? (cached.Count > 0 ? cached.Max(r => r.GeneratedAt) : (DateTime?)null);

        if (cached.Count == 0 && analysisRun is null)
        {
            var message = TradingCalendar.IsTradingDay(TradingCalendar.TodayVietnam())
                ? $"Chưa phân tích phiên {targetDate:dd/MM/yyyy}. Bấm «Chạy phân tích» sau Job 2."
                : $"Hôm nay không phải phiên giao dịch. Chưa có list cho {targetDate:dd/MM/yyyy}.";

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
                availableAtEmpty,
                trust,
                OpportunityAnalysisStatuses.NotRun,
                null,
                targetDate,
                null,
                null);
        }

        if (cached.Count == 0 && analysisRun is not null)
        {
            return new OpportunitiesListDto(
                [],
                query.Page,
                query.PageSize,
                0,
                true,
                BuildZeroMatchesMessage(analysisRun),
                targetDate,
                lastAnalysisAt,
                false,
                canRun,
                analysisAvailableAt,
                trust,
                OpportunityAnalysisStatuses.ZeroMatches,
                lastAnalysisAt,
                targetDate,
                analysisRun.StocksScored,
                analysisRun.OpportunitiesSaved);
        }

        var trackFallback = await setupTracks.GetOpportunityMapForDateAsync(displayDate, cancellationToken);

        var dtos = cached
            .Select(r => ToOpportunityDto(r, trackFallback))
            .OrderByDescending(d => d.PredictedHitPercent)
            .ThenByDescending(d => d.Score)
            .ToList();

        var page = dtos.Skip(query.Skip).Take(query.PageSize).ToList();
        var hasFreshData = displayDate == targetDate;
        var statusMessage = fallbackNote;
        if (displayDate != targetDate && fallbackNote is null)
            statusMessage = $"Danh sách cho phiên {displayDate:dd/MM/yyyy}.";

        return new OpportunitiesListDto(
            page,
            query.Page,
            query.PageSize,
            dtos.Count,
            hasFreshData,
            statusMessage,
            displayDate,
            scanTimestamp,
            false,
            canRun,
            analysisAvailableAt,
            trust,
            analysisStatus,
            lastAnalysisAt,
            targetDate,
            analysisRun?.StocksScored,
            analysisRun?.OpportunitiesSaved);
    }

    public async Task<IReadOnlyList<string>> GetOpportunitySymbolsAsync(
        CancellationToken cancellationToken = default)
    {
        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var symbols = await dailyOpportunities.GetSymbolsForDateAsync(targetDate, cancellationToken);
        if (symbols.Count > 0)
            return symbols;

        var latest = await dailyOpportunities.GetLatestForDateAsync(cancellationToken);
        if (latest is null)
            return [];

        return await dailyOpportunities.GetSymbolsForDateAsync(latest.Value, cancellationToken);
    }

    private static OpportunityDto ToOpportunityDto(
        DailyOpportunityRecord r,
        IReadOnlyDictionary<string, SetupTrackRecord> trackFallback)
    {
        trackFallback.TryGetValue(r.Symbol, out var track);

        var score = r.BuyScore ?? track?.OpportunityScore ?? r.Score;
        var predictedHit = r.PredictedHitPercent ?? track?.PredictedHitPercent ?? 0m;
        var predictedSamples = r.PredictedSampleCount ?? 0;
        var setupDna = r.SetupDna ?? track?.SetupDna;
        var entry = EntryPointJsonMapper.FromJson(r.EntryPointJson);
        var explain = ExplainLinesJsonMapper.FromJson(r.ExplainJson);
        ResolveOpportunityTradeState(r, score, entry, out var tradeState, out var tradeStateLabelVi, out var tradeStateReason, out var recommendation);

        return new OpportunityDto(
            r.Symbol,
            r.Name,
            score,
            r.Price,
            r.ChangePercent,
            r.VolumeRatio,
            r.Sector,
            r.GeneratedAt,
            entry,
            recommendation,
            tradeState,
            tradeStateLabelVi,
            tradeStateReason,
            predictedHit,
            predictedSamples,
            setupDna,
            explain);
    }

    private static void ResolveOpportunityTradeState(
        DailyOpportunityRecord r,
        int score,
        EntryPointDto? entry,
        out string tradeState,
        out string tradeStateLabelVi,
        out string tradeStateReason,
        out string recommendation)
    {
        if (!string.IsNullOrEmpty(r.TradeState)
            && Enum.TryParse<StockTradeState>(r.TradeState, out var stored))
        {
            tradeState = stored.ToString();
            tradeStateLabelVi = TradeStateLabels.ToVi(stored);
            tradeStateReason = r.TradeStateReason ?? "";
            recommendation = TradeStateLabels.ToLegacyRecommendation(stored, score).ToString();
            return;
        }

        recommendation = OpportunityListRecommendation.NormalizeStored(r.Recommendation, score)
            ?? nameof(BuyRecommendation.Watch);

        if (string.Equals(recommendation, nameof(BuyRecommendation.Avoid), StringComparison.Ordinal))
        {
            tradeState = StockTradeState.Avoid.ToString();
            tradeStateLabelVi = TradeStateLabels.ToVi(StockTradeState.Avoid);
            tradeStateReason = entry?.Headline ?? "Không đạt tiêu chí tối thiểu";
            return;
        }

        if (entry is not null
            && string.Equals(entry.Status, nameof(EntryPointStatus.Ready), StringComparison.Ordinal))
        {
            tradeState = StockTradeState.Actionable.ToString();
            tradeStateLabelVi = TradeStateLabels.ToVi(StockTradeState.Actionable);
            tradeStateReason = score >= 80
                ? "Mua mạnh — đạt chuẩn SmartMoney"
                : "Đạt chuẩn SmartMoney";
            return;
        }

        if (entry?.Headline.Contains("MA stack", StringComparison.OrdinalIgnoreCase) == true
            || entry?.Headline.Contains("xu hướng dài hạn", StringComparison.OrdinalIgnoreCase) == true)
        {
            tradeState = StockTradeState.AwaitingTrigger.ToString();
            tradeStateLabelVi = TradeStateLabels.ToVi(StockTradeState.AwaitingTrigger);
            tradeStateReason = entry.Headline;
            return;
        }

        tradeState = StockTradeState.Watchlist.ToString();
        tradeStateLabelVi = TradeStateLabels.ToVi(StockTradeState.Watchlist);
        tradeStateReason = entry?.Headline ?? "Chưa phá vỡ nền / Chờ phiên kích hoạt";
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
            var waitUntil = TradingCalendar.FormatVietnamTime(analysisAvailableAt!.Value);
            throw new AppException(
                "Phân tích quá gần",
                $"Vui lòng chờ {_analysisCooldownMinutes} phút kể từ lần phân tích thành công cuối. Chạy lại sau {waitUntil}.",
                429);
        }

        return await dailyAnalysis.RunAsync(cancellationToken, runPostProcessing: false);
    }

    private static string BuildZeroMatchesMessage(DailyAnalysisRunRecord analysisRun)
    {
        var when = TradingCalendar.FormatVietnamDateTime(analysisRun.GeneratedAt);
        return $"Quét xong lúc {when} — 0 mã strict / {analysisRun.StocksScored} mã trong universe (MinPassScore strict).";
    }

    private static string ResolveAnalysisStatus(
        DailyAnalysisRunRecord? analysisRun,
        DateOnly targetDate,
        DateOnly displayDate,
        int targetCachedCount)
    {
        if (analysisRun is null)
        {
            return targetCachedCount > 0 && displayDate == targetDate
                ? OpportunityAnalysisStatuses.HasResults
                : displayDate != targetDate
                    ? OpportunityAnalysisStatuses.ReferenceList
                    : OpportunityAnalysisStatuses.NotRun;
        }

        if (analysisRun.OpportunitiesSaved == 0)
            return OpportunityAnalysisStatuses.ZeroMatches;

        return displayDate == targetDate
            ? OpportunityAnalysisStatuses.HasResults
            : OpportunityAnalysisStatuses.ReferenceList;
    }

    private static string BuildAnalyzedFallbackNote(
        DateOnly targetDate,
        DateOnly displayDate,
        DailyAnalysisRunRecord analysisRun)
    {
        var when = TradingCalendar.FormatVietnamDateTime(analysisRun.GeneratedAt);
        var outcome = analysisRun.OpportunitiesSaved == 0
            ? $"không có mã đạt SmartMoney ({analysisRun.StocksScored} mã quét)"
            : $"{analysisRun.OpportunitiesSaved} mã trong top ({analysisRun.StocksScored} mã quét)";

        return $"Quét phiên {targetDate:dd/MM/yyyy} lúc {when} — {outcome}. Danh sách bên dưới chỉ tham khảo ({displayDate:dd/MM/yyyy}).";
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

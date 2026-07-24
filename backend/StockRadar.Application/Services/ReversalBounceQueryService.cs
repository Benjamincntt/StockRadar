using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Application.Services;

internal sealed class ReversalBounceQueryService(
    IMarketBreadthSnapshotRepository breadth,
    IReversalCandidateSnapshotRepository snapshots,
    IReversalBounceAnalysisService analysis,
    IMarketIndexProvider marketIndex,
    IOptions<SmartMoneyOptions> smartMoneyOptions)
    : IReversalBounceQueryService
{
    public async Task<MarketRegimeDto> GetMarketRegimeAsync(CancellationToken cancellationToken = default)
    {
        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var (phase, phaseLabel) = await ResolveGrowthPhaseAsync(cancellationToken);
        var snapshot = await breadth.GetForDateAsync(targetDate, cancellationToken);
        string? statusMessage = null;

        if (snapshot is null)
        {
            snapshot = await breadth.GetLatestAsync(cancellationToken);
            if (snapshot is not null)
                statusMessage =
                    $"Chưa có breadth cho phiên {targetDate:dd/MM/yyyy}. Metrics từ bản gần nhất ({snapshot.TradingDate:dd/MM/yyyy}).";
        }

        // Nhận định thị trường UI = pha Top; cho phép bắt đáy khi không còn TT thuận.
        var allowsEntry = phase != MarketWyckoffPhase.Favorable;

        if (snapshot is null)
        {
            return new MarketRegimeDto(
                targetDate,
                phase.ToString(),
                phaseLabel,
                AllowsCounterTrendEntry: allowsEntry,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                VnIndexAboveMa20: false,
                VnIndexReclaimedMa20: false,
                ImproveStreak: 0,
                StatusMessage: statusMessage
                    ?? $"Chưa có snapshot breadth. Chạy phân tích trước ({targetDate:dd/MM/yyyy}).",
                BreadthRegime: null,
                BreadthRegimeLabel: null);
        }

        return new MarketRegimeDto(
            snapshot.TradingDate,
            phase.ToString(),
            phaseLabel,
            AllowsCounterTrendEntry: allowsEntry,
            snapshot.UniverseCount,
            snapshot.PctAboveMa20,
            snapshot.PctAboveMa50,
            snapshot.PctNewLow20,
            snapshot.PctUp,
            snapshot.PctDown,
            snapshot.FloorCount,
            snapshot.CeilingCount,
            snapshot.MedianReturnPercent,
            snapshot.VnIndexDrawdownPercent,
            snapshot.VnIndexDistanceToMa20Percent,
            snapshot.VnIndexAboveMa20,
            snapshot.VnIndexReclaimedMa20,
            snapshot.ImproveStreak,
            statusMessage,
            BreadthRegime: snapshot.Regime.ToString(),
            BreadthRegimeLabel: BreadthRegimeLabel(snapshot.Regime));
    }

    public async Task<ReversalBounceListDto> GetCandidatesAsync(
        DateOnly? date,
        string? stage,
        bool? actionableOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var targetDate = date ?? TradingCalendar.GetActiveOpportunityDate();
        var all = await snapshots.GetForDateAsync(targetDate, actionableOnly, cancellationToken);
        string? statusMessage = all.Count == 0
            ? $"Chưa có ứng viên sóng hồi cho phiên {targetDate:dd/MM/yyyy}."
            : null;

        if (!string.IsNullOrWhiteSpace(stage)
            && Enum.TryParse<ReversalBounceStage>(stage, ignoreCase: true, out var stageFilter))
        {
            all = all.Where(s => s.Stage == stageFilter).ToList();
        }

        var (phase, _) = await ResolveGrowthPhaseAsync(cancellationToken);
        var phaseKey = phase.ToString();
        var total = all.Count;
        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToItem(s, phaseKey))
            .ToList();

        return new ReversalBounceListDto(
            items,
            page,
            pageSize,
            total,
            targetDate,
            phaseKey,
            statusMessage);
    }

    public async Task<ReversalBounceDetailDto?> GetBySymbolAsync(
        string symbol,
        int lookback,
        CancellationToken cancellationToken = default)
    {
        lookback = lookback is < 1 or > 250 ? 30 : lookback;
        var to = TradingCalendar.GetActiveOpportunityDate();
        var from = to.AddDays(-lookback * 2);
        var sym = symbol.ToUpperInvariant();
        var history = await snapshots.GetHistoryAsync(sym, from, to, cancellationToken);

        var live = await analysis.AnalyzeSymbolLiveAsync(sym, cancellationToken);
        if (live is null && history.Count == 0)
            return null;

        var (phase, _) = await ResolveGrowthPhaseAsync(cancellationToken);
        var phaseKey = phase.ToString();
        var current = live ?? history[^1];
        var historyItems = history
            .OrderByDescending(s => s.TradingDate)
            .Take(lookback)
            .Select(s => new ReversalBounceHistoryItemDto(
                s.TradingDate,
                s.Stage.ToString(),
                s.TotalScore,
                s.Reasons.Select(ToReason).ToList()))
            .ToList();

        return new ReversalBounceDetailDto(ToItem(current, phaseKey), historyItems);
    }

    private async Task<(MarketWyckoffPhase Phase, string LabelVi)> ResolveGrowthPhaseAsync(
        CancellationToken cancellationToken)
    {
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var classified = MarketPhaseClassifier.Classify(
            index.Bars,
            smartMoneyOptions.Value.ToSettings().PhaseThresholds);
        return (classified.Phase, MarketPhaseDisplay.LabelVi(classified.Phase));
    }

    private static ReversalBounceItemDto ToItem(ReversalCandidateSnapshot s, string marketPhase) => new(
        Symbol: s.Symbol,
        Stage: s.Stage.ToString(),
        IsActionable: s.IsActionable,
        TotalScore: s.TotalScore,
        RecoveryAttemptCount: s.RecoveryAttemptCount,
        CapitulationDate: s.CapitulationDate,
        ComponentScores: new ReversalBounceComponentScoreDto(
            s.ComponentScores.Capitulation,
            s.ComponentScores.Stabilization,
            s.ComponentScores.Demand,
            s.ComponentScores.RelativeStrength,
            s.ComponentScores.Liquidity,
            s.ComponentScores.RiskPenalty),
        EntryReference: s.TradePlan?.EntryReference,
        MaxEntryPrice: s.TradePlan?.MaxEntryPrice,
        InvalidationPrice: s.TradePlan?.InvalidationPrice,
        FirstTarget: s.TradePlan?.FirstTarget,
        RewardToRisk: s.TradePlan?.RewardToRisk,
        PositionFactor: s.TradePlan?.PositionFactor,
        RiskWarnings: s.TradePlan?.RiskWarnings ?? [],
        MarketRegime: marketPhase,
        Reasons: s.Reasons.Select(ToReason).ToList());

    private static ReversalBounceReasonDto ToReason(ReversalBounceReason r) =>
        new(r.Code, r.Label, r.NumericValue, r.Threshold, r.Pass);

    private static string BreadthRegimeLabel(MarketRegime regime) => regime switch
    {
        MarketRegime.Panic => "Đang bán tháo",
        MarketRegime.Stabilizing => "Đang cân bằng",
        MarketRegime.ReboundConfirmed => "Hồi phục xác nhận",
        _ => "Bình thường"
    };
}

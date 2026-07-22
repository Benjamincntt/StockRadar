using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Application.Services;

internal sealed class ReversalBounceQueryService(
    IMarketBreadthSnapshotRepository breadth,
    IReversalCandidateSnapshotRepository snapshots,
    IReversalBounceAnalysisService analysis)
    : IReversalBounceQueryService
{
    public async Task<MarketRegimeDto> GetMarketRegimeAsync(CancellationToken cancellationToken = default)
    {
        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var snapshot = await breadth.GetForDateAsync(targetDate, cancellationToken);
        string? statusMessage = null;

        if (snapshot is null)
        {
            snapshot = await breadth.GetLatestAsync(cancellationToken);
            if (snapshot is not null)
                statusMessage =
                    $"Chưa có regime cho phiên {targetDate:dd/MM/yyyy}. Hiển thị bản gần nhất ({snapshot.TradingDate:dd/MM/yyyy}).";
        }

        if (snapshot is null)
        {
            return new MarketRegimeDto(
                targetDate,
                MarketRegime.Normal.ToString(),
                RegimeLabel(MarketRegime.Normal),
                AllowsCounterTrendEntry: false,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                VnIndexAboveMa20: false,
                VnIndexReclaimedMa20: false,
                ImproveStreak: 0,
                StatusMessage: $"Chưa có snapshot breadth. Chạy phân tích trước ({targetDate:dd/MM/yyyy}).");
        }

        return new MarketRegimeDto(
            snapshot.TradingDate,
            snapshot.Regime.ToString(),
            RegimeLabel(snapshot.Regime),
            AllowsCounterTrendEntry: snapshot.Regime != MarketRegime.Panic,
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
            statusMessage);
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

        var regime = await breadth.GetForDateAsync(targetDate, cancellationToken);
        var total = all.Count;
        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToItem)
            .ToList();

        return new ReversalBounceListDto(
            items,
            page,
            pageSize,
            total,
            targetDate,
            regime?.Regime.ToString(),
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

        // Luôn cố live-analyze cho phiên hiện tại (kể cả Stage=None — batch không lưu None).
        var live = await analysis.AnalyzeSymbolLiveAsync(sym, cancellationToken);
        if (live is null && history.Count == 0)
            return null;

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

        return new ReversalBounceDetailDto(ToItem(current), historyItems);
    }

    private static ReversalBounceItemDto ToItem(ReversalCandidateSnapshot s) => new(
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
        MarketRegime: s.MarketRegime.ToString(),
        Reasons: s.Reasons.Select(ToReason).ToList());

    private static ReversalBounceReasonDto ToReason(ReversalBounceReason r) =>
        new(r.Code, r.Label, r.NumericValue, r.Threshold, r.Pass);

    private static string RegimeLabel(MarketRegime regime) => regime switch
    {
        MarketRegime.Panic => "Đang bán tháo",
        MarketRegime.Stabilizing => "Đang cân bằng",
        MarketRegime.ReboundConfirmed => "Hồi phục xác nhận",
        _ => "Bình thường"
    };
}

using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Engine quyết định mua thống nhất: gates, Buy Score, điểm vào, khuyến nghị.
/// </summary>
public interface IBuyDecisionEngine
{
    BuyDecisionEvaluation Evaluate(Stock stock, SmartMoneyMarketContext context);
}

public sealed record BuyScoreComponent(string Id, string Label, int Points, int MaxPoints, string Detail);

public sealed record BuyDecisionEvaluation(
    string Symbol,
    int BuyScore,
    int ActionScore,
    BuyRecommendation Recommendation,
    bool PassesTopFilter,
    string? GateFailure,
    WyckoffPhase StockPhase,
    int SectorRank,
    decimal RelativeStrength5d,
    decimal VolumeRatio,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<SignalType> Signals,
    IReadOnlyList<BuyScoreComponent> Breakdown,
    EntryPointEvaluation Entry,
    StockTradeState TradeState,
    string TradeStateReason,
    decimal PredictedHitPercent = 0,
    int PredictedSampleCount = 0,
    string? SetupDna = null,
    IReadOnlyList<string>? TopExplainLines = null);

public sealed class BuyDecisionEngine(ISignalAnalyzer signals) : IBuyDecisionEngine
{
    private const int StrongBuyMinScore = 80;
    private const int WatchMinScore = 70;
    private const int WatchMinScoreWithSetup = 60;

    public BuyDecisionEvaluation Evaluate(Stock stock, SmartMoneyMarketContext context)
    {
        var settings = context.Settings;
        var runup = context.RunupFilter;
        var history = stock.History;
        var index5d = context.IndexChangePercent5d;
        var detected = signals.DetectSignals(stock, context.Index.ChangePercent, runup);
        var volRatio = history.Count > 0 ? signals.GetVolumeRatio(history) : 0m;
        var rs5 = history.Count > 0 ? signals.GetRelativeStrength(stock, index5d, 5) : 0m;
        var sectorRank = context.SectorRank.GetValueOrDefault(stock.Sector, context.SectorCount + 1);
        var stockPhase = ClassifyStock(stock, detected, volRatio, settings.BreakoutMinVolumeRatio);
        var flatBox = history.Count > 0
            ? signals.AnalyzeFlatBox(history, runup)
            : FlatBoxProfile.None;
        var meetsSessionBar = history.Count > 0
            && signals.MeetsSessionEntryBar(history, settings.MinSessionChangePercent, settings.MinSessionVolume);
        var hasBreakoutEntry = (detected.Contains(SignalType.Breakout)
                || detected.Contains(SignalType.DarvasBreakout))
            && meetsSessionBar
            && (detected.Contains(SignalType.DarvasBreakout)
                || volRatio >= settings.BreakoutMinVolumeRatio);
        var hasShakeoutEntry = flatBox.HasValidBox
            && signals.IsShakeoutFromBase(history, runup)
            && meetsSessionBar;
        var hasMaStack = history.Count > 0
            && signals.HasBullishMaStack(
                history,
                ResolveMaStackStrictness(context.MarketPhase, settings),
                settings.MinSessionsForMa50,
                settings.MinSessionsForFullStack);
        var rsPercentile = context.RsPercentile.GetValueOrDefault(stock.Symbol, 0m);
        var latestClose = history.Count > 0 ? history[^1].Close : stock.LatestPrice;
        var darvasCfg = runup.Darvas ?? DarvasBoxSettings.Default;
        var hasFlatBoxBreakout = flatBox.IsBreakoutConfirmed
            && flatBox.GainFromBoxTopPercent <= runup.MaxGainFromBasePercent;
        var hasFlatBoxSetup = DarvasBreakoutAnalyzer.IsSetupZone(
            flatBox,
            latestClose,
            runup.MaxGainFromBasePercent,
            darvasCfg.TouchThresholdPercent);

        var (breakdown, reasons, score) = BuildScore(
            context,
            settings,
            sectorRank,
            rs5,
            volRatio,
            stockPhase,
            flatBox,
            latestClose,
            hasFlatBoxBreakout,
            hasFlatBoxSetup,
            hasBreakoutEntry,
            hasShakeoutEntry,
            detected.Contains(SignalType.VolumeSpike),
            hasMaStack);

        var entry = BuildEntry(
            stock,
            context,
            flatBox,
            detected,
            volRatio,
            rs5,
            sectorRank,
            hasBreakoutEntry,
            hasShakeoutEntry,
            hasMaStack,
            meetsSessionBar);

        var gateFailure = ResolveTopGateFailure(
            settings,
            runup,
            history,
            context,
            flatBox,
            sectorRank,
            rs5,
            rsPercentile,
            hasBreakoutEntry,
            hasShakeoutEntry,
            hasMaStack,
            hasFlatBoxSetup,
            score);

        entry = AlignEntryWithTopGate(entry, gateFailure);

        var passesTop = gateFailure is null;
        var recommendation = ResolveRecommendation(score, entry, gateFailure);

        if (passesTop && recommendation == BuyRecommendation.StrongBuy)
            reasons.Insert(0, "Đạt Top cơ hội");

        var forecast = HitProbabilityPredictor.Predict(
            score,
            breakdown,
            entry,
            context,
            sectorRank);

        var refinedEntry = RefineEntryConfidence(entry, forecast.PredictedHitPercent);
        var actionScore = gateFailure is null ? score : 0;
        var tradeState = TradeStateResolver.Resolve(refinedEntry, gateFailure, score);

        return new BuyDecisionEvaluation(
            stock.Symbol,
            score,
            actionScore,
            recommendation,
            passesTop,
            gateFailure,
            stockPhase,
            sectorRank,
            rs5,
            volRatio,
            reasons,
            detected,
            breakdown,
            refinedEntry,
            tradeState.State,
            tradeState.Reason,
            forecast.PredictedHitPercent,
            forecast.SampleCount,
            forecast.SetupDna,
            forecast.TopExplainLines);
    }

    private static (List<BuyScoreComponent> Breakdown, List<string> Reasons, int Score) BuildScore(
        SmartMoneyMarketContext context,
        SmartMoneySettings settings,
        int sectorRank,
        decimal rs5,
        decimal volRatio,
        WyckoffPhase stockPhase,
        FlatBoxProfile flatBox,
        decimal latestClose,
        bool hasFlatBoxBreakout,
        bool hasFlatBoxSetup,
        bool hasBreakoutEntry,
        bool hasShakeoutEntry,
        bool hasVolSpike,
        bool hasMaStack)
    {
        var breakdown = new List<BuyScoreComponent>();
        var reasons = new List<string>();
        var score = 0;
        var profile = context.Adaptive;

        void Add(string id, string label, int rawPoints, int baseMax, string detail)
        {
            var state = profile.GetState(id, baseMax);
            var effectiveMax = state.EffectiveMaxPoints;
            var scaled = effectiveMax > 0 && baseMax > 0
                ? (int)Math.Round((decimal)rawPoints / baseMax * effectiveMax)
                : 0;
            breakdown.Add(new(id, label, scaled, effectiveMax, detail));
            if (scaled > 0)
                reasons.Add(detail);
            score += scaled;
        }

        var marketPts = context.MarketPhase switch
        {
            MarketWyckoffPhase.Favorable => 12,
            MarketWyckoffPhase.Neutral => 6,
            _ => 0
        };
        Add(
            "market",
            "Thị trường",
            marketPts,
            12,
            context.MarketPhase switch
            {
                MarketWyckoffPhase.Favorable => "Pha thị trường thuận",
                MarketWyckoffPhase.Neutral => "Thị trường trung tính",
                _ => "Thị trường bất lợi"
            });

        var (sectorPts, sectorMax, sectorDetail) = sectorRank switch
        {
            <= 3 => (18, 18, $"Ngành top #{sectorRank}"),
            var r when r <= settings.TopSectorCount => (10, 18, $"Ngành mạnh #{sectorRank}"),
            _ => (0, 18, $"Ngành #{sectorRank}")
        };
        Add("sector", "Ngành", sectorPts, sectorMax, sectorDetail);

        var (rsPts, rsDetail) = rs5 switch
        {
            >= 3m => (20, $"RS +{rs5:0.#}% vs VN (5 phiên)"),
            >= 0m => (12, "Khỏe hơn VNINDEX (5 phiên)"),
            _ => (0, $"RS {rs5:0.#}%")
        };
        Add("rs", "Relative Strength", rsPts, 20, rsDetail);

        var baseEventLabel = flatBox.HasValidBox
            ? BasePriceLabels.ResolveEventLabel(flatBox, latestClose)
            : BasePriceLabels.Base;
        var (basePts, baseDetail) = hasFlatBoxBreakout
            ? (18, baseEventLabel)
            : hasFlatBoxSetup
                ? (12, "Hộp Darvas — test cạnh hộp")
                : (0, "Chưa phá vỡ nền giá");
        Add("base", BasePriceLabels.Base, basePts, 18, baseDetail);

        if (hasBreakoutEntry)
        {
            var breakoutDetail = hasFlatBoxBreakout
                ? baseEventLabel
                : $"Breakout Vol×{volRatio:0.0}";
            Add("breakout", "Breakout + volume", 22, 22, breakoutDetail);
        }
        else
        {
            var max = profile.GetState("breakout", 22).EffectiveMaxPoints;
            breakdown.Add(new("breakout", "Breakout + volume", 0, max, "Chưa breakout đủ điều kiện"));
        }

        if (hasShakeoutEntry)
            Add("shakeout", "Shakeout đáy nền", 10, 10, "Shakeout đáy nền + hồi phục");
        else
        {
            var max = profile.GetState("shakeout", 10).EffectiveMaxPoints;
            breakdown.Add(new("shakeout", "Shakeout đáy nền", 0, max, "Chưa shakeout"));
        }

        if (hasVolSpike)
            Add("volume", "Volume spike", 8, 8, "KL bất thường");
        else
        {
            var max = profile.GetState("volume", 8).EffectiveMaxPoints;
            breakdown.Add(new("volume", "Volume spike", 0, max, $"Vol×{volRatio:0.0}"));
        }

        if (stockPhase == WyckoffPhase.Markup)
            Add("wyckoff", "Pha tăng giá", 5, 5, "Pha tăng giá");
        else
        {
            var max = profile.GetState("wyckoff", 5).EffectiveMaxPoints;
            breakdown.Add(new("wyckoff", "Pha tăng giá", 0, max, "Chưa markup"));
        }

        Add(
            "trend",
            "Xu hướng / MA",
            hasMaStack ? 5 : 0,
            5,
            hasMaStack ? "MA stack tăng" : "Chưa MA stack");

        return (breakdown, reasons, Math.Clamp(NormalizeAdaptiveScore(score, profile), 0, 100));
    }

    private static int NormalizeAdaptiveScore(int rawScore, AdaptiveScoringProfile profile)
    {
        var maxTotal = AdaptiveScoringProfile.BaseMaxPoints.Sum(kv =>
            profile.GetState(kv.Key, kv.Value).EffectiveMaxPoints);
        if (maxTotal <= 0)
            return rawScore;

        return (int)Math.Round((decimal)rawScore / maxTotal * 100m);
    }

    private EntryPointEvaluation BuildEntry(
        Stock stock,
        SmartMoneyMarketContext context,
        FlatBoxProfile flatBox,
        IReadOnlyList<SignalType> detected,
        decimal volRatio,
        decimal rs5,
        int sectorRank,
        bool hasBreakoutEntry,
        bool hasShakeoutEntry,
        bool hasMaStack,
        bool meetsSessionBar)
    {
        var settings = context.Settings;
        var runup = context.RunupFilter;
        var history = stock.History;
        var latest = history.Count > 0 ? history[^1] : null;
        var currentPrice = latest?.Close ?? stock.LatestPrice;
        var checklist = new List<EntryPointCheck>();

        void AddCheck(string id, string label, bool passed, string detail) =>
            checklist.Add(new(id, label, passed, detail));

        if (history.Count < settings.MinHistoryDays)
        {
            AddCheck("history", "Đủ lịch sử", false, $"<{settings.MinHistoryDays} phiên");
            return EntryBuild(
                EntryPointStatus.Invalid, EntryPointType.None, ChecklistConfidence(checklist), 0,
                0, 0, 0, 0, 0, 0, 0, false,
                "Chưa đủ dữ liệu", "Cần thêm lịch sử giá.", checklist);
        }

        var avgVol = signals.GetAverageVolume(history);
        var hasLiquidity = avgVol >= settings.MinAvgDailyVolume;
        AddCheck("liquidity", "Thanh khoản TB", hasLiquidity,
            hasLiquidity ? $"TB {avgVol:N0}" : $"Thấp ({avgVol:N0})");

        var isDistribution = signals.IsDistribution(history);
        AddCheck("distribution", "Không phân phối", !isDistribution,
            isDistribution ? "Pha phân phối" : "OK");

        if (isDistribution)
            return EntryBuild(EntryPointStatus.Invalid, EntryPointType.None, ChecklistConfidence(checklist), 0,
                0, 0, 0, 0, 0, 0, 0, false, "Phân phối — không vào",
                "Chờ setup mới.", checklist);

        if (!flatBox.HasValidBox)
        {
            AddCheck("flatbox", BasePriceLabels.Base, false, "Không có nền giá");
            return EntryBuild(EntryPointStatus.Invalid, EntryPointType.None, ChecklistConfidence(checklist), 0,
                0, 0, 0, 0, 0, 0, 0, false, "Không có nền giá",
                "Chưa có vùng tích lũy ping-pong.", checklist);
        }

        AddCheck("flatbox", BasePriceLabels.Base, true,
            $"Vùng {flatBox.BoxLow:N1}–{flatBox.BoxHigh:N1} ({flatBox.SessionDays} phiên)");

        if (!flatBox.IsBreakoutConfirmed)
        {
            var preBreakoutLevels = EntryLevels(flatBox.BoxLow, flatBox.BoxHigh, currentPrice, EntryPointType.None, runup);
            return EntryBuild(
                EntryPointStatus.Watch,
                EntryPointType.None,
                ChecklistConfidence(checklist),
                preBreakoutLevels.Entry,
                preBreakoutLevels.Stop,
                preBreakoutLevels.Trigger,
                preBreakoutLevels.Target,
                flatBox.BoxLow,
                flatBox.BoxHigh,
                flatBox.GainFromBoxTopPercent,
                preBreakoutLevels.RiskReward,
                false,
                $"Chờ {BasePriceLabels.Breakout.ToLower()}",
                $"Chờ {BasePriceLabels.Breakout.ToLower()} có xác nhận dòng tiền.",
                checklist);
        }

        var baseEventLabel = BasePriceLabels.ResolveEventLabel(flatBox, currentPrice);

        var baseLow = flatBox.BoxLow;
        var baseHigh = flatBox.BoxHigh;
        var gainFromBase = flatBox.GainFromBoxTopPercent;
        var notFomo = gainFromBase <= runup.MaxGainFromBasePercent;
        AddCheck("fomo", $"Chưa FOMO (≤{runup.MaxGainFromBasePercent:0.#}%)", notFomo,
            $"+{gainFromBase:0.#}% so đỉnh nền");

        if (!notFomo)
        {
            var late = EntryLevels(baseLow, baseHigh, currentPrice, EntryPointType.None, runup);
            return EntryBuild(EntryPointStatus.Late, EntryPointType.None, ChecklistConfidence(checklist),
                late.Entry, late.Stop, late.Trigger, late.Target, baseLow, baseHigh, gainFromBase,
                late.RiskReward, false, $"Đã chạy xa nền (+{gainFromBase:0.#}%)",
                "Tránh FOMO.", checklist);
        }

        var sessionChange = signals.GetChangePercent(history, 1);
        var sessionVol = latest?.Volume ?? 0;
        AddCheck("session", $"Phiên >{settings.MinSessionChangePercent:0.#}% & KL ≥{settings.MinSessionVolume:N0}",
            meetsSessionBar, $"+{sessionChange:0.#}%, KL {sessionVol:N0}");

        var hasBreakoutSignal = detected.Contains(SignalType.Breakout)
            || detected.Contains(SignalType.DarvasBreakout);
        AddCheck("breakout", $"Breakout Vol×≥{settings.BreakoutMinVolumeRatio:0.#}",
            hasBreakoutEntry,
            hasBreakoutEntry
                ? detected.Contains(SignalType.DarvasBreakout)
                    ? baseEventLabel
                    : $"Vol×{volRatio:0.0}"
                : "Chưa đủ");

        AddCheck("shakeout", "Shakeout đáy nền + hồi", hasShakeoutEntry,
            hasShakeoutEntry ? "Hồi phục trên đáy" : "Chưa");

        var rsOk = rs5 >= 0 || hasBreakoutEntry;
        AddCheck("rs", "Khỏe hơn VNINDEX", rsOk, $"RS {rs5:+0.#;-0.#}%");
        AddCheck("ma", "MA stack", hasMaStack, hasMaStack ? "OK" : "Chưa");
        AddCheck("sector", $"Ngành top {settings.TopSectorCount}",
            sectorRank <= settings.TopSectorCount,
            $"#{sectorRank} {stock.Sector}");

        var entryType = hasBreakoutEntry ? EntryPointType.Breakout
            : hasShakeoutEntry ? EntryPointType.Shakeout
            : EntryPointType.None;

        var levels = EntryLevels(baseLow, baseHigh, currentPrice, entryType, runup);
        var checklistConfidence = ChecklistConfidence(checklist);

        if (entryType != EntryPointType.None && rsOk && hasLiquidity)
        {
            var headline = entryType == EntryPointType.Breakout
                ? $"{baseEventLabel} — phiên kích hoạt"
                : "Điểm vào SHAKEOUT — rũ đáy nền + hồi";
            var action = $"Vào {levels.Entry:N1}, cắt lỗ {levels.Stop:N1}, mục tiêu {levels.Target:N1}.";
            return EntryBuild(EntryPointStatus.Ready, entryType, checklistConfidence,
                levels.Entry, levels.Stop, levels.Trigger, levels.Target,
                baseLow, baseHigh, gainFromBase, levels.RiskReward, true,
                headline, action, checklist);
        }

        var watchLevels = EntryLevels(baseLow, baseHigh, currentPrice, EntryPointType.Breakout, runup);
        return EntryBuild(EntryPointStatus.Watch, EntryPointType.None, checklistConfidence,
            watchLevels.Entry, watchLevels.Stop, watchLevels.Trigger, watchLevels.Target,
            baseLow, baseHigh, gainFromBase, watchLevels.RiskReward, false,
            "Chờ kích hoạt — có nền giá, chưa đủ điều kiện phiên",
            $"Chờ trên {watchLevels.Trigger:N1} hoặc shakeout đáy {baseLow:N1}.", checklist);
    }

    private static EntryPointEvaluation AlignEntryWithTopGate(
        EntryPointEvaluation entry,
        string? gateFailure)
    {
        if (gateFailure is null || entry.Status != EntryPointStatus.Ready)
            return entry;

        return entry with
        {
            Status = EntryPointStatus.Watch,
            IsActionable = false,
            Headline = gateFailure,
            Action = "Chưa đạt đủ điều kiện Top cơ hội — theo dõi.",
        };
    }

    private static int ChecklistConfidence(IReadOnlyList<EntryPointCheck> checklist)
    {
        if (checklist.Count == 0)
            return 0;

        var passed = checklist.Count(c => c.Passed);
        return (int)Math.Round(100m * passed / checklist.Count);
    }

    private static EntryPointEvaluation RefineEntryConfidence(
        EntryPointEvaluation entry,
        decimal predictedHitPercent)
    {
        var checklistPct = ChecklistConfidence(entry.Checklist);
        var confidence = entry.Status switch
        {
            EntryPointStatus.Ready => (int)Math.Round((checklistPct + predictedHitPercent) / 2m),
            EntryPointStatus.Watch => (int)Math.Round(checklistPct * 0.55m + predictedHitPercent * 0.45m),
            _ => checklistPct
        };

        return entry with { Confidence = Math.Clamp(confidence, 0, 100) };
    }

    private string? ResolveTopGateFailure(
        SmartMoneySettings settings,
        BasePriceFilterSettings runup,
        IReadOnlyList<OhlcvBar> history,
        SmartMoneyMarketContext context,
        FlatBoxProfile flatBox,
        int sectorRank,
        decimal rs5,
        decimal rsPercentile,
        bool hasBreakoutEntry,
        bool hasShakeoutEntry,
        bool hasMaStack,
        bool hasFlatBoxSetup,
        int score)
    {
        if (history.Count < settings.MinHistoryDays)
            return $"Thiếu lịch sử (<{settings.MinHistoryDays} phiên)";

        if (history.Count > 0 && signals.GetAverageVolume(history) < settings.MinAvgDailyVolume)
            return "Thanh khoản thấp";

        if (history.Count > 0 && signals.IsDistribution(history))
            return "Pha phân phối — không mua";

        if (!flatBox.IsBreakoutConfirmed && !hasFlatBoxSetup)
            return $"Chưa {BasePriceLabels.Breakout.ToLower()} / chưa test cạnh hộp";

        if (flatBox.GainFromBoxTopPercent > runup.MaxGainFromBasePercent)
            return $"FOMO +{flatBox.GainFromBoxTopPercent:0.#}% so đỉnh nền";

        if (!hasMaStack)
            return "Chưa đạt MA stack / xu hướng dài hạn";

        if (context.MarketPhase == MarketWyckoffPhase.Unfavorable
            && (rsPercentile < settings.MinRsPercentileForUnfavorable || rs5 <= 0m))
            return "Thị trường khó — chỉ mua mã dẫn dắt (RS top + khỏe hơn VNINDEX)";

        if (sectorRank > settings.TopSectorCount && rs5 < 2m)
            return "Ngành yếu + RS không đủ";

        if (!hasBreakoutEntry && !hasShakeoutEntry)
        {
            var activated = (flatBox.IsBreakoutConfirmed
                    && flatBox.GainFromBoxTopPercent <= runup.MaxGainFromBasePercent)
                || hasFlatBoxSetup;
            if (!activated)
                return $"Chưa breakout / shakeout (>{settings.MinSessionChangePercent:0.#}%, KL ≥{settings.MinSessionVolume:N0})";
        }

        if (rs5 < 0 && !hasBreakoutEntry)
            return "Yếu hơn VNINDEX (RS âm)";

        if (score < settings.MinPassScore)
            return $"Buy Score {score} < {settings.MinPassScore}";

        return null;
    }

    internal static MaStackStrictness ResolveMaStackStrictness(
        MarketWyckoffPhase phase,
        SmartMoneySettings settings)
    {
        if (!settings.RequireMaStack)
            return MaStackStrictness.Off;

        var mode = phase switch
        {
            MarketWyckoffPhase.Favorable => settings.MaStackFavorableMode,
            MarketWyckoffPhase.Unfavorable => settings.MaStackUnfavorableMode,
            _ => settings.MaStackNeutralMode
        };

        return Enum.TryParse<MaStackStrictness>(mode, ignoreCase: true, out var parsed)
            ? parsed
            : phase switch
            {
                MarketWyckoffPhase.Favorable => MaStackStrictness.Full,
                MarketWyckoffPhase.Unfavorable => MaStackStrictness.Loose,
                _ => MaStackStrictness.Medium
            };
    }

    private static BuyRecommendation ResolveRecommendation(
        int score,
        EntryPointEvaluation entry,
        string? gateFailure)
    {
        if (entry.Status == EntryPointStatus.Late)
            return BuyRecommendation.Avoid;

        if (gateFailure is not null)
            return BuyRecommendation.Avoid;

        if (score >= StrongBuyMinScore && entry.Status == EntryPointStatus.Ready)
            return BuyRecommendation.StrongBuy;

        if (score >= WatchMinScore
            || score >= WatchMinScoreWithSetup
            || entry.Status == EntryPointStatus.Watch
            || entry.Status == EntryPointStatus.Ready)
            return BuyRecommendation.Watch;

        return BuyRecommendation.Watch;
    }

    private static WyckoffPhase ClassifyStock(
        Stock stock,
        IReadOnlyList<SignalType> detected,
        decimal volRatio,
        decimal breakoutMinVolumeRatio)
    {
        if (detected.Contains(SignalType.Distribution))
            return WyckoffPhase.Distribution;
        if (detected.Contains(SignalType.Shakeout))
            return WyckoffPhase.Accumulation;
        if ((detected.Contains(SignalType.Breakout) && volRatio >= breakoutMinVolumeRatio)
            || detected.Contains(SignalType.DarvasBreakout))
            return WyckoffPhase.Markup;

        var change20 = stock.History.Count >= 21
            ? (stock.History[^1].Close - stock.History[^21].Close) / stock.History[^21].Close * 100m
            : 0m;
        if (change20 < -10m)
            return WyckoffPhase.Markdown;
        return WyckoffPhase.Unknown;
    }

    private static (decimal Entry, decimal Stop, decimal Trigger, decimal Target, decimal RiskReward) EntryLevels(
        decimal baseLow, decimal baseHigh, decimal currentPrice,
        EntryPointType type, BasePriceFilterSettings runup)
    {
        var range = Math.Max(baseHigh - baseLow, baseLow * 0.02m);
        var stop = Math.Round(baseLow * 0.98m, 2);
        var trigger = Math.Round(baseHigh * 1.01m, 2);
        var fomoCap = Math.Round(baseHigh * (1m + runup.MaxGainFromBasePercent / 100m), 2);
        var target = Math.Min(fomoCap, Math.Round(currentPrice + range * 2m, 2));
        var entry = type switch
        {
            EntryPointType.Breakout or EntryPointType.Shakeout => Math.Round(currentPrice, 2),
            _ => Math.Round(Math.Min(currentPrice, trigger), 2)
        };
        if (entry <= stop)
            entry = Math.Round(stop * 1.01m, 2);
        var risk = entry - stop;
        var reward = target - entry;
        var rr = risk > 0 && reward > 0 ? Math.Round(reward / risk, 2) : 0m;
        return (entry, stop, trigger, target, rr);
    }

    private static EntryPointEvaluation EntryBuild(
        EntryPointStatus status, EntryPointType type, int confidence,
        decimal entry, decimal stop, decimal trigger, decimal target,
        decimal baseLow, decimal baseHigh, decimal gainFromBase,
        decimal riskReward, bool isActionable,
        string headline, string action,
        IReadOnlyList<EntryPointCheck> checklist) =>
        new(status, type, confidence, entry, stop, trigger, target,
            baseLow, baseHigh, gainFromBase, riskReward, isActionable,
            headline, action, checklist);
}

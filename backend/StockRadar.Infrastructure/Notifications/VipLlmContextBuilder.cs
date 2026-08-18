using System.Text.Json;
using System.Text.Json.Serialization;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services.OpportunityRanking;
using StockRadar.Infrastructure.MarketData;
using Microsoft.Extensions.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Ghép hồ sơ đầy đủ cổ phiếu + ngữ cảnh fire để LLM veto (mua/bán).</summary>
internal sealed class VipLlmContextBuilder(
    IStockService stocks,
    IOptions<VipLlmJudgeOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public async Task<string> BuildAsync(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        string signal,
        string? branch,
        decimal pacedVolumeRatio,
        decimal mlProb,
        bool mlActive,
        decimal resolvedMinMlProb,
        VipPullbackMaContext pullbackMa,
        SessionFlowSnapshot? flow,
        TradeEventDetector.DetectedTradeEvent? scan,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var detail = await stocks.GetDetailAsync(opp.Symbol, cancellationToken);
        var historyLimit = Math.Clamp(cfg.MaxHistoryBars, 20, 250);

        object? stockDossier = null;
        if (detail is not null)
        {
            var history = detail.History
                .TakeLast(historyLimit)
                .Select(b => new
                {
                    b.Date,
                    b.Open,
                    b.High,
                    b.Low,
                    b.Close,
                    b.Volume,
                })
                .ToList();

            stockDossier = new
            {
                detail.Symbol,
                detail.Name,
                detail.Sector,
                detail.Price,
                detail.ChangePercent,
                detail.Score,
                detail.SectorWave,
                detail.PassesSmartMoneyFilter,
                detail.ScoreReasons,
                detail.Summary,
                detail.ActiveSignals,
                levels = new
                {
                    detail.BuyZone,
                    detail.StopLoss,
                    detail.Resistance,
                    detail.Target,
                    detail.RelativeStrength,
                    detail.VolumeRatio,
                },
                flatBox = detail.FlatBox,
                entryPoint = detail.EntryPoint,
                buyDecision = detail.BuyDecision,
                buyScoreAsOf = detail.BuyScoreAsOf,
                buyScoreSource = detail.BuyScoreSource,
                patternCompositeScore = detail.PatternCompositeScore,
                bundleCompositeScore = detail.BundleCompositeScore,
                opportunityCompositeScore = detail.OpportunityCompositeScore,
                criterionScores = detail.PatternScores,
                historyBars = history,
                historyBarsCount = history.Count,
            };
        }

        var payload = new
        {
            meta = new
            {
                purpose = "vip_telegram_signal_veto",
                signal,
                branch,
                asOfUtc = DateTime.UtcNow,
                sessionDate = opp.ForTradingDate,
            },
            topOpportunity = new
            {
                opp.Symbol,
                opp.Name,
                opp.Sector,
                opp.Rank,
                opp.Score,
                opp.BuyScore,
                opp.PredictedHitPercent,
                opp.PredictedSampleCount,
                opp.SetupDna,
                opp.TradeState,
                opp.TradeStateReason,
                opp.Recommendation,
                opp.MarketPhase,
                opp.VolumeRatio,
                opp.AverageDailyVolume,
                opp.Price,
                opp.ChangePercent,
                opp.EntryPointJson,
                opp.ExplainJson,
                opp.GeneratedAt,
            },
            liveQuote = new
            {
                row.Open,
                row.High,
                row.Low,
                row.Close,
                row.SessionVolume,
                row.ChangePercent,
                gainFromOpenPercent = row.Open > 0
                    ? Math.Round((row.Close - row.Open) / row.Open * 100m, 2)
                    : 0m,
                pacedVolumeRatio,
            },
            localMlGate = new
            {
                mlProb,
                mlActive,
                resolvedMinMlProb,
                pullbackMa = pullbackMa.Available
                    ? new
                    {
                        pullbackMa.Ma10,
                        pullbackMa.Ma20,
                        pullbackMa.Ma50,
                        pullbackMa.UptrendLong,
                        pullbackMa.FeaturesComplete,
                        atrPercent = pullbackMa.LiveAtrPercent(row.Close),
                        distMa20Percent = pullbackMa.LiveDistMa20Percent(row.Close),
                        rs5dPercent = pullbackMa.LiveRs5dPercent(row.Close),
                    }
                    : null,
            },
            orderFlow = flow is null
                ? null
                : new
                {
                    flow.SessionForeignNet,
                    flow.SessionPropNet,
                    flow.LastBookImbalance,
                    flow.SessionPressure,
                    vsaLabel = scan?.Label,
                    scanImmediateBlock = scan?.IsImmediateBlock,
                },
            stockDossier,
            domainHints = new
            {
                buyPoint1OpenPercent = "3-6% từ Open phiên (breakout) hoặc pullback sát MA10/20 trong uptrend",
                buyPoint2OpenPercent = "≥6% từ Open + volume paced cao hơn",
                note = "Rule+ML nội bộ đã PASS. Bạn chỉ veto ALLOW/BLOCK.",
            },
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public async Task<string> BuildForPositionAsync(
        MasterAlertPositionRecord position,
        KbsPriceBoardClient.KbsBoardRow row,
        string signal,
        decimal anchor,
        decimal dropFromAnchor,
        decimal currentGain,
        decimal peakGain,
        string marketPhase,
        TradeEventDetector.DetectedTradeEvent? scan,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var detail = await stocks.GetDetailAsync(position.Symbol, cancellationToken);
        var historyLimit = Math.Clamp(cfg.MaxHistoryBars, 20, 250);

        object? stockDossier = null;
        if (detail is not null)
        {
            var history = detail.History
                .TakeLast(historyLimit)
                .Select(b => new
                {
                    b.Date,
                    b.Open,
                    b.High,
                    b.Low,
                    b.Close,
                    b.Volume,
                })
                .ToList();

            stockDossier = new
            {
                detail.Symbol,
                detail.Name,
                detail.Sector,
                detail.Price,
                detail.ChangePercent,
                detail.Score,
                detail.BuyDecision,
                entryPoint = detail.EntryPoint,
                flatBox = detail.FlatBox,
                historyBars = history,
                historyBarsCount = history.Count,
            };
        }

        var payload = new
        {
            meta = new
            {
                purpose = "vip_telegram_signal_veto",
                signal,
                branch = position.ExitRegime,
                asOfUtc = DateTime.UtcNow,
                sessionDate = position.EntryDate,
            },
            openPosition = new
            {
                position.Symbol,
                position.EntryDate,
                position.EntryPrice,
                position.PeakPriceSinceEntry,
                position.CurrentPositionSize,
                position.ExitRegime,
                position.OverheadBaseLow,
                position.OverheadBaseHigh,
                position.EntryBarLow,
                position.FiredAlertKinds,
                position.MarketPhaseAtEntry,
            },
            liveQuote = new
            {
                row.Open,
                row.High,
                row.Low,
                row.Close,
                row.SessionVolume,
                row.ChangePercent,
                gainFromOpenPercent = row.Open > 0
                    ? Math.Round((row.Close - row.Open) / row.Open * 100m, 2)
                    : 0m,
            },
            exitMetrics = new
            {
                marketPhase,
                anchor,
                dropFromAnchorPercent = dropFromAnchor,
                pnlVsEntryPercent = currentGain,
                peakGainVsEntryPercent = peakGain,
                vsaLabel = scan?.Label,
                scanImmediateBlock = scan?.IsImmediateBlock,
            },
            stockDossier,
            domainHints = new
            {
                note = "Rule bán/cảnh báo nội bộ đã PASS. Bạn chỉ veto ALLOW/BLOCK. " +
                       "dropFromAnchorPercent là mức rút từ đỉnh/mốc (dương = đã rút).",
            },
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}

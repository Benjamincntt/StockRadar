using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public interface IEngineTrustQueryService
{
    Task<EngineTrustDto> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class EngineTrustQueryService(
    ISetupTrackRepository tracks,
    IHitCalibrationRepository calibration,
    IShadowAnalysisRepository shadowRepo,
    IMemoryCache cache,
    IOptions<CacheOptions> cacheOptions,
    IOptions<ShadowAnalysisOptions> shadowOptions) : IEngineTrustQueryService
{
    private const string CacheKey = "engine:trust";

    public Task<EngineTrustDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var cfg = cacheOptions.Value;
        if (!cfg.Enabled)
            return BuildAsync(cancellationToken);

        return cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cfg.EngineTrustSeconds);
            return await BuildAsync(cancellationToken);
        })!;
    }

    private async Task<EngineTrustDto> BuildAsync(CancellationToken cancellationToken)
    {
        var today = TradingCalendar.TodayVietnam();
        var from7d = TradingSessionMath.SubtractTradingSessions(today, 7);
        var (measured7d, good7d) = await tracks.GetMeasuredOpportunityCountsSinceAsync(from7d, cancellationToken);
        decimal? winRate7d = measured7d > 0
            ? Math.Round(100m * good7d / measured7d, 1)
            : null;

        var calMeta = await calibration.GetMetaAsync(cancellationToken);
        var dataAsOf = TradingSessionMath.SubtractTradingSessions(
            TradingCalendar.GetActiveOpportunityDate(),
            1);

        var shadowCfg = shadowOptions.Value;
        IReadOnlyList<ShadowVariantStatusDto>? variants = null;
        int? leaderScore = null;
        string? shadowMessage = null;

        if (shadowCfg.Enabled)
        {
            var summaries = await shadowRepo.GetSummariesAsync(cancellationToken);
            variants = summaries
                .Select(s => new ShadowVariantStatusDto(
                    s.VariantMinPassScore,
                    s.MeasuredCount,
                    s.SuccessRatePercent,
                    s.IsProduction,
                    s.IsLeader))
                .ToList();

            var leader = summaries.FirstOrDefault(s => s.IsLeader);
            if (leader is not null)
            {
                leaderScore = leader.VariantMinPassScore;
                var production = summaries.FirstOrDefault(s => s.IsProduction);
                if (leader.MeasuredCount >= shadowCfg.PromoteAfterMeasuredCount
                    && production is not null
                    && leader.VariantMinPassScore != production.VariantMinPassScore
                    && leader.SuccessRatePercent > production.SuccessRatePercent)
                {
                    shadowMessage =
                        $"Shadow gợi ý MinPassScore {leader.VariantMinPassScore} "
                        + $"({leader.SuccessRatePercent:0.#}% vs prod {production.SuccessRatePercent:0.#}%, n={leader.MeasuredCount})";
                }
                else if (leader.MeasuredCount < shadowCfg.PromoteAfterMeasuredCount)
                {
                    shadowMessage =
                        $"Shadow đang học ({leader.MeasuredCount}/{shadowCfg.PromoteAfterMeasuredCount} setup đo)";
                }
            }
            else
            {
                shadowMessage = "Shadow mode bật — chờ dữ liệu T+2.5";
            }
        }

        return new EngineTrustDto(
            winRate7d,
            measured7d,
            good7d,
            calMeta.TotalSamples > 0 ? calMeta.GlobalFactor : 1m,
            calMeta.TotalSamples,
            dataAsOf,
            shadowCfg.Enabled,
            leaderScore,
            shadowMessage,
            variants);
    }
}

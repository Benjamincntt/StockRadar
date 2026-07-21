using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));
        services.Configure<MarketJobsOptions>(configuration.GetSection(MarketJobsOptions.SectionName));
        services.Configure<IntradayScannerOptions>(configuration.GetSection(IntradayScannerOptions.SectionName));
        services.Configure<OpportunityMonitorOptions>(configuration.GetSection(OpportunityMonitorOptions.SectionName));
        services.Configure<PriceRunupFilterOptions>(configuration.GetSection(PriceRunupFilterOptions.SectionName));
        services.Configure<SmartMoneyOptions>(configuration.GetSection(SmartMoneyOptions.SectionName));
        services.Configure<CriterionAccuracyOptions>(configuration.GetSection(CriterionAccuracyOptions.SectionName));
        services.Configure<MasterAlertOptions>(configuration.GetSection(MasterAlertOptions.SectionName));
        services.Configure<OpportunityPerformanceOptions>(configuration.GetSection(OpportunityPerformanceOptions.SectionName));
        services.Configure<ShadowAnalysisOptions>(configuration.GetSection(ShadowAnalysisOptions.SectionName));
        services.Configure<SwingTradingOptions>(configuration.GetSection(SwingTradingOptions.SectionName));
        services.Configure<OpportunityRankerOptions>(configuration.GetSection(OpportunityRankerOptions.SectionName));
        services.Configure<TuneEvaluateOptions>(configuration.GetSection(TuneEvaluateOptions.SectionName));
        services.Configure<HyperparameterTuningOptions>(configuration.GetSection(HyperparameterTuningOptions.SectionName));
        services.Configure<TelegramNotifyOptions>(configuration.GetSection(TelegramNotifyOptions.SectionName));
        services.Configure<ReversalBounceOptions>(configuration.GetSection(ReversalBounceOptions.SectionName));
        services.Configure<ReversalBounceBacktestOptions>(configuration.GetSection(ReversalBounceBacktestOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CriterionAccuracyOptions>>().Value;
            return o.ToSettings();
        });
        services.AddSingleton<ISignalAnalyzer, SignalAnalyzer>();
        services.AddSingleton<StockRadar.Domain.Services.ReversalBounce.IMarketBreadthAnalyzer, StockRadar.Domain.Services.ReversalBounce.MarketBreadthAnalyzer>();
        services.AddSingleton<StockRadar.Domain.Services.ReversalBounce.IMarketRegimeClassifier, StockRadar.Domain.Services.ReversalBounce.MarketRegimeClassifier>();
        services.AddSingleton<StockRadar.Domain.Services.ReversalBounce.IReversalBounceAnalyzer, StockRadar.Domain.Services.ReversalBounce.ReversalBounceAnalyzer>();
        services.AddSingleton<StockRadar.Domain.Services.ReversalBounce.ICounterTrendDecisionEngine, StockRadar.Domain.Services.ReversalBounce.CounterTrendDecisionEngine>();
        services.AddSingleton<ITrendSetupEvaluator, TrendSetupEvaluator>();
        services.AddSingleton<IIndicatorBundleScorer, IndicatorBundleScorer>();
        services.AddSingleton<ITechnicalIndicatorAnalyzer, TechnicalIndicatorAnalyzer>();
        services.AddSingleton<IBuyDecisionEngine, BuyDecisionEngine>();
        services.AddSingleton<ISmartMoneyCriterionScorer, SmartMoneyCriterionScorer>();
        services.AddSingleton<ICriterionAccuracyEvaluator, CriterionAccuracyEvaluator>();
        services.AddSingleton<ISmartMoneyOpportunitySelector, SmartMoneyOpportunitySelector>();
        services.AddScoped<AdaptiveScoringProfileFactory>();
        services.AddScoped<HitCalibrationProfileFactory>();
        services.AddScoped<HitCalibrationService>();
        services.AddScoped<FalsePositiveMiningService>();
        services.AddScoped<ShadowAnalysisService>();
        services.AddScoped<IEngineTrustQueryService, EngineTrustQueryService>();
        services.AddScoped<ISwingDecisionService, SwingDecisionService>();
        services.AddScoped<ITradeJournalService, TradeJournalService>();
        services.AddScoped<EntryTimingService>();
        services.AddSingleton<IEntryPointEvaluator, EntryPointEvaluator>();
        services.AddScoped<SmartMoneyEvaluationService>();
        services.AddSingleton<ISignalFormatter, SignalFormatter>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMarketService, MarketService>();
        services.AddScoped<IRadarService, RadarService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<ISectorCatalogService, SectorCatalogService>();
        services.AddScoped<ICriterionScoringService, CriterionScoringService>();
        services.AddScoped<IOpportunityPerformanceQueryService, OpportunityPerformanceQueryService>();
        services.AddScoped<IOpportunityNorthStarQueryService, OpportunityNorthStarQueryService>();
        services.AddSingleton<OpportunityRankerService>();
        services.AddSingleton<IOpportunityRanker>(sp => sp.GetRequiredService<OpportunityRankerService>());
        services.AddScoped<IOpportunityRankingDatasetService, OpportunityRankingDatasetService>();
        services.AddScoped<IOpportunityRankerTrainingService, OpportunityRankerTrainingService>();
        services.AddScoped<ISetupTrackBackfillService, SetupTrackBackfillService>();
        services.AddScoped<ITuneEvaluateService, TuneEvaluateService>();

        services.AddScoped<IMarketSyncService, MarketSyncService>();
        services.AddScoped<IReversalBounceQueryService, ReversalBounceQueryService>();
        services.AddScoped<IReversalBounceShadowReportService, ReversalBounceShadowReportService>();

        return services;
    }
}

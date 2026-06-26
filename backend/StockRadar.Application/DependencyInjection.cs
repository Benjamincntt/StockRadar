using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Services;

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
        services.Configure<ZaloNotifyOptions>(configuration.GetSection(ZaloNotifyOptions.SectionName));
        services.Configure<OpportunityMonitorOptions>(configuration.GetSection(OpportunityMonitorOptions.SectionName));
        services.Configure<PriceRunupFilterOptions>(configuration.GetSection(PriceRunupFilterOptions.SectionName));
        services.Configure<SmartMoneyOptions>(configuration.GetSection(SmartMoneyOptions.SectionName));

        services.AddSingleton<ISignalAnalyzer, SignalAnalyzer>();
        services.AddSingleton<IIndicatorBundleScorer, IndicatorBundleScorer>();
        services.AddSingleton<ITechnicalIndicatorAnalyzer, TechnicalIndicatorAnalyzer>();
        services.AddSingleton<ISmartMoneyCriterionScorer, SmartMoneyCriterionScorer>();
        services.AddSingleton<ICriterionAccuracyEvaluator, CriterionAccuracyEvaluator>();
        services.AddSingleton<ISmartMoneyOpportunitySelector, SmartMoneyOpportunitySelector>();
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

        services.AddScoped<IMarketSyncService, MarketSyncService>();

        return services;
    }
}

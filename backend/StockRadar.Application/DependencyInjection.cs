using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;

namespace StockRadar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));
        services.Configure<MarketJobsOptions>(configuration.GetSection(MarketJobsOptions.SectionName));
        services.Configure<RealizedPnlOptions>(configuration.GetSection(RealizedPnlOptions.SectionName));
        services.Configure<TelegramNotifyOptions>(configuration.GetSection(TelegramNotifyOptions.SectionName));
        services.Configure<PushNotifyOptions>(configuration.GetSection(PushNotifyOptions.SectionName));
        services.Configure<SuKienQuyenOptions>(configuration.GetSection(SuKienQuyenOptions.SectionName));
        services.Configure<FireAntOptions>(configuration.GetSection(FireAntOptions.SectionName));
        services.Configure<V3Options>(configuration.GetSection(V3Options.SectionName));

        services.AddSingleton<IDichVuSuKienQuyen, DichVuSuKienQuyen>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMarketService, MarketService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<ISectorCatalogService, SectorCatalogService>();
        services.AddScoped<NhatKyService>();
        services.AddScoped<ThietBiService>();

        services.AddScoped<IMarketSyncService, MarketSyncService>();

        return services;
    }
}

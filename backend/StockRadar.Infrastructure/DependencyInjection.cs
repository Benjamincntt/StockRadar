using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Identity;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Caching;
using StockRadar.Infrastructure.Persistence.Repositories;
using StockRadar.Infrastructure.Scheduling;

namespace StockRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString)
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddHttpClient<KbsPriceBoardClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        });
        services.AddHttpClient<KbsSectorLookupClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        });
        services.AddHttpClient<KbsHistoryClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        });
        services.AddHttpClient<KbsIndexClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        });
        services.AddHttpClient<KbsGroupListingClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        });
        services.AddHttpClient<KbsStockSearchClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        });
        services.AddHttpClient<KbsStockListingClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        });
        services.AddScoped<IChartBarProvider, KbsChartBarProvider>();
        services.AddScoped<IStockLookupService, StockLookupService>();

        services.AddSingleton<IQuoteTickCache, QuoteTickCache>();
        services.AddScoped<KbsMarketSyncRunner>();
        services.AddSingleton<HistoryBackfillState>();
        services.AddScoped<HistoryBackfillRunner>();
        services.AddScoped<IHistoryBackfillService>(sp => sp.GetRequiredService<HistoryBackfillRunner>());
        services.AddScoped<DailySessionSyncRunner>();
        services.AddScoped<IDailySessionSyncService>(sp => sp.GetRequiredService<DailySessionSyncRunner>());
        services.AddScoped<DailyAnalysisRunner>();
        services.AddScoped<IDailyAnalysisService>(sp => sp.GetRequiredService<DailyAnalysisRunner>());
        services.AddScoped<DailyCriterionScoringRunner>();
        services.AddScoped<IDailyCriterionScoringService>(sp => sp.GetRequiredService<DailyCriterionScoringRunner>());
        services.AddScoped<EfCriterionScoringRepository>();
        services.AddScoped<ICriterionScoringRepository>(sp => sp.GetRequiredService<EfCriterionScoringRepository>());
        services.AddScoped<EfDailyOpportunityRepository>();
        services.AddScoped<IDailyOpportunityRepository>(sp => sp.GetRequiredService<EfDailyOpportunityRepository>());
        services.AddScoped<EfDailyAnalysisRunRepository>();
        services.AddScoped<IDailyAnalysisRunRepository>(sp => sp.GetRequiredService<EfDailyAnalysisRunRepository>());
        services.AddScoped<IntradayScannerRunner>();
        services.AddScoped<IIntradayScannerService>(sp => sp.GetRequiredService<IntradayScannerRunner>());
        services.AddScoped<EfSessionRadarRepository>();
        services.AddScoped<ISessionRadarRepository>(sp => sp.GetRequiredService<EfSessionRadarRepository>());
        services.AddSingleton<OrderFlowSnapshotTracker>();
        services.AddSingleton<ITradePrintStore, TradePrintStore>();
        services.AddSingleton<TradePrintDetector>();
        services.AddSingleton<IntradayMonitorStatusTracker>();
        services.AddSingleton<IIntradayMonitorStatusQuery, IntradayMonitorStatusQueryService>();
        services.AddScoped<WatchlistPatternAlertPublisher>();
        services.AddHttpClient<IZaloNotifier, WebhookZaloNotifier>();
        services.AddScoped<OpportunityIntradayMonitorRunner>();
        services.AddScoped<IOpportunityIntradayMonitorService>(sp => sp.GetRequiredService<OpportunityIntradayMonitorRunner>());

        services.AddStockRadarQuartz(configuration);

        services.AddScoped<EfMarketDataWriter>();
        services.AddScoped<IMarketDataWriter>(sp => sp.GetRequiredService<EfMarketDataWriter>());

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<EfMarketIndexRepository>();

        services.AddScoped<EfStockRepository>();
        services.AddScoped<IJobStockRepository>(sp => new CachedStockRepository(
            sp.GetRequiredService<EfStockRepository>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheOptions>>()));
        services.AddScoped<IStockRepository>(sp =>
            (IStockRepository)sp.GetRequiredService<IJobStockRepository>());

        services.AddScoped<EfAlertRepository>();
        services.AddScoped<IAlertRepository>(sp => sp.GetRequiredService<EfAlertRepository>());

        services.AddScoped<EfSetupTrackRepository>();
        services.AddScoped<ISetupTrackRepository>(sp => sp.GetRequiredService<EfSetupTrackRepository>());
        services.AddScoped<EfHitCalibrationRepository>();
        services.AddScoped<IHitCalibrationRepository>(sp => sp.GetRequiredService<EfHitCalibrationRepository>());
        services.AddScoped<EfFalsePositiveMiningRepository>();
        services.AddScoped<IFalsePositiveMiningRepository>(sp => sp.GetRequiredService<EfFalsePositiveMiningRepository>());
        services.AddScoped<EfWeeklyOpportunityReviewRepository>();
        services.AddScoped<IWeeklyOpportunityReviewRepository>(sp =>
            sp.GetRequiredService<EfWeeklyOpportunityReviewRepository>());
        services.AddScoped<EfShadowAnalysisRepository>();
        services.AddScoped<IShadowAnalysisRepository>(sp => sp.GetRequiredService<EfShadowAnalysisRepository>());
        services.AddScoped<EfEntryTimingRepository>();
        services.AddScoped<IEntryTimingRepository>(sp => sp.GetRequiredService<EfEntryTimingRepository>());
        services.AddScoped<EfTradeJournalRepository>();
        services.AddScoped<ITradeJournalRepository>(sp => sp.GetRequiredService<EfTradeJournalRepository>());
        services.AddScoped<OpportunityPerformanceRunner>();
        services.AddScoped<IOpportunityPerformanceService>(sp =>
            sp.GetRequiredService<OpportunityPerformanceRunner>());

        services.AddScoped<EfWatchlistRepository>();
        services.AddScoped<IWatchlistRepository>(sp => sp.GetRequiredService<EfWatchlistRepository>());

        services.AddScoped<EfSectorCatalogRepository>();
        services.AddScoped<ISectorCatalogRepository>(sp => sp.GetRequiredService<EfSectorCatalogRepository>());
        services.AddScoped<EfStockSectorRepository>();
        services.AddScoped<IStockSectorRepository>(sp => sp.GetRequiredService<EfStockSectorRepository>());

        services.AddScoped<EfUserRepository>();
        services.AddScoped<IUserRepository>(sp => sp.GetRequiredService<EfUserRepository>());

        services.AddScoped<DatabaseMarketIndexProvider>();
        services.AddScoped<IJobMarketIndexProvider>(sp => sp.GetRequiredService<DatabaseMarketIndexProvider>());
        services.AddScoped<IMarketIndexProvider>(sp => new CachedMarketIndexProvider(
            sp.GetRequiredService<DatabaseMarketIndexProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheOptions>>()));

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}

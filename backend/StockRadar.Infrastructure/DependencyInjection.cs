using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Identity;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Caching;
using StockRadar.Infrastructure.Persistence.Repositories;
using StockRadar.Infrastructure.Scheduling;
using StockRadar.Domain.Services;

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
        services.AddSingleton<Application.Abstractions.IJobStatusService, Jobs.JobStatusService>();
        services.AddSingleton<Scheduling.JobStatusQuartzListener>();
        services.AddScoped<KbsMarketSyncRunner>();
        services.AddScoped<IKbsMarketSyncService>(sp => sp.GetRequiredService<KbsMarketSyncRunner>());
        services.AddSingleton<HistoryBackfillState>();
        services.AddScoped<HistoryBackfillRunner>();
        services.AddScoped<IHistoryBackfillService>(sp => sp.GetRequiredService<HistoryBackfillRunner>());
        services.AddScoped<DailySessionSyncRunner>();
        services.AddScoped<IDailySessionSyncService>(sp => sp.GetRequiredService<DailySessionSyncRunner>());
        services.AddScoped<UniverseRescreenRunner>();
        services.AddScoped<IUniverseRescreenService>(sp => sp.GetRequiredService<UniverseRescreenRunner>());
        services.AddTelegramNotifier();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<PhamViDbThongBao>();
        services.AddSingleton<INguiTinDay, KenhDayTinFcm>();
        services.AddScoped<IGuiThongBao, GuiThongBao>();
        services.AddScoped<IThietBiNhanThongBaoRepository, EfThietBiNhanThongBaoRepository>();

        // Bộ kịch bản V3 (spec 012) — máy chia sẻ + 4 nhịp + truy vấn hiệu quả.
        services.AddScoped<V3Engine>();
        services.AddScoped<IV3TruocPhienService, V3TruocPhienRunner>();
        services.AddScoped<IV3TrongPhienService, V3TrongPhienRunner>();
        services.AddScoped<IV3AtcService, V3AtcRunner>();
        services.AddScoped<IV3SauPhienService, V3SauPhienRunner>();
        services.AddScoped<IV3NapChiSoBackfillService, V3NapChiSoBackfillRunner>();
        services.AddScoped<IV3HieuQuaService, V3HieuQuaService>();

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

        services.AddScoped<EfWatchlistListRepository>();
        services.AddScoped<IWatchlistListRepository>(sp => sp.GetRequiredService<EfWatchlistListRepository>());

        services.AddScoped<INhatKyRepository, EfNhatKyRepository>();
        services.AddScoped<INhatKyTinHieuRepository, EfNhatKyTinHieuRepository>();

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

        services.AddSingleton<INguonSuKienQuyen, FileNguonSuKienQuyen>();
        services.AddSingleton<BoDieuChinhGiaTheoQuyen>();

        // Lịch chốt quyền toàn thị trường (FireAnt) — cổng lọc "loại mã sắp chia".
        services.AddHttpClient<FireAntLichChotQuyenClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(40);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        });
        services.AddScoped<INguonLichChotQuyen>(sp =>
            sp.GetRequiredService<FireAntLichChotQuyenClient>());

        return services;
    }

    public static IServiceCollection AddTelegramNotifier(this IServiceCollection services)
    {
        services.AddHttpClient<ITelegramNotifier, TelegramNotifier>().RemoveAllLoggers();
        return services;
    }
}

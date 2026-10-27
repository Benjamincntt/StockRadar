using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Scheduling.Jobs;

namespace StockRadar.Infrastructure.Scheduling;

internal static class QuartzSchedulingExtensions
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public static IServiceCollection AddStockRadarQuartz(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var marketJobs = configuration.GetSection(MarketJobsOptions.SectionName).Get<MarketJobsOptions>()
            ?? new MarketJobsOptions();
        var marketData = configuration.GetSection(MarketDataOptions.SectionName).Get<MarketDataOptions>()
            ?? new MarketDataOptions();
        var intraday = configuration.GetSection(IntradayScannerOptions.SectionName).Get<IntradayScannerOptions>()
            ?? new IntradayScannerOptions();
        var monitor = configuration.GetSection(OpportunityMonitorOptions.SectionName).Get<OpportunityMonitorOptions>()
            ?? new OpportunityMonitorOptions();

        services.AddQuartz(q =>
        {
            q.SchedulerId = "StockRadar";
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 6);

            ConfigureHistoryJob(q, marketJobs.History);
            ConfigureDailySessionJob(q, marketJobs.DailySession);
            ConfigureDailyAnalysisJob(q, marketJobs.DailySession, marketJobs.DailyAnalysis);
            ConfigureKbsSyncJob(q, marketData);
            ConfigureIntradayScannerJob(q, intraday);
            ConfigureOpportunityMonitorJob(q, monitor);

            var v3 = configuration.GetSection(V3Options.SectionName).Get<V3Options>() ?? new V3Options();
            ConfigureV3Jobs(q, v3);
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
            options.AwaitApplicationStarted = true;
        });

        services.AddHostedService<QuartzSchedulerStartupLogger>();

        return services;
    }

    private static void ConfigureHistoryJob(IServiceCollectionQuartzConfigurator q, HistoryJobOptions cfg)
    {
        if (!cfg.Enabled)
            return;

        var jobKey = new JobKey(QuartzJobIds.HistoryBackfill);
        q.AddJob<HistoryBackfillJob>(opts => opts.WithIdentity(jobKey));

        if (cfg.RunOnStartup)
        {
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{QuartzJobIds.HistoryBackfill}-startup")
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(8))
                .WithSimpleSchedule(x => x.WithRepeatCount(0)));
        }

        if (cfg.WeeklyRefreshEnabled)
        {
            var day = cfg.WeeklyRefreshDay switch
            {
                DayOfWeek.Sunday => "SUN",
                DayOfWeek.Monday => "MON",
                DayOfWeek.Tuesday => "TUE",
                DayOfWeek.Wednesday => "WED",
                DayOfWeek.Thursday => "THU",
                DayOfWeek.Friday => "FRI",
                _ => "SAT",
            };
            var cron = $"0 {cfg.WeeklyRefreshMinute} {cfg.WeeklyRefreshHour} ? * {day}";
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{QuartzJobIds.HistoryBackfill}-weekly-refresh")
                .UsingJobData("Mode", "night")
                .WithCronSchedule(cron, x => x.InTimeZone(VietnamTimeZone)));
        }
    }

    private static void ConfigureDailySessionJob(IServiceCollectionQuartzConfigurator q, DailySessionJobOptions cfg)
    {
        if (!cfg.Enabled)
            return;

        var jobKey = new JobKey(QuartzJobIds.DailySessionSync);

        q.AddJob<DailySessionSyncJob>(opts => opts.WithIdentity(jobKey));

        if (cfg.IntervalMinutes > 0)
        {
            var intervalSec = Math.Max(60, cfg.IntervalMinutes * 60);
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{QuartzJobIds.DailySessionSync}-interval")
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(10))
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(intervalSec)
                    .RepeatForever()));
        }
        else
        {
            var cron = BuildWeekdayCron(cfg.Hour, cfg.Minute);
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{QuartzJobIds.DailySessionSync}-trigger")
                .WithCronSchedule(cron, x => x.InTimeZone(VietnamTimeZone)));
        }
    }

    private static void ConfigureDailyAnalysisJob(
        IServiceCollectionQuartzConfigurator q,
        DailySessionJobOptions session,
        DailyAnalysisJobOptions analysis)
    {
        if (!analysis.Enabled)
            return;

        var (hour, minute) = AddMinutes(session.Hour, session.Minute, analysis.DelayAfterSessionMinutes);
        var jobKey = new JobKey(QuartzJobIds.DailyAnalysis);
        var cron = BuildWeekdayCron(hour, minute);

        q.AddJob<DailyAnalysisJob>(opts => opts.WithIdentity(jobKey));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{QuartzJobIds.DailyAnalysis}-close-trigger")
            .WithCronSchedule(cron, x => x.InTimeZone(VietnamTimeZone)));

        if (analysis.MorningRunEnabled)
        {
            var morningCron = BuildWeekdayCron(analysis.MorningRunHour, analysis.MorningRunMinute);
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{QuartzJobIds.DailyAnalysis}-morning-trigger")
                .WithCronSchedule(morningCron, x => x.InTimeZone(VietnamTimeZone)));
        }

        if (analysis.IntradayRefreshEnabled)
        {
            var interval = Math.Clamp(analysis.IntradayRefreshIntervalMinutes, 5, 60);
            // Cron phủ giờ có thể hợp lệ (9–11, 13–14); job tự lọc nghỉ trưa + ngoài 11:30 / sau 14:45.
            var intradayCron = $"0 0/{interval} 9-11,13-14 ? * MON-FRI";
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{QuartzJobIds.DailyAnalysis}-intraday-trigger")
                .WithCronSchedule(intradayCron, x => x.InTimeZone(VietnamTimeZone)));
        }
    }

    private static void ConfigureKbsSyncJob(IServiceCollectionQuartzConfigurator q, MarketDataOptions cfg)
    {
        if (!cfg.AutoSyncEnabled)
            return;

        var interval = Math.Max(30, cfg.SyncIntervalSeconds);
        var jobKey = new JobKey(QuartzJobIds.KbsMarketSync);

        q.AddJob<KbsMarketSyncJob>(opts => opts.WithIdentity(jobKey));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{QuartzJobIds.KbsMarketSync}-trigger")
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(3))
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(interval)
                .RepeatForever()));
    }

    private static void ConfigureIntradayScannerJob(IServiceCollectionQuartzConfigurator q, IntradayScannerOptions cfg)
    {
        if (!cfg.Enabled)
            return;

        var interval = Math.Max(30, cfg.IntervalSeconds);
        var jobKey = new JobKey(QuartzJobIds.IntradayScanner);

        q.AddJob<IntradayScannerJob>(opts => opts.WithIdentity(jobKey));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{QuartzJobIds.IntradayScanner}-trigger")
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(5))
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(interval)
                .RepeatForever()));
    }

    private static void ConfigureOpportunityMonitorJob(
        IServiceCollectionQuartzConfigurator q,
        OpportunityMonitorOptions cfg)
    {
        if (!cfg.Enabled)
            return;

        var interval = Math.Max(30, cfg.IntervalSeconds);
        var jobKey = new JobKey(QuartzJobIds.OpportunityMonitor);

        q.AddJob<OpportunityIntradayMonitorJob>(opts => opts.WithIdentity(jobKey));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{QuartzJobIds.OpportunityMonitor}-trigger")
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(8))
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(interval)
                .RepeatForever()));
    }

  /// <summary>Cron Quartz: giây phút giờ ? * MON-FRI</summary>
    private static string BuildWeekdayCron(int hour, int minute) =>
        $"0 {minute} {hour} ? * MON-FRI";

    /// <summary>
    /// Bốn nhịp V3 (Phụ lục D): trước phiên 08:30, trong phiên mỗi N phút 9–14,
    /// ATC 14:25, sau phiên 16:00 — tất cả giờ VN, T2-T6. Bỏ hết nếu V3.Enabled = false.
    /// </summary>
    private static void ConfigureV3Jobs(IServiceCollectionQuartzConfigurator q, V3Options cfg)
    {
        if (!cfg.Enabled)
            return;

        var truocKey = new JobKey(QuartzJobIds.V3TruocPhien);
        q.AddJob<V3TruocPhienJob>(opts => opts.WithIdentity(truocKey));
        q.AddTrigger(opts => opts
            .ForJob(truocKey)
            .WithIdentity($"{QuartzJobIds.V3TruocPhien}-trigger")
            .WithCronSchedule("0 30 8 ? * MON-FRI", x => x.InTimeZone(VietnamTimeZone)));

        var interval = Math.Max(1, cfg.IntervalPhut);
        var trongKey = new JobKey(QuartzJobIds.V3TrongPhien);
        q.AddJob<V3TrongPhienJob>(opts => opts.WithIdentity(trongKey));
        q.AddTrigger(opts => opts
            .ForJob(trongKey)
            .WithIdentity($"{QuartzJobIds.V3TrongPhien}-trigger")
            .WithCronSchedule($"0 0/{interval} 9-14 ? * MON-FRI", x => x.InTimeZone(VietnamTimeZone)));

        var atcKey = new JobKey(QuartzJobIds.V3Atc);
        q.AddJob<V3AtcJob>(opts => opts.WithIdentity(atcKey));
        q.AddTrigger(opts => opts
            .ForJob(atcKey)
            .WithIdentity($"{QuartzJobIds.V3Atc}-trigger")
            .WithCronSchedule("0 25 14 ? * MON-FRI", x => x.InTimeZone(VietnamTimeZone)));

        var sauKey = new JobKey(QuartzJobIds.V3SauPhien);
        q.AddJob<V3SauPhienJob>(opts => opts.WithIdentity(sauKey));
        q.AddTrigger(opts => opts
            .ForJob(sauKey)
            .WithIdentity($"{QuartzJobIds.V3SauPhien}-trigger")
            .WithCronSchedule("0 0 16 ? * MON-FRI", x => x.InTimeZone(VietnamTimeZone)));
    }

    private static (int Hour, int Minute) AddMinutes(int hour, int minute, int addMinutes)
    {
        var total = hour * 60 + minute + addMinutes;
        return (total / 60 % 24, total % 60);
    }
}

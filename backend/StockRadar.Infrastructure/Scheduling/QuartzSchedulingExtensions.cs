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
            ConfigureWeeklyOpportunityReviewJob(q, configuration);
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
    }

    private static void ConfigureDailySessionJob(IServiceCollectionQuartzConfigurator q, DailySessionJobOptions cfg)
    {
        if (!cfg.Enabled)
            return;

        var jobKey = new JobKey(QuartzJobIds.DailySessionSync);
        var cron = BuildWeekdayCron(cfg.Hour, cfg.Minute);

        q.AddJob<DailySessionSyncJob>(opts => opts.WithIdentity(jobKey));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{QuartzJobIds.DailySessionSync}-trigger")
            .WithCronSchedule(cron, x => x.InTimeZone(VietnamTimeZone)));
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
            .WithIdentity($"{QuartzJobIds.DailyAnalysis}-trigger")
            .WithCronSchedule(cron, x => x.InTimeZone(VietnamTimeZone)));
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

    private static void ConfigureWeeklyOpportunityReviewJob(
        IServiceCollectionQuartzConfigurator q,
        IConfiguration configuration)
    {
        var cfg = configuration.GetSection(OpportunityPerformanceOptions.SectionName)
            .Get<OpportunityPerformanceOptions>() ?? new OpportunityPerformanceOptions();
        if (!cfg.Enabled)
            return;

        var day = cfg.WeeklyReviewDay switch
        {
            DayOfWeek.Monday => "MON",
            DayOfWeek.Tuesday => "TUE",
            DayOfWeek.Wednesday => "WED",
            DayOfWeek.Thursday => "THU",
            DayOfWeek.Friday => "FRI",
            _ => "FRI",
        };
        var cron = $"0 {cfg.WeeklyReviewMinute} {cfg.WeeklyReviewHour} ? * {day}";
        var jobKey = new JobKey(QuartzJobIds.WeeklyOpportunityReview);

        q.AddJob<WeeklyOpportunityReviewJob>(opts => opts.WithIdentity(jobKey));
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{QuartzJobIds.WeeklyOpportunityReview}-trigger")
            .WithCronSchedule(cron, x => x.InTimeZone(VietnamTimeZone)));
    }

  /// <summary>Cron Quartz: giây phút giờ ? * MON-FRI</summary>
    private static string BuildWeekdayCron(int hour, int minute) =>
        $"0 {minute} {hour} ? * MON-FRI";

    private static (int Hour, int Minute) AddMinutes(int hour, int minute, int addMinutes)
    {
        var total = hour * 60 + minute + addMinutes;
        return (total / 60 % 24, total % 60);
    }
}

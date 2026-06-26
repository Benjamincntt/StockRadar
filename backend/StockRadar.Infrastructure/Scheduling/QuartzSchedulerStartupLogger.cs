using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace StockRadar.Infrastructure.Scheduling;

internal sealed class QuartzSchedulerStartupLogger(
    ISchedulerFactory schedulerFactory,
    ILogger<QuartzSchedulerStartupLogger> logger) : Microsoft.Extensions.Hosting.IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var groups = await scheduler.GetJobGroupNames(cancellationToken);

        foreach (var group in groups)
        {
            var keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group), cancellationToken);
            foreach (var key in keys)
            {
                var triggers = await scheduler.GetTriggersOfJob(key, cancellationToken);
                foreach (var trigger in triggers)
                {
                    var next = trigger.GetNextFireTimeUtc();
                    logger.LogInformation(
                        "Quartz job {Job} — trigger {Trigger}, next: {Next}",
                        key.Name,
                        trigger.Key.Name,
                        next?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") ?? "—");
                }
            }
        }

        if (groups.Count == 0)
            logger.LogInformation("Quartz — không có job nào được lên lịch (kiểm tra appsettings).");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

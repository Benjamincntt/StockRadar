using Microsoft.Extensions.Options;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

[DisallowConcurrentExecution]
internal sealed class OpportunityIntradayMonitorJob(
    IOpportunityIntradayMonitorService monitor,
    IntradayMonitorStatusTracker monitorStatus,
    IOptions<OpportunityMonitorOptions> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsMarketOpen() && !options.Value.ForceRunOutsideHours)
        {
            monitorStatus.RecordSkipped(DateTime.UtcNow, "outside_hours");
            return;
        }

        await monitor.RunAsync(context.CancellationToken);
    }
}

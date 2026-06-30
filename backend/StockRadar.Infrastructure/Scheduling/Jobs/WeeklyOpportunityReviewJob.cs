using Quartz;
using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Review hiệu quả Top cơ hội + Master alerts — chạy cuối tuần.</summary>
public sealed class WeeklyOpportunityReviewJob(IOpportunityPerformanceService performance) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await performance.RunWeeklyReviewAsync(cancellationToken: context.CancellationToken);
    }
}

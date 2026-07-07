using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Review hiệu quả Top cơ hội + Master alerts — chạy cuối tuần.</summary>
public sealed class WeeklyOpportunityReviewJob(
    IOpportunityPerformanceService performance,
    IOpportunityRankerTrainingService rankerTraining,
    ILogger<WeeklyOpportunityReviewJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await performance.RunWeeklyReviewAsync(cancellationToken: CancellationToken.None);

        var trainResult = await rankerTraining.TryAutoRetrainAsync(CancellationToken.None);
        if (trainResult.Success)
        {
            logger.LogInformation("OpportunityRanker auto-retrain: {Message}", trainResult.Message);
        }
        else if (!trainResult.Message.Contains("AutoRetrain tắt", StringComparison.Ordinal))
        {
            logger.LogInformation("OpportunityRanker auto-retrain bỏ qua: {Message}", trainResult.Message);
        }
    }
}

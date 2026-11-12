using Quartz;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Đồng bộ giá KBS realtime trong khung giờ bảng giá (spec 018 Q1), bỏ qua ngoài khung.</summary>
[DisallowConcurrentExecution]
internal sealed class KbsMarketSyncJob(KbsMarketSyncRunner runner) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsInQuoteSyncWindow())
            return;

        await runner.RunAsync(context.CancellationToken);
    }
}

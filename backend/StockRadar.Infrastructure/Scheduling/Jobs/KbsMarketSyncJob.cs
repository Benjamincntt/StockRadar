using Microsoft.Extensions.Options;
using Quartz;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Đồng bộ giá KBS realtime trong phiên giao dịch.</summary>
[DisallowConcurrentExecution]
internal sealed class KbsMarketSyncJob(
    KbsMarketSyncRunner runner,
    IOptions<MarketDataOptions> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsMarketOpen() && !options.Value.ForceSyncOutsideHours)
            return;

        await runner.RunAsync(context.CancellationToken);
    }
}

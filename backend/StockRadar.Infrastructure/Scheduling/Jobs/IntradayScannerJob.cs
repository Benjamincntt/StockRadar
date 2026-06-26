using Microsoft.Extensions.Options;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

[DisallowConcurrentExecution]
internal sealed class IntradayScannerJob(
    IIntradayScannerService scanner,
    IOptions<IntradayScannerOptions> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsMarketOpen() && !options.Value.ForceScanOutsideHours)
            return;

        await scanner.ScanAsync(context.CancellationToken);
    }
}

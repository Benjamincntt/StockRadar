using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>
/// Job Quartz chạy Pha 2 mỗi 1 phút trong phiên (09:00-14:45 T2-T6).
/// Kiểm tra cò kích hoạt cho các mã FORMING + kiểm tra sell signals.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class Pha2TrongPhienJob(
    IPha2TrongPhienService runner,
    ILogger<Pha2TrongPhienJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua Pha 2 — không phải ngày giao dịch.");
            return;
        }

        if (!VietnamMarketCalendar.IsMarketOpen())
        {
            logger.LogDebug("Bỏ qua Pha 2 — ngoài giờ giao dịch.");
            return;
        }

        try
        {
            var ketQua = await runner.ChayAsync(context.CancellationToken);
            if (ketQua.SoTrigger > 0)
            {
                logger.LogInformation(
                    "Quartz Pha 2: {Trigger} trigger, {Alert} alert.",
                    ketQua.SoTrigger, ketQua.SoAlert);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Pha 2 trong phiên gặp lỗi.");
        }
    }
}

using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>
/// Job Quartz chạy Pha 3 lúc 16:00 các ngày T2-T6 (sau giờ đóng cửa phiên).
/// Đo lường outcome các kịch bản đã kích hoạt sau T+3 phiên: Thắng/Thua/Ngang + % lợi nhuận + R:R thực tế.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class Pha3DoLuongJob(
    IPha3DoLuongService runner,
    ILogger<Pha3DoLuongJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua Pha 3 — không phải ngày giao dịch.");
            return;
        }

        logger.LogInformation("Quartz — Pha 3 đo lường outcome các kịch bản đã kích hoạt.");
        await runner.RunAsync(context.CancellationToken);
    }
}

using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>
/// Job Quartz chạy Pha 1 lúc 08:30 các ngày T2-T6 (trước phiên).
/// Sơ tuyển + đánh giá Bối cảnh/Hình thái, lưu trạng thái WATCHING/FORMING.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class Pha1TruocPhienJob(
    IPha1TruocPhienService runner,
    ILogger<Pha1TruocPhienJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua Pha 1 — không phải ngày giao dịch.");
            return;
        }

        logger.LogInformation("Quartz — Pha 1 trước phiên: sơ tuyển + đánh giá bối cảnh/hình thái.");
        await runner.ChayAsync(context.CancellationToken);
    }
}

using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>
/// Mùa Tết — theo dõi giỏ đã mua, hai mốc 14:20 (kịp đặt ATC) và 15:40 (sau khi Job 2 chốt giá đóng cửa).
/// Job chỉ gác lịch + kiểm tra ngày giao dịch; toàn bộ nghiệp vụ và chặn gửi lặp ở runner.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class MuaTetTheoDoiJob(
    IMuaTetTheoDoiService runner,
    ILogger<MuaTetTheoDoiJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua Mùa Tết theo dõi — không phải ngày giao dịch.");
            return;
        }

        try
        {
            await runner.KiemVaBaoAsync(boQuaChanLap: false, context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Mùa Tết theo dõi gặp lỗi.");
        }
    }
}

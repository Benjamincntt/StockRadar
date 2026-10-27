using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

// Bốn nhịp V3 (Phụ lục D). Job chỉ gác lịch + kiểm tra ngày giao dịch; toàn bộ nghiệp vụ ở runner.

/// <summary>V3 trước phiên — 08:30 T2-T6 (giờ VN): KB1/KB2 + nhắc ATO N3.</summary>
[DisallowConcurrentExecution]
internal sealed class V3TruocPhienJob(
    IV3TruocPhienService runner,
    ILogger<V3TruocPhienJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua V3 trước phiên — không phải ngày giao dịch.");
            return;
        }
        var kq = await runner.ChayAsync(context.CancellationToken);
        logger.LogInformation("V3 trước phiên: {Tin} tín hiệu, {Mo} lệnh, {Thoat} thoát.",
            kq.SoTinHieu, kq.SoLenhMo, kq.SoLenhThoat);
    }
}

/// <summary>V3 trong phiên — mỗi 1 phút 09:00–14:45 T2-T6: khớp ATO + xét bán realtime.</summary>
[DisallowConcurrentExecution]
internal sealed class V3TrongPhienJob(
    IV3TrongPhienService runner,
    ILogger<V3TrongPhienJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam())
            || !VietnamMarketCalendar.IsMarketOpen())
            return;
        try
        {
            var kq = await runner.ChayAsync(context.CancellationToken);
            if (kq.SoLenhThoat > 0)
                logger.LogInformation("V3 trong phiên: {Thoat} lệnh thoát.", kq.SoLenhThoat);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "V3 trong phiên gặp lỗi.");
        }
    }
}

/// <summary>V3 ATC — 14:25 T2-T6: KB4/N1/N3 mua ATC + bán hết hạn.</summary>
[DisallowConcurrentExecution]
internal sealed class V3AtcJob(
    IV3AtcService runner,
    ILogger<V3AtcJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua V3 ATC — không phải ngày giao dịch.");
            return;
        }
        var kq = await runner.ChayAsync(context.CancellationToken);
        logger.LogInformation("V3 ATC: {Tin} tín hiệu, {Mo} lệnh, {Thoat} thoát.",
            kq.SoTinHieu, kq.SoLenhMo, kq.SoLenhThoat);
    }
}

/// <summary>V3 sau phiên — 16:00 T2-T6: đóng ngày, cập nhật phiên giữ + ngưỡng MA.</summary>
[DisallowConcurrentExecution]
internal sealed class V3SauPhienJob(
    IV3SauPhienService runner,
    ILogger<V3SauPhienJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua V3 sau phiên — không phải ngày giao dịch.");
            return;
        }
        await runner.ChayAsync(context.CancellationToken);
    }
}

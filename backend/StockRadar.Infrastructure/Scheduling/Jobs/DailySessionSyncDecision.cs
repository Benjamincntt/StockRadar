namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Quyết định lần chạy Job 2 có đồng bộ hay bỏ qua. Hàm thuần, kiểm thử không cần Quartz.</summary>
internal static class DailySessionSyncDecision
{
    /// <summary>Khoá trong JobDataMap của trigger chạy một lần sau đóng cửa.</summary>
    public const string AfterCloseKey = "sauDongCua";

    /// <summary>Kết quả quyết định.</summary>
    internal enum Result
    {
        /// <summary>Đồng bộ phiên hôm nay.</summary>
        Run,
        /// <summary>Bỏ qua: không phải ngày giao dịch.</summary>
        SkipNotTradingDay,
        /// <summary>Bỏ qua: ngoài giờ khớp lệnh.</summary>
        SkipOutsideHours,
    }

    /// <summary>
    /// Quyết định chạy hay bỏ qua một lần kích hoạt của Job 2.
    /// </summary>
    /// <param name="isTradingDay">Hôm nay có phải ngày giao dịch.</param>
    /// <param name="isMarketOpen">Đang trong giờ khớp lệnh.</param>
    /// <param name="intervalMinutes">Khoảng lặp trong phiên của Job 2; 0 nghĩa là chỉ chạy theo giờ cố định.</param>
    /// <param name="forceRunOutsideHours">Cấu hình cho phép chạy cả ngoài giờ.</param>
    /// <param name="isAfterCloseTrigger">Lần kích hoạt đến từ trigger sau đóng cửa.</param>
    /// <returns>Chạy hay lý do bỏ qua.</returns>
    public static Result Decide(
        bool isTradingDay,
        bool isMarketOpen,
        int intervalMinutes,
        bool forceRunOutsideHours,
        bool isAfterCloseTrigger)
    {
        if (!isTradingDay)
            return Result.SkipNotTradingDay;

        // DO-NOT-CHANGE: trigger sau đóng cửa phải chạy ngoài giờ; bỏ qua thì HistoryJson thiếu nến ATC và nhịp V3 16:00 tính trên giá 14:45
        if (isAfterCloseTrigger)
            return Result.Run;

        if (intervalMinutes > 0 && !forceRunOutsideHours && !isMarketOpen)
            return Result.SkipOutsideHours;

        return Result.Run;
    }
}

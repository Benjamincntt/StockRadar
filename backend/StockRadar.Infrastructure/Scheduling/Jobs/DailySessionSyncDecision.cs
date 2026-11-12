namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Quyết định lần chạy Job 2 có đồng bộ hay bỏ qua. Hàm thuần, kiểm thử không cần Quartz.</summary>
internal static class DailySessionSyncDecision
{
    /// <summary>Khoá trong JobDataMap của trigger thử lại (15:15, 15:30) — chỉ lấy mã còn thiếu nến.</summary>
    public const string RetryKey = "thuLai";

    /// <summary>Kết quả quyết định.</summary>
    internal enum Result
    {
        /// <summary>Chạy đầy đủ như lần chính, kèm lọc lại danh sách mã.</summary>
        RunLanChinh,
        /// <summary>Chạy thử lại: chỉ lấy các mã còn thiếu nến hôm nay.</summary>
        RunThuLai,
        /// <summary>Bỏ qua: không phải ngày giao dịch.</summary>
        SkipNotTradingDay,
    }

    /// <summary>
    /// Quyết định chế độ chạy cho một lần kích hoạt của Job 2 (spec 018 Q2/Q4).
    /// </summary>
    /// <param name="isTradingDay">Hôm nay có phải ngày giao dịch.</param>
    /// <param name="isRetryTrigger">Lần kích hoạt đến từ trigger thử lại 15:15/15:30.</param>
    /// <returns>Chế độ chạy hoặc lý do bỏ qua.</returns>
    public static Result Decide(bool isTradingDay, bool isRetryTrigger)
    {
        if (!isTradingDay)
            return Result.SkipNotTradingDay;

        // DO-NOT-CHANGE: mọi trigger Job 2 đều nằm ngoài giờ khớp lệnh; chặn theo giờ thì HistoryJson thiếu nến ATC và nhịp V3 16:00 tính trên giá 14:45
        return isRetryTrigger ? Result.RunThuLai : Result.RunLanChinh;
    }
}

namespace StockRadar.Domain.Enums;

/// <summary>Chế độ chạy Job 2 (spec 018 Q2/Q4): lần chính sau đóng cửa hoặc lần thử lại chỉ lấy mã thiếu.</summary>
public enum CheDoJob2
{
    /// <summary>Lần chính 15:05: lấy toàn universe, cuối lần lọc lại danh sách mã.</summary>
    LanChinh,
    /// <summary>Thử lại 15:15/15:30: chỉ lấy các mã còn thiếu nến hôm nay, không lọc lại danh sách mã.</summary>
    ThuLai,
    /// <summary>Thử lại khi chưa có trạng thái hôm nay (server vừa khởi động lại): chạy đầy đủ nhưng không lọc lại danh sách mã.</summary>
    ThuLaiDayDu,
}

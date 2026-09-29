namespace StockRadar.Application.Options;

/// <summary>Cấu hình Pha 2 — kiểm tra trigger trong phiên</summary>
public class Pha2Options
{
    public const string SectionName = "Pha2";

    /// <summary>Khoảng cách giữa 2 lần kiểm tra (phút). Mặc định: 1</summary>
    public int IntervalPhut { get; set; } = 1;

    /// <summary>Giờ bắt đầu phiên. Mặc định: 09:00</summary>
    public string GioBatDau { get; set; } = "09:00";

    /// <summary>Giờ kết thúc phiên. Mặc định: 14:45</summary>
    public string GioKetThuc { get; set; } = "14:45";
}

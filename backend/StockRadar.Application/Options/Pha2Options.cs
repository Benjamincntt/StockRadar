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

    // BUSINESS-RULE: chưa đủ số phiên này kể từ ngày kích hoạt thì chưa bán được (T+2.5).
    public int MinTradingSessionsToSell { get; set; } = 3;

    /// <summary>Số phiên tối đa Pha 2 theo dõi một vị thế trước khi buộc thoát vì hết hạn.</summary>
    public int SoPhienTheoDoiToiDa { get; set; } = 20;

    /// <summary>Hệ số k nhân ATR để tính mức dừng lỗ đuổi theo.</summary>
    public decimal HeSoAtrDungLoDuoi { get; set; } = 2.5m;

    /// <summary>Chu kỳ ATR (số phiên ngày) tính một lần cho mỗi mã mỗi phiên.</summary>
    public int ChuKyAtr { get; set; } = 14;

    /// <summary>Số lượt quét liên tiếp giá phải nằm dưới dừng lỗ trước khi báo (ngoài ATC).</summary>
    public int SoLuotXacNhanDungLo { get; set; } = 2;

    /// <summary>Số lượt quét liên tiếp cần thiết từ giờ ATC trở đi (thanh khoản biến động mạnh).</summary>
    public int SoLuotXacNhanDungLoAtc { get; set; } = 3;

    /// <summary>Giờ bắt đầu ATC (HH:mm, giờ VN). Từ giờ này trở đi dùng `SoLuotXacNhanDungLoAtc`.</summary>
    public string GioBatDauAtc { get; set; } = "14:30";

    /// <summary>Danh sách loại kịch bản mua được phép dùng dừng lỗ đuổi theo.</summary>
    public string[] KichBanDungLoDuoi { get; set; } = ["NoHuongLen", "QuetThanhKhoan"];

    /// <summary>Số phiên sau kích hoạt mà giá chưa chạy đủ ngưỡng MFE thì thoát vì hết thời gian.</summary>
    public int SoPhienDungTheoThoiGian { get; set; } = 5;

    /// <summary>Ngưỡng MFE (%), đơn vị phần trăm. MFE dưới ngưỡng này -> bán hết.</summary>
    public decimal NguongMfeToiThieuPhanTram { get; set; } = 3m;
}

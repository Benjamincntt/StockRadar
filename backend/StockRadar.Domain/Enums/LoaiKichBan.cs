namespace StockRadar.Domain.Enums;

/// <summary>
/// Loại kịch bản giao dịch được nhận diện bởi Máy nhận kịch bản.
/// </summary>
public enum LoaiKichBan
{
    /// <summary>Nổ hướng lên — giá phá vỡ đỉnh hộp/nền với volume mạnh</summary>
    NoHuongLen = 1,

    /// <summary>Hồi về hỗ trợ — giá trong uptrend lùi về EMA20/VWAP rồi bật lại</summary>
    HoiHoTro = 2,

    /// <summary>Quét thanh khoản — giá quét đáy nền rồi giành lại (Smart Money gom hàng)</summary>
    QuetThanhKhoan = 3,

    /// <summary>Kiệt sức — tăng quá nóng, hết đà (SELL signal)</summary>
    KietSuc = 4,

    /// <summary>Gãy nền — phá vỡ hỗ trợ, mất cấu trúc (EXIT signal)</summary>
    GayNen = 5
}

namespace StockRadar.Domain.Enums;

/// <summary>
/// Loại kịch bản V3 — bộ P1++ (bán realtime). Năm kịch bản bắt đáy / mùa vụ,
/// thay toàn bộ bộ kịch bản V2 (đã gỡ theo spec 012).
/// </summary>
public enum LoaiKichBanV3
{
    /// <summary>KB1 Hoảng loạn — thị trường sụp diện rộng, mua mở cửa phiên sau.</summary>
    KB1 = 1,

    /// <summary>KB2 Chỉ số thủng MA50 — chỉ số bình quân đều dưới MA50 sâu, mua mở cửa phiên sau.</summary>
    KB2 = 2,

    /// <summary>KB4 Ngành bán quá mức — ngành giảm sâu, mã còn giảm hơn ngành, mua ATC phiên tín hiệu.</summary>
    KB4 = 3,

    /// <summary>N1 Giải chấp cạn kiệt — sàn trong 20 phiên + ngành kiệt + gần cuối tháng, mua ATC.</summary>
    N1 = 4,

    /// <summary>N3 Đón dòng tiền đầu tháng — 5 phiên tới cuối tháng, mua ATC, bán ATO đầu tháng sau.</summary>
    N3 = 5
}

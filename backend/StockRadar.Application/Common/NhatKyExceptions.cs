namespace StockRadar.Application.Common;

/// <summary>Nghiệp vụ nhật ký chặn cứng (Q7 — bán quá vị thế / tồn kho âm). Controller trả 422 kèm khoLuongHienCo.</summary>
public sealed class NhatKyVuotBanException(string symbol, decimal khoiLuongHienCo, decimal khoiLuongXinBan)
    : Exception($"Đang giữ {khoiLuongHienCo:0.####} {symbol}, không thể bán {khoiLuongXinBan:0.####}.")
{
    public string Symbol { get; } = symbol;
    public decimal KhoiLuongHienCo { get; } = khoiLuongHienCo;
    public decimal KhoiLuongXinBan { get; } = khoiLuongXinBan;
}

/// <summary>Validation cơ bản fail — controller trả 400 {"detail": ...}.</summary>
public sealed class NhatKyValidationException(string detail) : Exception(detail);

/// <summary>Tín hiệu nguồn (V2) không tồn tại khi gắn/bo-qua — controller trả 404.</summary>
public sealed class NhatKyTinHieuNotFoundException(string nguon, string maTinHieu)
    : Exception($"Không tìm thấy tín hiệu {nguon} {maTinHieu}.");

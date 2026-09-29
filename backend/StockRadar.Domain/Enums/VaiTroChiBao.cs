namespace StockRadar.Domain.Enums;

/// <summary>
/// Vai trò của chỉ báo trong một kịch bản giao dịch.
/// </summary>
public enum VaiTroChiBao
{
    /// <summary>Bối cảnh — "Mã này có đáng để ý không?"</summary>
    BoiCanh = 1,

    /// <summary>Hình thái — "Đang có setup đẹp không?"</summary>
    HinhThai = 2,

    /// <summary>Cò kích hoạt — "BÂY GIỜ có phải lúc hành động?"</summary>
    CoKichHoat = 3,

    /// <summary>Rủi ro/Thoát — "SL/TP ở đâu, khi nào chạy?"</summary>
    RuiRo = 4
}

using StockRadar.Domain.Enums;

namespace StockRadar.Domain.ValueObjects;

/// <summary>
/// Một bằng chứng cụ thể xác nhận điều kiện kịch bản.
/// Ví dụ: "Volume 2.1× TB20 (ngưỡng: 1.5×)"
/// </summary>
public record BangChung
{
    /// <summary>Vai trò chỉ báo mà bằng chứng này thuộc về</summary>
    public VaiTroChiBao VaiTro { get; init; }

    /// <summary>Mô tả điều kiện (tiếng Việt)</summary>
    public string MoTa { get; init; } = string.Empty;

    /// <summary>Giá trị thực tế đo được</summary>
    public string GiaTriThucTe { get; init; } = string.Empty;

    /// <summary>Ngưỡng yêu cầu</summary>
    public string Nguong { get; init; } = string.Empty;

    /// <summary>Điều kiện có đạt không</summary>
    public bool Dat { get; init; }
}

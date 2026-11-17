namespace StockRadar.Application.Options;

/// <summary>
/// Cấu hình kịch bản "Mùa Tết — mã khỏe" (P4-19, sổ V4). Chỉ ảnh hưởng tin nhắn/quy mô gợi ý,
/// KHÔNG đổi luật V3 nào. Đọc từ <c>V3:MuaTet</c> để nằm cùng khối V3 trong appsettings.
/// </summary>
public class MuaTetOptions
{
    public const string SectionName = "V3:MuaTet";

    /// <summary>
    /// Vốn cả giỏ, đơn vị đồng. Giả định của AI chờ owner chốt: giá trị một chỗ = vốn ÷ 10.
    /// </summary>
    public decimal VonBanDau { get; set; } = 40_000_000m;
}

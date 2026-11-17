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

    /// <summary>
    /// Ngày mua thực tế — owner điền sau khi khớp lệnh. Null = chưa mua, đợt theo dõi không làm gì.
    /// </summary>
    public DateOnly? NgayMua { get; set; }

    /// <summary>
    /// Giỏ ĐÃ MUA do owner ghi tay (đợt 2a chưa có bảng): mỗi dòng một mã, giá vào nghìn đồng, số cổ.
    /// Giỏ rỗng = chưa mua, job theo dõi bỏ qua.
    /// </summary>
    public List<MuaTetGioDaMuaOptions> GioDaMua { get; set; } = [];

    /// <summary>Ngưỡng cảnh báo sớm: giá trị giỏ ≤ 95% vốn đã mua.</summary>
    public decimal NguongCanhBao { get; set; } = 0.95m;

    /// <summary>Ngưỡng bán cả giỏ (P4-19): giá trị giỏ ≤ 90% vốn đã mua.</summary>
    public decimal NguongBan { get; set; } = 0.90m;

    /// <summary>Bật/tắt job theo dõi giỏ. Tắt thì job chỉ ghi log, không gửi tin.</summary>
    public bool TheoDoiBat { get; set; } = true;
}

/// <summary>Một mã trong giỏ Mùa Tết đã mua — owner ghi vào cấu hình sau khi khớp lệnh.</summary>
public class MuaTetGioDaMuaOptions
{
    /// <summary>Mã cổ phiếu.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Giá vào thực tế, nghìn đồng (cùng đơn vị giá trong DB).</summary>
    public decimal GiaVao { get; set; }

    /// <summary>Số cổ phiếu đã mua.</summary>
    public int SoCo { get; set; }
}

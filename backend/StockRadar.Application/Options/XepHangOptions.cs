namespace StockRadar.Application.Options;

/// <summary>Trọng số xếp hạng cơ hội V2 — 6 tiêu chí, KHÔNG có quyền veto</summary>
public class XepHangOptions
{
    public const string SectionName = "XepHang";

    /// <summary>Trọng số RS (sức mạnh tương đối). Mặc định: 30%</summary>
    public decimal TrongSoRs { get; set; } = 0.30m;

    /// <summary>Trọng số Sector (sóng ngành). Mặc định: 20%</summary>
    public decimal TrongSoSector { get; set; } = 0.20m;

    /// <summary>Trọng số chất lượng trigger. Mặc định: 20%</summary>
    public decimal TrongSoChatLuongTrigger { get; set; } = 0.20m;

    /// <summary>Trọng số pha thị trường (regime). Mặc định: 10%</summary>
    public decimal TrongSoRegime { get; set; } = 0.10m;

    /// <summary>Trọng số tỷ lệ lãi/lỗ (R:R). Mặc định: 10%</summary>
    public decimal TrongSoTyLeLaiLo { get; set; } = 0.10m;

    /// <summary>Trọng số nhiều kịch bản đồng thời (confluence). Mặc định: 10%</summary>
    public decimal TrongSoConfluence { get; set; } = 0.10m;

    /// <summary>Số lượng top kết quả trả về. Mặc định: 5</summary>
    public int SoLuongTop { get; set; } = 5;
}

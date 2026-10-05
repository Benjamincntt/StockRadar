namespace StockRadar.Application.Options;

/// <summary>Cấu hình nguồn lịch chốt quyền FireAnt (feed sự kiện quyền toàn thị trường).</summary>
public sealed class FireAntOptions
{
    public const string SectionName = "FireAnt";

    /// <summary>Tắt = cổng chia chác mở hoàn toàn (không gọi FireAnt).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Trang chủ để scrape token khách (guest) từ bundle Next.js.</summary>
    public string SiteUrl { get; set; } = "https://fireant.vn";

    /// <summary>API REST FireAnt (events/search ...).</summary>
    public string ApiBaseUrl { get; set; } = "https://restv2.fireant.vn";

    /// <summary>Số ngày lịch về phía trước coi là "sắp chia" (≈ số phiên × 7/5). Mặc định 7.</summary>
    public int LookaheadDays { get; set; } = 7;

    /// <summary>
    /// Token Bearer cứng nếu muốn bỏ qua việc scrape bundle (lưu ý: đây là secret —
    /// nên đặt ở appsettings.Production.json, KHÔNG commit). Để trống = tự scrape.
    /// </summary>
    public string? AccessToken { get; set; }
}

namespace StockRadar.Application.Options;

public sealed class ZaloNotifyOptions
{
    public const string SectionName = "ZaloNotify";

    public bool Enabled { get; set; }

    /// <summary>Số Zalo (E.164 hoặc 0xxx) — gửi trong payload webhook.</summary>
    public string PhoneNumber { get; set; } = "";

    /// <summary>POST JSON { phone, message, symbol, ... } — nối automation/Zalo OA của bạn.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Không gửi lại cùng mã trong N phút.</summary>
    public int CooldownMinutes { get; set; } = 30;
}

public sealed class OpportunityMonitorOptions
{
    public const string SectionName = "OpportunityMonitor";

    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 60;

    /// <summary>Khối lượng khớp tối thiểu mỗi lần phát hiện (CP) — chỉ lệnh block lớn.</summary>
    public long MinTradeVolume { get; set; } = 25_000;

    /// <summary>Giá trị khớp tối thiểu (VND) = volume × giá. Lọc dòng tiền thật sự mạnh.</summary>
    public decimal MinTradeValueVnd { get; set; } = 500_000_000m;

    /// <summary>Ngưỡng thấp để gom lô ẩn (buffer trước khi cộng dồn).</summary>
    public long MinMicroVolume { get; set; } = 8_000;

    public decimal MinMicroValueVnd { get; set; } = 150_000_000m;

    /// <summary>Cửa sổ gom lô ẩn (giây).</summary>
    public int AggregateWindowSeconds { get; set; } = 180;

    /// <summary>Biên độ giá % trong ~60s — Gom im.</summary>
    public decimal VsaSpreadTightPercent { get; set; } = 0.25m;

    /// <summary>Biên độ giá % — Đẩy giá / Xả hàng.</summary>
    public decimal VsaSpreadWidePercent { get; set; } = 0.4m;

    /// <summary>Khối ngoại ròng phiên ≥ ngưỡng → filter ForeignStrong.</summary>
    public long ForeignStrongSessionNet { get; set; } = 500_000;

    public int BatchSize { get; set; } = 40;

    public bool ForceRunOutsideHours { get; set; }
}

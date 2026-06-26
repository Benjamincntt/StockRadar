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

    public long MinForeignFlowDelta { get; set; } = 50_000;

    public long MinProprietaryDelta { get; set; } = 30_000;

    public long MinHangVolume { get; set; } = 150_000;

    public long MinHangVolumeDelta { get; set; } = 50_000;

    public long MinPutThroughDelta { get; set; } = 10_000;

    public int BatchSize { get; set; } = 40;

    public bool ForceRunOutsideHours { get; set; }
}

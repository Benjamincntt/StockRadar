namespace StockRadar.Application.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public bool Enabled { get; set; } = true;
    public int StockListSeconds { get; set; } = 60;
    public int MarketIndexSeconds { get; set; } = 30;
    public int SmartMoneyContextSeconds { get; set; } = 60;
    public int EngineTrustSeconds { get; set; } = 30;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "StockRadar";
    public string Audience { get; set; } = "StockRadar.Client";
    public string Secret { get; set; } = "dev-secret-change-in-production-min-32-chars!!";
    public int ExpiryMinutes { get; set; } = 1440;
}

public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    /// <summary>API key cho job thủ công (header X-Sync-Key)</summary>
    public string SyncApiKey { get; set; } = "dev-sync-key-change-me";

    /// <summary>Tự đồng bộ KBS trong API (Quartz KbsMarketSyncJob)</summary>
    public bool AutoSyncEnabled { get; set; } = true;

    /// <summary>Chu kỳ auto-sync (giây)</summary>
    public int SyncIntervalSeconds { get; set; } = 120;

    /// <summary>Sync cả ngoài giờ giao dịch (dev/test)</summary>
    public bool ForceSyncOutsideHours { get; set; } = true;
}

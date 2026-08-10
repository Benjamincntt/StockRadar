namespace StockRadar.Application.Options;

public sealed class VipLlmJudgeOptions
{
    public const string SectionName = "VipLlmJudge";

    /// <summary>Bật DeepSeek veto trước khi bắn Telegram BuyPoint.</summary>
    public bool Enabled { get; set; }

    /// <summary>deepseek | (reserved)</summary>
    public string Provider { get; set; } = "deepseek";

    public string ApiBaseUrl { get; set; } = "https://api.deepseek.com";

    public string ApiKey { get; set; } = "";

    /// <summary>Model nhanh/rẻ cho veto realtime. Override: deepseek-v4-pro nếu cần.</summary>
    public string Model { get; set; } = "deepseek-v4-flash";

    public int TimeoutMs { get; set; } = 3000;

    public int MaxHistoryBars { get; set; } = 120;

    /// <summary>Lỗi/timeout → vẫn bắn (true) hoặc chặn (false).</summary>
    public bool FailOpen { get; set; } = true;

    /// <summary>Gọi AI và log quyết định nhưng vẫn bắn Telegram (đo trước khi veto thật).</summary>
    public bool ShadowMode { get; set; } = true;

    public decimal Temperature { get; set; } = 0.1m;

    public int MaxTokens { get; set; } = 400;
}

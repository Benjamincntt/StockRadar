namespace StockRadar.Application.Options;

public sealed class VipLlmJudgeOptions
{
    public const string SectionName = "VipLlmJudge";

    /// <summary>Bật LLM veto trước khi bắn Telegram BuyPoint.</summary>
    public bool Enabled { get; set; }

    /// <summary>ShopAIKey / Anthropic base (không cần /v1). VD: https://api.shopaikey.com</summary>
    public string ApiBaseUrl { get; set; } = "https://api.shopaikey.com";

    public string ApiKey { get; set; } = "";

    /// <summary>Model Claude trên ShopAIKey. Mặc định Haiku (rẻ).</summary>
    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    public int TimeoutMs { get; set; } = 8000;

    public int MaxHistoryBars { get; set; } = 120;

    /// <summary>Lỗi/timeout → vẫn bắn (true) hoặc chặn (false).</summary>
    public bool FailOpen { get; set; } = true;

    /// <summary>Gọi AI và log nhưng vẫn bắn Telegram (đo trước khi veto thật).</summary>
    public bool ShadowMode { get; set; } = true;

    public decimal Temperature { get; set; } = 0.1m;

    public int MaxTokens { get; set; } = 200;
}

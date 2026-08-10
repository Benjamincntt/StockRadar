namespace StockRadar.Application.Options;

public sealed class VipLlmJudgeOptions
{
    public const string SectionName = "VipLlmJudge";

    /// <summary>Bật LLM veto trước khi bắn Telegram BuyPoint.</summary>
    public bool Enabled { get; set; }

    /// <summary>Provider ưu tiên: deepseek | gemini.</summary>
    public string Provider { get; set; } = "deepseek";

    /// <summary>Khi primary lỗi/quota → thử provider còn lại (nếu có key).</summary>
    public bool AutoFallback { get; set; } = true;

    public string ApiBaseUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>DeepSeek API key (sk-…).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Model DeepSeek. Override: deepseek-v4-pro nếu cần.</summary>
    public string Model { get; set; } = "deepseek-v4-flash";

    public string GeminiApiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public string GeminiApiKey { get; set; } = "";

    public string GeminiModel { get; set; } = "gemini-2.0-flash";

    public int TimeoutMs { get; set; } = 3000;

    public int MaxHistoryBars { get; set; } = 120;

    /// <summary>Lỗi/timeout (sau khi hết fallback) → vẫn bắn (true) hoặc chặn (false).</summary>
    public bool FailOpen { get; set; } = true;

    /// <summary>Gọi AI và log quyết định nhưng vẫn bắn Telegram (đo trước khi veto thật).</summary>
    public bool ShadowMode { get; set; } = true;

    public decimal Temperature { get; set; } = 0.1m;

    public int MaxTokens { get; set; } = 400;

    public string ResolveDeepSeekKey() =>
        string.IsNullOrWhiteSpace(ApiKey) ? "" : ApiKey.Trim();

    public string ResolveGeminiKey() =>
        string.IsNullOrWhiteSpace(GeminiApiKey) ? "" : GeminiApiKey.Trim();
}

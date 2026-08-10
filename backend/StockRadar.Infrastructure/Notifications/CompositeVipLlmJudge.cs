using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>
/// Router: gọi provider ưu tiên; hết quota/auth → Gemini/DeepSeek còn lại nếu <see cref="VipLlmJudgeOptions.AutoFallback"/>.
/// </summary>
internal sealed class CompositeVipLlmJudge(
    DeepSeekVipLlmJudge deepSeek,
    GeminiVipLlmJudge gemini,
    IOptions<VipLlmJudgeOptions> options,
    ILogger<CompositeVipLlmJudge> logger) : IVipLlmJudge
{
    public bool IsEnabled
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled && (deepSeek.HasKey || gemini.HasKey);
        }
    }

    public async Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!IsEnabled)
            return VipLlmJudgeResult.FallbackAllow("VipLlmJudge tắt hoặc thiếu mọi ApiKey.", cfg.Model, 0);

        var primary = ResolvePrimary(cfg);
        var secondary = primary == "gemini" ? "deepseek" : "gemini";

        var first = await CallAsync(primary, request, cancellationToken);
        if (!first.FromFallback)
            return first;

        if (!cfg.AutoFallback || !DeepSeekVipLlmJudge.IsRetryableFallback(first))
            return first;

        if (!HasProvider(secondary))
            return first;

        logger.LogWarning(
            "VIP LLM primary {Primary} fail-open/retryable ({Reason}) → fallback {Secondary}",
            primary,
            first.Reason,
            secondary);

        var second = await CallAsync(secondary, request, cancellationToken);
        if (!second.FromFallback)
            return second;

        // Giữ kết quả secondary (đã FailOpen/FailClosed theo config).
        return second with { Reason = $"{second.Reason} (sau {primary}: {first.Reason})" };
    }

    private bool HasProvider(string name) =>
        name.Equals("gemini", StringComparison.OrdinalIgnoreCase) ? gemini.HasKey : deepSeek.HasKey;

    private Task<VipLlmJudgeResult> CallAsync(
        string name,
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken) =>
        name.Equals("gemini", StringComparison.OrdinalIgnoreCase)
            ? gemini.DecideAsync(request, cancellationToken)
            : deepSeek.DecideAsync(request, cancellationToken);

    private static string ResolvePrimary(VipLlmJudgeOptions cfg)
    {
        var p = (cfg.Provider ?? "deepseek").Trim().ToLowerInvariant();
        if (p == "gemini")
            return string.IsNullOrWhiteSpace(cfg.ResolveGeminiKey()) && !string.IsNullOrWhiteSpace(cfg.ResolveDeepSeekKey())
                ? "deepseek"
                : "gemini";

        // deepseek (default): nếu thiếu DS key nhưng có Gemini → dùng Gemini
        if (string.IsNullOrWhiteSpace(cfg.ResolveDeepSeekKey()) && !string.IsNullOrWhiteSpace(cfg.ResolveGeminiKey()))
            return "gemini";

        return "deepseek";
    }
}

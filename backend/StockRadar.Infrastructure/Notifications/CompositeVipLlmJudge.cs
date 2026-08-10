using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>
/// Router: gọi provider ưu tiên; hết quota/auth → provider còn lại nếu <see cref="VipLlmJudgeOptions.AutoFallback"/>.
/// </summary>
internal sealed class CompositeVipLlmJudge(
    DeepSeekVipLlmJudge deepSeek,
    GeminiVipLlmJudge gemini,
    AnthropicVipLlmJudge anthropic,
    IOptions<VipLlmJudgeOptions> options,
    ILogger<CompositeVipLlmJudge> logger) : IVipLlmJudge
{
    public bool IsEnabled
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled && (deepSeek.HasKey || gemini.HasKey || anthropic.HasKey);
        }
    }

    public async Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!IsEnabled)
            return VipLlmJudgeResult.FallbackAllow("VipLlmJudge tắt hoặc thiếu mọi ApiKey.", cfg.Model, 0);

        var order = BuildOrder(cfg);
        VipLlmJudgeResult? last = null;

        for (var i = 0; i < order.Count; i++)
        {
            var name = order[i];
            if (!HasProvider(name))
                continue;

            var result = await CallAsync(name, request, cancellationToken);
            if (!result.FromFallback)
                return result;

            last = result;
            var hasNext = order.Skip(i + 1).Any(HasProvider);
            if (!cfg.AutoFallback || !IsRetryable(result) || !hasNext)
                return result;

            var next = order.Skip(i + 1).First(HasProvider);
            logger.LogWarning(
                "VIP LLM primary {Primary} fail-open/retryable ({Reason}) → fallback {Secondary}",
                name,
                result.Reason,
                next);
        }

        return last ?? VipLlmJudgeResult.FallbackAllow("Không có provider khả dụng.", cfg.Model, 0);
    }

    private bool HasProvider(string name) => name switch
    {
        "gemini" => gemini.HasKey,
        "anthropic" => anthropic.HasKey,
        _ => deepSeek.HasKey,
    };

    private Task<VipLlmJudgeResult> CallAsync(
        string name,
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken) => name switch
    {
        "gemini" => gemini.DecideAsync(request, cancellationToken),
        "anthropic" => anthropic.DecideAsync(request, cancellationToken),
        _ => deepSeek.DecideAsync(request, cancellationToken),
    };

    private static bool IsRetryable(VipLlmJudgeResult result) =>
        DeepSeekVipLlmJudge.IsRetryableFallback(result)
        || AnthropicVipLlmJudge.IsRetryableFallback(result);

    private static List<string> BuildOrder(VipLlmJudgeOptions cfg)
    {
        var primary = ResolvePrimary(cfg);
        var rest = new[] { "anthropic", "deepseek", "gemini" }
            .Where(x => !string.Equals(x, primary, StringComparison.OrdinalIgnoreCase));
        return new List<string> { primary }.Concat(rest).ToList();
    }

    private static string ResolvePrimary(VipLlmJudgeOptions cfg)
    {
        if (AnthropicVipLlmJudge.IsAnthropicProvider(cfg.Provider))
            return "anthropic";

        var p = (cfg.Provider ?? "deepseek").Trim().ToLowerInvariant();
        if (p == "gemini")
        {
            if (string.IsNullOrWhiteSpace(cfg.ResolveGeminiKey())
                && !string.IsNullOrWhiteSpace(cfg.ResolveDeepSeekKey()))
                return AnthropicVipLlmJudge.IsAnthropicProvider(cfg.Provider) ? "anthropic" : "deepseek";
            return "gemini";
        }

        if (string.IsNullOrWhiteSpace(cfg.ResolveDeepSeekKey())
            && !string.IsNullOrWhiteSpace(cfg.ResolveGeminiKey()))
            return "gemini";

        return "deepseek";
    }
}

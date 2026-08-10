using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Anthropic Messages API (ShopAIKey / Claude) — veto ALLOW/BLOCK.</summary>
internal sealed class AnthropicVipLlmJudge(
    IHttpClientFactory httpClientFactory,
    IOptions<VipLlmJudgeOptions> options,
    ILogger<AnthropicVipLlmJudge> logger)
{
    public bool HasKey
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled
                && IsAnthropicProvider(cfg.Provider)
                && !string.IsNullOrWhiteSpace(cfg.ResolveDeepSeekKey());
        }
    }

    public static bool IsAnthropicProvider(string? provider) =>
        string.Equals(provider, "anthropic", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "shopaikey", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "claude", StringComparison.OrdinalIgnoreCase);

    public async Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var sw = Stopwatch.StartNew();
        var model = string.IsNullOrWhiteSpace(cfg.Model) ? "claude-haiku-4-5-20251001" : cfg.Model.Trim();
        var apiKey = cfg.ResolveDeepSeekKey();
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(apiKey))
            return VipLlmJudgeResult.FallbackAllow("Anthropic VIP judge tắt hoặc thiếu ApiKey.", model, 0);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Math.Clamp(cfg.TimeoutMs, 500, 15_000));

            var payload = new
            {
                model,
                max_tokens = Math.Clamp(cfg.MaxTokens, 32, 1024),
                temperature = cfg.Temperature,
                system = VipLlmJudgeParsing.SystemPrompt,
                messages = new[]
                {
                    new { role = "user", content = VipLlmJudgeParsing.BuildUserPrompt(request) },
                },
            };

            var url = CombineMessagesUrl(cfg.ApiBaseUrl);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            httpRequest.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var http = httpClientFactory.CreateClient("VipLlmJudge");
            using var response = await http.SendAsync(httpRequest, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Anthropic VIP judge HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    VipLlmJudgeParsing.Truncate(body, 400));
                return Fallback(
                    model,
                    sw.ElapsedMilliseconds,
                    $"HTTP {(int)response.StatusCode}",
                    IsQuotaLike(response.StatusCode, body));
            }

            var content = ExtractText(body);
            var parsed = VipLlmJudgeParsing.ParseDecision(content);
            if (parsed is null)
            {
                logger.LogWarning(
                    "Anthropic VIP judge parse fail: {Content}",
                    VipLlmJudgeParsing.Truncate(content, 400));
                return Fallback(model, sw.ElapsedMilliseconds, "Parse JSON thất bại", quotaLike: false);
            }

            var (decision, reason) = parsed.Value;
            logger.LogInformation(
                "Anthropic VIP judge {Symbol} {Signal} → {Decision} ({Ms}ms): {Reason}",
                request.Symbol,
                request.Signal,
                decision,
                sw.ElapsedMilliseconds,
                reason);

            return new VipLlmJudgeResult(
                decision,
                reason,
                (int)sw.ElapsedMilliseconds,
                model,
                FromFallback: false,
                RawResponse: VipLlmJudgeParsing.Truncate(content, 800));
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            logger.LogWarning(
                "Anthropic VIP judge timeout {Ms}ms cho {Symbol}.",
                sw.ElapsedMilliseconds,
                request.Symbol);
            return Fallback(model, sw.ElapsedMilliseconds, "Timeout", quotaLike: false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "Anthropic VIP judge lỗi {Symbol}.", request.Symbol);
            return Fallback(model, sw.ElapsedMilliseconds, "Exception: " + ex.Message, quotaLike: false);
        }
    }

    public static bool IsRetryableFallback(VipLlmJudgeResult result) =>
        GeminiVipLlmJudge.IsRetryableFallback(result);

    private VipLlmJudgeResult Fallback(string model, long latencyMs, string reason, bool quotaLike)
    {
        var cfg = options.Value;
        var tag = quotaLike ? $"quota/auth: {reason}" : reason;
        if (cfg.FailOpen)
            return VipLlmJudgeResult.FallbackAllow($"Fail-open: {tag}", model, (int)latencyMs);

        return new VipLlmJudgeResult(
            VipLlmJudgeResult.Block,
            $"Fail-closed: {tag}",
            (int)latencyMs,
            model,
            FromFallback: true);
    }

    private static bool IsQuotaLike(HttpStatusCode status, string body) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.PaymentRequired
        || body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
        || body.Contains("quota", StringComparison.OrdinalIgnoreCase)
        || body.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
        || body.Contains("balance", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array
            || content.GetArrayLength() == 0)
            return null;

        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("type", out var type)
                && type.GetString() == "text"
                && part.TryGetProperty("text", out var text))
                return text.GetString();
        }

        return null;
    }

    private static string CombineMessagesUrl(string? baseUrl)
    {
        var b = (baseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(b))
            b = "https://api.shopaikey.com";
        if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return b + "/messages";
        return b + "/v1/messages";
    }
}

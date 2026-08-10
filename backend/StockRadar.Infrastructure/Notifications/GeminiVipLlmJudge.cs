using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Gemini generateContent — veto ALLOW/BLOCK (fallback / primary).</summary>
internal sealed class GeminiVipLlmJudge(
    IHttpClientFactory httpClientFactory,
    IOptions<VipLlmJudgeOptions> options,
    ILogger<GeminiVipLlmJudge> logger)
{
    public bool HasKey
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled && !string.IsNullOrWhiteSpace(cfg.ResolveGeminiKey());
        }
    }

    public async Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var sw = Stopwatch.StartNew();
        var model = string.IsNullOrWhiteSpace(cfg.GeminiModel) ? "gemini-2.0-flash" : cfg.GeminiModel.Trim();
        var apiKey = cfg.ResolveGeminiKey();
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(apiKey))
            return VipLlmJudgeResult.FallbackAllow("Gemini VIP judge tắt hoặc thiếu ApiKey.", model, 0);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Math.Clamp(cfg.TimeoutMs, 500, 15_000));

            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = VipLlmJudgeParsing.SystemPrompt } },
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = VipLlmJudgeParsing.BuildUserPrompt(request) } },
                    },
                },
                generationConfig = new
                {
                    temperature = cfg.Temperature,
                    maxOutputTokens = cfg.MaxTokens,
                    responseMimeType = "application/json",
                },
            };

            var baseUrl = (cfg.GeminiApiBaseUrl ?? "").TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = "https://generativelanguage.googleapis.com/v1beta";

            var url =
                $"{baseUrl}/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
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
                    "Gemini VIP judge HTTP {Status}: {Body}",
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
                logger.LogWarning("Gemini VIP judge parse fail: {Content}", VipLlmJudgeParsing.Truncate(content, 400));
                return Fallback(model, sw.ElapsedMilliseconds, "Parse JSON thất bại", quotaLike: false);
            }

            var (decision, reason) = parsed.Value;
            logger.LogInformation(
                "Gemini VIP judge {Symbol} {Signal} → {Decision} ({Ms}ms): {Reason}",
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
                "Gemini VIP judge timeout {Ms}ms cho {Symbol}.",
                sw.ElapsedMilliseconds,
                request.Symbol);
            return Fallback(model, sw.ElapsedMilliseconds, "Timeout", quotaLike: false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "Gemini VIP judge lỗi {Symbol}.", request.Symbol);
            return Fallback(model, sw.ElapsedMilliseconds, "Exception: " + ex.Message, quotaLike: false);
        }
    }

    /// <summary>True khi nên thử provider khác (quota / auth / rate limit).</summary>
    public static bool IsRetryableFallback(VipLlmJudgeResult result) =>
        result.FromFallback
        && (result.Reason.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase)
            || result.Reason.Contains("HTTP 402", StringComparison.OrdinalIgnoreCase)
            || result.Reason.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase)
            || result.Reason.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase)
            || result.Reason.Contains("quota", StringComparison.OrdinalIgnoreCase));

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

    private static bool IsQuotaLike(HttpStatusCode status, string body)
    {
        if (status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or (HttpStatusCode)402)
            return true;

        return body.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
            || body.Contains("quota", StringComparison.OrdinalIgnoreCase)
            || body.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
            return null;

        var content = candidates[0].GetProperty("content");
        if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            return null;

        var part = parts[0];
        return part.TryGetProperty("text", out var text) ? text.GetString() : null;
    }
}

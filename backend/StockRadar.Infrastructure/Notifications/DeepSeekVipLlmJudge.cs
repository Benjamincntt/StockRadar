using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>DeepSeek chat completions — veto ALLOW/BLOCK cho VIP BuyPoint (phương án A).</summary>
internal sealed class DeepSeekVipLlmJudge(
    IHttpClientFactory httpClientFactory,
    IOptions<VipLlmJudgeOptions> options,
    ILogger<DeepSeekVipLlmJudge> logger)
{
    public bool HasKey
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled && !string.IsNullOrWhiteSpace(cfg.ResolveDeepSeekKey());
        }
    }

    public async Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var sw = Stopwatch.StartNew();
        var model = cfg.Model;
        var apiKey = cfg.ResolveDeepSeekKey();
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(apiKey))
            return VipLlmJudgeResult.FallbackAllow("DeepSeek VIP judge tắt hoặc thiếu ApiKey.", model, 0);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Math.Clamp(cfg.TimeoutMs, 500, 15_000));

            var payload = new
            {
                model = cfg.Model,
                temperature = cfg.Temperature,
                max_tokens = cfg.MaxTokens,
                messages = new object[]
                {
                    new { role = "system", content = VipLlmJudgeParsing.SystemPrompt },
                    new { role = "user", content = VipLlmJudgeParsing.BuildUserPrompt(request) },
                },
            };

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                CombineUrl(cfg.ApiBaseUrl, "/chat/completions"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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
                    "DeepSeek VIP judge HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    VipLlmJudgeParsing.Truncate(body, 400));
                return Fallback(
                    cfg,
                    sw.ElapsedMilliseconds,
                    $"HTTP {(int)response.StatusCode}",
                    IsQuotaLike(response.StatusCode, body));
            }

            var content = ExtractAssistantContent(body);
            var parsed = VipLlmJudgeParsing.ParseDecision(content);
            if (parsed is null)
            {
                logger.LogWarning("DeepSeek VIP judge parse fail: {Content}", VipLlmJudgeParsing.Truncate(content, 400));
                return Fallback(cfg, sw.ElapsedMilliseconds, "Parse JSON thất bại", quotaLike: false);
            }

            var (decision, reason) = parsed.Value;
            logger.LogInformation(
                "DeepSeek VIP judge {Symbol} {Signal} → {Decision} ({Ms}ms): {Reason}",
                request.Symbol,
                request.Signal,
                decision,
                sw.ElapsedMilliseconds,
                reason);

            return new VipLlmJudgeResult(
                decision,
                reason,
                (int)sw.ElapsedMilliseconds,
                cfg.Model,
                FromFallback: false,
                RawResponse: VipLlmJudgeParsing.Truncate(content, 800));
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            logger.LogWarning(
                "DeepSeek VIP judge timeout {Ms}ms cho {Symbol}.",
                sw.ElapsedMilliseconds,
                request.Symbol);
            return Fallback(cfg, sw.ElapsedMilliseconds, "Timeout", quotaLike: false);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "DeepSeek VIP judge lỗi {Symbol}.", request.Symbol);
            return Fallback(cfg, sw.ElapsedMilliseconds, "Exception: " + ex.Message, quotaLike: false);
        }
    }

    public static bool IsRetryableFallback(VipLlmJudgeResult result) =>
        GeminiVipLlmJudge.IsRetryableFallback(result);

    private VipLlmJudgeResult Fallback(VipLlmJudgeOptions cfg, long latencyMs, string reason, bool quotaLike)
    {
        var tag = quotaLike ? $"quota/auth: {reason}" : reason;
        if (cfg.FailOpen)
            return VipLlmJudgeResult.FallbackAllow($"Fail-open: {tag}", cfg.Model, (int)latencyMs);

        return new VipLlmJudgeResult(
            VipLlmJudgeResult.Block,
            $"Fail-closed: {tag}",
            (int)latencyMs,
            cfg.Model,
            FromFallback: true);
    }

    private static bool IsQuotaLike(HttpStatusCode status, string body)
    {
        if (status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or (HttpStatusCode)402
            or HttpStatusCode.PaymentRequired)
            return true;

        return body.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
            || body.Contains("quota", StringComparison.OrdinalIgnoreCase)
            || body.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || body.Contains("balance", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractAssistantContent(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("choices", out var choices)
            || choices.GetArrayLength() == 0)
            return null;

        var msg = choices[0].GetProperty("message");
        if (msg.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            return content.GetString();

        return null;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var b = (baseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(b))
            b = "https://api.deepseek.com";
        if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return b + path;
        return b + "/v1" + path;
    }
}

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>ShopAIKey Anthropic Messages — veto ALLOW/BLOCK cho VIP BuyPoint.</summary>
internal sealed class AnthropicVipLlmJudge(
    IHttpClientFactory httpClientFactory,
    IOptions<VipLlmJudgeOptions> options,
    ILogger<AnthropicVipLlmJudge> logger) : IVipLlmJudge
{
    public bool IsEnabled
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled && !string.IsNullOrWhiteSpace(cfg.ApiKey);
        }
    }

    public async Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var sw = Stopwatch.StartNew();
        var model = string.IsNullOrWhiteSpace(cfg.Model) ? "claude-haiku-4-5-20251001" : cfg.Model.Trim();
        var apiKey = (cfg.ApiKey ?? "").Trim();
        if (!IsEnabled)
            return VipLlmJudgeResult.FallbackAllow("VipLlmJudge tắt hoặc thiếu ApiKey.", model, 0);

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

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CombineMessagesUrl(cfg.ApiBaseUrl));
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
                    "VIP LLM judge HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    VipLlmJudgeParsing.Truncate(body, 400));
                return Fallback(model, sw.ElapsedMilliseconds, $"HTTP {(int)response.StatusCode}");
            }

            var content = ExtractText(body);
            var parsed = VipLlmJudgeParsing.ParseDecision(content);
            if (parsed is null)
            {
                logger.LogWarning(
                    "VIP LLM judge parse fail: {Content}",
                    VipLlmJudgeParsing.Truncate(content, 400));
                return Fallback(model, sw.ElapsedMilliseconds, "Parse JSON thất bại");
            }

            var (decision, reason) = parsed.Value;
            logger.LogInformation(
                "VIP LLM judge {Symbol} {Signal} → {Decision} ({Ms}ms): {Reason}",
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
                "VIP LLM judge timeout {Ms}ms cho {Symbol}.",
                sw.ElapsedMilliseconds,
                request.Symbol);
            return Fallback(model, sw.ElapsedMilliseconds, "Timeout");
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "VIP LLM judge lỗi {Symbol}.", request.Symbol);
            return Fallback(model, sw.ElapsedMilliseconds, "Exception: " + ex.Message);
        }
    }

    private VipLlmJudgeResult Fallback(string model, long latencyMs, string reason)
    {
        var cfg = options.Value;
        if (cfg.FailOpen)
            return VipLlmJudgeResult.FallbackAllow($"Fail-open: {reason}", model, (int)latencyMs);

        return new VipLlmJudgeResult(
            VipLlmJudgeResult.Block,
            $"Fail-closed: {reason}",
            (int)latencyMs,
            model,
            FromFallback: true);
    }

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

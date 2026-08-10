using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>DeepSeek chat completions — veto ALLOW/BLOCK cho VIP BuyPoint (phương án A).</summary>
internal sealed class DeepSeekVipLlmJudge(
    HttpClient http,
    IOptions<VipLlmJudgeOptions> options,
    ILogger<DeepSeekVipLlmJudge> logger) : IVipLlmJudge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex JsonObjectRegex = new(
        @"\{[\s\S]*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public bool IsEnabled
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled
                && string.Equals(cfg.Provider, "deepseek", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(cfg.ApiKey);
        }
    }

    public async Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var sw = Stopwatch.StartNew();
        if (!IsEnabled)
            return VipLlmJudgeResult.FallbackAllow("VipLlmJudge tắt hoặc thiếu ApiKey.", cfg.Model, 0);

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
                    new { role = "system", content = SystemPrompt },
                    new
                    {
                        role = "user",
                        content =
                            $"Quyết định veto cho tín hiệu {request.Signal} mã {request.Symbol} (nhánh {request.Branch ?? "n/a"}).\n" +
                            "Dưới đây là hồ sơ đầy đủ cổ phiếu + ngữ cảnh trong phiên. Chỉ trả JSON.\n\n" +
                            request.ContextJson,
                    },
                },
            };

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                CombineUrl(cfg.ApiBaseUrl, "/chat/completions"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey.Trim());
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await http.SendAsync(httpRequest, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "DeepSeek VIP judge HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    Truncate(body, 400));
                return Fallback(cfg, sw.ElapsedMilliseconds, $"HTTP {(int)response.StatusCode}");
            }

            var content = ExtractAssistantContent(body);
            var parsed = ParseDecision(content);
            if (parsed is null)
            {
                logger.LogWarning("DeepSeek VIP judge parse fail: {Content}", Truncate(content, 400));
                return Fallback(cfg, sw.ElapsedMilliseconds, "Parse JSON thất bại");
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
                RawResponse: Truncate(content, 800));
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            logger.LogWarning(
                "DeepSeek VIP judge timeout {Ms}ms cho {Symbol}.",
                sw.ElapsedMilliseconds,
                request.Symbol);
            return Fallback(cfg, sw.ElapsedMilliseconds, "Timeout");
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "DeepSeek VIP judge lỗi {Symbol}.", request.Symbol);
            return Fallback(cfg, sw.ElapsedMilliseconds, "Exception: " + ex.Message);
        }
    }

    private VipLlmJudgeResult Fallback(VipLlmJudgeOptions cfg, long latencyMs, string reason)
    {
        if (cfg.FailOpen)
            return VipLlmJudgeResult.FallbackAllow($"Fail-open: {reason}", cfg.Model, (int)latencyMs);

        return new VipLlmJudgeResult(
            VipLlmJudgeResult.Block,
            $"Fail-closed: {reason}",
            (int)latencyMs,
            cfg.Model,
            FromFallback: true);
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

    private static (string Decision, string Reason)? ParseDecision(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var json = content.Trim();
        if (!json.StartsWith('{'))
        {
            var m = JsonObjectRegex.Match(json);
            if (!m.Success)
                return null;
            json = m.Value;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("decision", out var dEl))
            return null;

        var decision = (dEl.GetString() ?? "").Trim().ToUpperInvariant();
        if (decision is not (VipLlmJudgeResult.Allow or VipLlmJudgeResult.Block))
            return null;

        var reason = root.TryGetProperty("reason", out var rEl)
            ? (rEl.GetString() ?? "").Trim()
            : "";
        if (reason.Length > 280)
            reason = reason[..280];

        return (decision, string.IsNullOrWhiteSpace(reason) ? decision : reason);
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var b = baseUrl.TrimEnd('/');
        if (b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return b + path;
        return b + path;
    }

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";

    private const string SystemPrompt =
        """
        Bạn là giám đốc rủi ro giao dịch chứng khoán Việt Nam (HOSE/HNX/UPCOM) cho hệ thống StockRadar VIP Telegram.
        Nhiệm vụ: VETO lần cuối tín hiệu MUA đã qua rule + ML nội bộ.

        Chỉ trả đúng một JSON object, không markdown:
        {"decision":"ALLOW"|"BLOCK","reason":"≤2 câu tiếng Việt"}

        Nguyên tắc:
        - ALLOW nếu hồ sơ ủng hộ breakout/pullback hợp lý, thanh khoản ổn, rủi ro chấp nhận được trong ngữ cảnh pha TT.
        - BLOCK nếu: bẫy tăng đầu phiên / thanh khoản yếu / phân phối rõ / quá extended so MA / mâu thuẫn SetupDna-BuyScore-orderflow / pha Unfavorable mà tín hiệu yếu.
        - Không bịa số liệu ngoài JSON hồ sơ. Không tư vấn pháp lý. Không trả lời ngoài JSON.
        """;
}

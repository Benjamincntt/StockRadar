using System.Text.RegularExpressions;
using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.Notifications;

internal static class VipLlmJudgeParsing
{
    private static readonly Regex JsonObjectRegex = new(
        @"\{[\s\S]*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const string SystemPrompt =
        """
        Bạn là cổng lọc tín hiệu tự động StockRadar (VIP Telegram). Không phải tư vấn đầu tư.
        Nhiệm vụ: lọc lần cuối tín hiệu MUA hoặc BÁN/CẢNH BÁO đã qua rule nội bộ (+ ML với lệnh mua).

        Chỉ trả đúng một JSON object, không markdown, không giải thích ngoài JSON:
        {"decision":"ALLOW"|"BLOCK","reason":"≤2 câu tiếng Việt"}

        Nguyên tắc MUA:
        - ALLOW nếu hồ sơ ủng hộ breakout/pullback hợp lý, thanh khoản ổn, rủi ro chấp nhận được trong ngữ cảnh pha TT.
        - BLOCK nếu: bẫy tăng đầu phiên / thanh khoản yếu / phân phối rõ / quá extended so MA / mâu thuẫn SetupDna-BuyScore-orderflow / pha Unfavorable mà tín hiệu yếu.

        Nguyên tắc BÁN / CẢNH BÁO RỦI RO:
        - ALLOW nếu rút từ đỉnh/mốc đủ mạnh, phân phối rõ, hoặc phủ nhận cây vượt đỉnh — hợp lý để bảo vệ vốn.
        - BLOCK nếu tín hiệu bán sớm / nhiễu (rút nông, pha Favorable còn mạnh, thiếu xác nhận orderflow) mà hồ sơ không ủng hộ thoát.

        - Không bịa số liệu ngoài JSON hồ sơ. Không trả lời ngoài JSON.
        """;

    public static string BuildUserPrompt(VipLlmJudgeRequest request) =>
        $"Quyết định veto cho tín hiệu {request.Signal} mã {request.Symbol} (nhánh {request.Branch ?? "n/a"}).\n" +
        "Dưới đây là hồ sơ đầy đủ cổ phiếu + ngữ cảnh trong phiên. Chỉ trả JSON.\n\n" +
        request.ContextJson;

    public static (string Decision, string Reason)? ParseDecision(string? content)
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

        using var doc = System.Text.Json.JsonDocument.Parse(json);
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

    public static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";
}

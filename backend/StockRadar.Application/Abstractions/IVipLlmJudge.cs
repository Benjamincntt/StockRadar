namespace StockRadar.Application.Abstractions;

public sealed record VipLlmJudgeRequest(
    string Symbol,
    string Signal,
    string? Branch,
    string ContextJson);

public sealed record VipLlmJudgeResult(
    string Decision,
    string Reason,
    int LatencyMs,
    string Model,
    bool FromFallback,
    string? RawResponse = null)
{
    public const string Allow = "ALLOW";
    public const string Block = "BLOCK";

    public bool IsAllow => string.Equals(Decision, Allow, StringComparison.OrdinalIgnoreCase);
    public bool IsBlock => string.Equals(Decision, Block, StringComparison.OrdinalIgnoreCase);

    public static VipLlmJudgeResult FallbackAllow(string reason, string model, int latencyMs) =>
        new(Allow, reason, latencyMs, model, FromFallback: true);
}

public interface IVipLlmJudge
{
    bool IsEnabled { get; }

    Task<VipLlmJudgeResult> DecideAsync(
        VipLlmJudgeRequest request,
        CancellationToken cancellationToken = default);
}

using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Phase 1 — Shadow mode: đo hiệu quả các tín hiệu counter-trend đã lưu (không alert),
/// mô phỏng outcome trên OHLCV forward. Read-only, tính on-the-fly.
/// </summary>
public interface IReversalBounceShadowReportService
{
    Task<ReversalBounceShadowSummary> RunAsync(
        DateOnly from,
        DateOnly to,
        bool allowDefensiveEarlyExit = false,
        CancellationToken cancellationToken = default);
}

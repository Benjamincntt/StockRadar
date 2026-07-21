namespace StockRadar.Domain.Services.ReversalBounce;

public interface IMarketRegimeClassifier
{
    MarketBreadthSnapshot Classify(
        MarketBreadthSnapshot current,
        MarketBreadthSnapshot? previous,
        MarketRegimeThresholds thresholds);
}

/// <summary>
/// Phân loại regime 4 trạng thái với hysteresis <b>stateless</b>: trạng thái phiên trước lấy từ
/// snapshot đã lưu (không dùng state machine mutable). Nguyên tắc: <b>nâng chậm, hạ nhanh</b>.
/// </summary>
public sealed class MarketRegimeClassifier : IMarketRegimeClassifier
{
    public MarketBreadthSnapshot Classify(
        MarketBreadthSnapshot current,
        MarketBreadthSnapshot? previous,
        MarketRegimeThresholds thresholds)
    {
        var improveStreak = ComputeImproveStreak(current, previous);
        var previousRegime = previous?.Regime ?? MarketRegime.Normal;

        var isPanicRaw =
            current.VnIndexDrawdownPercent <= thresholds.PanicMaxDrawdownPercent
            && current.PctAboveMa20 <= thresholds.PanicMaxPctAboveMa20
            && current.FloorCount >= thresholds.PanicMinFloorCount;

        var isReboundRaw =
            current.VnIndexReclaimedMa20
            && current.PctAboveMa20 >= thresholds.ReboundMinPctAboveMa20;

        var isHealthy =
            current.VnIndexAboveMa20
            && current.PctAboveMa20 >= thresholds.NormalMinPctAboveMa20;

        var worsened = previous is not null && current.PctAboveMa20 < previous.PctAboveMa20;

        MarketRegime regime;
        if (isPanicRaw)
        {
            // Hạ nhanh: gặp điều kiện panic là vào ngay, không cần chờ.
            regime = MarketRegime.Panic;
        }
        else
        {
            regime = previousRegime switch
            {
                // Nâng chậm: cần đủ số phiên cải thiện liên tiếp mới thoát Panic.
                MarketRegime.Panic => improveStreak >= thresholds.PanicExitImproveStreak
                    ? MarketRegime.Stabilizing
                    : MarketRegime.Panic,

                // Hạ nhanh: chỉ 1 phiên độ rộng xấu đi là rớt khỏi Rebound.
                MarketRegime.ReboundConfirmed => worsened
                    ? MarketRegime.Stabilizing
                    : MarketRegime.ReboundConfirmed,

                MarketRegime.Stabilizing => isReboundRaw
                    ? MarketRegime.ReboundConfirmed
                    : isHealthy
                        ? MarketRegime.Normal
                        : MarketRegime.Stabilizing,

                _ => isReboundRaw
                    ? MarketRegime.ReboundConfirmed
                    : isHealthy
                        ? MarketRegime.Normal
                        : MarketRegime.Stabilizing
            };
        }

        return current with { Regime = regime, ImproveStreak = improveStreak };
    }

    /// <summary>
    /// Số phiên cải thiện liên tiếp: PctAboveMa20 tăng VÀ FloorCount giảm so phiên trước.
    /// </summary>
    private static int ComputeImproveStreak(MarketBreadthSnapshot current, MarketBreadthSnapshot? previous)
    {
        if (previous is null)
            return 0;

        var improved =
            current.PctAboveMa20 > previous.PctAboveMa20
            && current.FloorCount < previous.FloorCount;

        return improved ? previous.ImproveStreak + 1 : 0;
    }
}

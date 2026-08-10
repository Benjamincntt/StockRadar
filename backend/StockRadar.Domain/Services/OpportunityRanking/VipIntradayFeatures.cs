namespace StockRadar.Domain.Services.OpportunityRanking;

/// <summary>Feature intraday VIP — orderflow + session context (Phase 3).</summary>
public static class VipIntradayFeatures
{
    public static readonly string[] Names =
    [
        "gain_from_open_norm",
        "paced_vol_norm",
        "daily_ml_prob_norm",
        "atr_norm",
        "dist_ma20_norm",
        "uptrend_long",
        "foreign_net_norm",
        "prop_net_norm",
        "pressure_norm",
        "vsa_xa",
    ];

    public static double[] Vectorize(VipIntradayInput input) =>
    [
        Math.Clamp((double)input.GainFromOpenPercent / 8.0, -1.0, 1.5),
        Math.Clamp((double)input.PacedVolumeRatio / 3.0, 0.0, 2.0),
        (double)Math.Clamp(input.DailyMlProbPercent, 0m, 100m) / 100.0,
        Math.Clamp((double)(input.AtrPercent ?? 0m) / 5.0, 0.0, 1.0),
        Math.Clamp((double)(input.DistMa20Percent ?? 0m) / 10.0, -1.0, 1.0),
        input.UptrendLong == true ? 1.0 : 0.0,
        Math.Clamp((input.ForeignNet ?? 0L) / 200_000.0, -1.0, 1.0),
        Math.Clamp((input.PropNet ?? 0L) / 150_000.0, -1.0, 1.0),
        Math.Clamp((double)(input.SessionPressure ?? 0m) / 40.0, -1.0, 1.0),
        input.IsVsaXa ? 1.0 : 0.0,
    ];
}

public sealed record VipIntradayInput(
    decimal GainFromOpenPercent,
    decimal PacedVolumeRatio,
    decimal DailyMlProbPercent,
    decimal? AtrPercent,
    decimal? DistMa20Percent,
    bool? UptrendLong,
    long? ForeignNet,
    long? PropNet,
    decimal? SessionPressure,
    bool IsVsaXa);

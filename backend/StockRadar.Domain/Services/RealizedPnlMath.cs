using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Domain.Services;

/// <summary>Bộ phí/thuế áp cho 1 lệnh: mua trả phí, bán trả phí + thuế.</summary>
public readonly record struct FeeProfile(decimal BuyFeePercent, decimal SellFeePercent, decimal SellTaxPercent)
{
    public static readonly FeeProfile Zero = new(0m, 0m, 0m);

    /// <summary>
    /// Khoá nhận diện cấu hình phí — dùng để tự động phát hiện đổi phí cần recompute (so <c>RealizedFeeProfile</c> đã lưu).
    /// InvariantCulture để "0.15" không bị serialize thành "0,15" theo culture máy chủ.
    /// </summary>
    public string Key(decimal winThresholdPercent)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return $"b{BuyFeePercent.ToString(ci)}/s{SellFeePercent.ToString(ci)}/t{SellTaxPercent.ToString(ci)}/w{winThresholdPercent.ToString(ci)}";
    }
}

/// <summary>1 nhịp bán (Bán 1 nửa hoặc Bán hết) với giá và size thực bán.</summary>
public sealed record SellLeg(DateOnly SellDate, decimal SellPrice, decimal SoldSize);

/// <summary>Kết quả realized P&amp;L của 1 vị thế đã đóng, tổng hợp từ các nhịp bán.</summary>
public sealed record RealizedResult(
    decimal WeightedReturnPercent,
    decimal ReturnOnDeployedPercent,
    decimal GrossReturnOnDeployedPercent,
    decimal TotalSoldSize,
    int LegCount,
    DateOnly LastSellDate);

/// <summary>Math thuần tính lợi nhuận thực (giá bán thật, trừ phí) — cạnh <see cref="TradingSessionMath"/>, không đụng DB.</summary>
public static class RealizedPnlMath
{
    /// <summary>% lợi nhuận ròng 1 nhịp bán sau phí mua + phí/thuế bán. Null nếu giá vào lệnh/giá bán không hợp lệ.</summary>
    public static decimal? NetLegReturnPercent(decimal entryPrice, decimal sellPrice, FeeProfile fees)
    {
        if (entryPrice <= 0 || sellPrice <= 0)
            return null;

        var buyCost = entryPrice * (1 + fees.BuyFeePercent / 100m);
        var netProceeds = sellPrice * (1 - (fees.SellFeePercent + fees.SellTaxPercent) / 100m);
        return (netProceeds - buyCost) / buyCost * 100m;
    }

    /// <summary>
    /// Tổng hợp realized P&amp;L từ các nhịp bán. Trọng số theo size thực bán mỗi nhịp (không normalize).
    /// Null nếu <paramref name="entryPrice"/> &lt;= 0, không có leg hợp lệ, hoặc tổng size bán &lt;= 0.
    /// </summary>
    public static RealizedResult? Compute(decimal entryPrice, IReadOnlyList<SellLeg> legs, FeeProfile fees)
    {
        if (entryPrice <= 0 || legs is null || legs.Count == 0)
            return null;

        var weightedNet = 0m;
        var weightedGross = 0m;
        var totalSize = 0m;
        var legCount = 0;
        var lastSellDate = default(DateOnly);

        foreach (var leg in legs)
        {
            if (leg.SellPrice <= 0 || leg.SoldSize <= 0)
                continue;

            var netReturn = NetLegReturnPercent(entryPrice, leg.SellPrice, fees);
            var grossReturn = NetLegReturnPercent(entryPrice, leg.SellPrice, FeeProfile.Zero);
            if (netReturn is null || grossReturn is null)
                continue;

            weightedNet += leg.SoldSize * netReturn.Value;
            weightedGross += leg.SoldSize * grossReturn.Value;
            totalSize += leg.SoldSize;
            legCount++;
            if (leg.SellDate > lastSellDate)
                lastSellDate = leg.SellDate;
        }

        if (totalSize <= 0 || legCount == 0)
            return null;

        var onDeployed = weightedNet / totalSize;
        var grossOnDeployed = weightedGross / totalSize;

        return new RealizedResult(
            Math.Round(weightedNet, 2),
            Math.Round(onDeployed, 2),
            Math.Round(grossOnDeployed, 2),
            totalSize,
            legCount,
            lastSellDate);
    }

    /// <summary>
    /// Phân loại Good/Flat/Failed dựa trên <c>ReturnOnDeployedPercent</c> — KHÔNG dùng Weighted
    /// (nếu dùng Weighted thì lệnh size 0.5 bị so cùng thang với lệnh 1.0, sai ngay khi ngưỡng khác 0).
    /// </summary>
    public static string Classify(decimal returnOnDeployedPercent, decimal winThresholdPercent)
    {
        if (returnOnDeployedPercent > winThresholdPercent)
            return OutcomeBucketNames.Good;

        if (returnOnDeployedPercent < winThresholdPercent)
            return OutcomeBucketNames.Failed;

        return OutcomeBucketNames.Flat;
    }
}

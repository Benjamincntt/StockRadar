using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.RealizedPnl;

public sealed class RealizedPnlMathTests
{
    private static readonly DateOnly SellDate1 = new(2026, 7, 8);
    private static readonly DateOnly SellDate2 = new(2026, 7, 10);

    [Fact]
    public void Two_legs_size_1_weighted_equals_on_deployed()
    {
        // size 1.0: bán nửa 0.5 @R1=+10%, bán hết 0.5 @R2=+5% (bỏ phí) → Weighted = OnDeployed = 0.5*10 + 0.5*5 = 7.5%
        var legs = new List<SellLeg>
        {
            new(SellDate1, 110m, 0.5m), // entry 100 → R1 = +10%
            new(SellDate2, 105m, 0.5m), // entry 100 → R2 = +5%
        };

        var result = RealizedPnlMath.Compute(100m, legs, FeeProfile.Zero);

        Assert.NotNull(result);
        Assert.Equal(7.5m, result!.WeightedReturnPercent);
        Assert.Equal(7.5m, result.ReturnOnDeployedPercent);
        Assert.Equal(1.0m, result.TotalSoldSize);
        Assert.Equal(2, result.LegCount);
        Assert.Equal(SellDate2, result.LastSellDate);
    }

    [Fact]
    public void Two_legs_size_half_weighted_differs_from_on_deployed()
    {
        // size 0.5: bán nửa 0.25 + bán hết 0.25, cùng R1/R2 → Weighted = 0.25*10+0.25*5 = 3.75%, OnDeployed vẫn 7.5%
        var legs = new List<SellLeg>
        {
            new(SellDate1, 110m, 0.25m),
            new(SellDate2, 105m, 0.25m),
        };

        var result = RealizedPnlMath.Compute(100m, legs, FeeProfile.Zero);

        Assert.NotNull(result);
        Assert.Equal(3.75m, result!.WeightedReturnPercent);
        Assert.Equal(7.5m, result.ReturnOnDeployedPercent);
        Assert.Equal(0.5m, result.TotalSoldSize);
    }

    [Fact]
    public void Single_leg_BanHet_without_BanNua_on_deployed_equals_return()
    {
        // BanHet không có BanNua trước — 1 leg SoldSize = MaxPositionSize (0.5) — vẫn hợp lệ.
        var legs = new List<SellLeg> { new(SellDate1, 112m, 0.5m) }; // entry 100 → R = +12%

        var result = RealizedPnlMath.Compute(100m, legs, FeeProfile.Zero);

        Assert.NotNull(result);
        Assert.Equal(1, result!.LegCount);
        Assert.Equal(12m, result.ReturnOnDeployedPercent);
    }

    [Fact]
    public void Fees_make_net_return_negative_when_gross_barely_positive_and_classifies_Failed()
    {
        // Gross +0.3% nhưng phí round-trip (mua 0.15 + bán 0.25 + thuế 0.1 = 0.5%) đủ ăn hết lãi → net âm.
        var fees = new FeeProfile(0.15m, 0.25m, 0.1m);
        var legs = new List<SellLeg> { new(SellDate1, 100.3m, 1.0m) };

        var result = RealizedPnlMath.Compute(100m, legs, fees);

        Assert.NotNull(result);
        Assert.Equal(0.30m, result!.GrossReturnOnDeployedPercent);
        Assert.True(result.ReturnOnDeployedPercent < 0m, $"Expected net return < 0, got {result.ReturnOnDeployedPercent}");
        // Lệch gross vs net đúng ~0.5% (tổng phí round-trip).
        Assert.Equal(0.50m, result.GrossReturnOnDeployedPercent - result.ReturnOnDeployedPercent);
        Assert.Equal(OutcomeBucketNames.Failed, RealizedPnlMath.Classify(result.ReturnOnDeployedPercent, 0m));
    }

    [Fact]
    public void Classify_boundary_at_zero()
    {
        Assert.Equal(OutcomeBucketNames.Flat, RealizedPnlMath.Classify(0m, 0m));
        Assert.Equal(OutcomeBucketNames.Good, RealizedPnlMath.Classify(0.01m, 0m));
        Assert.Equal(OutcomeBucketNames.Failed, RealizedPnlMath.Classify(-0.01m, 0m));
    }

    [Fact]
    public void FeeProfile_Key_uses_invariant_culture()
    {
        var fees = new FeeProfile(0.15m, 0.25m, 0.1m);

        var key = fees.Key(0m);

        Assert.Equal("b0.15/s0.25/t0.1/w0", key);
        Assert.DoesNotContain(',', key);
    }

    [Fact]
    public void Compute_returns_null_when_entry_price_is_not_positive()
    {
        var legs = new List<SellLeg> { new(SellDate1, 100m, 1.0m) };

        Assert.Null(RealizedPnlMath.Compute(0m, legs, FeeProfile.Zero));
        Assert.Null(RealizedPnlMath.Compute(-10m, legs, FeeProfile.Zero));
    }

    [Fact]
    public void Compute_returns_null_when_legs_empty()
    {
        Assert.Null(RealizedPnlMath.Compute(100m, [], FeeProfile.Zero));
    }

    [Fact]
    public void Compute_returns_null_when_total_sold_size_not_positive()
    {
        // SellPrice <= 0 → bỏ leg; SoldSize <= 0 → bỏ leg → Σ SoldSize = 0 → null.
        var legs = new List<SellLeg>
        {
            new(SellDate1, 0m, 1.0m),
            new(SellDate2, 100m, 0m),
        };

        Assert.Null(RealizedPnlMath.Compute(100m, legs, FeeProfile.Zero));
    }
}

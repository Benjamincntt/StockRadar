using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

public sealed record RegimeOverlayInput(
    MarketWyckoffPhase MarketPhase,
    decimal? WinRate7d,
    int MeasuredCount7d,
    decimal PersonalCalibrationFactor = 1m);

public sealed record RegimeOverlayResult(
    decimal AdjustedHitPercent,
    decimal RegimeFactor,
    decimal SizeFactorPercent,
    IReadOnlyList<string> Notes);

public static class RegimeOverlayEvaluator
{
    public static RegimeOverlayResult Apply(
        decimal rawHitPercent,
        RegimeOverlayInput input,
        decimal lowWinRateThreshold = 45m)
    {
        var notes = new List<string>();
        var regimeFactor = 1m;
        var sizeFactor = 100m;

        switch (input.MarketPhase)
        {
            case MarketWyckoffPhase.Favorable:
                regimeFactor = 1.03m;
                notes.Add("TT thuận lợi");
                break;
            case MarketWyckoffPhase.Neutral:
                regimeFactor = 1m;
                break;
            default:
                regimeFactor = 0.9m;
                sizeFactor *= 0.65m;
                notes.Add("TT bất lợi — giảm size");
                break;
        }

        if (input.MeasuredCount7d >= 5 && input.WinRate7d is not null)
        {
            if (input.WinRate7d < lowWinRateThreshold)
            {
                sizeFactor *= 0.7m;
                regimeFactor *= 0.95m;
                notes.Add($"Win 7d thấp ({input.WinRate7d:0.#}%)");
            }
            else if (input.WinRate7d >= 55m)
            {
                notes.Add($"Win 7d tốt ({input.WinRate7d:0.#}%)");
            }
        }

        var personal = Math.Clamp(input.PersonalCalibrationFactor, 0.75m, 1.25m);
        if (Math.Abs(personal - 1m) > 0.03m)
            notes.Add($"Cal cá nhân ×{personal:0.##}");

        var adjusted = Math.Round(
            Math.Clamp(rawHitPercent * regimeFactor * personal, 5m, 95m),
            1);

        return new RegimeOverlayResult(
            adjusted,
            regimeFactor,
            Math.Round(sizeFactor, 0),
            notes);
    }
}

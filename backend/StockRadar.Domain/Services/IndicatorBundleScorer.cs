using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public interface IIndicatorBundleScorer
{
    IReadOnlyList<CriterionScore> ScoreBundles(
        IReadOnlyList<OhlcvBar> history,
        IReadOnlyDictionary<CriterionType, CriterionScore> singles);
}

public sealed class IndicatorBundleScorer(ISignalAnalyzer signals) : IIndicatorBundleScorer
{
    public IReadOnlyList<CriterionScore> ScoreBundles(
        IReadOnlyList<OhlcvBar> history,
        IReadOnlyDictionary<CriterionType, CriterionScore> singles) =>
    [
        Combine(CriterionType.BundleBeginner, "Mới", "EMA + RSI + Volume", singles,
            CriterionType.MovingAverage, CriterionType.Rsi, CriterionType.Volume),
        Combine(CriterionType.BundleIntermediate, "Trung cấp", "EMA + Volume + ATR", singles,
            CriterionType.MovingAverage, CriterionType.Volume, CriterionType.Atr),
        Combine(CriterionType.BundleAdvanced, "Nâng cao", "VWAP + EMA + Volume + ATR", singles,
            CriterionType.Vwap, CriterionType.MovingAverage, CriterionType.Volume, CriterionType.Atr),
        ScoreProfessional(history, singles),
        ScoreInstitutional(history, singles),
        ScoreSmartMoneyConcept(history, singles),
    ];

    private static CriterionScore Combine(
        CriterionType bundleType,
        string level,
        string components,
        IReadOnlyDictionary<CriterionType, CriterionScore> singles,
        params CriterionType[] parts)
    {
        var items = parts
            .Select(p => singles.TryGetValue(p, out var s) ? s : null)
            .Where(s => s is not null)
            .Cast<CriterionScore>()
            .ToList();

        if (items.Count < parts.Length || items.Any(s => s.Score == 0))
            return new(bundleType, 0, PatternBias.Neutral, $"{level}: chưa đủ dữ liệu ({components})");

        var avg = (int)Math.Round(items.Average(s => s.Score));
        var bull = items.Count(s => s.Bias == PatternBias.Bullish);
        var bear = items.Count(s => s.Bias == PatternBias.Bearish);
        var aligned = bull == items.Count || bear == items.Count;
        if (aligned) avg = Math.Min(100, avg + 10);

        var bias = bull > bear ? PatternBias.Bullish
            : bear > bull ? PatternBias.Bearish
            : PatternBias.Neutral;

        var detail = string.Join(" · ", items.Select(s => $"{CriterionLabelsShort.Get(s.Type)} {s.Score}"));
        return new(bundleType, avg, bias, $"{components} — {detail}");
    }

    private CriterionScore ScoreProfessional(
        IReadOnlyList<OhlcvBar> history,
        IReadOnlyDictionary<CriterionType, CriterionScore> singles)
    {
        if (history.Count < 20)
            return new(CriterionType.BundleProfessional, 0, PatternBias.Neutral, "Wyckoff + VSA: cần ≥20 phiên");

        var stock = new Stock("", "", "", history);
        var detected = signals.DetectSignals(stock, 0m);
        var vsa = ScoreVsa(history);

        var wyckoffScore = 45;
        var wyckoffBias = PatternBias.Neutral;
        var wyckoffNote = "Wyckoff chưa rõ";

        if (detected.Contains(SignalType.Accumulation) || detected.Contains(SignalType.Shakeout))
        {
            wyckoffScore = 82;
            wyckoffBias = PatternBias.Bullish;
            wyckoffNote = "Wyckoff tích lũy / shakeout";
        }
        else if (detected.Contains(SignalType.Breakout) || detected.Contains(SignalType.DarvasBreakout))
        {
            wyckoffScore = 85;
            wyckoffBias = PatternBias.Bullish;
            wyckoffNote = "Wyckoff markup / breakout";
        }
        else if (detected.Contains(SignalType.Distribution))
        {
            wyckoffScore = 84;
            wyckoffBias = PatternBias.Bearish;
            wyckoffNote = "Wyckoff phân phối";
        }

        var avg = (wyckoffScore + vsa.Score) / 2;
        var bias = wyckoffBias == vsa.Bias ? wyckoffBias
            : wyckoffBias == PatternBias.Neutral ? vsa.Bias
            : vsa.Bias == PatternBias.Neutral ? wyckoffBias
            : PatternBias.Neutral;

        if (wyckoffBias == vsa.Bias && wyckoffBias != PatternBias.Neutral)
            avg = Math.Min(100, avg + 8);

        return new(
            CriterionType.BundleProfessional,
            avg,
            bias,
            $"Wyckoff + VSA — {wyckoffNote}; {vsa.Summary}");
    }

    private CriterionScore ScoreInstitutional(
        IReadOnlyList<OhlcvBar> history,
        IReadOnlyDictionary<CriterionType, CriterionScore> singles)
    {
        if (history.Count < 20)
            return new(CriterionType.BundleInstitutional, 0, PatternBias.Neutral,
                "Volume Profile + VWAP + Delta: cần ≥20 phiên");

        var vwap = singles.GetValueOrDefault(CriterionType.Vwap);
        var volProfile = ScoreVolumeProfile(history);
        var delta = ScoreDelta(history);

        var parts = new[] { volProfile, delta };
        if (vwap is not null && vwap.Score > 0)
            parts = [volProfile, vwap, delta];

        var scores = parts.Where(p => p.Score > 0).ToList();
        if (scores.Count < 2)
            return new(CriterionType.BundleInstitutional, 0, PatternBias.Neutral, "Chưa đủ dữ liệu thành phần");

        var avg = (int)Math.Round(scores.Average(p => p.Score));
        var bull = scores.Count(p => p.Bias == PatternBias.Bullish);
        var bear = scores.Count(p => p.Bias == PatternBias.Bearish);
        var bias = bull > bear ? PatternBias.Bullish : bear > bull ? PatternBias.Bearish : PatternBias.Neutral;
        if (bull == scores.Count || bear == scores.Count) avg = Math.Min(100, avg + 8);

        return new(
            CriterionType.BundleInstitutional,
            avg,
            bias,
            $"Vol Profile + VWAP + Delta — {volProfile.Summary}; Δ {delta.Summary}");
    }

    private CriterionScore ScoreSmartMoneyConcept(
        IReadOnlyList<OhlcvBar> history,
        IReadOnlyDictionary<CriterionType, CriterionScore> singles)
    {
        if (history.Count < 25)
            return new(CriterionType.BundleSmartMoneyConcept, 0, PatternBias.Neutral, "SMC + Volume + VWAP: cần ≥25 phiên");

        var smc = ScoreSmc(history);
        var vol = singles.GetValueOrDefault(CriterionType.Volume);
        var vwap = singles.GetValueOrDefault(CriterionType.Vwap);

        var items = new List<(int Score, PatternBias Bias, string Note)>
        {
            (smc.Score, smc.Bias, smc.Summary),
        };
        if (vol is { Score: > 0 }) items.Add((vol.Score, vol.Bias, $"Vol {vol.Score}"));
        if (vwap is { Score: > 0 }) items.Add((vwap.Score, vwap.Bias, $"VWAP {vwap.Score}"));

        var avg = (int)Math.Round(items.Average(i => i.Score));
        var bull = items.Count(i => i.Bias == PatternBias.Bullish);
        var bear = items.Count(i => i.Bias == PatternBias.Bearish);
        var bias = bull > bear ? PatternBias.Bullish : bear > bull ? PatternBias.Bearish : PatternBias.Neutral;
        if (bull == items.Count || bear == items.Count) avg = Math.Min(100, avg + 8);

        return new(
            CriterionType.BundleSmartMoneyConcept,
            avg,
            bias,
            $"SMC + Volume + VWAP — {string.Join(" · ", items.Select(i => i.Note))}");
    }

    private CriterionScore ScoreVsa(IReadOnlyList<OhlcvBar> history)
    {
        var bar = history[^1];
        var spread = bar.High - bar.Low;
        var avgSpread = history.TakeLast(10).Average(b => b.High - b.Low);
        var volRatio = signals.GetVolumeRatio(history);
        var upBar = bar.Close >= bar.Open;
        var narrow = avgSpread > 0 && spread < avgSpread * 0.75m;
        var wide = avgSpread > 0 && spread > avgSpread * 1.25m;

        if (narrow && volRatio >= 1.2m && upBar)
            return new(CriterionType.BundleProfessional, 80, PatternBias.Bullish,
                "VSA: spread hẹp + volume — dấu chân phe mua");
        if (wide && volRatio >= 1.4m && !upBar)
            return new(CriterionType.BundleProfessional, 78, PatternBias.Bearish,
                "VSA: spread rộng + volume — áp lực bán");
        if (narrow && volRatio < 0.8m)
            return new(CriterionType.BundleProfessional, 55, PatternBias.Neutral, "VSA: sideway, volume mỏng");

        var score = (int)Math.Clamp(50 + (upBar ? 8 : -8) + (volRatio - 1) * 15, 30, 70);
        var bias = upBar && volRatio >= 1.1m ? PatternBias.Bullish
            : !upBar && volRatio >= 1.1m ? PatternBias.Bearish
            : PatternBias.Neutral;
        return new(CriterionType.BundleProfessional, score, bias, $"VSA spread/vol {volRatio:0.#}×");
    }

    private static CriterionScore ScoreVolumeProfile(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 20;
        const int bins = 12;
        var slice = history.TakeLast(period).ToList();
        var min = slice.Min(b => b.Low);
        var max = slice.Max(b => b.High);
        if (max <= min)
            return new(CriterionType.BundleInstitutional, 40, PatternBias.Neutral, "Vol Profile: biên độ bằng 0");

        var step = (max - min) / bins;
        var volumes = new decimal[bins];
        foreach (var b in slice)
        {
            var idx = (int)Math.Clamp((b.Close - min) / step, 0, bins - 1);
            volumes[idx] += b.Volume;
        }

        var pocIdx = Array.IndexOf(volumes, volumes.Max());
        var poc = min + (pocIdx + 0.5m) * step;
        var close = history[^1].Close;
        var dist = poc > 0 ? (close - poc) / poc * 100m : 0m;

        if (dist > 0 && dist <= 2m)
            return new(CriterionType.BundleInstitutional, 78, PatternBias.Bullish,
                $"POC {poc:N0} — giá trên vùng khối lượng");
        if (dist < 0 && dist >= -2m)
            return new(CriterionType.BundleInstitutional, 76, PatternBias.Bearish,
                $"POC {poc:N0} — giá dưới vùng khối lượng");
        if (Math.Abs(dist) <= 1m)
            return new(CriterionType.BundleInstitutional, 65, PatternBias.Neutral,
                $"POC {poc:N0} — giá quanh vùng HVN");

        var bias = dist > 0 ? PatternBias.Bullish : PatternBias.Bearish;
        return new(CriterionType.BundleInstitutional, 55, bias, $"POC {poc:N0} · lệch {dist:0.#}%");
    }

    private static CriterionScore ScoreDelta(IReadOnlyList<OhlcvBar> history)
    {
        decimal delta = 0;
        foreach (var b in history.TakeLast(5))
        {
            var range = b.High - b.Low;
            if (range <= 0) continue;
            delta += (b.Close - b.Open) / range * b.Volume;
        }

        var avgVol = (decimal)history.TakeLast(5).Average(b => (double)b.Volume);
        var norm = avgVol > 0 ? delta / (avgVol * 5m) : 0m;

        if (norm > 0.25m)
            return new(CriterionType.BundleInstitutional, 82, PatternBias.Bullish,
                $"Delta dương mạnh ({norm:0.##})");
        if (norm < -0.25m)
            return new(CriterionType.BundleInstitutional, 80, PatternBias.Bearish,
                $"Delta âm mạnh ({norm:0.##})");
        if (norm > 0.08m)
            return new(CriterionType.BundleInstitutional, 68, PatternBias.Bullish, $"Delta +{norm:0.##}");
        if (norm < -0.08m)
            return new(CriterionType.BundleInstitutional, 66, PatternBias.Bearish, $"Delta {norm:0.##}");

        return new(CriterionType.BundleInstitutional, 50, PatternBias.Neutral, "Delta cân bằng");
    }

    private static CriterionScore ScoreSmc(IReadOnlyList<OhlcvBar> history)
    {
        var lookback = Math.Min(20, history.Count - 1);
        var slice = history.TakeLast(lookback + 1).ToList();
        var lows = slice.SkipLast(1).Select(b => b.Low).ToList();
        var highs = slice.SkipLast(1).Select(b => b.High).ToList();
        var bar = slice[^1];
        var minLow = lows.Min();
        var maxHigh = highs.Max();

        var sweepLow = bar.Low < minLow * 0.995m && bar.Close > minLow;
        var sweepHigh = bar.High > maxHigh * 1.005m && bar.Close < maxHigh;
        var bosUp = bar.Close > maxHigh * 1.01m;
        var bosDown = bar.Close < minLow * 0.99m;

        if (sweepLow && bar.Close > bar.Open)
            return new(CriterionType.BundleSmartMoneyConcept, 88, PatternBias.Bullish,
                "SMC: quét thanh khoản đáy + hồi");
        if (bosUp)
            return new(CriterionType.BundleSmartMoneyConcept, 85, PatternBias.Bullish,
                "SMC: break of structure tăng");
        if (sweepHigh && bar.Close < bar.Open)
            return new(CriterionType.BundleSmartMoneyConcept, 86, PatternBias.Bearish,
                "SMC: quét thanh khoản đỉnh + yếu");
        if (bosDown)
            return new(CriterionType.BundleSmartMoneyConcept, 84, PatternBias.Bearish,
                "SMC: break of structure giảm");

        return new(CriterionType.BundleSmartMoneyConcept, 48, PatternBias.Neutral, "SMC: chưa có BOS/sweep rõ");
    }
}

internal static class CriterionLabelsShort
{
    public static string Get(CriterionType t) => t switch
    {
        CriterionType.MovingAverage => "EMA",
        CriterionType.Rsi => "RSI",
        CriterionType.Volume => "Vol",
        CriterionType.Atr => "ATR",
        CriterionType.Vwap => "VWAP",
        _ => t.ToString(),
    };
}

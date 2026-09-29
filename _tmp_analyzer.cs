using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public interface ITechnicalIndicatorAnalyzer
{
    IReadOnlyList<CriterionScore> ScoreIndicators(Stock stock);
    IReadOnlyList<CriterionScore> ScoreIndicators(IReadOnlyList<OhlcvBar> history);
}

public sealed class TechnicalIndicatorAnalyzer(
    ISignalAnalyzer signals,
    IIndicatorBundleScorer bundleScorer) : ITechnicalIndicatorAnalyzer
{
    public IReadOnlyList<CriterionScore> ScoreIndicators(Stock stock) =>
        ScoreIndicators(stock.History);

    public IReadOnlyList<CriterionScore> ScoreIndicators(IReadOnlyList<OhlcvBar> history)
    {
        var singles = ScoreSingles(history);
        var dict = singles.ToDictionary(s => s.Type);
        var bundles = bundleScorer.ScoreBundles(history, dict);
        return singles.Concat(bundles).ToList();
    }

    private IReadOnlyList<CriterionScore> ScoreSingles(IReadOnlyList<OhlcvBar> history) =>
    [
        ScoreRsi(history),
        ScoreMovingAverage(history),
        ScoreMacd(history),
        ScoreVolume(history),
        ScoreVwap(history),
        ScoreBollinger(history),
        ScoreAtr(history),
        ScoreIchimoku(history),
        ScoreStochastic(history),
        ScoreAdx(history),
    ];

    private static CriterionScore ScoreRsi(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 14;
        if (history.Count < period + 2)
            return new(CriterionType.Rsi, 0, PatternBias.Neutral, $"Cß║ºn ΓëÑ{period + 2} phi├¬n");

        var rsi = IndicatorMath.Rsi(history, period);
        var prevRsi = IndicatorMath.Rsi(history.Take(history.Count - 1).ToList(), period);
        var rising = rsi > prevRsi;

        if (rsi < 30 && rising)
            return new(CriterionType.Rsi, 88, PatternBias.Bullish, $"RSI {rsi:0} ΓÇö qu├í b├ín, hß╗ôi phß╗Ñc");
        if (rsi > 70 && !rising)
            return new(CriterionType.Rsi, 85, PatternBias.Bearish, $"RSI {rsi:0} ΓÇö qu├í mua, yß║┐u dß║ºn");
        if (rsi is >= 45 and <= 60 && rising)
            return new(CriterionType.Rsi, 78, PatternBias.Bullish, $"RSI {rsi:0} ΓÇö momentum t─âng");
        if (rsi is >= 40 and <= 55 && !rising)
            return new(CriterionType.Rsi, 72, PatternBias.Bearish, $"RSI {rsi:0} ΓÇö momentum giß║úm");

        var bias = rsi > 55 ? PatternBias.Bullish : rsi < 45 ? PatternBias.Bearish : PatternBias.Neutral;
        var score = (int)Math.Clamp(50 + (rsi - 50) * 0.8m + (rising ? 5 : -5), 25, 75);
        return new(CriterionType.Rsi, score, bias, $"RSI {rsi:0}");
    }

    private static CriterionScore ScoreMovingAverage(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 50)
            return new(CriterionType.MovingAverage, 0, PatternBias.Neutral, "Cß║ºn ΓëÑ50 phi├¬n (EMA/SMA)");

        var closes = history.Select(b => b.Close).ToList();
        var ema20 = IndicatorMath.Ema(closes, 20);
        var ema50 = IndicatorMath.Ema(closes, 50);
        var sma200 = IndicatorMath.Sma(history, 200);
        var close = history[^1].Close;
        var prevEma20 = IndicatorMath.Ema(closes.Take(closes.Count - 1).ToList(), 20);
        var goldenCross = prevEma20 <= ema50 && ema20 > ema50;

        if (close > ema20 && ema20 > ema50 && close > sma200)
            return new(CriterionType.MovingAverage, 92, PatternBias.Bullish,
                goldenCross ? "EMA20 cß║»t l├¬n EMA50 ┬╖ gi├í tr├¬n MA" : "Gi├í tr├¬n EMA20/50 ┬╖ xu h╞░ß╗¢ng t─âng");
        if (close < ema20 && ema20 < ema50 && close < sma200)
            return new(CriterionType.MovingAverage, 90, PatternBias.Bearish, "Gi├í d╞░ß╗¢i EMA20/50 ┬╖ xu h╞░ß╗¢ng giß║úm");
        if (close > ema20 && ema20 > ema50)
            return new(CriterionType.MovingAverage, 75, PatternBias.Bullish, "EMA stack t─âng ngß║»n hß║ín");
        if (close < ema20 && ema20 < ema50)
            return new(CriterionType.MovingAverage, 73, PatternBias.Bearish, "EMA stack giß║úm ngß║»n hß║ín");

        return new(CriterionType.MovingAverage, 45, PatternBias.Neutral, "MA ch╞░a x├íc nhß║¡n xu h╞░ß╗¢ng");
    }

    private static CriterionScore ScoreMacd(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 35)
            return new(CriterionType.Macd, 0, PatternBias.Neutral, "Cß║ºn ΓëÑ35 phi├¬n");

        var (macd, signal, hist) = IndicatorMath.Macd(history);
        var prev = IndicatorMath.Macd(history.Take(history.Count - 1).ToList());
        var crossUp = prev.macd <= prev.signal && macd > signal;
        var crossDown = prev.macd >= prev.signal && macd < signal;

        if (crossUp && hist > 0)
            return new(CriterionType.Macd, 90, PatternBias.Bullish, "MACD cß║»t l├¬n signal ┬╖ histogram d╞░╞íng");
        if (crossDown && hist < 0)
            return new(CriterionType.Macd, 88, PatternBias.Bearish, "MACD cß║»t xuß╗æng signal ┬╖ histogram ├óm");
        if (macd > signal && hist > prev.hist)
            return new(CriterionType.Macd, 76, PatternBias.Bullish, "MACD tr├¬n signal ┬╖ momentum t─âng");
        if (macd < signal && hist < prev.hist)
            return new(CriterionType.Macd, 74, PatternBias.Bearish, "MACD d╞░ß╗¢i signal ┬╖ momentum giß║úm");

        var bias = macd > signal ? PatternBias.Bullish : macd < signal ? PatternBias.Bearish : PatternBias.Neutral;
        return new(CriterionType.Macd, 50, bias, $"MACD {macd:0.##} ┬╖ signal {signal:0.##}");
    }

    private CriterionScore ScoreVolume(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 20)
            return new(CriterionType.Volume, 0, PatternBias.Neutral, "Cß║ºn ΓëÑ20 phi├¬n");

        var ratio = signals.GetVolumeRatio(history);
        var change = history.Count >= 2
            ? (history[^1].Close - history[^2].Close) / history[^2].Close * 100m
            : 0m;

        if (ratio >= 1.8m && change > 1m)
            return new(CriterionType.Volume, 88, PatternBias.Bullish, $"Volume {ratio:0.#}├ù ┬╖ gi├í t─âng mß║ính");
        if (ratio >= 1.8m && change < -1m)
            return new(CriterionType.Volume, 85, PatternBias.Bearish, $"Volume {ratio:0.#}├ù ┬╖ b├ín mß║ính");
        if (ratio >= 1.3m && change > 0)
            return new(CriterionType.Volume, 72, PatternBias.Bullish, $"Volume mß╗ƒ rß╗Öng {ratio:0.#}├ù");
        if (ratio < 0.7m)
            return new(CriterionType.Volume, 55, PatternBias.Neutral, $"Volume thß║Ñp {ratio:0.#}├ù");

        var bias = change > 0.3m ? PatternBias.Bullish : change < -0.3m ? PatternBias.Bearish : PatternBias.Neutral;
        return new(CriterionType.Volume, 60, bias, $"Volume ratio {ratio:0.#}├ù");
    }

    private static CriterionScore ScoreVwap(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 20;
        if (history.Count < period)
            return new(CriterionType.Vwap, 0, PatternBias.Neutral, $"Cß║ºn ΓëÑ{period} phi├¬n");

        var slice = history.TakeLast(period).ToList();
        decimal sumPv = 0, sumV = 0;
        foreach (var b in slice)
        {
            var typical = (b.High + b.Low + b.Close) / 3m;
            sumPv += typical * b.Volume;
            sumV += b.Volume;
        }

        if (sumV <= 0)
            return new(CriterionType.Vwap, 40, PatternBias.Neutral, "Kh├┤ng ─æß╗º volume");

        var vwap = sumPv / sumV;
        var close = history[^1].Close;
        var dist = vwap > 0 ? (close - vwap) / vwap * 100m : 0m;

        if (dist > 1.5m)
            return new(CriterionType.Vwap, 82, PatternBias.Bullish, $"Gi├í tr├¬n VWAP {dist:0.#}% ΓÇö d├▓ng tiß╗ün mua");
        if (dist < -1.5m)
            return new(CriterionType.Vwap, 80, PatternBias.Bearish, $"Gi├í d╞░ß╗¢i VWAP {Math.Abs(dist):0.#}% ΓÇö ├íp lß╗▒c b├ín");
        if (dist > 0)
            return new(CriterionType.Vwap, 65, PatternBias.Bullish, "Gi├í nhß║╣ tr├¬n VWAP 20 phi├¬n");
        if (dist < 0)
            return new(CriterionType.Vwap, 63, PatternBias.Bearish, "Gi├í nhß║╣ d╞░ß╗¢i VWAP 20 phi├¬n");

        return new(CriterionType.Vwap, 50, PatternBias.Neutral, "Gi├í quanh VWAP");
    }

    private static CriterionScore ScoreBollinger(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 20;
        if (history.Count < period + 2)
            return new(CriterionType.BollingerBands, 0, PatternBias.Neutral, "Cß║ºn ΓëÑ22 phi├¬n");

        var (upper, mid, lower, percentB, bandwidth) = IndicatorMath.Bollinger(history, period);
        var close = history[^1].Close;
        var rising = close > history[^2].Close;

        if (percentB <= 0.15m && rising)
            return new(CriterionType.BollingerBands, 85, PatternBias.Bullish, $"%B {percentB:P0} ΓÇö chß║ím dß║úi d╞░ß╗¢i, hß╗ôi");
        if (percentB >= 0.85m && !rising)
            return new(CriterionType.BollingerBands, 82, PatternBias.Bearish, $"%B {percentB:P0} ΓÇö chß║ím dß║úi tr├¬n, yß║┐u");
        if (percentB > 0.55m && rising && bandwidth < 12m)
            return new(CriterionType.BollingerBands, 74, PatternBias.Bullish, "Squeeze breakout l├¬n");

        var bias = percentB > 0.55m ? PatternBias.Bullish : percentB < 0.45m ? PatternBias.Bearish : PatternBias.Neutral;
        var score = (int)Math.Clamp(50 + (percentB - 0.5m) * 40m, 25, 75);
        return new(CriterionType.BollingerBands, score, bias, $"B─âng {bandwidth:0.#}% ┬╖ %B {percentB:P0}");
    }

    private static CriterionScore ScoreAtr(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 14;
        if (history.Count < period + 5)
            return new(CriterionType.Atr, 0, PatternBias.Neutral, "Cß║ºn ΓëÑ19 phi├¬n");

        var atr = IndicatorMath.Atr(history, period);
        var prevAtr = IndicatorMath.Atr(history.Take(history.Count - 1).ToList(), period);
        var close = history[^1].Close;
        var atrPct = close > 0 ? atr / close * 100m : 0m;
        var expanding = atr > prevAtr * 1.05m;
        var change = history.Count >= 2
            ? (history[^1].Close - history[^2].Close) / history[^2].Close * 100m
            : 0m;

        if (expanding && change > 1m)
            return new(CriterionType.Atr, 80, PatternBias.Bullish, $"ATR mß╗ƒ rß╗Öng {atrPct:0.#}% ┬╖ breakout t─âng");
        if (expanding && change < -1m)
            return new(CriterionType.Atr, 78, PatternBias.Bearish, $"ATR mß╗ƒ rß╗Öng {atrPct:0.#}% ┬╖ breakout giß║úm");
        if (atrPct < 2.5m)
            return new(CriterionType.Atr, 60, PatternBias.Neutral, $"ATR thß║Ñp {atrPct:0.#}% ΓÇö t├¡ch l┼⌐y");

        return new(CriterionType.Atr, 50, PatternBias.Neutral, $"ATR {atrPct:0.#}% gi├í");
    }

    private static CriterionScore ScoreIchimoku(IReadOnlyList<OhlcvBar> history)
    {
        const int kijunPeriod = 26;
        if (history.Count < kijunPeriod + 5)
            return new(CriterionType.Ichimoku, 0, PatternBias.Neutral, $"Cß║ºn ΓëÑ{kijunPeriod + 5} phi├¬n");

        decimal Mid(int period) =>
            (history.TakeLast(period).Max(b => b.High) + history.TakeLast(period).Min(b => b.Low)) / 2m;

        var tenkan = Mid(9);
        var kijun = Mid(26);
        var close = history[^1].Close;
        var prev = history.Take(history.Count - 1).ToList();
        decimal PrevMid(IReadOnlyList<OhlcvBar> bars, int period) =>
            (bars.TakeLast(period).Max(b => b.High) + bars.TakeLast(period).Min(b => b.Low)) / 2m;

        var prevTenkan = PrevMid(prev, 9);
        var prevKijun = PrevMid(prev, 26);
        var tkCrossUp = prevTenkan <= prevKijun && tenkan > kijun;
        var tkCrossDown = prevTenkan >= prevKijun && tenkan < kijun;

        if (close > tenkan && close > kijun && tkCrossUp)
            return new(CriterionType.Ichimoku, 90, PatternBias.Bullish, "Gi├í tr├¬n cloud ┬╖ TK cross t─âng");
        if (close > tenkan && close > kijun)
            return new(CriterionType.Ichimoku, 78, PatternBias.Bullish, "Gi├í tr├¬n Tenkan/Kijun");
        if (close < tenkan && close < kijun && tkCrossDown)
            return new(CriterionType.Ichimoku, 88, PatternBias.Bearish, "Gi├í d╞░ß╗¢i cloud ┬╖ TK cross giß║úm");
        if (close < tenkan && close < kijun)
            return new(CriterionType.Ichimoku, 75, PatternBias.Bearish, "Gi├í d╞░ß╗¢i Tenkan/Kijun");

        return new(CriterionType.Ichimoku, 45, PatternBias.Neutral, "Gi├í trong v├╣ng cloud");
    }

    private static CriterionScore ScoreStochastic(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 14;
        if (history.Count < period + 3)
            return new(CriterionType.Stochastic, 0, PatternBias.Neutral, "Cß║ºn ΓëÑ17 phi├¬n");

        var (k, d) = IndicatorMath.Stochastic(history, period, 3);
        var prev = IndicatorMath.Stochastic(history.Take(history.Count - 1).ToList(), period, 3);
        var crossUp = prev.k <= prev.d && k > d;
        var crossDown = prev.k >= prev.d && k < d;

        if (k < 25 && crossUp)
            return new(CriterionType.Stochastic, 86, PatternBias.Bullish, $"%K {k:0} qu├í b├ín ┬╖ cß║»t l├¬n %D");
        if (k > 75 && crossDown)
            return new(CriterionType.Stochastic, 84, PatternBias.Bearish, $"%K {k:0} qu├í mua ┬╖ cß║»t xuß╗æng %D");
        if (k > d && k > 50)
            return new(CriterionType.Stochastic, 70, PatternBias.Bullish, $"%K {k:0} > %D {d:0} ΓÇö momentum t─âng");
        if (k < d && k < 50)
            return new(CriterionType.Stochastic, 68, PatternBias.Bearish, $"%K {k:0} < %D {d:0} ΓÇö momentum giß║úm");

        return new(CriterionType.Stochastic, 50, PatternBias.Neutral, $"%K {k:0} ┬╖ %D {d:0}");
    }

    private static CriterionScore ScoreAdx(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 14;
        if (history.Count < period * 2 + 2)
            return new(CriterionType.Adx, 0, PatternBias.Neutral, "Cß║ºn ΓëÑ30 phi├¬n");

        var (adx, plusDi, minusDi) = IndicatorMath.Adx(history, period);

        if (adx >= 25 && plusDi > minusDi + 3)
            return new(CriterionType.Adx, 88, PatternBias.Bullish, $"ADX {adx:0} ┬╖ +DI > -DI ΓÇö xu h╞░ß╗¢ng t─âng mß║ính");
        if (adx >= 25 && minusDi > plusDi + 3)
            return new(CriterionType.Adx, 86, PatternBias.Bearish, $"ADX {adx:0} ┬╖ -DI > +DI ΓÇö xu h╞░ß╗¢ng giß║úm mß║ính");
        if (adx < 20)
            return new(CriterionType.Adx, 45, PatternBias.Neutral, $"ADX {adx:0} ΓÇö sideway, kh├┤ng c├│ trend");

        var bias = plusDi > minusDi ? PatternBias.Bullish : minusDi > plusDi ? PatternBias.Bearish : PatternBias.Neutral;
        return new(CriterionType.Adx, 60, bias, $"ADX {adx:0} ┬╖ +DI {plusDi:0} / -DI {minusDi:0}");
    }
}

/// <summary>
/// Nguß╗ôn duy nhß║Ñt cho c├┤ng thß╗⌐c chß╗ë sß╗æ. Mß╗Öt m├ú + mß╗Öt khung thß╗¥i gian = mß╗Öt gi├í trß╗ï:
/// mß╗ìi service phß║úi gß╗ìi v├áo ─æ├óy thay v├¼ tß╗▒ c├ái lß║íi ATR / RSI / KL trung b├¼nh.
/// </summary>
public static class IndicatorMath
{
    /// <summary>True Range tß║íi mß╗Öt phi├¬n (cß║ºn phi├¬n liß╗ün tr╞░ß╗¢c).</summary>
    public static decimal TrueRange(IReadOnlyList<OhlcvBar> history, int index)
    {
        var bar = history[index];
        var prevClose = history[index - 1].Close;
        return Math.Max(
            bar.High - bar.Low,
            Math.Max(Math.Abs(bar.High - prevClose), Math.Abs(bar.Low - prevClose)));
    }

    /// <summary>
    /// ATR = trung b├¼nh ─æ╞ín giß║ún cß╗ºa True Range tr├¬n <paramref name="period"/> phi├¬n
    /// kß║┐t th├║c tß║íi <paramref name="index"/>. Thiß║┐u dß╗» liß╗çu th├¼ thu hß║╣p cß╗¡a sß╗ò
    /// (kh├┤ng trß║ú 0 giß║ú) ΓÇö 0 chß╗ë khi kh├┤ng ─æß╗º 2 phi├¬n ─æß╗â c├│ True Range.
    /// </summary>
    public static decimal AtrAt(IReadOnlyList<OhlcvBar> history, int index, int period)
    {
        if (history.Count < 2 || index < 1)
            return 0;

        var start = Math.Max(1, index - period + 1);
        var sum = 0m;
        var count = 0;
        for (var i = start; i <= index; i++)
        {
            sum += TrueRange(history, i);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    /// <summary>SMA gi├í ─æ├│ng cß╗¡a tr├¬n <paramref name="period"/> phi├¬n gß║ºn nhß║Ñt.</summary>
    public static decimal Sma(IReadOnlyList<OhlcvBar> history, int period) =>
        SmaAt(history, history.Count - 1, period);

    /// <summary>SMA gi├í ─æ├│ng cß╗¡a tr├¬n <paramref name="period"/> phi├¬n kß║┐t th├║c tß║íi <paramref name="index"/>.</summary>
    public static decimal SmaAt(IReadOnlyList<OhlcvBar> history, int index, int period)
    {
        if (history.Count == 0 || index < 0)
            return 0;

        var end = Math.Min(index, history.Count - 1);
        return AverageClose(history, Math.Max(0, end - period + 1), end);
    }

    /// <summary>Trung b├¼nh gi├í ─æ├│ng cß╗¡a trong khoß║úng chß╗ë sß╗æ [start, end].</summary>
    public static decimal AverageClose(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end || start < 0 || end >= history.Count)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].Close;
        return sum / (end - start + 1);
    }

    /// <summary>EMA gi├í ─æ├│ng cß╗¡a ΓÇö mß╗ôi bß║▒ng SMA <paramref name="period"/> phi├¬n ─æß║ºu.</summary>
    public static decimal Ema(IReadOnlyList<OhlcvBar> history, int period) =>
        EmaAt(history, history.Count - 1, period);

    /// <summary>
    /// EMA gi├í ─æ├│ng cß╗¡a t├¡nh ─æß║┐n <paramref name="index"/> ΓÇö c├╣ng c├ích mß╗ôi vß╗¢i
    /// <see cref="Ema(IReadOnlyList{decimal}, int)"/>: SMA <paramref name="period"/> phi├¬n ─æß║ºu
    /// rß╗ôi chß║íy hß║┐t prefix, kh├┤ng mß╗ôi bß║▒ng mß╗Öt nß║┐n ─æ╞ín lß║╗.
    /// </summary>
    public static decimal EmaAt(IReadOnlyList<OhlcvBar> history, int index, int period)
    {
        if (history.Count == 0 || index < 0)
            return 0;

        var end = Math.Min(index, history.Count - 1);
        if (end + 1 < period)
            return AverageClose(history, 0, end);

        var k = 2m / (period + 1);
        var ema = AverageClose(history, 0, period - 1);
        for (var i = period; i <= end; i++)
            ema = history[i].Close * k + ema * (1 - k);
        return ema;
    }

    /// <summary>KL trung b├¼nh <paramref name="period"/> phi├¬n gß║ºn nhß║Ñt.</summary>
    public static decimal AverageVolume(IReadOnlyList<OhlcvBar> history, int period)
    {
        if (history.Count == 0)
            return 0;

        return AverageVolume(history, history.Count - Math.Min(period, history.Count), history.Count - 1);
    }

    /// <summary>KL trung b├¼nh trong khoß║úng chß╗ë sß╗æ [start, end] (bao gß╗ôm hai ─æß║ºu).</summary>
    public static decimal AverageVolume(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end || start < 0 || end >= history.Count)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].Volume;
        return sum / (end - start + 1);
    }

    /// <summary>
    /// Gi├í trß╗ï khß╗¢p trung b├¼nh (VND/phi├¬n) <paramref name="period"/> phi├¬n gß║ºn nhß║Ñt.
    /// Close l╞░u theo ─æ╞ín vß╗ï ngh├¼n VND (gi├í hiß╗ân thß╗ï ├ù 1000) ΓåÆ nh├ón 1000 ─æß╗â ra VND.
    /// D├╣ng ─æß╗â ─æo thanh khoß║ún c├┤ng bß║▒ng giß╗»a m├ú gi├í cao v├á gi├í thß║Ñp (sß╗æ CP mß╗Öt m├¼nh th├¼ thi├¬n vß╗ï CP rß║╗).
    /// </summary>
    public static decimal AverageTurnoverValue(IReadOnlyList<OhlcvBar> history, int period)
    {
        if (history.Count == 0 || period <= 0)
            return 0;

        var start = history.Count - Math.Min(period, history.Count);
        var sum = 0m;
        for (var i = start; i < history.Count; i++)
            sum += history[i].Close * 1000m * history[i].Volume;
        return sum / (history.Count - start);
    }

    /// <summary>
    /// M├ú ─æß╗º thanh khoß║ún nß║┐u TB khß╗æi l╞░ß╗úng (cp) ΓëÑ <paramref name="minAvgVolume"/>
    /// <b>HOß║╢C</b> TB gi├í trß╗ï khß╗¢p (VND) ΓëÑ <paramref name="minAvgValueVnd"/>.
    /// ─Éiß╗üu kiß╗çn OR chß╗ë th├¬m m├ú gi├í cao thanh khoß║ún tß╗æt v├áo tß║¡p ─æß╗º ─æiß╗üu kiß╗çn, kh├┤ng loß║íi bß╗¢t m├ú n├áo
    /// ─æang ─æß║ít theo khß╗æi l╞░ß╗úng. <paramref name="minAvgValueVnd"/> Γëñ 0 ΓåÆ tß║»t ti├¬u ch├¡ gi├í trß╗ï (h├ánh vi c┼⌐).
    /// </summary>
    public static bool IsLiquid(
        IReadOnlyList<OhlcvBar> history,
        int period,
        decimal minAvgVolume,
        decimal minAvgValueVnd)
    {
        if (AverageVolume(history, period) >= minAvgVolume)
            return true;
        if (minAvgValueVnd > 0 && AverageTurnoverValue(history, period) >= minAvgValueVnd)
            return true;
        return false;
    }

    public static decimal Ema(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count == 0) return 0;
        if (values.Count < period) return values.Average();

        var k = 2m / (period + 1);
        var ema = values.Take(period).Average();
        for (var i = period; i < values.Count; i++)
            ema = values[i] * k + ema * (1 - k);
        return ema;
    }

    /// <summary>
    /// RSI trung b├¼nh ─æ╞ín giß║ún tr├¬n <paramref name="period"/> phi├¬n cuß╗æi.
    /// Kh├┤ng l├ám tr├▓n ΓÇö chß╗ù hiß╗ân thß╗ï tß╗▒ ─æß╗ïnh dß║íng, chß╗ù so ng╞░ß╗íng cß║ºn ─æß╗º ─æß╗Ö ch├¡nh x├íc.
    /// </summary>
    public static decimal Rsi(IReadOnlyList<OhlcvBar> history, int period)
    {
        if (history.Count < period + 1) return 50;

        decimal gain = 0, loss = 0;
        for (var i = history.Count - period; i < history.Count; i++)
        {
            var change = history[i].Close - history[i - 1].Close;
            if (change > 0) gain += change;
            else loss -= change;
        }

        if (loss == 0) return 100;
        var rs = gain / loss;
        return 100m - 100m / (1m + rs);
    }

    private const int MacdFastPeriod = 12;
    private const int MacdSlowPeriod = 26;
    private const int MacdSignalPeriod = 9;

    /// <summary>
    /// MACD = EMA12 ΓêÆ EMA26, signal = EMA9 cß╗ºa chuß╗ùi MACD.
    /// </summary>
    /// <remarks>
    /// Cuß╗Ön EMA nhanh/chß║¡m t─âng dß║ºn trong <b>mß╗Öt</b> l╞░ß╗út duyß╗çt. Bß║ún c┼⌐ cß║»t
    /// <c>Take(i).ToList()</c> rß╗ôi chß║íy lß║íi cß║ú hai EMA tß╗½ ─æß║ºu cho tß╗½ng phi├¬n ΓÇö O(n┬▓) thß╗¥i gian
    /// lß║½n cß║Ñp ph├ít. Chuß╗ùi ph├⌐p t├¡nh giß╗» nguy├¬n (mß╗ôi = SMA <c>period</c> phi├¬n ─æß║ºu, c├╣ng thß╗⌐ tß╗▒
    /// cß║¡p nhß║¡t) n├¬n kß║┐t quß║ú tr├╣ng khß╗¢p bß║ún c┼⌐ tß╗½ng chß╗» sß╗æ ΓÇö xem <c>IndicatorMathMacdTests</c>.
    /// </remarks>
    public static (decimal macd, decimal signal, decimal hist) Macd(IReadOnlyList<OhlcvBar> history)
    {
        var n = history.Count;
        if (n < MacdSlowPeriod) return (0, 0, 0);

        var kFast = 2m / (MacdFastPeriod + 1);
        var kSlow = 2m / (MacdSlowPeriod + 1);

        // ─É╞░a EMA nhanh tß╗½ mß╗æc mß╗ôi cß╗ºa n├│ (12 phi├¬n) tß╗¢i c├╣ng mß╗æc vß╗¢i EMA chß║¡m (26 phi├¬n).
        var emaFast = AverageClose(history, 0, MacdFastPeriod - 1);
        for (var i = MacdFastPeriod; i < MacdSlowPeriod; i++)
            emaFast = history[i].Close * kFast + emaFast * (1 - kFast);

        var emaSlow = AverageClose(history, 0, MacdSlowPeriod - 1);

        var macdSeries = new List<decimal>(n - MacdSlowPeriod + 1) { emaFast - emaSlow };
        for (var i = MacdSlowPeriod; i < n; i++)
        {
            var close = history[i].Close;
            emaFast = close * kFast + emaFast * (1 - kFast);
            emaSlow = close * kSlow + emaSlow * (1 - kSlow);
            macdSeries.Add(emaFast - emaSlow);
        }

        var macd = macdSeries[^1];
        var signal = macdSeries.Count >= MacdSignalPeriod ? Ema(macdSeries, MacdSignalPeriod) : macd;
        return (macd, signal, macd - signal);
    }

    public static (decimal upper, decimal mid, decimal lower, decimal percentB, decimal bandwidth) Bollinger(
        IReadOnlyList<OhlcvBar> history,
        int period)
    {
        var slice = history.TakeLast(period).ToList();
        var closes = slice.Select(b => b.Close).ToList();
        var mean = closes.Average();
        var variance = closes.Sum(c => (double)(c - mean) * (double)(c - mean)) / period;
        var std = (decimal)Math.Sqrt(variance);
        var upper = mean + 2 * std;
        var lower = mean - 2 * std;
        var close = history[^1].Close;
        var percentB = upper > lower ? (close - lower) / (upper - lower) : 0.5m;
        var bandwidth = mean > 0 ? (upper - lower) / mean * 100m : 0m;
        return (upper, mean, lower, percentB, bandwidth);
    }

    /// <summary>ATR tr├¬n <paramref name="period"/> phi├¬n cuß╗æi.</summary>
    public static decimal Atr(IReadOnlyList<OhlcvBar> history, int period) =>
        AtrAt(history, history.Count - 1, period);

    public static (decimal k, decimal d) Stochastic(IReadOnlyList<OhlcvBar> history, int period, int smooth)
    {
        var slice = history.TakeLast(period).ToList();
        var high = slice.Max(b => b.High);
        var low = slice.Min(b => b.Low);
        var close = history[^1].Close;
        var kRaw = high > low ? (close - low) / (high - low) * 100m : 50m;

        var kValues = new List<decimal>();
        for (var i = period; i <= history.Count; i++)
        {
            var s = history.Skip(i - period).Take(period).ToList();
            var h = s.Max(b => b.High);
            var l = s.Min(b => b.Low);
            var c = history[i - 1].Close;
            kValues.Add(h > l ? (c - l) / (h - l) * 100m : 50m);
        }

        var k = kValues.TakeLast(smooth).DefaultIfEmpty(kRaw).Average();
        var d = kValues.TakeLast(smooth * 2).DefaultIfEmpty(k).Average();
        return (Math.Round(k, 1), Math.Round(d, 1));
    }

    public static (decimal adx, decimal plusDi, decimal minusDi) Adx(IReadOnlyList<OhlcvBar> history, int period)
    {
        if (history.Count < period + 2) return (0, 0, 0);

        var plusDm = new List<decimal>();
        var minusDm = new List<decimal>();
        var tr = new List<decimal>();

        for (var i = 1; i < history.Count; i++)
        {
            var up = history[i].High - history[i - 1].High;
            var down = history[i - 1].Low - history[i].Low;
            plusDm.Add(up > down && up > 0 ? up : 0);
            minusDm.Add(down > up && down > 0 ? down : 0);
            var h = history[i].High;
            var l = history[i].Low;
            var pc = history[i - 1].Close;
            tr.Add(Math.Max(h - l, Math.Max(Math.Abs(h - pc), Math.Abs(l - pc))));
        }

        var atrVal = tr.TakeLast(period).Average();
        if (atrVal == 0) return (0, 0, 0);

        var pdi = plusDm.TakeLast(period).Average() / atrVal * 100m;
        var mdi = minusDm.TakeLast(period).Average() / atrVal * 100m;
        var dx = pdi + mdi > 0 ? Math.Abs(pdi - mdi) / (pdi + mdi) * 100m : 0m;

        return (Math.Round(dx, 1), Math.Round(pdi, 1), Math.Round(mdi, 1));
    }
}

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
            return new(CriterionType.Rsi, 0, PatternBias.Neutral, $"Cần ≥{period + 2} phiên");

        var rsi = IndicatorMath.Rsi(history, period);
        var prevRsi = IndicatorMath.Rsi(history.Take(history.Count - 1).ToList(), period);
        var rising = rsi > prevRsi;

        if (rsi < 30 && rising)
            return new(CriterionType.Rsi, 88, PatternBias.Bullish, $"RSI {rsi:0} — quá bán, hồi phục");
        if (rsi > 70 && !rising)
            return new(CriterionType.Rsi, 85, PatternBias.Bearish, $"RSI {rsi:0} — quá mua, yếu dần");
        if (rsi is >= 45 and <= 60 && rising)
            return new(CriterionType.Rsi, 78, PatternBias.Bullish, $"RSI {rsi:0} — momentum tăng");
        if (rsi is >= 40 and <= 55 && !rising)
            return new(CriterionType.Rsi, 72, PatternBias.Bearish, $"RSI {rsi:0} — momentum giảm");

        var bias = rsi > 55 ? PatternBias.Bullish : rsi < 45 ? PatternBias.Bearish : PatternBias.Neutral;
        var score = (int)Math.Clamp(50 + (rsi - 50) * 0.8m + (rising ? 5 : -5), 25, 75);
        return new(CriterionType.Rsi, score, bias, $"RSI {rsi:0}");
    }

    private static CriterionScore ScoreMovingAverage(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 50)
            return new(CriterionType.MovingAverage, 0, PatternBias.Neutral, "Cần ≥50 phiên (EMA/SMA)");

        var closes = history.Select(b => b.Close).ToList();
        var ema20 = IndicatorMath.Ema(closes, 20);
        var ema50 = IndicatorMath.Ema(closes, 50);
        var sma200 = IndicatorMath.Sma(history, 200);
        var close = history[^1].Close;
        var prevEma20 = IndicatorMath.Ema(closes.Take(closes.Count - 1).ToList(), 20);
        var goldenCross = prevEma20 <= ema50 && ema20 > ema50;

        if (close > ema20 && ema20 > ema50 && close > sma200)
            return new(CriterionType.MovingAverage, 92, PatternBias.Bullish,
                goldenCross ? "EMA20 cắt lên EMA50 · giá trên MA" : "Giá trên EMA20/50 · xu hướng tăng");
        if (close < ema20 && ema20 < ema50 && close < sma200)
            return new(CriterionType.MovingAverage, 90, PatternBias.Bearish, "Giá dưới EMA20/50 · xu hướng giảm");
        if (close > ema20 && ema20 > ema50)
            return new(CriterionType.MovingAverage, 75, PatternBias.Bullish, "EMA stack tăng ngắn hạn");
        if (close < ema20 && ema20 < ema50)
            return new(CriterionType.MovingAverage, 73, PatternBias.Bearish, "EMA stack giảm ngắn hạn");

        return new(CriterionType.MovingAverage, 45, PatternBias.Neutral, "MA chưa xác nhận xu hướng");
    }

    private static CriterionScore ScoreMacd(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 35)
            return new(CriterionType.Macd, 0, PatternBias.Neutral, "Cần ≥35 phiên");

        var (macd, signal, hist) = IndicatorMath.Macd(history);
        var prev = IndicatorMath.Macd(history.Take(history.Count - 1).ToList());
        var crossUp = prev.macd <= prev.signal && macd > signal;
        var crossDown = prev.macd >= prev.signal && macd < signal;

        if (crossUp && hist > 0)
            return new(CriterionType.Macd, 90, PatternBias.Bullish, "MACD cắt lên signal · histogram dương");
        if (crossDown && hist < 0)
            return new(CriterionType.Macd, 88, PatternBias.Bearish, "MACD cắt xuống signal · histogram âm");
        if (macd > signal && hist > prev.hist)
            return new(CriterionType.Macd, 76, PatternBias.Bullish, "MACD trên signal · momentum tăng");
        if (macd < signal && hist < prev.hist)
            return new(CriterionType.Macd, 74, PatternBias.Bearish, "MACD dưới signal · momentum giảm");

        var bias = macd > signal ? PatternBias.Bullish : macd < signal ? PatternBias.Bearish : PatternBias.Neutral;
        return new(CriterionType.Macd, 50, bias, $"MACD {macd:0.##} · signal {signal:0.##}");
    }

    private CriterionScore ScoreVolume(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 20)
            return new(CriterionType.Volume, 0, PatternBias.Neutral, "Cần ≥20 phiên");

        var ratio = signals.GetVolumeRatio(history);
        var change = history.Count >= 2
            ? (history[^1].Close - history[^2].Close) / history[^2].Close * 100m
            : 0m;

        if (ratio >= 1.8m && change > 1m)
            return new(CriterionType.Volume, 88, PatternBias.Bullish, $"Volume {ratio:0.#}× · giá tăng mạnh");
        if (ratio >= 1.8m && change < -1m)
            return new(CriterionType.Volume, 85, PatternBias.Bearish, $"Volume {ratio:0.#}× · bán mạnh");
        if (ratio >= 1.3m && change > 0)
            return new(CriterionType.Volume, 72, PatternBias.Bullish, $"Volume mở rộng {ratio:0.#}×");
        if (ratio < 0.7m)
            return new(CriterionType.Volume, 55, PatternBias.Neutral, $"Volume thấp {ratio:0.#}×");

        var bias = change > 0.3m ? PatternBias.Bullish : change < -0.3m ? PatternBias.Bearish : PatternBias.Neutral;
        return new(CriterionType.Volume, 60, bias, $"Volume ratio {ratio:0.#}×");
    }

    private static CriterionScore ScoreVwap(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 20;
        if (history.Count < period)
            return new(CriterionType.Vwap, 0, PatternBias.Neutral, $"Cần ≥{period} phiên");

        var slice = history.TakeLast(period).ToList();
        decimal sumPv = 0, sumV = 0;
        foreach (var b in slice)
        {
            var typical = (b.High + b.Low + b.Close) / 3m;
            sumPv += typical * b.Volume;
            sumV += b.Volume;
        }

        if (sumV <= 0)
            return new(CriterionType.Vwap, 40, PatternBias.Neutral, "Không đủ volume");

        var vwap = sumPv / sumV;
        var close = history[^1].Close;
        var dist = vwap > 0 ? (close - vwap) / vwap * 100m : 0m;

        if (dist > 1.5m)
            return new(CriterionType.Vwap, 82, PatternBias.Bullish, $"Giá trên VWAP {dist:0.#}% — dòng tiền mua");
        if (dist < -1.5m)
            return new(CriterionType.Vwap, 80, PatternBias.Bearish, $"Giá dưới VWAP {Math.Abs(dist):0.#}% — áp lực bán");
        if (dist > 0)
            return new(CriterionType.Vwap, 65, PatternBias.Bullish, "Giá nhẹ trên VWAP 20 phiên");
        if (dist < 0)
            return new(CriterionType.Vwap, 63, PatternBias.Bearish, "Giá nhẹ dưới VWAP 20 phiên");

        return new(CriterionType.Vwap, 50, PatternBias.Neutral, "Giá quanh VWAP");
    }

    private static CriterionScore ScoreBollinger(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 20;
        if (history.Count < period + 2)
            return new(CriterionType.BollingerBands, 0, PatternBias.Neutral, "Cần ≥22 phiên");

        var (upper, mid, lower, percentB, bandwidth) = IndicatorMath.Bollinger(history, period);
        var close = history[^1].Close;
        var rising = close > history[^2].Close;

        if (percentB <= 0.15m && rising)
            return new(CriterionType.BollingerBands, 85, PatternBias.Bullish, $"%B {percentB:P0} — chạm dải dưới, hồi");
        if (percentB >= 0.85m && !rising)
            return new(CriterionType.BollingerBands, 82, PatternBias.Bearish, $"%B {percentB:P0} — chạm dải trên, yếu");
        if (percentB > 0.55m && rising && bandwidth < 12m)
            return new(CriterionType.BollingerBands, 74, PatternBias.Bullish, "Squeeze breakout lên");

        var bias = percentB > 0.55m ? PatternBias.Bullish : percentB < 0.45m ? PatternBias.Bearish : PatternBias.Neutral;
        var score = (int)Math.Clamp(50 + (percentB - 0.5m) * 40m, 25, 75);
        return new(CriterionType.BollingerBands, score, bias, $"Băng {bandwidth:0.#}% · %B {percentB:P0}");
    }

    private static CriterionScore ScoreAtr(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 14;
        if (history.Count < period + 5)
            return new(CriterionType.Atr, 0, PatternBias.Neutral, "Cần ≥19 phiên");

        var atr = IndicatorMath.Atr(history, period);
        var prevAtr = IndicatorMath.Atr(history.Take(history.Count - 1).ToList(), period);
        var close = history[^1].Close;
        var atrPct = close > 0 ? atr / close * 100m : 0m;
        var expanding = atr > prevAtr * 1.05m;
        var change = history.Count >= 2
            ? (history[^1].Close - history[^2].Close) / history[^2].Close * 100m
            : 0m;

        if (expanding && change > 1m)
            return new(CriterionType.Atr, 80, PatternBias.Bullish, $"ATR mở rộng {atrPct:0.#}% · breakout tăng");
        if (expanding && change < -1m)
            return new(CriterionType.Atr, 78, PatternBias.Bearish, $"ATR mở rộng {atrPct:0.#}% · breakout giảm");
        if (atrPct < 2.5m)
            return new(CriterionType.Atr, 60, PatternBias.Neutral, $"ATR thấp {atrPct:0.#}% — tích lũy");

        return new(CriterionType.Atr, 50, PatternBias.Neutral, $"ATR {atrPct:0.#}% giá");
    }

    private static CriterionScore ScoreIchimoku(IReadOnlyList<OhlcvBar> history)
    {
        const int kijunPeriod = 26;
        if (history.Count < kijunPeriod + 5)
            return new(CriterionType.Ichimoku, 0, PatternBias.Neutral, $"Cần ≥{kijunPeriod + 5} phiên");

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
            return new(CriterionType.Ichimoku, 90, PatternBias.Bullish, "Giá trên cloud · TK cross tăng");
        if (close > tenkan && close > kijun)
            return new(CriterionType.Ichimoku, 78, PatternBias.Bullish, "Giá trên Tenkan/Kijun");
        if (close < tenkan && close < kijun && tkCrossDown)
            return new(CriterionType.Ichimoku, 88, PatternBias.Bearish, "Giá dưới cloud · TK cross giảm");
        if (close < tenkan && close < kijun)
            return new(CriterionType.Ichimoku, 75, PatternBias.Bearish, "Giá dưới Tenkan/Kijun");

        return new(CriterionType.Ichimoku, 45, PatternBias.Neutral, "Giá trong vùng cloud");
    }

    private static CriterionScore ScoreStochastic(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 14;
        if (history.Count < period + 3)
            return new(CriterionType.Stochastic, 0, PatternBias.Neutral, "Cần ≥17 phiên");

        var (k, d) = IndicatorMath.Stochastic(history, period, 3);
        var prev = IndicatorMath.Stochastic(history.Take(history.Count - 1).ToList(), period, 3);
        var crossUp = prev.k <= prev.d && k > d;
        var crossDown = prev.k >= prev.d && k < d;

        if (k < 25 && crossUp)
            return new(CriterionType.Stochastic, 86, PatternBias.Bullish, $"%K {k:0} quá bán · cắt lên %D");
        if (k > 75 && crossDown)
            return new(CriterionType.Stochastic, 84, PatternBias.Bearish, $"%K {k:0} quá mua · cắt xuống %D");
        if (k > d && k > 50)
            return new(CriterionType.Stochastic, 70, PatternBias.Bullish, $"%K {k:0} > %D {d:0} — momentum tăng");
        if (k < d && k < 50)
            return new(CriterionType.Stochastic, 68, PatternBias.Bearish, $"%K {k:0} < %D {d:0} — momentum giảm");

        return new(CriterionType.Stochastic, 50, PatternBias.Neutral, $"%K {k:0} · %D {d:0}");
    }

    private static CriterionScore ScoreAdx(IReadOnlyList<OhlcvBar> history)
    {
        const int period = 14;
        if (history.Count < period * 2 + 2)
            return new(CriterionType.Adx, 0, PatternBias.Neutral, "Cần ≥30 phiên");

        var (adx, plusDi, minusDi) = IndicatorMath.Adx(history, period);

        if (adx >= 25 && plusDi > minusDi + 3)
            return new(CriterionType.Adx, 88, PatternBias.Bullish, $"ADX {adx:0} · +DI > -DI — xu hướng tăng mạnh");
        if (adx >= 25 && minusDi > plusDi + 3)
            return new(CriterionType.Adx, 86, PatternBias.Bearish, $"ADX {adx:0} · -DI > +DI — xu hướng giảm mạnh");
        if (adx < 20)
            return new(CriterionType.Adx, 45, PatternBias.Neutral, $"ADX {adx:0} — sideway, không có trend");

        var bias = plusDi > minusDi ? PatternBias.Bullish : minusDi > plusDi ? PatternBias.Bearish : PatternBias.Neutral;
        return new(CriterionType.Adx, 60, bias, $"ADX {adx:0} · +DI {plusDi:0} / -DI {minusDi:0}");
    }
}

/// <summary>
/// Nguồn duy nhất cho công thức chỉ số. Một mã + một khung thời gian = một giá trị:
/// mọi service phải gọi vào đây thay vì tự cài lại ATR / RSI / KL trung bình.
/// </summary>
public static class IndicatorMath
{
    /// <summary>True Range tại một phiên (cần phiên liền trước).</summary>
    public static decimal TrueRange(IReadOnlyList<OhlcvBar> history, int index)
    {
        var bar = history[index];
        var prevClose = history[index - 1].Close;
        return Math.Max(
            bar.High - bar.Low,
            Math.Max(Math.Abs(bar.High - prevClose), Math.Abs(bar.Low - prevClose)));
    }

    /// <summary>
    /// ATR = trung bình đơn giản của True Range trên <paramref name="period"/> phiên
    /// kết thúc tại <paramref name="index"/>. Thiếu dữ liệu thì thu hẹp cửa sổ
    /// (không trả 0 giả) — 0 chỉ khi không đủ 2 phiên để có True Range.
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

    /// <summary>SMA giá đóng cửa trên <paramref name="period"/> phiên gần nhất.</summary>
    public static decimal Sma(IReadOnlyList<OhlcvBar> history, int period) =>
        SmaAt(history, history.Count - 1, period);

    /// <summary>SMA giá đóng cửa trên <paramref name="period"/> phiên kết thúc tại <paramref name="index"/>.</summary>
    public static decimal SmaAt(IReadOnlyList<OhlcvBar> history, int index, int period)
    {
        if (history.Count == 0 || index < 0)
            return 0;

        var end = Math.Min(index, history.Count - 1);
        return AverageClose(history, Math.Max(0, end - period + 1), end);
    }

    /// <summary>Trung bình giá đóng cửa trong khoảng chỉ số [start, end].</summary>
    public static decimal AverageClose(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end || start < 0 || end >= history.Count)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].Close;
        return sum / (end - start + 1);
    }

    /// <summary>EMA giá đóng cửa — mồi bằng SMA <paramref name="period"/> phiên đầu.</summary>
    public static decimal Ema(IReadOnlyList<OhlcvBar> history, int period) =>
        EmaAt(history, history.Count - 1, period);

    /// <summary>
    /// EMA giá đóng cửa tính đến <paramref name="index"/> — cùng cách mồi với
    /// <see cref="Ema(IReadOnlyList{decimal}, int)"/>: SMA <paramref name="period"/> phiên đầu
    /// rồi chạy hết prefix, không mồi bằng một nến đơn lẻ.
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

    /// <summary>KL trung bình <paramref name="period"/> phiên gần nhất.</summary>
    public static decimal AverageVolume(IReadOnlyList<OhlcvBar> history, int period)
    {
        if (history.Count == 0)
            return 0;

        return AverageVolume(history, history.Count - Math.Min(period, history.Count), history.Count - 1);
    }

    /// <summary>KL trung bình trong khoảng chỉ số [start, end] (bao gồm hai đầu).</summary>
    public static decimal AverageVolume(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end || start < 0 || end >= history.Count)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].Volume;
        return sum / (end - start + 1);
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
    /// RSI trung bình đơn giản trên <paramref name="period"/> phiên cuối.
    /// Không làm tròn — chỗ hiển thị tự định dạng, chỗ so ngưỡng cần đủ độ chính xác.
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
    /// MACD = EMA12 − EMA26, signal = EMA9 của chuỗi MACD.
    /// </summary>
    /// <remarks>
    /// Cuộn EMA nhanh/chậm tăng dần trong <b>một</b> lượt duyệt. Bản cũ cắt
    /// <c>Take(i).ToList()</c> rồi chạy lại cả hai EMA từ đầu cho từng phiên — O(n²) thời gian
    /// lẫn cấp phát. Chuỗi phép tính giữ nguyên (mồi = SMA <c>period</c> phiên đầu, cùng thứ tự
    /// cập nhật) nên kết quả trùng khớp bản cũ từng chữ số — xem <c>IndicatorMathMacdTests</c>.
    /// </remarks>
    public static (decimal macd, decimal signal, decimal hist) Macd(IReadOnlyList<OhlcvBar> history)
    {
        var n = history.Count;
        if (n < MacdSlowPeriod) return (0, 0, 0);

        var kFast = 2m / (MacdFastPeriod + 1);
        var kSlow = 2m / (MacdSlowPeriod + 1);

        // Đưa EMA nhanh từ mốc mồi của nó (12 phiên) tới cùng mốc với EMA chậm (26 phiên).
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

    /// <summary>ATR trên <paramref name="period"/> phiên cuối.</summary>
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

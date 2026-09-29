using StockRadar.Domain.Entities;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

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

    /// <summary>
    /// Giá trị khớp trung bình (VND/phiên) <paramref name="period"/> phiên gần nhất.
    /// Close lưu theo đơn vị nghìn VND (giá hiển thị × 1000) → nhân 1000 để ra VND.
    /// Dùng để đo thanh khoản công bằng giữa mã giá cao và giá thấp (số CP một mình thì thiên vị CP rẻ).
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
    /// Mã đủ thanh khoản nếu TB khối lượng (cp) ≥ <paramref name="minAvgVolume"/>
    /// <b>HOẶC</b> TB giá trị khớp (VND) ≥ <paramref name="minAvgValueVnd"/>.
    /// Điều kiện OR chỉ thêm mã giá cao thanh khoản tốt vào tập đủ điều kiện, không loại bớt mã nào
    /// đang đạt theo khối lượng. <paramref name="minAvgValueVnd"/> ≤ 0 → tắt tiêu chí giá trị (hành vi cũ).
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

    /// <summary>
    /// Stochastic Oscillator — đo vị trí giá đóng cửa so với biên độ N phiên.
    /// %K = SMA(`smooth` phiên) của (Close − LowN)/(HighN − LowN) × 100; %D = SMA thêm một nhịp của %K.
    /// </summary>
    public static (decimal k, decimal d) Stochastic(IReadOnlyList<OhlcvBar> history, int period = 14, int smooth = 3)
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

    /// <summary>
    /// ADX (Average Directional Index) — đo độ mạnh xu hướng theo Wilder smoothing.
    /// +DM/−DM/TR được làm mượt kiểu Wilder (prev × (period−1)/period + current/period),
    /// DX = |+DI − −DI| / (+DI + −DI) × 100, ADX tiếp tục làm mượt Wilder trên chuỗi DX.
    /// Trả về: ADX, +DI, −DI (giá trị tại phiên cuối).
    /// </summary>
    public static (decimal adx, decimal plusDi, decimal minusDi) Adx(IReadOnlyList<OhlcvBar> history, int period)
    {
        if (history.Count < period * 2) return (0, 0, 0);

        // Bước 1: gom +DM, −DM, TR từng phiên (bỏ phiên đầu — không có tham chiếu).
        var plusDm = new List<decimal>(history.Count - 1);
        var minusDm = new List<decimal>(history.Count - 1);
        var tr = new List<decimal>(history.Count - 1);
        for (var i = 1; i < history.Count; i++)
        {
            var up = history[i].High - history[i - 1].High;
            var down = history[i - 1].Low - history[i].Low;
            plusDm.Add(up > down && up > 0 ? up : 0);
            minusDm.Add(down > up && down > 0 ? down : 0);
            tr.Add(TrueRange(history, i));
        }

        // Bước 2: mồi Wilder = tổng `period` giá trị đầu, sau đó làm mượt kế tiếp theo công thức Wilder.
        var smoothPlusDm = plusDm.Take(period).Sum();
        var smoothMinusDm = minusDm.Take(period).Sum();
        var smoothTr = tr.Take(period).Sum();

        var dxSeries = new List<decimal>(tr.Count - period + 1);
        dxSeries.Add(DxFromSmoothed(smoothPlusDm, smoothMinusDm, smoothTr));
        for (var i = period; i < tr.Count; i++)
        {
            smoothPlusDm = smoothPlusDm - smoothPlusDm / period + plusDm[i];
            smoothMinusDm = smoothMinusDm - smoothMinusDm / period + minusDm[i];
            smoothTr = smoothTr - smoothTr / period + tr[i];
            dxSeries.Add(DxFromSmoothed(smoothPlusDm, smoothMinusDm, smoothTr));
        }

        // Bước 3: ADX = Wilder smoothing của chuỗi DX (mồi bằng SMA `period` DX đầu).
        if (dxSeries.Count < period) return (0, 0, 0);
        var adx = dxSeries.Take(period).Average();
        for (var i = period; i < dxSeries.Count; i++)
            adx = (adx * (period - 1) + dxSeries[i]) / period;

        var plusDi = smoothTr == 0 ? 0 : 100m * smoothPlusDm / smoothTr;
        var minusDi = smoothTr == 0 ? 0 : 100m * smoothMinusDm / smoothTr;
        return (Math.Round(adx, 1), Math.Round(plusDi, 1), Math.Round(minusDi, 1));
    }

    /// <summary>DX từ bộ ba giá trị Wilder-smoothed: |+DI − −DI| / (+DI + −DI) × 100.</summary>
    private static decimal DxFromSmoothed(decimal smoothPlusDm, decimal smoothMinusDm, decimal smoothTr)
    {
        if (smoothTr == 0) return 0;
        var plusDi = 100m * smoothPlusDm / smoothTr;
        var minusDi = 100m * smoothMinusDm / smoothTr;
        var tong = plusDi + minusDi;
        return tong == 0 ? 0 : Math.Abs(plusDi - minusDi) / tong * 100m;
    }

    private const int IchimokuTenkanPeriod = 9;
    private const int IchimokuKijunPeriod = 26;
    private const int IchimokuSenkouBPeriod = 52;

    /// <summary>
    /// Ichimoku Kinko Hyo — hệ thống đường mây Nhật Bản.
    /// TenkanSen = trung điểm (max High + min Low)/2 trên 9 phiên; KijunSen tương tự 26 phiên;
    /// SenkouSpanA = (Tenkan + Kijun)/2 chiếu 26 phiên; SenkouSpanB = trung điểm 52 phiên.
    /// Cần tối thiểu 52 phiên — thiếu dữ liệu trả về toàn 0 (không đoán).
    /// </summary>
    public static IchimokuResult Ichimoku(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < IchimokuSenkouBPeriod)
            return new IchimokuResult(0, 0, 0, 0, false);

        var tenkan = Midpoint(history, IchimokuTenkanPeriod);
        var kijun = Midpoint(history, IchimokuKijunPeriod);
        var senkouA = (tenkan + kijun) / 2m;
        var senkouB = Midpoint(history, IchimokuSenkouBPeriod);
        var mayTren = Math.Max(senkouA, senkouB);
        var giaTrenMay = history[^1].Close > mayTren;
        return new IchimokuResult(tenkan, kijun, senkouA, senkouB, giaTrenMay);
    }

    /// <summary>Trung điểm (High cao nhất + Low thấp nhất)/2 trên `period` phiên cuối.</summary>
    private static decimal Midpoint(IReadOnlyList<OhlcvBar> history, int period)
    {
        var high = decimal.MinValue;
        var low = decimal.MaxValue;
        for (var i = history.Count - period; i < history.Count; i++)
        {
            if (history[i].High > high) high = history[i].High;
            if (history[i].Low < low) low = history[i].Low;
        }
        return (high + low) / 2m;
    }

    /// <summary>
    /// VWAP (Volume Weighted Average Price) — giá trung bình theo khối lượng.
    /// = Σ(TypicalPrice × Volume) / Σ(Volume), TypicalPrice = (High + Low + Close)/3.
    /// Tính trên toàn bộ dãy bars truyền vào (thường là 1 phiên intraday hoặc cửa sổ N phiên).
    /// </summary>
    public static decimal Vwap(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count == 0) return 0;

        var tongGiaTri = 0m;
        var tongKhoiLuong = 0m;
        foreach (var bar in history)
        {
            var typical = (bar.High + bar.Low + bar.Close) / 3m;
            tongGiaTri += typical * bar.Volume;
            tongKhoiLuong += bar.Volume;
        }
        return tongKhoiLuong == 0 ? 0 : tongGiaTri / tongKhoiLuong;
    }

    /// <summary>
    /// VSA (Volume Spread Analysis) — phân tích quan hệ giá-khối lượng trên một nến.
    /// Nhãn trả về: "GomIm" (volume lớn + biên độ hẹp → gom hàng im lặng),
    /// "DayGia" (nến tăng + volume ≥ 2× TB → đẩy giá),
    /// "XaHang" (nến giảm + volume ≥ 2× TB → xả hàng),
    /// "BinhThuong" (không có tín hiệu đặc biệt).
    /// </summary>
    public static string VsaLabel(OhlcvBar currentBar, decimal avgVolume20)
    {
        if (avgVolume20 <= 0) return "BinhThuong";

        var volumeRatio = currentBar.Volume / avgVolume20;
        var spread = currentBar.High - currentBar.Low;
        var typical = (currentBar.High + currentBar.Low + currentBar.Close) / 3m;
        // Biên độ hẹp: spread < 50% biên độ của chính tỷ lệ giá trung bình (proxy khi không có ATR).
        var spreadHep = typical > 0 && spread / typical < 0.01m;

        if (volumeRatio >= 2.0m && spreadHep)
            return "GomIm";
        if (volumeRatio >= 2.0m && currentBar.Close > currentBar.Open)
            return "DayGia";
        if (volumeRatio >= 2.0m && currentBar.Close < currentBar.Open)
            return "XaHang";
        return "BinhThuong";
    }

    /// <summary>
    /// POC (Point of Control) — mức giá có khối lượng giao dịch nhiều nhất trong `lookback` phiên.
    /// Gom volume theo bucket giá 0.5% (đủ mịn cho CP penny, không quá nhiễu cho CP giá cao);
    /// trả về giá đại diện (TypicalPrice gần tâm bucket nhất) của bucket có volume lớn nhất.
    /// </summary>
    public static decimal Poc(IReadOnlyList<OhlcvBar> history, int lookback = 60)
    {
        if (history.Count == 0) return 0;

        var start = Math.Max(0, history.Count - lookback);
        var buckets = new Dictionary<long, (decimal volume, decimal representative, decimal distance)>();
        for (var i = start; i < history.Count; i++)
        {
            var bar = history[i];
            var typical = (bar.High + bar.Low + bar.Close) / 3m;
            if (typical <= 0) continue;

            // Bucket key: sàn của (typical / (0.5% × typical chuẩn)) — dùng logarit để bucket theo tỷ lệ %.
            var key = (long)Math.Floor(Math.Log((double)typical) / Math.Log(1.005));
            var center = (decimal)Math.Pow(1.005, key + 0.5);
            var distance = Math.Abs(typical - center);
            if (buckets.TryGetValue(key, out var existing))
            {
                // Đại diện bucket = TypicalPrice gần tâm nhất (điển hình hơn giá trung bình cộng).
                var representative = distance < existing.distance ? typical : existing.representative;
                var bestDistance = Math.Min(distance, existing.distance);
                buckets[key] = (existing.volume + bar.Volume, representative, bestDistance);
            }
            else
            {
                buckets[key] = (bar.Volume, typical, distance);
            }
        }

        if (buckets.Count == 0) return history[^1].Close;
        return buckets.Values.OrderByDescending(b => b.volume).First().representative;
    }

    /// <summary>
    /// SMC (Smart Money Concepts) — phát hiện BOS và liquidity sweep trên nến cuối.
    /// BOS (Break of Structure): close vượt đỉnh `lookback` phiên trước (BOS tăng)
    /// hoặc thủng đáy `lookback` phiên trước (BOS giảm) — không tính nến hiện tại.
    /// Liquidity sweep: Low xuyên đáy N phiên (quét stop-loss) rồi Close giành lại trên đáy
    /// (sweep hướng lên — Smart Money gom hàng), hoặc High xuyên đỉnh rồi Close dưới đỉnh (sweep hướng xuống).
    /// </summary>
    public static SmcResult Smc(IReadOnlyList<OhlcvBar> history, int lookback = 20)
    {
        if (history.Count < lookback + 1)
            return new SmcResult(false, false, string.Empty);

        var current = history[^1];
        var dinhKyTruoc = decimal.MinValue;
        var dayKyTruoc = decimal.MaxValue;
        for (var i = history.Count - 1 - lookback; i < history.Count - 1; i++)
        {
            if (history[i].High > dinhKyTruoc) dinhKyTruoc = history[i].High;
            if (history[i].Low < dayKyTruoc) dayKyTruoc = history[i].Low;
        }

        // BOS: close phá vỡ cấu trúc đỉnh/đáy của N phiên trước.
        var hasBos = current.Close > dinhKyTruoc || current.Close < dayKyTruoc;

        // Liquidity sweep hướng lên: râu dưới xuyên đáy (quét stop) nhưng close giành lại trên đáy.
        var sweepLen = current.Low < dayKyTruoc && current.Close > dayKyTruoc;
        // Liquidity sweep hướng xuống: râu trên xuyên đỉnh nhưng close bị đè xuống dưới đỉnh.
        var sweepXuong = current.High > dinhKyTruoc && current.Close < dinhKyTruoc;

        var direction = sweepLen ? "Len" : sweepXuong ? "Xuong" : string.Empty;
        return new SmcResult(hasBos, sweepLen || sweepXuong, direction);
    }
}

using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Tạo bản chụp toàn bộ chỉ báo tại thời điểm trigger.
/// Dùng để đo outcome sau này (T+1/T+2/T+3).
/// </summary>
public class BanChupChiBaoBuilder
{
    /// <summary>Chu kỳ RSI mặc định.</summary>
    private const int RsiPeriod = 14;

    /// <summary>Chu kỳ ATR mặc định.</summary>
    private const int AtrPeriod = 14;

    /// <summary>Chu kỳ ADX mặc định.</summary>
    private const int AdxPeriod = 14;

    /// <summary>Chu kỳ Bollinger mặc định.</summary>
    private const int BollingerPeriod = 20;

    /// <summary>Chu kỳ Stochastic mặc định.</summary>
    private const int StochPeriod = 14;

    /// <summary>Số phiên tính VWAP (phiên intraday gần nhất).</summary>
    private const int VwapLookback = 20;

    /// <summary>Số phiên tính Volume TB20.</summary>
    private const int VolumeAvgPeriod = 20;

    /// <summary>Số phiên lookback cho POC.</summary>
    private const int PocLookback = 60;

    /// <summary>Số phiên lookback cho SMC.</summary>
    private const int SmcLookback = 20;

    /// <summary>Chụp toàn bộ giá trị chỉ báo từ history bars + giá hiện tại.</summary>
    public BangChupChiBao Chup(IReadOnlyList<OhlcvBar> history, decimal giaHienTai, long volumeHienTai)
    {
        if (history.Count == 0)
            return new BangChupChiBao { ThoiDiemChup = DateTime.UtcNow };

        // RSI(14)
        var rsi = IndicatorMath.Rsi(history, RsiPeriod);

        // MACD histogram
        var (_, _, macdHist) = IndicatorMath.Macd(history);

        // EMA 20/50/200
        var ema20 = IndicatorMath.Ema(history, 20);
        var ema50 = IndicatorMath.Ema(history, 50);
        var ema200 = history.Count >= 200 ? IndicatorMath.Ema(history, 200) : 0m;

        // ADX / +DI / -DI
        var (adx, plusDi, minusDi) = IndicatorMath.Adx(history, AdxPeriod);

        // Stochastic K/D
        var (stochK, stochD) = IndicatorMath.Stochastic(history, StochPeriod);

        // VWAP (trên 20 phiên gần nhất)
        var vwapSlice = history.Count > VwapLookback
            ? history.TakeLast(VwapLookback).ToList()
            : history.ToList();
        var vwap = IndicatorMath.Vwap(vwapSlice);

        // ATR% = ATR / giá hiện tại × 100
        var atr = IndicatorMath.Atr(history, AtrPeriod);
        var atrPercent = giaHienTai > 0 ? atr / giaHienTai * 100m : 0m;

        // Bollinger: width / upper / lower
        var (bbUpper, _, bbLower, _, bbWidth) = IndicatorMath.Bollinger(history, BollingerPeriod);

        // Volume ratio: volume hiện tại / TB20
        var avgVol20 = history.Count >= VolumeAvgPeriod
            ? history.TakeLast(VolumeAvgPeriod).Average(b => (decimal)b.Volume)
            : (history.Count > 0 ? history.Average(b => (decimal)b.Volume) : 0m);
        var volumeRatio = avgVol20 > 0 ? volumeHienTai / avgVol20 : 0m;

        // Ichimoku: tenkan / kijun / trên mây
        var ichimoku = IndicatorMath.Ichimoku(history);

        // VSA label trên nến cuối
        var lastBar = history[^1];
        var vsaLabel = IndicatorMath.VsaLabel(lastBar, avgVol20);

        // POC
        var poc = IndicatorMath.Poc(history, PocLookback);

        // SMC: BOS + liquidity sweep
        var smc = IndicatorMath.Smc(history, SmcLookback);

        return new BangChupChiBao
        {
            Rsi = rsi,
            MacdHistogram = macdHist,
            Ema20 = ema20,
            Ema50 = ema50,
            Ema200 = ema200,
            Adx = adx,
            PlusDi = plusDi,
            MinusDi = minusDi,
            StochasticK = stochK,
            StochasticD = stochD,
            Vwap = vwap,
            AtrPercent = atrPercent,
            BollingerWidth = bbWidth,
            BollingerUpper = bbUpper,
            BollingerLower = bbLower,
            VolumeRatio = volumeRatio,
            IchimokuTenkanSen = ichimoku.TenkanSen,
            IchimokuKijunSen = ichimoku.KijunSen,
            GiaTrenMayIchimoku = ichimoku.GiaTrenMay,
            VsaLabel = vsaLabel,
            Poc = poc,
            SmcHasBos = smc.HasBos,
            SmcHasLiquiditySweep = smc.HasLiquiditySweep,
            ThoiDiemChup = DateTime.UtcNow
        };
    }
}

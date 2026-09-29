namespace StockRadar.Domain.ValueObjects;

/// <summary>
/// Bản chụp toàn bộ giá trị 13 chỉ báo tại thời điểm kích hoạt.
/// Dùng để đo outcome sau này (T+1/T+2/T+3 MFE/MAE).
/// </summary>
public record BangChupChiBao
{
    public decimal Rsi { get; init; }
    public decimal MacdHistogram { get; init; }
    public decimal Ema20 { get; init; }
    public decimal Ema50 { get; init; }
    public decimal Ema200 { get; init; }
    public decimal Adx { get; init; }
    public decimal PlusDi { get; init; }
    public decimal MinusDi { get; init; }
    public decimal StochasticK { get; init; }
    public decimal StochasticD { get; init; }
    public decimal Vwap { get; init; }
    public decimal AtrPercent { get; init; }
    public decimal BollingerWidth { get; init; }
    public decimal BollingerUpper { get; init; }
    public decimal BollingerLower { get; init; }

    /// <summary>Volume hiện tại / TB20</summary>
    public decimal VolumeRatio { get; init; }

    public decimal IchimokuTenkanSen { get; init; }
    public decimal IchimokuKijunSen { get; init; }
    public bool GiaTrenMayIchimoku { get; init; }
    public string VsaLabel { get; init; } = string.Empty;
    public decimal Poc { get; init; }
    public bool SmcHasBos { get; init; }
    public bool SmcHasLiquiditySweep { get; init; }

    /// <summary>Thời điểm chụp bản ghi</summary>
    public DateTime ThoiDiemChup { get; init; }
}

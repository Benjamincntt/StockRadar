namespace StockRadar.Application.Options;

/// <summary>Cấu hình ngưỡng cho từng kịch bản — tất cả tunable qua appsettings</summary>
public class KichBanOptions
{
    public const string SectionName = "KichBan";

    public KichBanNoHuongLenOptions NoHuongLen { get; set; } = new();
    public KichBanHoiHoTroOptions HoiHoTro { get; set; } = new();
    public KichBanQuetThanhKhoanOptions QuetThanhKhoan { get; set; } = new();
    public KichBanKietSucOptions KietSuc { get; set; } = new();
    public KichBanGayNenOptions GayNen { get; set; } = new();
}

/// <summary>Ngưỡng kịch bản Nổ hướng lên — breakout đỉnh hộp/nền với volume mạnh</summary>
public class KichBanNoHuongLenOptions
{
    /// <summary>ADX tối thiểu để xác nhận có xu hướng. Mặc định: 20</summary>
    public decimal MinAdx { get; set; } = 20m;

    /// <summary>Volume ratio tối thiểu khi breakout. Mặc định: 1.5</summary>
    public decimal MinVolumeRatio { get; set; } = 1.5m;

    /// <summary>Volume contraction tối đa trong giai đoạn tích lũy. Mặc định: 0.7</summary>
    public decimal MaxVolumeContraction { get; set; } = 0.7m;

    /// <summary>Ngưỡng Bollinger width để coi là "co hẹp". Mặc định: 0.05</summary>
    public decimal BollingerCompressionThreshold { get; set; } = 0.05m;
}

/// <summary>Ngưỡng kịch bản Hồi về hỗ trợ — uptrend lùi về EMA20/VWAP rồi bật lại</summary>
public class KichBanHoiHoTroOptions
{
    /// <summary>RSI tối thiểu khi hồi (không quá yếu). Mặc định: 40</summary>
    public decimal RsiPullbackMin { get; set; } = 40m;

    /// <summary>RSI tối đa khi hồi (chưa vượt mua trở lại). Mặc định: 50</summary>
    public decimal RsiPullbackMax { get; set; } = 50m;

    /// <summary>Khoảng cách tối đa từ giá đến EMA20 (tỷ lệ). Mặc định: 3%</summary>
    public decimal MaxEma20Distance { get; set; } = 0.03m;
}

/// <summary>Ngưỡng kịch bản Quét thanh khoản — quét đáy nền rồi giành lại</summary>
public class KichBanQuetThanhKhoanOptions
{
    /// <summary>Volume spike tối thiểu khi quét. Mặc định: 2.0</summary>
    public decimal MinVolumeSpike { get; set; } = 2.0m;

    /// <summary>Margin trên đáy nền để coi là "reclaim". Mặc định: 1%</summary>
    public decimal ReclaimMargin { get; set; } = 0.01m;
}

/// <summary>Ngưỡng kịch bản Kiệt sức — tăng quá nóng, hết đà (SELL)</summary>
public class KichBanKietSucOptions
{
    /// <summary>RSI tối thiểu để coi là quá mua. Mặc định: 78</summary>
    public decimal MinRsi { get; set; } = 78m;

    /// <summary>Volume climax tối thiểu (bán đỉnh khối lượng lớn). Mặc định: 3.0</summary>
    public decimal MinVolumeClimax { get; set; } = 3.0m;

    /// <summary>Số ATR giãn ra tối thiểu so với EMA20. Mặc định: 2.0</summary>
    public decimal MinExtensionAtr { get; set; } = 2.0m;

    /// <summary>Lãi tối thiểu từ entry để coi là "đã tăng nóng". Mặc định: 10%</summary>
    public decimal MinGainFromEntry { get; set; } = 0.10m;
}

/// <summary>Ngưỡng kịch bản Gãy nền — phá vỡ hỗ trợ, mất cấu trúc (EXIT)</summary>
public class KichBanGayNenOptions
{
    /// <summary>Volume ratio tối thiểu của phiên gãy. Mặc định: 1.8</summary>
    public decimal MinSellVolumeRatio { get; set; } = 1.8m;

    /// <summary>Số phiên MACD âm liên tiếp tối thiểu. Mặc định: 3</summary>
    public int MinMacdNegativeDays { get; set; } = 3;
}

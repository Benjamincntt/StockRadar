using StockRadar.Domain.Enums;



namespace StockRadar.Application.Services;



public sealed class SignalFormatter : Abstractions.ISignalFormatter

{

    public string FormatTitle(SignalType type, string symbol) =>

        $"{Emoji(type)} {symbol} — {GetLabelVi(type)}";



    public string FormatAlertTitle(SignalType type, string symbol) =>

        $"{symbol} — {GetLabelVi(type)} mới";



    public string FormatDescription(SignalType type, string symbol, decimal volumeRatio) => type switch

    {

        SignalType.Breakout => $"Giá vượt đỉnh 20 phiên, khối lượng gấp {volumeRatio:0.0} lần trung bình.",

        SignalType.DarvasBreakout =>
            $"Phá vỡ hộp tích lũy phẳng, khối lượng gấp {volumeRatio:0.0} lần trung bình.",

        SignalType.VolumeSpike => $"Khối lượng tăng {volumeRatio:0.0} lần so với trung bình 20 phiên.",

        SignalType.Accumulation => "Biên độ thu hẹp, khối lượng giảm dần trong 20 phiên — giai đoạn tích lũy.",

        SignalType.Shakeout => "Giá thủng hỗ trợ rồi hồi phục nhanh với khối lượng thấp — dấu hiệu rũ hàng.",

        SignalType.Distribution => "Khối lượng tăng nhưng giá đi ngang, xuất hiện nến rút đầu — phân phối.",

        SignalType.RelativeStrength => "Cổ phiếu mạnh hơn VNINDEX trong 5 phiên gần nhất.",

        _ => "Tín hiệu kỹ thuật mới trên watchlist."

    };



    public string GetLabelVi(SignalType type) => type switch

    {

        SignalType.Breakout => "Vượt đỉnh",

        SignalType.DarvasBreakout => "Phá vỡ hộp tích lũy phẳng có xác nhận dòng tiền",

        SignalType.VolumeSpike => "Bùng nổ khối lượng",

        SignalType.Accumulation => "Tích lũy",

        SignalType.Shakeout => "Rũ hàng",

        SignalType.Distribution => "Phân phối",

        SignalType.RelativeStrength => "Mạnh hơn thị trường",

        _ => "Tín hiệu"

    };



    private static string Emoji(SignalType type) => type switch

    {

        SignalType.Breakout => "🚀",

        SignalType.DarvasBreakout => "📦",

        SignalType.VolumeSpike => "🔥",

        SignalType.Accumulation => "📦",

        SignalType.Shakeout => "⚡",

        SignalType.Distribution => "⚠️",

        SignalType.RelativeStrength => "💪",

        _ => "📊"

    };

}



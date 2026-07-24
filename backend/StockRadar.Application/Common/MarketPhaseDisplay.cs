using StockRadar.Domain.Services;

namespace StockRadar.Application.Common;

/// <summary>Nhãn pha thị trường — cùng nguồn <see cref="MarketPhaseClassifier"/> với Top / VNINDEX card.</summary>
public static class MarketPhaseDisplay
{
    public static string LabelVi(MarketWyckoffPhase phase) => phase switch
    {
        MarketWyckoffPhase.Favorable => "TT thuận",
        MarketWyckoffPhase.Neutral => "Nỗ lực hồi phục",
        MarketWyckoffPhase.Unfavorable => "Điều chỉnh",
        _ => "Chưa xác định",
    };
}

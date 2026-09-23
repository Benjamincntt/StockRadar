namespace StockRadar.Domain.Services;

/// <summary>
/// Chuẩn hóa thông điệp gate failure (từ <see cref="BuyDecisionEngine.ResolveTopGateFailure"/>,
/// gồm bản rewrite MA-stack cho thị trường chưa xác nhận) thành nhãn ổn định để đếm thống kê
/// "bao nhiêu mã bị loại ở từng gate". Nhãn không chứa số biến (phần trăm, điểm, khối lượng)
/// để khóa đếm không bị phân mảnh theo từng phiên.
/// </summary>
public static class GateFailureClassifier
{
    public const string HistoryGate = "Thiếu lịch sử";
    public const string LiquidityGate = "Thanh khoản thấp";
    public const string DistributionGate = "Pha phân phối";
    public const string NoBaseBreakoutGate = "Chưa phá vỡ nền giá / chưa test cạnh hộp";
    public const string FomoGate = "FOMO vượt ngưỡng so đỉnh nền";
    public const string MaStackGate = BuyDecisionEngine.MaStackGateMessage;
    public const string UnfavorableLeaderGate = "Thị trường khó — chỉ mua mã dẫn dắt";
    public const string NoSectorWaveGate = "Ngành chưa có sóng + RS không đủ";
    public const string NoEntryActivationGate = "Chưa breakout / shakeout / phân kỳ";
    public const string NegativeRsGate = "Yếu hơn VNINDEX (RS âm)";
    public const string MinPassScoreGate = "Buy Score < MinPassScore";

    /// <summary>Lọc Buy Score mức job (DailyAnalysis.MinScore) sau khi đã qua hết gate engine.</summary>
    public const string BelowJobMinScoreGate = "Buy Score < MinScore (job)";

    public const string OtherGate = "Khác";

    public static string Classify(string? gateFailure)
    {
        if (string.IsNullOrWhiteSpace(gateFailure))
            return OtherGate;

        // "Chưa {BasePriceLabels.Breakout.ToLower()} / chưa test cạnh hộp" — tiền tố tính từ hằng số nguồn.
        var noBaseBreakoutPrefix = $"chưa {BasePriceLabels.Breakout.ToLower()}";

        // Thứ tự khớp theo ResolveTopGateFailure trong BuyDecisionEngine.
        if (gateFailure.StartsWith("Thiếu lịch sử", StringComparison.Ordinal))
            return HistoryGate;

        if (gateFailure.StartsWith("Thanh khoản thấp", StringComparison.Ordinal))
            return LiquidityGate;

        if (gateFailure.StartsWith("Pha phân phối", StringComparison.Ordinal))
            return DistributionGate;

        if (gateFailure.StartsWith(noBaseBreakoutPrefix, StringComparison.OrdinalIgnoreCase))
            return NoBaseBreakoutGate;

        if (gateFailure.StartsWith("FOMO", StringComparison.Ordinal))
            return FomoGate;

        // MA-stack gate bị RewriteMaGateForUnconfirmedMarket đổi thành
        // "Chờ xác nhận thị trường chung" khi thị trường chưa Favorable — vẫn là 1 gate, gộp chung.
        if (gateFailure.Equals(BuyDecisionEngine.MaStackGateMessage, StringComparison.Ordinal)
            || gateFailure.Equals(BuyDecisionEngine.AwaitingMarketConfirmationMessage, StringComparison.Ordinal)
            || gateFailure.Contains("MA stack", StringComparison.OrdinalIgnoreCase))
            return MaStackGate;

        if (gateFailure.StartsWith("Thị trường khó", StringComparison.Ordinal))
            return UnfavorableLeaderGate;

        if (gateFailure.StartsWith("Ngành chưa có sóng", StringComparison.Ordinal))
            return NoSectorWaveGate;

        if (gateFailure.StartsWith("Chưa breakout / shakeout / phân kỳ", StringComparison.Ordinal))
            return NoEntryActivationGate;

        if (gateFailure.StartsWith("Yếu hơn VNINDEX", StringComparison.Ordinal))
            return NegativeRsGate;

        if (gateFailure.StartsWith("Buy Score", StringComparison.Ordinal))
            return MinPassScoreGate;

        return OtherGate;
    }
}

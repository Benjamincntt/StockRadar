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
    public const string NoBaseBreakoutGate = "Chưa phá vỡ nền giá / chưa test cạnh hộp";
    public const string FomoGate = "FOMO vượt ngưỡng so đáy 5 phiên";
    public const string UnfavorableLeaderGate = "Thị trường khó — chỉ mua mã dẫn dắt";
    public const string NoSectorWaveGate = "Ngành chưa có sóng + RS không đủ";
    public const string NoDivergenceGate = "Chưa có phân kỳ dương 15m/1h/N";

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

        if (gateFailure.StartsWith(noBaseBreakoutPrefix, StringComparison.OrdinalIgnoreCase))
            return NoBaseBreakoutGate;

        if (gateFailure.StartsWith("FOMO", StringComparison.Ordinal))
            return FomoGate;

        if (gateFailure.StartsWith("Thị trường khó", StringComparison.Ordinal))
            return UnfavorableLeaderGate;

        if (gateFailure.StartsWith("Ngành chưa có sóng", StringComparison.Ordinal))
            return NoSectorWaveGate;

        if (gateFailure.StartsWith("Chưa có phân kỳ dương", StringComparison.Ordinal))
            return NoDivergenceGate;

        return OtherGate;
    }
}

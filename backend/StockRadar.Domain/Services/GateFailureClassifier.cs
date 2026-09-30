namespace StockRadar.Domain.Services;

/// <summary>
/// Chuẩn hóa thông điệp gate failure (từ <see cref="BuyDecisionEngine.ResolveTopGateFailure"/>,
/// gồm bản rewrite MA-stack cho thị trường chưa xác nhận) thành nhãn ổn định để đếm thống kê
/// "bao nhiêu mã bị loại ở từng gate". Nhãn không chứa số biến (phần trăm, điểm, khối lượng)
/// để khóa đếm không bị phân mảnh theo từng phiên.
/// </summary>
public static class GateFailureClassifier
{
    public const string FomoGate = "FOMO vượt ngưỡng so đáy 5 phiên";
    public const string UnfavorableLeaderGate = "Thị trường khó — chỉ mua mã dẫn dắt";
    public const string NoSectorWaveGate = "Ngành chưa có sóng + RS âm";

    public const string OtherGate = "Khác";

    public static string Classify(string? gateFailure)
    {
        if (string.IsNullOrWhiteSpace(gateFailure))
            return OtherGate;

        // Đã bỏ gate: Thiếu lịch sử / Thanh khoản thấp / Chưa phá vỡ nền giá / Phân kỳ dương —
        // các nhãn không còn tồn tại trong ResolveTopGateFailure nên không còn khớp nữa.

        // Thứ tự khớp theo ResolveTopGateFailure trong BuyDecisionEngine.
        if (gateFailure.StartsWith("FOMO", StringComparison.Ordinal))
            return FomoGate;

        if (gateFailure.StartsWith("Thị trường khó", StringComparison.Ordinal))
            return UnfavorableLeaderGate;

        if (gateFailure.StartsWith("Ngành chưa có sóng", StringComparison.Ordinal))
            return NoSectorWaveGate;

        return OtherGate;
    }
}

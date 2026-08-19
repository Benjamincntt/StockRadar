using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services;

/// <summary>
/// Xếp hạng percentile RS trong rổ — <b>một định nghĩa duy nhất</b> cho toàn hệ thống.
/// RS luôn trừ index cùng khung, rổ luôn lọc lịch sử + thanh khoản; thứ duy nhất được
/// phép khác giữa các nơi gọi là <paramref name="days"/>.
/// </summary>
/// <remarks>
/// Trừ index là hằng số chung toàn rổ nên <b>không</b> đổi thứ hạng — nó giữ cho đại lượng
/// đúng nghĩa "RS", còn thứ hạng chỉ đổi khi <paramref name="days"/> hoặc rổ đủ điều kiện đổi.
/// Lọc thanh khoản ngay tại đây là có chủ đích: lọc sau khi xếp hạng sẽ để mã thanh khoản
/// thấp chiếm chỗ rồi bị loại, bóp hạng các mã đủ điều kiện.
/// </remarks>
public static class RsPercentileCalculator
{
    public static IReadOnlyDictionary<string, decimal> Build(
        IReadOnlyList<Stock> universe,
        ISignalAnalyzer signals,
        decimal indexChangePercent,
        int days,
        int minHistoryDays,
        decimal minAvgDailyVolume)
    {
        var minBars = Math.Max(minHistoryDays, days + 1);
        var eligible = universe
            .Where(s =>
                s.History.Count >= minBars
                && signals.GetAverageVolume(s.History) >= minAvgDailyVolume)
            .Select(s => (s.Symbol, Rs: signals.GetRelativeStrength(s, indexChangePercent, days)))
            .OrderBy(x => x.Rs)
            .ToList();

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (eligible.Count == 0)
            return result;

        if (eligible.Count == 1)
        {
            result[eligible[0].Symbol] = 100m;
            return result;
        }

        var denom = eligible.Count - 1;
        for (var i = 0; i < eligible.Count; i++)
            result[eligible[i].Symbol] = Math.Round(i / (decimal)denom * 100m, 2);

        return result;
    }
}

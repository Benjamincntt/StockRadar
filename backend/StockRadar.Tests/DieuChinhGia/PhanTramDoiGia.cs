using StockRadar.Domain.Entities;

namespace StockRadar.Tests.DieuChinhGia;

internal static class PhanTramDoiGia
{
    // BUSINESS-RULE: % đổi giá theo đóng cửa, đúng công thức bộ phân tích tín hiệu cũ; sửa ở đây là đổi ý nghĩa mọi ca DieuChinhGia.
    internal static decimal Tinh(IReadOnlyList<OhlcvBar> lichSu, int soPhien)
    {
        var previous = lichSu[^(soPhien + 1)].Close;
        if (previous == 0)
            return 0;

        return Math.Round((lichSu[^1].Close - previous) / previous * 100m, 2);
    }
}

using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services;

/// <summary>
/// Chuẩn hóa nến ngày trước khi đưa vào <see cref="V3ChiSoToanThiTruong"/> (FR-002, Edge Case nến giữ chỗ).
/// Bẫy: lúc 08:30 <c>Stocks.HistoryJson</c> ĐÃ chứa nến hôm nay giữ chỗ (giá tham chiếu, KL 0);
/// trong phiên chứa nến dở dang. Không xử lý → tín hiệu lệch một ngày (bằng chứng PH-14: Pha 1 V2
/// chỉ khớp 44/49 khi tính nến giữ chỗ).
/// </summary>
public static class V3CotNen
{
    /// <summary>
    /// Nến HOÀN THÀNH tính tới (nhưng chưa gồm) <paramref name="homNay"/> — dùng cho nhịp trước phiên
    /// (KB1/KB2 chấm trên giá đóng cửa phiên T = hôm qua): loại nến giữ chỗ/dở dang của hôm nay.
    /// </summary>
    public static IReadOnlyList<OhlcvBar> NenHoanThanh(IReadOnlyList<OhlcvBar> bars, DateOnly homNay)
    {
        var ketQua = new List<OhlcvBar>(bars.Count);
        foreach (var b in bars.OrderBy(x => x.Date))
        {
            if (b.Date < homNay)
                ketQua.Add(b);
        }
        return ketQua;
    }

    /// <summary>
    /// Nến hoàn thành tới hôm qua + nến hôm nay DÙNG GIÁ KHỚP hiện tại thay giá đóng cửa —
    /// cho nhịp trong phiên / ATC ~14:25 (KB4/N1/N3, bán realtime). Trả nến cuối = phiên tín hiệu T hôm nay.
    /// </summary>
    public static IReadOnlyList<OhlcvBar> GheGiaKhopTrongPhien(
        IReadOnlyList<OhlcvBar> bars,
        DateOnly homNay,
        decimal giaKhop,
        long volumeKhop,
        decimal open = 0m,
        decimal high = 0m,
        decimal low = 0m)
    {
        var nen = NenHoanThanh(bars, homNay).ToList();
        var o = open > 0m ? open : giaKhop;
        var h = high > 0m ? high : giaKhop;
        var l = low > 0m ? low : giaKhop;
        nen.Add(new OhlcvBar(homNay, o, h, l, giaKhop, volumeKhop));
        return nen;
    }

    /// <summary>Áp <paramref name="chuyen"/> lên History của từng mã, giữ nguyên các trường khác.</summary>
    public static IReadOnlyList<Stock> ChuyenHistory(
        IEnumerable<Stock> cacMa,
        Func<IReadOnlyList<OhlcvBar>, IReadOnlyList<OhlcvBar>> chuyen)
    {
        var ds = new List<Stock>();
        foreach (var s in cacMa)
            ds.Add(s with { History = chuyen(s.History) });
        return ds;
    }

    /// <summary>Mã nào sau chuẩn hóa vẫn còn đủ <paramref name="soPhienToiThieu"/> nến? (loại mã quá ngắn).</summary>
    public static bool DuNen(IReadOnlyList<OhlcvBar> bars, int soPhienToiThieu) =>
        bars.Count >= soPhienToiThieu;
}

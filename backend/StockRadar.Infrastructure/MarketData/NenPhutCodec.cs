using System.IO.Compression;
using System.Text.Json;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Mã hoá khối nến 1 phút của một phiên thành gzip + JSON mảng lồng <c>[p,o,h,l,c,v]</c> và đọc lại.
/// Thiết kế định dạng, đơn vị giá và cách nhận nến ATC: specs/017-intraday-minute-bars/plan.md (D2, D4).
/// </summary>
internal static class NenPhutCodec
{
    // LIMIT: nến ATC chỉ nhận khi nến cuối ≥ 14:39 và nến liền trước ≤ 14:29. Quan sát 09–12/11/2026
    // KBS không trả nến 14:30–14:39, còn giờ nến ATC thật là 14:40 hoặc 14:45 — không được khoá cứng.
    private const int PhutDauAtc = 14 * 60 + 39;
    private const int PhutCuoiPhienChinh = 14 * 60 + 29;

    /// <summary>
    /// Đổi mốc thời gian KBS thành phút kể từ 00:00 giờ Việt Nam.
    /// </summary>
    /// <param name="thoiDiem">Mốc thời gian mang giờ VN (server UTC vẫn trả offset +00:00 nhưng giờ là giờ VN).</param>
    /// <returns>Số phút tính trên mặt đồng hồ của <paramref name="thoiDiem"/>, không quy đổi múi giờ.</returns>
    public static int PhutTrongNgay(DateTimeOffset thoiDiem) =>
        thoiDiem.DateTime.Hour * 60 + thoiDiem.DateTime.Minute;

    /// <summary>
    /// Nén nến của một phiên thành khối gzip để lưu vào cột <c>NenPhutTheoPhien.DuLieuNen</c>.
    /// </summary>
    /// <param name="bars">Nến theo thứ tự thời gian, giá đã chia 1000 (nghìn đồng).</param>
    /// <returns>Khối gzip của mảng JSON <c>[[p,o,h,l,c,v], ...]</c>.</returns>
    public static byte[] Nen(IReadOnlyList<KbsHistoryClient.KbsChartBar> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        using var dauRa = new MemoryStream();
        using (var gzip = new GZipStream(dauRa, CompressionLevel.Optimal, leaveOpen: true))
        using (var json = new Utf8JsonWriter(gzip))
        {
            json.WriteStartArray();
            foreach (var bar in bars)
            {
                json.WriteStartArray();
                json.WriteNumberValue(PhutTrongNgay(bar.Time));
                json.WriteNumberValue(bar.Open);
                json.WriteNumberValue(bar.High);
                json.WriteNumberValue(bar.Low);
                json.WriteNumberValue(bar.Close);
                json.WriteNumberValue(bar.Volume);
                json.WriteEndArray();
            }

            json.WriteEndArray();
        }

        return dauRa.ToArray();
    }

    /// <summary>
    /// Giải nén khối <c>DuLieuNen</c> thành danh sách nến để job và script Python kiểm tra cùng một nguồn.
    /// </summary>
    /// <param name="khoiNen">Khối gzip do <see cref="Nen"/> tạo ra.</param>
    /// <returns>Nến đã giải nén, giữ nguyên thứ tự đã lưu.</returns>
    public static IReadOnlyList<NenPhutBar> GiaiNen(byte[] khoiNen)
    {
        ArgumentNullException.ThrowIfNull(khoiNen);

        using var nguon = new MemoryStream(khoiNen, writable: false);
        using var gzip = new GZipStream(nguon, CompressionMode.Decompress);
        using var json = JsonDocument.Parse(gzip);

        var bars = new List<NenPhutBar>();
        foreach (var hang in json.RootElement.EnumerateArray())
        {
            bars.Add(new NenPhutBar(
                hang[0].GetInt32(),
                hang[1].GetDecimal(),
                hang[2].GetDecimal(),
                hang[3].GetDecimal(),
                hang[4].GetDecimal(),
                hang[5].GetInt64()));
        }

        return bars;
    }

    /// <summary>
    /// Nhận nến ATC theo vị trí cuối phiên, không theo giờ cứng.
    /// </summary>
    /// <param name="bars">Nến của một phiên, đã sắp theo thời gian.</param>
    /// <returns>Nến cuối phiên nếu nó nằm sau khoảng trống 14:30–14:39; ngược lại <c>null</c>.</returns>
    public static KbsHistoryClient.KbsChartBar? TimNenAtc(IReadOnlyList<KbsHistoryClient.KbsChartBar> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (bars.Count < 2)
            return null;

        var nenCuoi = bars[^1];
        return PhutTrongNgay(nenCuoi.Time) >= PhutDauAtc
               && PhutTrongNgay(bars[^2].Time) <= PhutCuoiPhienChinh
            ? nenCuoi
            : null;
    }
}

/// <summary>Một nến đã giải nén từ khối <c>DuLieuNen</c>; <see cref="Phut"/> là phút kể từ 00:00 giờ VN.</summary>
internal sealed record NenPhutBar(
    int Phut,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

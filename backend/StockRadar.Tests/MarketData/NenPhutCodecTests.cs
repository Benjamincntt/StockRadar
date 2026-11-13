using System.Globalization;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Tests.MarketData;

/// <summary>
/// Spec 017 T003 — codec khối nến phút và nhận nến ATC theo vị trí (không theo giờ cứng).
/// Mọi nến dựng bằng <see cref="DateTimeOffset"/> offset +00:00 mang giờ VN, đúng như KBS trả
/// và như server UTC đang lưu; test phải xanh cả khi máy dev ở múi +07.
/// </summary>
public sealed class NenPhutCodecTests
{
    private const int Phut0915 = 9 * 60 + 15;
    private const int Phut1129 = 11 * 60 + 29;
    private const int Phut1300 = 13 * 60;
    private const int Phut1429 = 14 * 60 + 29;
    private const int Phut1440 = 14 * 60 + 40;
    private const int Phut1445 = 14 * 60 + 45;

    private static KbsHistoryClient.KbsChartBar Nen(int phut, decimal gia, long khoiLuong = 0) =>
        new(
            new DateTimeOffset(2026, 11, 12, phut / 60, phut % 60, 0, TimeSpan.Zero),
            gia,
            gia + 0.05m,
            gia - 0.05m,
            gia + 0.01m,
            khoiLuong > 0 ? khoiLuong : 1_000 + phut);

    /// <summary>Phiên đủ 226 nến: 09:15–11:29, 13:00–14:29, rồi nến ATC ở phút truyền vào.</summary>
    private static List<KbsHistoryClient.KbsChartBar> Phien226(int phutNenAtc)
    {
        var bars = new List<KbsHistoryClient.KbsChartBar>();
        for (var p = Phut0915; p <= Phut1129; p++)
            bars.Add(Nen(p, 30.10m));
        for (var p = Phut1300; p <= Phut1429; p++)
            bars.Add(Nen(p, 30.20m));
        bars.Add(Nen(phutNenAtc, 30.30m));
        return bars;
    }

    [Fact]
    public void Nen_RoiGiaiNen_GiuDu226NenVaTungGiaTri()
    {
        var bars = Phien226(Phut1445);
        Assert.Equal(226, bars.Count);

        var khoiNen = NenPhutCodec.Nen(bars);
        var docLai = NenPhutCodec.GiaiNen(khoiNen);

        Assert.Equal(226, docLai.Count);
        for (var i = 0; i < bars.Count; i++)
        {
            Assert.Equal(NenPhutCodec.PhutTrongNgay(bars[i].Time), docLai[i].Phut);
            Assert.Equal(bars[i].Open, docLai[i].Open);
            Assert.Equal(bars[i].High, docLai[i].High);
            Assert.Equal(bars[i].Low, docLai[i].Low);
            Assert.Equal(bars[i].Close, docLai[i].Close);
            Assert.Equal(bars[i].Volume, docLai[i].Volume);
        }
    }

    [Theory]
    [InlineData(Phut1440, "14:40")]
    [InlineData(Phut1445, "14:45")]
    public void TimNenAtc_NhanNenCuoiPhien_Ca1440Va1445(int phutNenAtc, string gioMongDoi)
    {
        var bars = Phien226(phutNenAtc);

        var nenAtc = NenPhutCodec.TimNenAtc(bars);

        Assert.NotNull(nenAtc);
        Assert.Equal(gioMongDoi, nenAtc!.Time.DateTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        Assert.Equal(30.30m, nenAtc.Open);
    }

    [Fact]
    public void TimNenAtc_PhienNgatKhongCoAtc_TraNull()
    {
        var bars = new List<KbsHistoryClient.KbsChartBar>();
        for (var p = Phut1300; p <= Phut1429; p++)
            bars.Add(Nen(p, 30.20m));

        Assert.Null(NenPhutCodec.TimNenAtc(bars));
    }

    [Fact]
    public void PhutTrongNgay_GiuGioMatDongHo_KhongDoiSangUtc()
    {
        var nen1429 = Nen(Phut1429, 30.10m);
        Assert.Equal(14, nen1429.Time.Hour);

        Assert.Equal(869, NenPhutCodec.PhutTrongNgay(nen1429.Time));
        Assert.Equal(869, NenPhutCodec.PhutTrongNgay(
            new DateTimeOffset(2026, 11, 12, 14, 29, 0, TimeSpan.FromHours(7))));

        var docLai = Assert.Single(NenPhutCodec.GiaiNen(NenPhutCodec.Nen([nen1429])));
        Assert.Equal(869, docLai.Phut);
    }
}

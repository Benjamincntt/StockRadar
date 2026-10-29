using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

public sealed class V3PushFormatterTests
{
    /// <summary>Verifies the four prescribed titles.</summary>
    /// <param name="loai">The V3 notification kind.</param>
    /// <param name="kichBan">The scenario associated with the signal.</param>
    /// <param name="mongDoi">The exact title required by the client contract.</param>
    [Theory]
    [InlineData(LoaiTinV3.Mua, LoaiKichBanV3.N1, "MUA · HPG · N1")]
    [InlineData(LoaiTinV3.Ban, LoaiKichBanV3.KB1, "BÁN · HPG")]
    [InlineData(LoaiTinV3.HuyMua, LoaiKichBanV3.KB2, "HUỶ MUA · HPG")]
    [InlineData(LoaiTinV3.AtoN3, LoaiKichBanV3.N3, "ATO · HPG · N3")]
    public void TieuDeDungHopDong(LoaiTinV3 loai, LoaiKichBanV3 kichBan, string mongDoi)
    {
        Assert.Equal(mongDoi, V3PushFormatter.TieuDe(loai, "HPG", kichBan));
    }

    /// <summary>Uses the unchanged production Telegram buy formatter and preserves the signal price and rationale.</summary>
    [Fact]
    public void TinMuaTelegramThatGiuDuTamDongVaCanhBao()
    {
        var noiDungHtml = V3TelegramFormatter.FormatMua(LoaiKichBanV3.N1, "HPG", 27.3m, 38m,
            4m, 1, 5, 0.12m, -8.1m, 1.5m, 0.3m, false, 15, null, new V3CauHinh());
        Assert.Contains("<b>", noiDungHtml);
        Assert.Contains("<code>", noiDungHtml);

        var noiDung = V3PushFormatter.NoiDung(noiDungHtml);

        Assert.DoesNotContain("<b>", noiDung);
        Assert.DoesNotContain("<code>", noiDung);
        Assert.DoesNotContain("&amp;", noiDung);
        Assert.Contains("Giá tín hiệu: 27.3", noiDung);
        Assert.Contains("GIẢI CHẤP CẠN KIỆT", noiDung);
        Assert.Equal(8, noiDung.Split('\n').Length);
        Assert.Contains("Bán realtime: giá khớp ≥ max(chạm MA20, giá vào +0,4%) · Giữ tối đa 15 phiên", noiDung);
        Assert.EndsWith("⚠️Backtest P1++ chưa kiểm chứng live", noiDung);
        Assert.DoesNotContain("…", noiDung);
    }

    /// <summary>Preserves the price and exit rationale in a short real Telegram sell message.</summary>
    [Fact]
    public void TinBanNganGiuGiaVaLyDoGiaiMaEntity()
    {
        var noiDungHtml = V3TelegramFormatter.FormatBan(LoaiKichBanV3.N1, "HPG", 31.5m, 27.3m,
            3, 15, "chạm MA20 & đủ lãi");
        Assert.Contains("&amp;", noiDungHtml);
        var noiDung = V3PushFormatter.NoiDung(noiDungHtml);
        Assert.Contains("Giá khớp: 31.5", noiDung);
        Assert.Contains("Lý do: chạm MA20 & đủ lãi", noiDung);
        Assert.DoesNotContain("&amp;", noiDung);
        Assert.DoesNotContain("<", noiDung);
        Assert.DoesNotContain("…", noiDung);
    }

    /// <summary>Strips tags before decoding entities so escaped comparisons survive, and collapses blank lines.</summary>
    [Fact]
    public void BoTheGiaiMaVaGopDongTrang()
    {
        var noiDung = V3PushFormatter.NoiDung("  <b>HPG &amp; FPT</b>\r\n\r\n  \r\n<code>27.3</code><br/>\n\n&lt; 30 &gt; 20 &#x1F680; &#39;ổn&#39;  ");
        Assert.Equal("HPG & FPT\n27.3\n< 30 > 20 🚀 'ổn'", noiDung);
    }

    /// <summary>Returns no text for empty or markup-only input.</summary>
    /// <param name="noiDungHtml">Input containing no visible text.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" <b></b>\r\n\t ")]
    public void NoiDungRongKhongSinhKyTu(string noiDungHtml)
    {
        Assert.Equal("", V3PushFormatter.NoiDung(noiDungHtml));
    }

    /// <summary>Preserves the entire body on both sides of the former clipping boundary.</summary>
    /// <param name="doDai">The input text-element count.</param>
    [Theory]
    [InlineData(239)]
    [InlineData(240)]
    [InlineData(241)]
    public void GiuDuNoiDungQuaBienCu(int doDai)
    {
        var noiDungGoc = new string('a', doDai);
        var noiDung = V3PushFormatter.NoiDung(noiDungGoc);
        Assert.Equal(noiDungGoc, noiDung);
    }

    /// <summary>Preserves every Unicode cluster and trailing text without clipping.</summary>
    /// <param name="phanTu">A complete Unicode text element.</param>
    [Theory]
    [InlineData("🚀")]
    [InlineData("👩🏽‍💻")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("🇻🇳")]
    [InlineData("a\u0301")]
    public void GiuDuUnicodeVaDuoi(string phanTu)
    {
        var tienTo = new string('a', 238);
        var noiDung = V3PushFormatter.NoiDung(tienTo + phanTu + phanTu + "đuôi");
        Assert.Equal(tienTo + phanTu + phanTu + "đuôi", noiDung);
    }
}

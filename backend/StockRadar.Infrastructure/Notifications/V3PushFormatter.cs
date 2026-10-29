using System.Net;
using System.Text.RegularExpressions;
using StockRadar.Domain.Enums;

namespace StockRadar.Infrastructure.Notifications;

internal static class V3PushFormatter
{
    private static readonly Regex TheXuongDong = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
    private static readonly Regex CacThe = new(@"<[^>]+>", RegexOptions.NonBacktracking);
    /// <summary>Formats the V3 Android notification title.</summary>
    /// <param name="loai">The notification kind.</param>
    /// <param name="ma">The stock symbol.</param>
    /// <param name="kichBan">The V3 scenario.</param>
    /// <returns>The title prescribed by the mobile delivery contract.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The notification kind is not supported.</exception>
    internal static string TieuDe(LoaiTinV3 loai, string ma, LoaiKichBanV3 kichBan) => loai switch
    {
        LoaiTinV3.Mua => $"MUA · {ma} · {kichBan}",
        LoaiTinV3.Ban => $"BÁN · {ma}",
        LoaiTinV3.HuyMua => $"HUỶ MUA · {ma}",
        LoaiTinV3.AtoN3 => $"ATO · {ma} · N3",
        _ => throw new ArgumentOutOfRangeException(nameof(loai))
    };

    /// <summary>Converts Telegram HTML into complete plain text without clipping safety information.</summary>
    /// <param name="noiDungHtml">The original Telegram notification.</param>
    /// <returns>The complete decoded text with normalized nonempty lines.</returns>
    internal static string NoiDung(string noiDungHtml)
    {
        var boThe = CacThe.Replace(TheXuongDong.Replace(noiDungHtml, "\n"), "");
        var giaiMa = WebUtility.HtmlDecode(boThe).ReplaceLineEndings("\n");
        return string.Join("\n", giaiMa.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }
}

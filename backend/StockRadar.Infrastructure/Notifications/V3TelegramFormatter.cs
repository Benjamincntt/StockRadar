using System.Globalization;
using System.Text;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>
/// Formatter Telegram V3 (Phụ lục E). Nhãn tiếng Việt có dấu; giữ thuật ngữ trading (MA10, RS126, ATC, ATO, T+2.5).
/// HTML: escape dữ liệu thô nhưng giữ thẻ b/code (nối cách V2 đã kiểm). Giá nghìn đồng, 2 chữ số.
/// </summary>
internal static class V3TelegramFormatter
{
    public static string TenKichBan(LoaiKichBanV3 loai) => loai switch
    {
        LoaiKichBanV3.KB1 => "KB1 · HOẢNG LOẠN",
        LoaiKichBanV3.KB2 => "KB2 · CHỈ SỐ THỦNG MA50",
        LoaiKichBanV3.KB4 => "KB4 · NGÀNH BÁN QUÁ MỨC",
        LoaiKichBanV3.N1 => "N1 · GIẢI CHẤP CẠN KIỆT",
        LoaiKichBanV3.N3 => "N3 · ĐÓN DÒNG TIỀN ĐẦU THÁNG",
        _ => loai.ToString(),
    };

    private static string ThuocXepHang(LoaiKichBanV3 loai) => loai switch
    {
        LoaiKichBanV3.KB1 => "RS126",
        LoaiKichBanV3.KB2 => "RSI14",
        LoaiKichBanV3.KB4 => "RET20",
        LoaiKichBanV3.N1 => "SECR20",
        LoaiKichBanV3.N3 => "RS63",
        _ => "hạng",
    };

    /// <summary>Tin MUA. thamSoMoCua: true → ATO mở cửa phiên nay (KB1/KB2); false → ATC cuối phiên nay (KB4/N1/N3).</summary>
    public static string FormatMua(
        LoaiKichBanV3 loai, string symbol, decimal giaTinHieu, decimal tv20Ty,
        decimal giaTriThuocXepHang, int hangTrongKichBan, int topN,
        decimal breadth50, decimal chiSoDoLechMa50, decimal heSoTrongSo, decimal giaTriLenhTyDong,
        bool moCuaPhienSau, int giuToiDa, string? ngayAtoN3,
        V3CauHinh cfg, bool laHoangLoanSau = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🟢 <b>{E(TenKichBan(loai).Split('·')[0].Trim())} · {E(symbol)}</b> — {E(TenKichBan(loai).Split('·', 2)[1].Trim())}");

        if (moCuaPhienSau)
            sb.AppendLine("Mua <b>ATO mở cửa phiên hôm nay (T+1)</b> — tín hiệu từ đóng cửa phiên qua");
        else
            sb.AppendLine("Mua <b>ATC cuối phiên nay</b>");

        sb.AppendLine($"Giá tín hiệu: <code>{F(giaTinHieu)}</code>  ·  TV20: <code>{F(tv20Ty)} tỷ</code>");
        sb.AppendLine($"{ThuocXepHang(loai)}: <code>{F(giaTriThuocXepHang)}</code> (hạng {hangTrongKichBan}/{topN})");
        sb.AppendLine($"Độ rộng: <code>BREADTH50 {F(breadth50)}</code> · Chỉ số so MA50 <code>{SignedPct(chiSoDoLechMa50)}</code>");

        var phanTram = (heSoTrongSo * 100m / 5m);
        sb.AppendLine($"Quy mô: <code>{F(phanTram)}%</code> DANH MỤC ({F(giaTriLenhTyDong)} tỷ — V/5 × {F(heSoTrongSo)})");

        if (loai == LoaiKichBanV3.N3)
            sb.AppendLine($"Bán ATO phiên TDM=2 tháng sau{(ngayAtoN3 is null ? "" : $" ({E(ngayAtoN3)})")} · Giữ tối đa {giuToiDa} phiên");
        else if (laHoangLoanSau)
            // Phụ lục E (bổ sung v3): ngưỡng 0,15/+15% lấy từ CẤU HÌNH (FR-011), không in cứng — đổi cfg thì tin đổi theo.
            sb.AppendLine($"⚡ Hoảng loạn sâu (BREADTH50 <code>{F(breadth50)}</code> &lt; {F(cfg.HoangLoanSauBreadth50Max)}) · Bán realtime: giá khớp ≥ giá vào +{F((cfg.HoangLoanSauChotLoi - 1m) * 100m)}% · Giữ tối đa {giuToiDa} phiên");
        else if (loai == LoaiKichBanV3.N1)
            sb.AppendLine($"Bán realtime: giá khớp ≥ max(chạm MA20, giá vào +0,4%) · Giữ tối đa {giuToiDa} phiên");
        else
            sb.AppendLine($"Bán realtime: giá khớp ≥ max(chạm MA10, giá vào +0,4%) · Giữ tối đa {giuToiDa} phiên");

        sb.AppendLine("⚠️Backtest P1++ chưa kiểm chứng live");
        return sb.ToString();
    }

    /// <summary>Tin BÁN realtime (trong phiên).</summary>
    public static string FormatBan(
        LoaiKichBanV3 loai, string symbol, decimal giaKhop, decimal giaVao, int phienGi, int giuToiDa, string lyDo)
    {
        var sb = new StringBuilder();
        var pct = giaVao > 0 ? (giaKhop - giaVao) / giaVao * 100m : 0m;
        sb.AppendLine($"🔴 <b>BÁN · {E(symbol)}</b> — {E(loai.ToString())}");
        sb.AppendLine($"Giá khớp: <code>{F(giaKhop)}</code>  ·  Giá vào: <code>{F(giaVao)}</code>  ·  {SignedPct(pct)}");
        sb.AppendLine($"Lý do: {E(lyDo)}");
        sb.AppendLine($"Phiên giữ: {phienGi}/{giuToiDa}");
        return sb.ToString();
    }

    /// <summary>Tin nhắc ATO bán N3 (08:30 phiên TDM=2 tháng sau).</summary>
    public static string FormatAtoN3(string symbol, decimal giaVao, int phienGi, int giuToiDa)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🔴 <b>ATO · {E(symbol)}</b> — N3");
        sb.AppendLine("Bán ATO mở cửa <b>ngay phiên hôm nay</b> (đón dòng tiền đầu tháng, đã hết chu kỳ)");
        sb.AppendLine($"Giá vào: <code>{F(giaVao)}</code>  ·  Phiên giữ: {phienGi}/{giuToiDa}");
        return sb.ToString();
    }

    private static string F(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string SignedPct(decimal pct)
    {
        var abs = Math.Abs(pct).ToString("0.#", CultureInfo.InvariantCulture);
        if (pct > 0) return $"+{abs}%";
        if (pct < 0) return $"−{abs}%";
        return "0%";
    }

    private static readonly string[] CacTheHopLe = { "b", "/b", "code", "/code" };

    // DO-NOT-CHANGE: escape & < > thô nhưng giữ thẻ b/code hợp lệ — runner truyền <code> trong nội dung.
    private static string E(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;
        var sb = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '&')
            {
                var entity = text.AsSpan(i);
                var daCoEntity = entity.StartsWith("&amp;") || entity.StartsWith("&lt;") || entity.StartsWith("&gt;");
                sb.Append(daCoEntity ? "&" : "&amp;");
                continue;
            }
            if (c == '<')
            {
                var j = i + 1;
                var tenThe = "";
                if (j < text.Length && text[j] == '/') { j++; tenThe = "/"; }
                while (j < text.Length && char.IsLetter(text[j])) { tenThe += text[j]; j++; }
                if (j < text.Length && text[j] == '>' && CacTheHopLe.Contains(tenThe))
                {
                    sb.Append(text, i, j - i + 1);
                    i = j;
                    continue;
                }
                sb.Append("&lt;");
                continue;
            }
            if (c == '>') { sb.Append("&gt;"); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }
}

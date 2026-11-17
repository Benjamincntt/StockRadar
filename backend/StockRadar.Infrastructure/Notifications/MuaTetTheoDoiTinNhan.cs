using System.Globalization;
using System.Text;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>
/// Tin cảnh báo giỏ "Mùa Tết — mã khỏe" (P4-19). Kênh hệ thống gửi Telegram KHÔNG kèm parse mode nên phải là
/// văn bản thuần — không thẻ HTML. Giá theo nghìn đồng, tiền theo triệu để không lệ thuộc dấu phân cách.
/// </summary>
internal static class MuaTetTheoDoiTinNhan
{
    private const string Ten = "MÙA TẾT — MÃ KHỎE (P4-19)";

    /// <summary>Tiêu đề đẩy app theo mức báo — ngắn, dưới hạn payload.</summary>
    /// <param name="muc">Mức báo của lần kiểm.</param>
    public static string TieuDeDay(MuaTetTheoDoi.MucBao muc) => muc switch
    {
        MuaTetTheoDoi.MucBao.Ban => "Mùa Tết — BÁN CẢ GIỎ",
        MuaTetTheoDoi.MucBao.CanhBao => "Mùa Tết — cảnh báo sớm",
        _ => "Mùa Tết — giỏ đã mua"
    };

    /// <summary>Dựng nội dung tin từ trạng thái giỏ đã mua.</summary>
    /// <param name="tt">Trạng thái giỏ kèm chi tiết từng mã.</param>
    /// <returns>Văn bản thuần gửi Telegram và đẩy app.</returns>
    public static string Dung(MuaTetTheoDoiDto tt)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Ten);
        sb.AppendLine($"{DongMuc(tt.Muc)}: giá trị giỏ {Trieu(tt.GiaTriHienTai)} triệu / vốn {Trieu(tt.VonDaMua)} triệu = {F(tt.TyLe * 100m)}% (cảnh báo 95%, bán 90%)");
        sb.AppendLine(tt.NgayMua is { } ngay
            ? $"Ngày mua: {ngay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} · giá: LastClose trong DB (KBS sync mỗi 60 giây trong phiên)"
            : "Chưa ghi ngày mua · giá: LastClose trong DB (KBS sync mỗi 60 giây trong phiên)");
        sb.AppendLine();

        for (var i = 0; i < tt.CacMa.Count; i++)
        {
            var m = tt.CacMa[i];
            sb.Append($"{i + 1}. {m.Symbol} · {m.SoCo} cổ · vào {F(m.GiaVao)} · hiện {F(m.GiaHienTai)}");
            sb.Append($" · {F(m.LaiLoPhanTram)}% · {Trieu(m.Von)} → {Trieu(m.GiaTriHienTai)} triệu");
            if (m.ThieuGia)
                sb.Append(" · THIẾU GIÁ (dùng giá vào)");
            sb.AppendLine();
        }

        if (tt.MaKeoXuongNhieuNhat is { } ma)
            sb.AppendLine($"Mã kéo giỏ xuống nhiều nhất: {ma}.");

        if (tt.SoMaThieuGia > 0)
            sb.AppendLine($"Thiếu giá {tt.SoMaThieuGia} mã nên chưa kết luận BÁN CẢ GIỎ — kiểm tra lại trước khi bán.");

        sb.AppendLine();
        sb.AppendLine("Luật: bán CẢ GIỎ khi giá trị giỏ ≤ 90% vốn đã mua, cảnh báo sớm ở 95% (P4-19).");
        sb.AppendLine("Kết quả theo luật backtest, không phải lời khuyên đầu tư.");
        return sb.ToString().TrimEnd();
    }

    private static string DongMuc(string muc) => muc switch
    {
        "Ban" => "BÁN CẢ GIỎ",
        "CanhBao" => "CẢNH BÁO SỚM",
        _ => "BÌNH THƯỜNG"
    };

    private static string F(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Trieu(decimal dong) => F(Math.Round(dong / 1_000_000m, 2));
}

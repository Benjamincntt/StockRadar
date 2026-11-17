using System.Globalization;
using System.Text;
using StockRadar.Application.DTOs;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>
/// Tin "Mùa Tết — mã khỏe" (P4-19, sổ V4). Kênh hệ thống gửi Telegram KHÔNG kèm parse mode nên phải là
/// văn bản thuần — không thẻ HTML. Giá theo nghìn đồng, tiền theo triệu/tỷ để không lệ thuộc dấu phân cách.
/// </summary>
internal static class MuaTetTinNhan
{
    /// <summary>Tiêu đề đẩy app — ngắn, không dấu ngoặc, dưới hạn payload.</summary>
    public const string TieuDeDay = "Mùa Tết — giỏ 10 mã";

    private const string Ten = "MÙA TẾT — MÃ KHỎE (P4-19)";

    /// <summary>Dựng nội dung tin từ giỏ đã chọn. Giỏ rỗng vẫn có tin (báo không đủ mã).</summary>
    /// <param name="gio">Giỏ kèm số liệu từng mã.</param>
    /// <returns>Văn bản thuần gửi Telegram và đẩy app.</returns>
    public static string Dung(MuaTetGioDto gio)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Ten);
        sb.AppendLine($"Ngày dữ liệu: {Ngay(gio.NgayDuLieu)} · vốn giả định {Trieu(gio.VonBanDau)} triệu · 1 chỗ = {Trieu(gio.GiaTriMotCho)} triệu");
        sb.AppendLine("Giá: nghìn đồng · TV20: giá trị giao dịch TB 20 phiên, tỷ đồng");
        sb.AppendLine();

        if (gio.CacMa.Count == 0)
        {
            sb.AppendLine("Hôm nay KHÔNG đủ mã đạt luật (10 mã, tối đa 2 mã/ngành). Không vào giỏ.");
        }
        else
        {
            foreach (var ma in gio.CacMa)
            {
                var nganh = string.IsNullOrWhiteSpace(ma.Sector) ? "chưa rõ ngành" : ma.Sector;
                sb.Append($"{ma.ThuTu}. {ma.Symbol} · {nganh} · giá {F(ma.GiaDongCua)} · gần đỉnh {F(ma.GanDinh * 100m)}%");
                sb.Append($" · TV20 {F(ma.GiaTriGiaoDichTb20Ty)} tỷ · {ma.SoLoGoiY} lô ≈ {Trieu(ma.GiaMotLo * ma.SoLoGoiY)} triệu");
                sb.AppendLine();
            }
        }

        if (gio.BiBoVuotCho.Count > 0)
            sb.AppendLine($"Bỏ {gio.BiBoVuotCho.Count} mã vì 1 lô vượt 1,5 lần một chỗ: {string.Join(", ", gio.BiBoVuotCho)}.");

        if (gio.BiLoaiLanDonVi.Count > 0)
            sb.AppendLine($"Loại {gio.BiLoaiLanDonVi.Count} mã vì giá lưu lẫn đơn vị (không tính vào giỏ).");

        sb.AppendLine();
        sb.AppendLine($"Luật giữ: mua một lần quanh giữa tháng 11, giữ tới giữa tháng 3/{NamBan(gio.NgayDuLieu)}.");
        sb.AppendLine("Luật bán: bán CẢ GIỎ khi giá trị giỏ ≤ 90% vốn ban đầu.");
        sb.AppendLine("Cách chọn: 10 mã gần đỉnh 1 năm, top 100 thanh khoản, tối đa 2 mã/ngành.");
        sb.AppendLine();
        sb.AppendLine("Backtest 10 mùa: TB +18.7…+19.0%, 9/10 mùa có lãi, tệ nhất −10.6…−10.8%.");
        sb.AppendLine("Kết quả backtest, không phải lời khuyên đầu tư.");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Năm bán mục tiêu: giỏ tính từ tháng 10 trở đi thì giữ tới tháng 3 năm sau.</summary>
    private static int NamBan(DateOnly ngayDuLieu) =>
        ngayDuLieu.Month >= 10 ? ngayDuLieu.Year + 1 : ngayDuLieu.Year;

    private static string Ngay(DateOnly ngay) => ngay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string F(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Trieu(decimal dong) => F(Math.Round(dong / 1_000_000m, 2));
}

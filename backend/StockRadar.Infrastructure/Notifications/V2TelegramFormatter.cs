using System.Globalization;
using System.Text;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>
/// Formatter Telegram cho V2 Scenario Engine.
/// Tạo message theo template spec cho kịch bản BUY triggered và SELL triggered.
/// </summary>
internal static class V2TelegramFormatter
{
    /// <summary>Format alert MUA (kịch bản BUY triggered).</summary>
    public static string FormatMua(KetQuaKichBan ketQua, KetQuaXepHang? xepHang = null)
    {
        var tenKichBan = ketQua.LoaiKichBan switch
        {
            LoaiKichBan.NoHuongLen => "NỔ HƯỚNG LÊN",
            LoaiKichBan.HoiHoTro => "HỒI VỀ HỖ TRỢ",
            LoaiKichBan.QuetThanhKhoan => "QUÉT THANH KHOẢN",
            _ => ketQua.LoaiKichBan.ToString()
        };

        var sb = new StringBuilder();
        sb.AppendLine($"🎯 <b>{ketQua.Symbol}</b> — {tenKichBan}");
        sb.AppendLine("━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📈 <b>{ketQua.Symbol}</b> — MUA ĐIỂM 1");
        sb.AppendLine();

        if (ketQua.ThoiGianKichHoat.HasValue)
            sb.AppendLine($"Kích hoạt lúc: {ketQua.ThoiGianKichHoat.Value:HH:mm} (VN)");
        sb.AppendLine();

        // Trạng thái các vai trò
        sb.AppendLine($"Bối cảnh: {(ketQua.DatBoiCanh ? "✓" : "✗")}");
        sb.AppendLine($"Hình thái: {(ketQua.DatHinhThai ? "✓" : "✗")}");
        sb.AppendLine($"Kích hoạt: {(ketQua.DatCoKichHoat ? "✓" : "✗")}");
        sb.AppendLine();

        // Kế hoạch giao dịch
        if (ketQua.KeHoach != null)
        {
            var kh = ketQua.KeHoach;
            sb.AppendLine($"💰 Vào lệnh: <code>{F(kh.GiaVaoLenhMin)}</code> – <code>{F(kh.GiaVaoLenhMax)}</code>");
            sb.AppendLine($"🛑 Dừng lỗ: <code>{F(kh.GiaDungLo)}</code>");
            sb.AppendLine($"🎯 TP1: <code>{F(kh.GiaChotLoi1)}</code> ({SignedPct(Pct(kh.GiaChotLoi1, kh.GiaVaoLenhMin))})");
            sb.AppendLine($"🎯 TP2: <code>{F(kh.GiaChotLoi2)}</code> ({SignedPct(Pct(kh.GiaChotLoi2, kh.GiaVaoLenhMin))})");
            sb.AppendLine($"📐 R:R = {kh.TyLeLaiLo:F1}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(kh.DieuKienHuy))
                sb.AppendLine($"❌ Hủy nếu: {kh.DieuKienHuy}");
        }

        // Điểm xếp hạng
        if (xepHang != null)
        {
            sb.AppendLine();
            sb.AppendLine($"⭐ Điểm xếp hạng: <b>{xepHang.DiemTong:F1}</b>/100");
            sb.AppendLine($"   RS={xepHang.DiemRs:F0} Sector={xepHang.DiemSector:F0} Trigger={xepHang.DiemTrigger:F0}");
            sb.AppendLine($"   Regime={xepHang.DiemRegime:F0} R:R={xepHang.DiemTyLeLaiLo:F0} Confluence={xepHang.DiemConfluence:F0}");
        }

        // Evidence details
        var datEvidence = ketQua.DanhSachBangChung.Where(b => b.Dat).ToList();
        if (datEvidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Bằng chứng:");
            foreach (var bc in datEvidence)
                sb.AppendLine($"✓ {bc.MoTa} ({bc.GiaTriThucTe})");
        }

        return sb.ToString();
    }

    /// <summary>Format alert BÁN (Kiệt sức / Gãy nền).</summary>
    public static string FormatBan(KetQuaKichBan ketQua)
    {
        var tenKichBan = ketQua.LoaiKichBan == LoaiKichBan.KietSuc ? "KIỆT SỨC" : "GÃY NỀN";
        var hanhDong = ketQua.LoaiKichBan == LoaiKichBan.KietSuc ? "BÁN 50%" : "BÁN 100%";
        var icon = ketQua.LoaiKichBan == LoaiKichBan.KietSuc ? "⚠️" : "🚨";

        var sb = new StringBuilder();
        sb.AppendLine($"{icon} <b>{ketQua.Symbol}</b> — {hanhDong}");
        sb.AppendLine("━━━━━━━━━━━━━━━━");
        sb.AppendLine($"Kịch bản: {tenKichBan}");
        sb.AppendLine();

        if (ketQua.ThoiGianKichHoat.HasValue)
            sb.AppendLine($"Kích hoạt lúc: {ketQua.ThoiGianKichHoat.Value:HH:mm} (VN)");

        // Evidence
        var datEvidence = ketQua.DanhSachBangChung.Where(b => b.Dat).ToList();
        if (datEvidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Lý do:");
            foreach (var bc in datEvidence)
                sb.AppendLine($"• {bc.MoTa} ({bc.GiaTriThucTe})");
        }

        sb.AppendLine();
        sb.AppendLine($"Hành động: <b>{hanhDong}</b> vị thế");

        if (!string.IsNullOrEmpty(ketQua.KeHoach?.DieuKienHuy))
            sb.AppendLine($"Điều kiện hủy: {ketQua.KeHoach.DieuKienHuy}");

        return sb.ToString();
    }

    /// <summary>Định dạng cảnh báo “chưa bán được” khi kịch bản bán kích hoạt nhưng chưa đủ phiên T+2.5.</summary>
    /// <param name="ketQua">Kết quả kịch bản bán đã trigger.</param>
    /// <param name="conLaiPhien">Số phiên còn thiếu để được bán.</param>
    /// <returns>Nội dung tin nhắn Telegram.</returns>
    public static string FormatCanhBaoChuaBanDuoc(KetQuaKichBan ketQua, int conLaiPhien)
    {
        var tenKichBan = ketQua.LoaiKichBan == LoaiKichBan.KietSuc ? "KIỆT SỨC" : "GÃY NỀN";
        var sb = new StringBuilder();
        sb.AppendLine($"\uD83D\uDFE1 <b>{ketQua.Symbol}</b> — CẢNH BÁO {tenKichBan}");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Chưa đủ T+2.5 — còn {conLaiPhien} phiên.");
        sb.AppendLine("Khi đủ điều kiện, hệ thống sẽ gửi tin BÁN chính thức.");
        return sb.ToString();
    }

    /// <summary>Cảnh báo chạm mức giá khi chưa đủ phiên T+2.5.</summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="tenSuKien">Tên sự kiện (Chạm dừng lỗ / Chạm chốt lời 1 / Chạm chốt lời 2).</param>
    /// <param name="conLaiPhien">Số phiên còn thiếu.</param>
    /// <returns>Nội dung tin nhắn Telegram.</returns>
    public static string FormatCanhBaoChuaBanDuoc(string symbol, string tenSuKien, int conLaiPhien)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\uD83D\uDFE1 <b>{symbol}</b> — {tenSuKien}");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Chưa đủ T+2.5 — còn {conLaiPhien} phiên.");
        sb.AppendLine("Khi đủ điều kiện, hệ thống sẽ gửi tin BÁN chính thức.");
        return sb.ToString();
    }

    /// <summary>Tin báo chạm mức giá (dừng lỗ / chốt lời 1 / chốt lời 2).</summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="tenSuKien">Tên sự kiện.</param>
    /// <param name="hanhDong">Hành động (BÁN 50% / BÁN HẾT).</param>
    /// <param name="gia">Giá hiện tại.</param>
    /// <param name="giaVao">Giá vào lệnh.</param>
    /// <param name="themLine">Dòng bổ sung (vd: “Dừng lỗ dời về X”).</param>
    /// <returns>Nội dung tin nhắn Telegram.</returns>
    public static string FormatChamMucGia(
        string symbol, string tenSuKien, string hanhDong, decimal gia, decimal giaVao, string? themLine)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🔴 <b>{symbol}</b> — {hanhDong}");
        sb.AppendLine("━━━━━━━━━━━━━━━━");
        sb.AppendLine($"Sự kiện: {tenSuKien}");
        sb.AppendLine($"Giá hiện tại: <code>{F(gia)}</code>");
        sb.AppendLine($"Giá vào lệnh: <code>{F(giaVao)}</code>");
        if (!string.IsNullOrEmpty(themLine))
            sb.AppendLine(themLine);
        sb.AppendLine();
        sb.AppendLine($"Hành động: <b>{hanhDong}</b> vị thế");
        return sb.ToString();
    }

    /// <summary>Định dạng giá (bỏ số 0 thừa).</summary>
    private static string F(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
    
    /// <summary>Tin bán hết vì dừng lỗ theo thời gian: đủ số phiên mà MFE chưa chạm ngưỡng.</summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="gia">Giá hiện tại.</param>
    /// <param name="giaVao">Giá vào.</param>
    /// <param name="soPhienDaQua">Số phiên đã giữ vị thế.</param>
    /// <param name="mfePhanTram">MFE hiện tại (%).</param>
    /// <param name="nguongMfePhanTram">Ngưỡng MFE tối thiểu (%).</param>
    public static string FormatHetThoiGian(
        string symbol, decimal gia, decimal giaVao, int soPhienDaQua,
        decimal mfePhanTram, decimal nguongMfePhanTram)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\u23F1\uFE0F <b>{symbol}</b> — BÁN HẾT");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Sự kiện: Hết thời gian");
        sb.AppendLine($"Đã giữ: {soPhienDaQua} phiên, MFE cao nhất {mfePhanTram:0.##}% (dưới ngưỡng {nguongMfePhanTram:0.##}%).");
        sb.AppendLine($"Giá hiện tại: <code>{F(gia)}</code>");
        sb.AppendLine($"Giá vào lệnh: <code>{F(giaVao)}</code>");
        sb.AppendLine();
        sb.AppendLine("Hành động: <b>BÁN HẾT</b> vị thế");
        return sb.ToString();
    }
    
    /// <summary>Tin bán hết vì quá hạn theo dõi — rào thời gian triple-barrier.</summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="gia">Giá hiện tại.</param>
    /// <param name="giaVao">Giá vào.</param>
    /// <param name="soPhienDaQua">Số phiên đã giữ.</param>
    /// <param name="soPhienToiDa">Giới hạn theo dõi cấu hình.</param>
    public static string FormatHetHanTheoDoi(
        string symbol, decimal gia, decimal giaVao, int soPhienDaQua, int soPhienToiDa)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\u231B <b>{symbol}</b> — BÁN HẾT");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Sự kiện: Hết hạn theo dõi");
        sb.AppendLine($"Vị thế đã quá {soPhienDaQua} phiên (giới hạn {soPhienToiDa}) mà chưa có tín hiệu thoát.");
        sb.AppendLine($"Giá hiện tại: <code>{F(gia)}</code>");
        sb.AppendLine($"Giá vào lệnh: <code>{F(giaVao)}</code>");
        sb.AppendLine();
        sb.AppendLine("Hành động: <b>BÁN HẾT</b> vị thế");
        return sb.ToString();
    }

    /// <summary>Tính % thay đổi từ giá gốc.</summary>
    private static decimal Pct(decimal target, decimal basePrice) =>
        basePrice > 0 ? (target - basePrice) / basePrice * 100m : 0m;

    /// <summary>Format có dấu + / −.</summary>
    private static string SignedPct(decimal pct)
    {
        var abs = Math.Abs(pct).ToString("0.#", CultureInfo.InvariantCulture);
        if (pct > 0) return $"+{abs}%";
        if (pct < 0) return $"-{abs}%";
        return "0%";
    }
}

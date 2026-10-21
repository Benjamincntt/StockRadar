using System.Globalization;
using System.Text;
using StockRadar.Domain.Constants;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>
/// Formatter Telegram cho V2 Scenario Engine.
/// Tạo message theo template spec cho kịch bản BUY triggered và SELL triggered.
/// </summary>
internal static class V2TelegramFormatter
{
    /// <summary>Tên hiển thị kịch bản theo hợp đồng tin Telegram.</summary>
    public static string TenKichBan(LoaiKichBan loai) => loai switch
    {
        LoaiKichBan.NoHuongLen => "NỔ HƯỚNG LÊN",
        LoaiKichBan.HoiHoTro => "HỒI VỀ HỖ TRỢ",
        LoaiKichBan.QuetThanhKhoan => "QUÉT THANH KHOẢN",
        _ => loai.ToString()
    };

    /// <summary>Format alert MUA (kịch bản BUY triggered).</summary>
    public static string FormatMua(KetQuaKichBan ketQua, KetQuaXepHang? xepHang = null)
    {
        var tenKichBan = TenKichBan(ketQua.LoaiKichBan);

        var sb = new StringBuilder();
        sb.AppendLine($"🎯 <b>{ketQua.Symbol}</b> — {tenKichBan} · MUA");
        sb.AppendLine("━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        if (ketQua.ThoiGianKichHoat.HasValue)
            sb.AppendLine($"Kích hoạt lúc: {ketQua.ThoiGianKichHoat.Value:HH:mm} (VN)");
        sb.AppendLine();

        // Trạng thái các vai trò
        sb.AppendLine($"Bối cảnh: {(ketQua.DatBoiCanh ? "✓" : "✗")}");
        sb.AppendLine($"Hình thái: {(ketQua.DatHinhThai ? "✓" : "✗")}");
        sb.AppendLine($"Kích hoạt: {(ketQua.DatCoKichHoat ? "✓" : "✗")}");
        sb.AppendLine();

        // Kế hoạch giao dịch — in đủ các mục theo specs/009-v2-trade-plan/contracts/telegram-mua.md.
        if (ketQua.KeHoach != null)
        {
            var kh = ketQua.KeHoach;
            // % tính từ giá vào thực tế (giá khớp), fallback về biên dưới cho kế hoạch cũ chưa có.
            var giaVaoThucTe = ketQua.GiaKhop ?? kh.GiaVaoLenhMin;
            sb.AppendLine($"💰 Giá vào: <code>{F(giaVaoThucTe)}</code> (vùng <code>{F(kh.GiaVaoLenhMin)}</code>–<code>{F(kh.GiaVaoLenhMax)}</code>)");
            sb.AppendLine($"🛑 Cắt lỗ: <code>{F(kh.GiaDungLo)}</code> ({SignedPct(Pct(kh.GiaDungLo, giaVaoThucTe))})");
            var noteDoi = kh.DoiDungLoVeGiaVao ? ", sau đó dời cắt lỗ về giá vào" : "";
            sb.AppendLine($"🎯 Chốt lời 1: <code>{F(kh.GiaChotLoi1)}</code> ({SignedPct(Pct(kh.GiaChotLoi1, giaVaoThucTe))}) — bán {F(kh.TyLeBanChotLoi1 * 100m)}%{noteDoi}");
            sb.AppendLine($"🎯 Chốt lời 2: <code>{F(kh.GiaChotLoi2)}</code> ({SignedPct(Pct(kh.GiaChotLoi2, giaVaoThucTe))}) — bán phần còn lại");
            // BUSINESS-RULE: dòng điểm mua 2 chỉ in khi kế hoạch có điểm 2 (C-9); không có → in ngắn (format chưa duyệt chi tiết).
            if (kh.GiaMuaDiem2 is > 0)
            {
                var diem2 = kh.GiaMuaDiem2!.Value;
                var tren2 = diem2 + (kh.BandRongDiem2 ?? 0m);
                sb.AppendLine($"🎯 Điểm mua 2: <code>{F(diem2)}</code> ({SignedPct(Pct(diem2, giaVaoThucTe))}) — khớp trong <code>{F(diem2)}</code>–<code>{F(tren2)}</code>");
                sb.AppendLine("    · Vốn 50/50; sau điểm 2 bỏ chốt lời 1/2, chỉ thoát theo dừng lỗ / rút mốc / thời gian / hạn");
            }
            else
            {
                sb.AppendLine("Điểm mua 2: không (điểm 2 ≥ chốt lời 1)");
            }
            if (kh.CoDungLoDuoi && kh.KhoangCachDuoi is > 0)
                sb.AppendLine($"Dừng lỗ đuổi theo: đỉnh − <code>{F(kh.KhoangCachDuoi!.Value)}</code>");
            // BUSINESS-RULE: ngưỡng rút mốc đọc từ kế hoạch (C-12 cố định 4/6 mọi mã); dòng này in cho mọi vị thế.
            sb.AppendLine($"Rút từ mốc: \u2212{kh.NguongRutMocBanNuaPhanTram:0.##}% \u2192 b\u00e1n n\u1eefa; \u2212{kh.NguongRutMocBanHetPhanTram:0.##}% \u2192 b\u00e1n h\u1ebft");
            sb.AppendLine($"⏳ Thời gian: sau {kh.SoPhienDungTheoThoiGian} phiên chưa lãi {F(kh.NguongLaiToiThieuPhanTram)}% → bán; tối đa {kh.SoPhienToiDa} phiên");
            sb.AppendLine($"📐 Lãi/lỗ kỳ vọng: {kh.TyLeLaiLo:F1}");
            sb.AppendLine("Chưa bán được trước T+2.5");
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

    /// <summary>
    /// Tin MUA THÊM (điểm 2) theo contracts/telegram-mua.md (C-9, C-3, C-5, C-14, C-17, C-18).
    /// Chỉ gửi khi thực sự khớp: kế hoạch có điểm 2, chưa bán nửa, chưa có điểm 2, giá ∈ band, không bị luật bán thắng lượt đó.
    /// </summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="tenKichBan">Tên kịch bản hiển thị.</param>
    /// <param name="giaDiem1">Giá khớp điểm 1 (GiaVaoThucTe).</param>
    /// <param name="giaKhopDiem2">Giá khớp điểm 2 (GiaMuaDiem2ThucTe).</param>
    /// <param name="giaBinhQuan">Bình quân hai lần mua.</param>
    /// <param name="dinhTuLucMua">Mốc đỉnh từ lúc mua (không reset — C-23).</param>
    /// <param name="mucCatHieuLuc">Mức cắt hiệu lực cao nhất (0 nếu chưa có).</param>
    public static string FormatMuaDiem2(
        string symbol, string tenKichBan, decimal giaDiem1, decimal giaKhopDiem2,
        decimal giaBinhQuan, decimal dinhTuLucMua, decimal mucCatHieuLuc)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🎯 <b>{symbol}</b> — {tenKichBan} · MUA THÊM (điểm 2)");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Giá khớp: <code>{F(giaKhopDiem2)}</code> \u2014 điểm 1 <code>{F(giaDiem1)}</code>");
        sb.AppendLine($"Bình quân 2 lần: <code>{F(giaBinhQuan)}</code>");
        sb.AppendLine("Vốn: 50% điểm 1 + 50% điểm 2");
        sb.AppendLine("→ Bỏ chốt lời 1/2. Chỉ thoát: dừng lỗ/đuổi, rút mốc 4/6%, thời gian, hạn.");
        sb.AppendLine($"Mốc hiện tại: <code>{F(dinhTuLucMua)}</code>   \u00b7 Cắt hiệu lực: {(mucCatHieuLuc > 0 ? $"<code>{F(mucCatHieuLuc)}</code>" : "chưa có")}");
        return sb.ToString();
    }

    /// <summary>
    /// Tin BÁN NỬA dùng chung cho chốt lời 1 và rút mốc (C-16) theo contracts/telegram-mua.md (C-29).
    /// Bán nửa KHÔNG ghi LyDoThoatHet — nguồn (nguon) chỉ hiện trong tin và log.
    /// Nội dung không in mã quyết định nội bộ (C-30).
    /// </summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="nguon">Nguồn bán nửa: "Chốt lời 1" hoặc "Rút mốc 4%".</param>
    /// <param name="gia">Giá khớp bán nửa.</param>
    /// <param name="mucCatHieuLuc">Mức cắt còn lại đang áp dụng (0 nếu chưa có).</param>
    /// <param name="lyDo">Dòng lý do đầy đủ (không chứa mã quyết định).</param>
    public static string FormatBanNua(
        string symbol, string nguon, decimal gia, decimal mucCatHieuLuc, string lyDo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\uD83D\uDD3B <b>{symbol}</b> — BÁN NỬA ({nguon})");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Giá: <code>{F(gia)}</code>  \u00b7 Lý do: {lyDo}");
        sb.AppendLine($"Cắt còn lại: {(mucCatHieuLuc > 0 ? $"<code>{F(mucCatHieuLuc)}</code>" : "chưa có")}");
        sb.AppendLine("Mỗi chu kỳ tối đa một lần bán nửa — sau tin này chỉ còn luật bán hết.");
        return sb.ToString();
    }

    /// <summary>
    /// Tin BÁN HẾT vì rút mốc 6% (C-11/C-19) theo contracts/telegram-mua.md — đóng chu kỳ, lý do RutMoc6.
    /// </summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="gia">Giá khớp bán hết.</param>
    /// <param name="giaVao">Giá bình quân (điểm 1, hoặc bình quân hai lần khi đã có điểm 2).</param>
    /// <param name="moc">Mốc rút mốc = max(đỉnh từ lúc mua, giá điểm 1).</param>
    public static string FormatBanHetRutMoc(string symbol, decimal gia, decimal giaVao, decimal moc)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\uD83D\uDD34 <b>{symbol}</b> — BÁN HẾT ({SuKienBan.RutMoc6})");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Giá: <code>{F(gia)}</code>  \u00b7 Lãi/lỗ: {SignedPct(Pct(gia, giaVao))} so bình quân <code>{F(giaVao)}</code>");
        sb.AppendLine($"Lý do: {SuKienBan.RutMoc6} \u2014 mốc <code>{F(moc)}</code> giảm \u2265 6%");
        sb.AppendLine("Chu kỳ đóng. Phiên sau sẽ đánh giá lại mã.");
        return sb.ToString();
    }

    /// <summary>Tin cảnh báo kịch bản thoát (Kiệt sức / Gãy nền) — Q1/FR-011 spec 009: chỉ cảnh báo kèm mức cắt hiệu lực, không phải lệnh bán.</summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="tenSuKien">KIỆT SỨC / GÃY NỀN.</param>
    /// <param name="giaHienTai">Giá hiện tại.</param>
    /// <param name="mucCatHieuLuc">Mức cắt hiệu lực cao nhất trong kế hoạch (0 nếu chưa đặt).</param>
    public static string FormatCanhBaoKichBanThoat(string symbol, string tenSuKien, decimal giaHienTai, decimal mucCatHieuLuc)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\uD83D\uDFE1 <b>{symbol}</b> — CẢNH BÁO {tenSuKien}");
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
        sb.AppendLine($"Giá hiện tại: <code>{F(giaHienTai)}</code>");
        sb.AppendLine(mucCatHieuLuc > 0
            ? $"Mức cắt hiệu lực: <code>{F(mucCatHieuLuc)}</code> — bán khi giá chạm mức này."
            : "Mức cắt hiệu lực: chưa có trong kế hoạch.");
        sb.AppendLine("Đây là cảnh báo, KHÔNG phải lệnh bán — trạng thái vị thế giữ nguyên.");
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
        sb.AppendLine("\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501");
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
    /// <param name="soPhienToiDa">Giới hạn theo dõi đọc từ kế hoạch.</param>
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

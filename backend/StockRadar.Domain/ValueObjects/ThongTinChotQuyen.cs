namespace StockRadar.Domain.ValueObjects;

/// <summary>
/// Tóm tắt một sự kiện chốt quyền chia chác (từ feed lịch quyền toàn thị trường):
/// ngày chốt danh sách hưởng quyền + ngày thực hiện quyền (trả cổ tức/cổ phiếu mới)
/// + mô tả ngắn chia gì/tỷ lệ bao nhiêu. Cổng chia chác chặn mã trong SUỐT khoảng
/// [ngày chốt quyền → ngày thực hiện quyền], không chỉ tới ngày chốt.
/// </summary>
public sealed record ThongTinChotQuyen(
    DateOnly ExDate,
    string? MoTa,
    DateOnly? NgayThucHien = null,
    /// <summary>Hôm nạp map đã QUA ngày chốt quyền (chỉ còn chờ ngày thực hiện).</summary>
    bool DaChot = false)
{
    /// <summary>Nhãn hiển thị gọn: "Chờ chốt quyền 05/10 — Cổ tức tiền 1.000đ/CP" hoặc
    /// "Đã chốt quyền 05/10, chờ thực hiện 16/10 — …". MoTa được cắt ~80 ký tự vì title
    /// nguồn (FireAnt) có thể dài, còn UI hiển thị một dòng.</summary>
    public string Nhan()
    {
        var moTa = string.IsNullOrWhiteSpace(MoTa) ? null : MoTa!.Trim();
        if (moTa is not null && moTa.Length > 80)
            moTa = moTa.Substring(0, 77).TrimEnd() + "…";

        var nhan = DaChot
            ? $"Đã chốt quyền {ExDate:dd/MM}"
            : $"Chờ chốt quyền {ExDate:dd/MM}";
        if (NgayThucHien is { } thucHien && thucHien > ExDate)
            nhan += $", chờ thực hiện {thucHien:dd/MM}";
        return moTa is null ? nhan : $"{nhan} — {moTa}";
    }
}

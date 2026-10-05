namespace StockRadar.Domain.ValueObjects;

/// <summary>
/// Tóm tắt một sự kiện chốt quyền chia chác sắp tới (từ feed lịch quyền toàn thị trường):
/// ngày không hưởng quyền + mô tả ngắn những gì sắp chia (tiền mặt / cổ phiếu thưởng /
/// quyền mua kèm tỷ lệ). Dùng cho cổng chia chác và nhãn "Chờ chốt quyền" trên Top/mobile.
/// </summary>
public sealed record ThongTinChotQuyen(DateOnly ExDate, string? MoTa)
{
    /// <summary>Nhãn hiển thị gọn: "05/10 — Cổ tức tiền 1.000đ/CP". MoTa được cắt
    /// ~80 ký tự vì title nguồn (FireAnt) có thể dài, còn UI hiển thị một dòng.</summary>
    public string Nhan()
    {
        var moTa = System.String.IsNullOrWhiteSpace(MoTa) ? null : MoTa!.Trim();
        if (moTa is not null && moTa.Length > 80)
            moTa = moTa.Substring(0, 77).TrimEnd() + "…";
        return moTa is null ? $"{ExDate:dd/MM}" : $"{ExDate:dd/MM} — {moTa}";
    }
}
